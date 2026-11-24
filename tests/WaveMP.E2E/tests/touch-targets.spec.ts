import { expect, test, type Page } from '@playwright/test'
import { sessionHeaders, send } from '../screenshots/helpers'
import { shippedThemeTargets, themeProfileName } from '../screenshots/themes'
import { adminToken, deleteUser, findAlbumId, uniqueSuffix } from './helpers/api'
import { homeUrl, profileTile } from './helpers/navigation'
import { emptyStorageState } from './helpers/storage'

// Touch targets on phones in every shipped theme: everything a finger taps is at least 44 pixels
// high, buttons with only an icon also as wide (THEMING.md, "Touch targets"). The entries of the
// drawer, the header, lists, tiles, detail pages, forms and the administration are measured.

test.skip(({ isMobile }) => !isMobile, 'Mobile layout only')
test.use({ storageState: emptyStorageState })

/** 2.75rem; a little less is rounding. */
const minimum = 43.5

/**
 * The visible targets of the page smaller than the minimum, as "part (text): width x height".
 * A checkbox counts with its label, a slider with the whole slider instead of its knob. The bars
 * of charts are exempt, and so is what lies in an inert area (behind the open drawer).
 */
async function smallTargets(page: Page): Promise<string[]> {
    // Size changes of the theme (transitions) have settled.
    await page.waitForTimeout(400)
    return page.evaluate((min) => {
        const selector = [
            'a[href]',
            'button',
            'input:not([type="hidden"])',
            'select',
            'textarea',
            '[role="button"]',
            '[role="radio"]',
            '[role="tab"]',
            '[role="treeitem"]',
            '[role="menuitem"]',
            '[role="menuitemradio"]',
            '[role="checkbox"]',
            '[role="switch"]',
            '[role="slider"]',
            '[data-part="tree-toggle"]',
        ].join(', ')
        const app = document.querySelector("[data-part='app']")!
        const small = new Set<string>()
        for (const found of app.querySelectorAll<HTMLElement>(selector)) {
            if (
                found.closest("[inert], [data-part='chart']") ||
                !found.checkVisibility({ visibilityProperty: true, opacityProperty: true }) ||
                getComputedStyle(found).pointerEvents === 'none'
            ) {
                continue
            }
            let target: HTMLElement = found
            if (found.matches("input[type='checkbox']") && found.closest('label')) {
                target = found.closest('label')!
            } else if (found.matches("[data-part='slider-thumb']")) {
                target = found.closest<HTMLElement>("[data-part='slider']")!
            }
            if (!target.checkVisibility({ visibilityProperty: true, opacityProperty: true })) {
                continue
            }
            const box = target.getBoundingClientRect()
            if (box.width === 0 || box.height === 0) {
                continue
            }
            // Buttons with only an icon (a label hidden for the eye is 1 pixel wide) need the
            // width as well.
            const iconOnly =
                target.matches('button, [role="button"]') &&
                ![...target.querySelectorAll("[data-part='label']")].some(
                    (label) => label.checkVisibility() && label.getBoundingClientRect().width > 1,
                )
            const part = target.getAttribute('data-part') ?? target.tagName.toLowerCase()
            const text = (target.textContent ?? '').trim().slice(0, 24)
            if (box.height < min || (iconOnly && box.width < min)) {
                small.add(`${part} (${text}): ${Math.round(box.width)}x${Math.round(box.height)}`)
            }
        }
        return [...small]
    }, minimum)
}

for (const target of shippedThemeTargets()) {
    test(`keeps the touch targets at 44 pixels in ${target.name} (${target.variant})`, async ({
        page,
        request,
    }) => {
        // An admin, so the header shows the review inbox and the administration can be opened.
        const created = await request.post('/api/v1/users', {
            data: { name: `${themeProfileName(target)} Touch ${uniqueSuffix()}`, role: 'Admin' },
            headers: { Authorization: `Bearer ${await adminToken(request)}` },
        })
        expect(created.status(), await created.text()).toBe(201)
        const user = (await created.json()) as { id: string; name: string }
        try {
            const headers = await sessionHeaders(request, user.id)
            await send(request, headers, 'patch', '/api/v1/me/settings', {
                language: 'en',
                theme: {
                    id: target.theme,
                    appearance: target.variant === 'dark' ? 'Dark' : 'Light',
                },
            })
            await page.goto('/profiles')
            await profileTile(page, user.name).click()
            await expect(page).toHaveURL(homeUrl)
            await expect(page.locator(':root')).toHaveAttribute('data-theme', target.theme)
            const theme = `${target.theme} ${target.variant}`

            const album = await findAlbumId(request, 'E2E Album')
            const pages = [
                { url: '/', ready: "[data-part='tile']" },
                { url: '/library/songs', ready: "[data-part='list-row']" },
                { url: `/library/albums/${album}`, ready: "[data-part='detail-title']" },
                { url: '/library/albums', ready: "[data-part='tile']" },
                { url: '/settings', ready: "[data-part='theme-option']" },
                { url: '/admin/library', ready: "[data-part='section']" },
                { url: '/admin/organize', ready: "[data-part='section'][data-item='own-albums']" },
                { url: '/admin/organize/albums/new', ready: "[data-part='cover-drop']" },
            ]
            for (const { url, ready } of pages) {
                await page.goto(url)
                await expect(page.locator(ready).first()).toBeVisible()
                expect(await smallTargets(page), `${theme} ${url}`).toEqual([])
            }

            // The drawer with the whole tree of the sidebar.
            await page.goto('/library/songs')
            await page.locator("[data-part='app'] [data-part='btn-menu']").click()
            const sidebar = page.locator("[data-part='app'] > [data-part='sidebar']")
            await expect(sidebar).toHaveAttribute('data-state', /open/)
            await expect(sidebar.locator("[data-part='tree-item']").first()).toBeVisible()
            expect(await smallTargets(page), `${theme} drawer`).toEqual([])
            const entries = await sidebar
                .locator("[data-part='tree-item']")
                .evaluateAll((items) =>
                    items
                        .filter((item) => item.checkVisibility())
                        .map((item) => item.getBoundingClientRect().height),
                )
            expect(entries.length, theme).toBeGreaterThan(5)
            for (const height of entries) {
                expect(height, `${theme} drawer entry`).toBeGreaterThanOrEqual(minimum)
            }
        } finally {
            await deleteUser(request, user.id)
        }
    })
}
