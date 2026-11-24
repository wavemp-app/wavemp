import { expect, test, type Page } from '@playwright/test'

// Layout options of the theme API (THEMING.md, section "Layout options"): a theme switches optional
// parts of the desktop layout on with variables on :root. The tests use the default theme with a
// style that plays the part of such a theme; without it, the default theme sets no option.

/**
 * A theme that moves the breadcrumb into the header and keeps the base layout of the header for
 * it (revert-layer lets the base template for a header with a breadcrumb apply).
 */
const breadcrumbInHeader = `
    :root {
        --wmp-layout-breadcrumb: header;
    }

    [data-part='header']:has(> [data-part='breadcrumb']) {
        grid-template-areas: revert-layer;
        grid-template-columns: revert-layer;
    }
`

/** Adds the style to every page before the app starts, as if it were part of the theme. */
async function useThemeStyle(page: Page, css: string): Promise<void> {
    await page.addInitScript((text) => {
        const style = document.createElement('style')
        style.dataset.e2e = 'layout-options'
        style.textContent = text
        const attach = () => {
            if (!document.head) {
                return false
            }
            document.head.append(style)
            return true
        }
        if (!attach()) {
            const observer = new MutationObserver(() => {
                if (attach()) {
                    observer.disconnect()
                }
            })
            observer.observe(document, { childList: true, subtree: true })
        }
    }, css)
}

const header = "[data-part='app'] > [data-part='header']"
const toolbar = "[data-part='app'] > [data-part='toolbar']"

test('keeps the breadcrumb in the toolbar while the theme sets no option', async ({ page }) => {
    await page.goto('/library/albums')
    await expect(page.locator("[data-part='tile']").first()).toBeVisible()

    await expect(page.locator("[data-part='breadcrumb']")).toHaveCount(1)
    await expect(page.locator(`${toolbar} > [data-part='breadcrumb']`)).toHaveCount(1)
    await expect(page.locator(`${header} > [data-part='breadcrumb']`)).toHaveCount(0)
})

test.describe('with the breadcrumb in the header', () => {
    test.beforeEach(async ({ page }) => {
        await useThemeStyle(page, breadcrumbInHeader)
    })

    test('shows the breadcrumb in the header on the desktop', async ({ page, isMobile }) => {
        test.skip(isMobile, 'Desktop layout only')
        await page.goto('/library/albums')

        const breadcrumb = page.locator(`${header} > [data-part='breadcrumb']`)
        await expect(breadcrumb).toBeVisible()
        await expect(page.locator("[data-part='breadcrumb']")).toHaveCount(1)
        await expect(page.locator(`${toolbar} > [data-part='breadcrumb']`)).toHaveCount(0)
        await expect(breadcrumb.locator("[data-part='breadcrumb-link']")).toHaveText([
            'Music',
            'Library',
            'Album',
        ])

        // In the base layout of the header: after the history buttons, before the tabs, in one row.
        const history = (await page.locator(`${header} > [data-part='nav-history']`).boundingBox())!
        const tabs = (await page.locator(`${header} > [data-part='tabs']`).boundingBox())!
        const crumbs = (await breadcrumb.boundingBox())!
        const bar = (await page.locator(header).boundingBox())!
        expect(crumbs.x).toBeGreaterThanOrEqual(history.x + history.width)
        expect(crumbs.x + crumbs.width).toBeLessThanOrEqual(tabs.x + 0.5)
        expect(crumbs.y).toBeGreaterThanOrEqual(bar.y)
        expect(crumbs.y + crumbs.height).toBeLessThanOrEqual(bar.y + bar.height + 0.5)

        await breadcrumb.getByRole('link', { name: 'Library' }).click()
        await expect(page).toHaveURL(/\/library\/songs$/)
        await expect(page.locator(`${header} > [data-part='breadcrumb']`)).toContainText('Songs')
    })

    test('keeps the breadcrumb in the toolbar on phones', async ({ page, isMobile }) => {
        test.skip(!isMobile, 'Mobile layout only')
        await page.goto('/library/albums')
        await expect(page.locator("[data-part='tile']").first()).toBeVisible()

        await expect(page.locator("[data-part='breadcrumb']")).toHaveCount(1)
        await expect(page.locator(`${toolbar} > [data-part='breadcrumb']`)).toHaveCount(1)
        await expect(page.locator(`${header} > [data-part='breadcrumb']`)).toHaveCount(0)
    })
})

const viewOptions = `
    :root {
        --wmp-layout-view-options: on;
    }
`

const viewSwitch = `${toolbar} > [data-part='view-switch']`
const viewMenu = "[data-part='context-menu'][aria-label='View options']"

// A long list of songs with three pages of ten songs per album, answered in the browser
// (page.route), so the library of the test media stays as it is. The songs do not exist on the
// server.
const longListSize = 250

function longSong(index: number) {
    const album = Math.floor(index / 10)
    return {
        id: `00000000-0000-4000-8000-${String(index).padStart(12, '0')}`,
        title: `Long Song ${index}`,
        artist: { id: '00000000-0000-4000-9000-000000000001', name: 'Long Artist' },
        album: {
            id: `00000000-0000-4000-a000-${String(album).padStart(12, '0')}`,
            title: `Long Album ${album}`,
        },
        trackNumber: (index % 10) + 1,
        discNumber: null,
        year: 2026,
        genre: null,
        durationMilliseconds: 120_000,
        hasAudio: true,
        hasVideo: false,
        hasCover: false,
        firstSeenAt: '2026-01-01T00:00:00Z',
        isFavorite: false,
        rating: 0,
    }
}

async function mockLongList(page: Page): Promise<void> {
    await page.route(
        (url) => url.pathname === '/api/v1/tracks',
        (route) => {
            const url = new URL(route.request().url())
            const offset = Number(url.searchParams.get('offset') ?? 0)
            const limit = Number(url.searchParams.get('limit') ?? 100)
            const items = []
            for (let index = offset; index < Math.min(offset + limit, longListSize); index++) {
                items.push(longSong(index))
            }
            return route.fulfill({ json: { items, offset, limit, total: longListSize } })
        },
    )
}

test('keeps the view options disabled while the theme does not offer them', async ({ page }) => {
    await page.goto('/library/songs?view=list')
    await expect(page.locator("[data-part='list-row']").first()).toBeVisible()

    await expect(page.locator(viewSwitch)).toBeDisabled()
    await expect(page.locator(viewMenu)).toHaveCount(0)
    // The view of the address is ignored: the songs stay grouped by album.
    await expect(page.locator("[data-part='list'][data-state~='ungrouped']")).toHaveCount(0)
    await expect(page.locator("[data-part='list-group-header']").first()).toBeVisible()
})

test.describe('with the view options', () => {
    // Requests of the page must reach page.route, which does not see what the service worker
    // fetches.
    test.use({ serviceWorkers: 'block' })

    test.beforeEach(async ({ page }) => {
        await useThemeStyle(page, viewOptions)
    })

    test('switches a long list of songs to a plain list and back', async ({ page, isMobile }) => {
        test.skip(isMobile, 'Desktop layout only')
        await mockLongList(page)
        await page.goto('/library/songs')
        const list = page.locator("[data-part='app'] [data-part='list']")
        const rows = list.locator("[data-part='list-row']")
        await expect(rows.first()).toBeVisible()
        await expect(list.locator("[data-part='list-group-info']").first()).toBeVisible()

        async function choose(item: 'view-grouped' | 'view-list'): Promise<void> {
            await page.locator(viewSwitch).click()
            const menu = page.locator(viewMenu)
            await expect(menu).toBeVisible()
            await expect(menu.locator("[data-part='menu-item']")).toHaveText([
                'Grouped by album',
                'List',
            ])
            await menu.locator(`[data-item='${item}']`).click()
            await expect(menu).toBeHidden()
        }

        await expect(page.locator(viewSwitch)).toBeEnabled()
        await choose('view-list')
        await expect(page).toHaveURL(/\/library\/songs\?view=list$/)
        await expect(list).toHaveAttribute('data-state', /(^| )ungrouped( |$)/)
        await expect(list.locator("[data-part='list-group']")).toHaveCount(1)
        await expect(list.locator("[data-part='list-group-header']")).toHaveCount(0)
        await expect(list.locator("[data-part='list-group-info']")).toHaveCount(0)

        // The rows take the whole width of the list: the column headers start at its start.
        const listBox = (await list.boundingBox())!
        const number = (await list
            .locator("[data-part='list-header'] > [data-part='list-column']")
            .first()
            .boundingBox())!
        const cell = (await rows.first().locator("[data-part='list-cell']").first().boundingBox())!
        expect(number.x - listBox.x).toBeLessThan(1)
        expect(Math.abs(cell.x - number.x)).toBeLessThan(1)

        // Loading more pages keeps the plain list.
        await expect(rows).toHaveCount(100)
        await page.locator("[data-part='load-more']").click()
        await expect(rows).toHaveCount(200)
        await expect(list.locator("[data-part='list-group']")).toHaveCount(1)
        await expect(rows.filter({ hasText: 'Long Song 150' })).toHaveCount(1)

        // The menu marks the chosen view and leads back to the groups, with all loaded songs.
        await page.locator(viewSwitch).click()
        await expect(page.locator(`${viewMenu} [data-item='view-list']`)).toHaveAttribute(
            'aria-checked',
            'true',
        )
        await page.keyboard.press('Escape')
        await choose('view-grouped')
        await expect(page).toHaveURL(/\/library\/songs$/)
        await expect(list).not.toHaveAttribute('data-state', /(^| )ungrouped( |$)/)
        await expect(list.locator("[data-part='list-group']")).toHaveCount(20)
        await expect(rows).toHaveCount(200)
        await expect(list.locator("[data-part='list-group-info']").first()).toBeVisible()
    })

    test('keeps the view options disabled where no songs are listed', async ({
        page,
        isMobile,
    }) => {
        test.skip(isMobile, 'Desktop layout only')
        await page.goto('/library/albums')
        await expect(page.locator("[data-part='tile']").first()).toBeVisible()

        await expect(page.locator(viewSwitch)).toBeDisabled()
    })

    test('keeps the view options disabled on phones', async ({ page, isMobile }) => {
        test.skip(!isMobile, 'Mobile layout only')
        await page.goto('/library/songs?view=list')
        await expect(page.locator("[data-part='list-row']").first()).toBeVisible()

        await expect(page.locator(viewSwitch)).toBeDisabled()
        await expect(page.locator("[data-part='list'][data-state~='ungrouped']")).toHaveCount(0)
    })
})
