import { expect, test, type Locator, type Page } from '@playwright/test'
import { expectIndicatorOnActive, indicatorGroups, showIndicators } from './helpers/indicator'

// Theme-capable DOM: the groups with one active entry carry an indicator and the box of the active
// entry, so a theme can move a line or a highlight to it with CSS only. The tests show the
// indicators like a theme would and expect them on the active entry, also after it changed.

function part(page: Page, name: string): Locator {
    return page.locator(`[data-part='app'] [data-part='${name}']`)
}

test('moves the indicator of the main navigation to the current page', async ({
    page,
    isMobile,
}) => {
    await page.goto('/library/songs')
    await showIndicators(page)
    const name = isMobile ? 'bottom-nav' : 'tabs'
    const navigation = part(page, name)
    const { entry, active } = indicatorGroups[name]

    await expectIndicatorOnActive(navigation, entry, active)
    await expect(navigation.locator(active)).toHaveAttribute('href', '/library')

    await navigation.locator("a[href='/playlists']").click()
    await expect(page).toHaveURL(/\/playlists$/)
    await expectIndicatorOnActive(navigation, entry, active)
    await expect(navigation.locator(active)).toHaveAttribute('href', '/playlists')
})

test('moves the indicator of the tree to the current page', async ({ page, isMobile }) => {
    await page.goto('/library/albums')
    await showIndicators(page)
    if (isMobile) {
        await part(page, 'btn-menu').click()
        await expect(part(page, 'sidebar')).toHaveAttribute('data-state', /\bopen\b/)
    }
    const tree = part(page, 'tree')
    const { entry, active } = indicatorGroups.tree

    await expectIndicatorOnActive(tree, entry, active)
    await expect(tree.locator(active)).toHaveAttribute('href', '/library/albums')

    await tree.locator("[data-part='tree-item'][href='/library/genres']").click()
    await expect(page).toHaveURL(/\/library\/genres$/)
    if (isMobile) {
        await part(page, 'btn-menu').click()
        await expect(part(page, 'sidebar')).toHaveAttribute('data-state', /\bopen\b/)
    }
    await expectIndicatorOnActive(tree, entry, active)
    await expect(tree.locator(active)).toHaveAttribute('href', '/library/genres')
})

test('moves the indicator of the administration to the current page', async ({ page }) => {
    await page.goto('/admin/library')
    await showIndicators(page)
    const navigation = part(page, 'admin-nav')
    const { entry, active } = indicatorGroups['admin-nav']

    await expectIndicatorOnActive(navigation, entry, active)

    await navigation.locator("[data-item='admin-users']").click()
    await expect(page).toHaveURL(/\/admin\/users$/)
    await expectIndicatorOnActive(navigation, entry, active)
    await expect(navigation.locator(active)).toHaveAttribute('data-item', 'admin-users')
})

test('moves the indicator of a segmented control to the checked option', async ({ page }) => {
    await page.goto('/library/songs')
    await showIndicators(page)
    const group = part(page, 'scope-switch').locator("[data-part='segmented-control']")
    const { entry, active } = indicatorGroups['segmented-control']

    await expectIndicatorOnActive(group, entry, active)
    await expect(group.locator(active)).toHaveAttribute('data-item', 'all-music')

    await group.locator("[data-item='my-music']").click()
    await expect(page).toHaveURL(/myMusic=true/)
    await expectIndicatorOnActive(group, entry, active)
    await expect(group.locator(active)).toHaveAttribute('data-item', 'my-music')
})

test('moves the indicator of the color picker to the chosen color', async ({ page }) => {
    await page.goto('/admin/users')
    await part(page, 'view-actions').locator('button').first().click()
    const dialog = page.locator("[data-part='dialog'][open]")
    await expect(dialog).toBeVisible()
    await showIndicators(page)
    const picker = dialog.locator("[data-part='color-picker']")
    const swatches = picker.locator("[data-part='color-swatch']")
    const entry = "[data-part='color-swatch']"
    const active = "[data-part='color-swatch'][aria-checked='true']"

    await swatches.nth(1).click()
    await expectIndicatorOnActive(picker, entry, active)

    await swatches.last().click()
    await expect(swatches.last()).toHaveAttribute('aria-checked', 'true')
    await expectIndicatorOnActive(picker, entry, active)
    await page.keyboard.press('Escape')
    await expect(dialog).toBeHidden()
})
