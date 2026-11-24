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
