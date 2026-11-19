import { expect, test, type Locator, type Page } from '@playwright/test'

// The context menus with the synthetic test media of scripts/generate-media.sh: right click and
// keyboard on the desktop, the multi-selection of songs, and the button "More options" on a phone.
// Nothing is changed in the library; the queue lives in the page only.

function part(page: Page, name: string): Locator {
    return page.locator(`[data-part='app'] [data-part='${name}']`)
}

function openMenu(page: Page): Locator {
    return page.locator("[data-part='context-menu']:popover-open:not([data-kind='submenu'])")
}

function menuEntry(page: Page, item: string): Locator {
    return page.locator(`[data-part='context-menu'] [data-item='${item}']`)
}

/** Songs that can be played (video-only songs cannot be queued). */
function playableRows(page: Page): Locator {
    return page.locator("[data-part='app'] [data-part='list-row']:not([data-state~='video-only'])")
}

test.describe('on the desktop', () => {
    test.beforeEach(({}, testInfo) => {
        test.skip(testInfo.project.name !== 'chromium', 'Right click and keyboard of the desktop')
    })

    test('opens the menu of an album with a right click and leads to its artist', async ({
        page,
    }) => {
        await page.goto('/library/albums')
        const tile = part(page, 'tile-item').filter({ hasText: 'E2E Album' }).first()
        await tile.click({ button: 'right' })

        await expect(openMenu(page)).toHaveAttribute('aria-label', 'Album actions')
        const items = openMenu(page).locator(":scope > [data-part='menu-item']")
        await expect
            .poll(() =>
                items.evaluateAll((elements) => elements.map((element) => element.dataset.item)),
            )
            .toEqual(['play', 'shuffle', 'add-to-queue', 'add-to-playlist', 'favorite', 'artist'])
        // The base shows "More options" only where no pointer can hover.
        await expect(tile.locator("[data-part='btn-item-menu']")).toBeHidden()

        await menuEntry(page, 'artist').click()
        await expect(page).toHaveURL(/\/library\/artists\/[0-9a-f-]+$/)
        await expect(openMenu(page)).toHaveCount(0)
    })

    test('opens the menu of a song with the keyboard and moves through it with the arrow keys', async ({
        page,
    }) => {
        await page.goto('/library/songs')
        const row = playableRows(page).first()
        await row.click()
        await row.press('Shift+F10')

        await expect(openMenu(page)).toHaveAttribute('aria-label', 'Song actions')
        await expect(menuEntry(page, 'play')).toBeFocused()
        await page.keyboard.press('ArrowDown')
        await expect(menuEntry(page, 'play-next')).toBeFocused()

        await page.keyboard.press('Escape')
        await expect(openMenu(page)).toHaveCount(0)
        await expect(row).toBeFocused()

        await row.press('ContextMenu')
        await expect(menuEntry(page, 'play')).toBeFocused()
        for (let step = 0; step < 5; step++) {
            await page.keyboard.press('ArrowDown')
        }
        const rate = menuEntry(page, 'rate')
        await expect(rate).toBeFocused()
        await expect(rate).toHaveAttribute('aria-haspopup', 'menu')

        await page.keyboard.press('ArrowRight')
        await expect(rate).toHaveAttribute('aria-expanded', 'true')
        const submenu = page.locator("[data-part='context-menu'][data-kind='submenu']")
        await expect(submenu).toBeVisible()
        await expect(submenu.locator("[role='menuitemradio']")).toHaveCount(6)
        await expect(submenu.locator("[role='menuitemradio']:focus")).toHaveCount(1)

        await page.keyboard.press('ArrowLeft')
        await expect(rate).toHaveAttribute('aria-expanded', 'false')
        await expect(rate).toBeFocused()

        await page.keyboard.press('Escape')
        await expect(openMenu(page)).toHaveCount(0)
    })

    test('selects several songs with Ctrl and Shift and queues them all from the menu', async ({
        page,
    }) => {
        await page.goto('/library/songs')
        const rows = part(page, 'list-row')
        await expect(rows.nth(2)).toBeVisible()
        const selected = page.locator(
            "[data-part='app'] [data-part='list-row'][aria-selected='true']",
        )

        await rows.first().click()
        await rows.nth(2).click({ modifiers: ['Shift'] })
        await expect(selected).toHaveCount(3)
        await rows.nth(1).click({ modifiers: ['ControlOrMeta'] })
        await expect(rows.nth(1)).toHaveAttribute('aria-selected', 'false')
        await rows.nth(1).click({ modifiers: ['ControlOrMeta'] })
        await expect(selected).toHaveCount(3)

        // Songs with a video only cannot be queued.
        const playable = await selected.evaluateAll(
            (elements) =>
                elements.filter((element) => !element.dataset.state?.includes('video-only')).length,
        )
        expect(playable).toBeGreaterThan(1)

        await rows.first().click({ button: 'right' })
        await expect(openMenu(page)).toHaveAttribute('aria-label', 'Actions for 3 songs')
        await expect(menuEntry(page, 'details')).toBeDisabled()
        await menuEntry(page, 'add-to-queue').click()
        await expect(selected).toHaveCount(3)

        await page
            .locator("[data-part='app'] a[href='/now-playing']")
            .filter({ visible: true })
            .click()
        await expect(part(page, 'queue-item')).toHaveCount(playable)

        // The menu of a queue entry moves the last one up.
        const titles = part(page, 'queue-item').locator("[data-part='queue-item-title']")
        const lastTitle = (await titles.last().textContent()) ?? ''
        await part(page, 'queue-item').last().click({ button: 'right' })
        await expect(openMenu(page)).toHaveAttribute('aria-label', 'Queue entry actions')
        await menuEntry(page, 'move-up').click()
        await expect(titles.nth(playable - 2)).toHaveText(lastTitle)
    })
})

test.describe('on a phone', () => {
    test.beforeEach(({}, testInfo) => {
        test.skip(testInfo.project.name !== 'mobile', 'The button "More options" of touch screens')
    })

    test('opens the menu of a song with the button "More options"', async ({ page }) => {
        await page.goto('/library/songs')
        const row = playableRows(page).first()
        const button = row.locator("[data-part='btn-item-menu']")
        await expect(button).toBeVisible()

        await button.tap()
        await expect(openMenu(page)).toHaveAttribute('aria-label', 'Song actions')
        await expect(menuEntry(page, 'play')).toBeEnabled()
        await page.keyboard.press('Escape')
        await expect(openMenu(page)).toHaveCount(0)
    })

    test('opens the menu of an album tile with the button "More options"', async ({ page }) => {
        await page.goto('/library/albums')
        const tile = part(page, 'tile-item').filter({ hasText: 'E2E Album' }).first()
        await tile.locator("[data-part='btn-item-menu']").tap()

        await expect(openMenu(page)).toHaveAttribute('aria-label', 'Album actions')
        await expect(page).toHaveURL(/\/library\/albums$/)
    })
})
