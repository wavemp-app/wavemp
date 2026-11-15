import { expect, test, type Locator, type Page } from '@playwright/test'
import { findAlbumId, findTrackId } from '../helpers/api'
import { expectIndicatorOnActive, indicatorGroups, showIndicators } from '../helpers/indicator'

// The corrections from the first test on phones and tablets, on an iPhone, an iPad in portrait
// (both with the mobile layout) and an iPad in landscape (desktop layout). Runs in the projects
// "iphone", "ipad" and "ipad-landscape" (playwright.config.ts) with the session of the built-in
// admin, which these tests do not change.

function part(page: Page, name: string): Locator {
    return page.locator(`[data-part='app'] [data-part='${name}']`)
}

/** True where the desktop layout applies (from 64rem on). */
function desktopLayout(page: Page): boolean {
    return page.viewportSize()!.width >= 1024
}

async function box(locator: Locator) {
    const result = await locator.boundingBox()
    expect(result).not.toBeNull()
    return result!
}

const pages = [
    '/',
    '/library',
    '/library/songs',
    '/library/albums',
    '/now-playing',
    '/playlists',
    '/settings',
    '/admin/library',
    '/admin/transcoding',
    '/admin/users',
]

test('fits every page into the screen and scrolls only its areas', async ({ page }) => {
    for (const path of pages) {
        await page.goto(path)
        await expect(part(page, 'view').first()).toBeVisible()
        const overflow = await page.evaluate(() => {
            const root = document.documentElement
            window.scrollTo(200, 200)
            return {
                width: root.scrollWidth - root.clientWidth,
                height: root.scrollHeight - root.clientHeight,
                scrolled: [window.scrollX, window.scrollY],
            }
        })
        expect(overflow, path).toEqual({ width: 0, height: 0, scrolled: [0, 0] })
    }
})

test('blends the status bar into the header', async ({ page }) => {
    await page.goto('/library/songs')
    await expect(page.locator('meta[name="viewport"]')).toHaveAttribute(
        'content',
        /viewport-fit=cover/,
    )
    await expect(
        page.locator('meta[name="apple-mobile-web-app-status-bar-style"]'),
    ).toHaveAttribute('content', 'black-translucent')
    const themeColor = await page.evaluate(() =>
        getComputedStyle(document.documentElement).getPropertyValue('--wmp-theme-color').trim(),
    )
    expect(themeColor).not.toBe('')
    await expect(page.locator('meta[name="theme-color"]')).toHaveAttribute('content', themeColor)

    const header = await box(part(page, 'header'))
    expect(header.y).toBe(0)
})

test('shows the video of a song whole inside the stage', async ({ page, request }) => {
    await page.goto(`/library/tracks/${await findTrackId(request, 'Loose Song')}`)
    await part(page, 'detail-actions').locator("[data-item='play']").click()
    await expect(part(page, 'now-playing-title')).not.toBeEmpty()
    await page.locator("[data-part='app'] a[href='/now-playing']").filter({ visible: true }).click()
    await expect(page).toHaveURL(/\/now-playing$/)

    const video = part(page, 'video')
    await expect
        .poll(() => video.evaluate((element: HTMLVideoElement) => element.readyState), {
            timeout: 15_000,
        })
        .toBeGreaterThanOrEqual(1)
    const stage = await box(part(page, 'video-stage'))
    const picture = await box(video)
    expect(Math.abs(picture.x - stage.x)).toBeLessThanOrEqual(1)
    expect(Math.abs(picture.width - stage.width)).toBeLessThanOrEqual(1)
    expect(Math.abs(picture.height - stage.height)).toBeLessThanOrEqual(1)
    expect(await video.evaluate((element) => getComputedStyle(element).objectFit)).toBe('contain')
    expect(stage.x).toBeGreaterThanOrEqual(0)
    expect(stage.x + stage.width).toBeLessThanOrEqual(page.viewportSize()!.width + 1)
    await expect(part(page, 'video-stage')).toHaveAttribute('data-orientation', 'landscape')

    // The queue shows the current song.
    await expect(part(page, 'queue-item').and(page.locator("[aria-current='true']"))).toHaveCount(1)
})

test('lines the column headers up with the cells or leaves them out', async ({ page }) => {
    await page.goto('/library/songs')
    const list = part(page, 'list')
    await expect(list.locator("[data-part='list-row']").first()).toBeVisible()

    const columns = await list.evaluate((element) => {
        const visible = (node: Element) => getComputedStyle(node).display !== 'none'
        const edges = (node: Element) => {
            const rect = node.getBoundingClientRect()
            return [Math.round(rect.left), Math.round(rect.right)]
        }
        const header = element.querySelector("[data-part='list-header']")!
        const row = element.querySelector("[data-part='list-row']")!
        return {
            headerShown: visible(header) && header.getBoundingClientRect().height > 0,
            headers: [...header.querySelectorAll("[data-part='list-column']")]
                .filter(visible)
                .map(edges),
            cells: [...row.querySelectorAll(":scope > [data-part='list-cell']")]
                .filter(visible)
                .map(edges),
            listRight: Math.round(element.getBoundingClientRect().right),
        }
    })
    if (desktopLayout(page)) {
        expect(columns.headerShown).toBe(true)
        expect(columns.cells).toEqual(columns.headers)
    }
    for (const [, right] of columns.cells) {
        expect(right).toBeLessThanOrEqual(columns.listRight)
    }
})

test('fills the icons of primary buttons with their text color', async ({ page, request }) => {
    await page.goto(`/library/albums/${await findAlbumId(request, 'E2E Album')}`)
    const play = part(page, 'detail-actions').locator("[data-item='play'][data-variant='primary']")
    await expect(play.locator("[data-part='icon']")).toBeVisible()

    const fill = await play.evaluate((element) => {
        const glyph = element.querySelector("[data-part='icon'] > [data-part='icon-glyph']")!
        return {
            image: getComputedStyle(glyph).backgroundImage,
            color: getComputedStyle(glyph).backgroundColor,
            text: getComputedStyle(element).color,
        }
    })
    expect(fill.image).toBe('none')
    expect(fill.color).toBe(fill.text)
})

test('offers the crossfade switch between repeat and stop', async ({ page }) => {
    await page.goto('/library/songs')
    const order = await part(page, 'transport-controls').evaluate((controls) =>
        [...controls.querySelectorAll('[data-part^="btn-"]')]
            .map((element) => element.getAttribute('data-part'))
            .filter((name) => ['btn-repeat', 'btn-crossfade', 'btn-stop'].includes(name!)),
    )
    expect(order).toEqual(['btn-repeat', 'btn-crossfade', 'btn-stop'])

    const crossfade = part(page, 'btn-crossfade')
    await expect(crossfade).toHaveAttribute('aria-pressed', /^(true|false)$/)
    await expect(crossfade).toHaveAttribute('data-state', /^(on|off)$/)
    if (desktopLayout(page)) {
        await expect(crossfade).toBeVisible()
    } else {
        // On phones and narrow tablets it belongs to the full-screen player.
        await expect(crossfade).toBeHidden()
    }
})

test('collapses the sidebar on the desktop layout only', async ({ page }) => {
    await page.goto('/library/songs')
    const toggle = part(page, 'btn-sidebar-toggle')
    if (desktopLayout(page)) {
        await expect(part(page, 'sidebar')).toBeVisible()
        await expect(toggle).toBeVisible()
        await expect(toggle).toHaveAttribute('aria-expanded', /^(true|false)$/)
    } else {
        await part(page, 'btn-menu').click()
        await expect(part(page, 'sidebar')).toHaveAttribute('data-state', /\bopen\b/)
        await expect(toggle).toBeHidden()
        await expect(
            part(page, 'tree').locator("[data-part='tree-item'] > [data-part='label']").first(),
        ).toBeVisible()
    }
})

test('fits the segmented controls into the screen', async ({ page }) => {
    for (const path of ['/library/songs', '/settings']) {
        await page.goto(path)
        const groups = part(page, 'segmented-control')
        await expect(groups.first()).toBeVisible()
        for (const group of await groups.all()) {
            await expect(group).toHaveAttribute('role', 'radiogroup')
            await expect(group.locator("[role='radio'][aria-checked='true']")).toHaveCount(1)
            const { x, width } = await box(group)
            expect(x, path).toBeGreaterThanOrEqual(0)
            expect(x + width, path).toBeLessThanOrEqual(page.viewportSize()!.width)
        }
    }
})

test('moves the indicators of the navigation to the current page', async ({ page }) => {
    await page.goto('/library/albums')
    await showIndicators(page)
    const name = desktopLayout(page) ? 'tabs' : 'bottom-nav'
    const { entry, active } = indicatorGroups[name]
    await expectIndicatorOnActive(part(page, name), entry, active)

    if (!desktopLayout(page)) {
        await part(page, 'btn-menu').click()
        await expect(part(page, 'sidebar')).toHaveAttribute('data-state', /\bopen\b/)
    }
    const tree = indicatorGroups.tree
    await expectIndicatorOnActive(part(page, 'tree'), tree.entry, tree.active)

    await page.goto('/admin/cache')
    await showIndicators(page)
    const admin = indicatorGroups['admin-nav']
    await expectIndicatorOnActive(part(page, 'admin-nav'), admin.entry, admin.active)
})
