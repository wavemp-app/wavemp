import { expect, test, type Locator, type Page } from '@playwright/test'
import { withoutSavedPlayback } from './helpers/player'
import { chooseFromProfileMenu } from './helpers/navigation'

// The keyboard shortcuts with the synthetic test media of scripts/generate-media.sh: E2E Album
// holds First Song and Second Song, two seconds each. ControlOrMeta is Ctrl, and Command on a Mac,
// like the shortcuts of the app.

function appPart(page: Page, name: string): Locator {
    return page.locator(`[data-part='app'] [data-part='${name}']`)
}

function searchInput(page: Page): Locator {
    return page.locator("[data-part='app'] [data-part='toolbar'] [data-part='search'] input")
}

/** Leaves the focus on the page itself, like after a click into an empty area. */
async function blur(page: Page): Promise<void> {
    await page.evaluate(() => (document.activeElement as HTMLElement | null)?.blur())
}

/** Plays E2E Album and pauses it at once, with the focus on the page. */
async function playAlbumPaused(page: Page): Promise<void> {
    await page.goto('/library/albums')
    await appPart(page, 'tile')
        .filter({ has: page.locator("[data-part='tile-title']", { hasText: 'E2E Album' }) })
        .click()
    await expect(appPart(page, 'detail-title')).toHaveText('E2E Album')
    await appPart(page, 'detail-actions').locator("[data-item='play']").click()
    await appPart(page, 'btn-play').click()
    await expect(appPart(page, 'btn-play')).toHaveAttribute('data-state', 'paused')
    await expect(appPart(page, 'now-playing-title')).toHaveText('First Song')
    await blur(page)
}

async function volume(page: Page): Promise<number> {
    const value = await appPart(page, 'volume')
        .locator("[role='slider']")
        .getAttribute('aria-valuenow')
    return Number(value)
}

// The queue, shuffle and repeat start empty and off: the tests of the built-in admin share one
// profile, so the saved playback state of another test must not be taken over.
test.beforeEach(async ({ page }) => {
    await withoutSavedPlayback(page)
})

test.describe('keyboard shortcuts', () => {
    test('control the player', async ({ page }) => {
        await playAlbumPaused(page)
        const transport = appPart(page, 'transport')

        // R first: with repeat the songs of two seconds never run out.
        await page.keyboard.press('r')
        await expect(appPart(page, 'btn-repeat')).toHaveAttribute('data-state', 'all')

        await page.keyboard.press('Space')
        await expect(transport).toHaveAttribute('data-state', /\bplaying\b/)
        await page.keyboard.press('Space')
        await expect(transport).toHaveAttribute('data-state', /\bpaused\b/)

        await page.keyboard.press('ControlOrMeta+ArrowRight')
        await expect(appPart(page, 'now-playing-title')).toHaveText('Second Song')
        await page.keyboard.press('ControlOrMeta+ArrowLeft')
        await expect(appPart(page, 'now-playing-title')).toHaveText('First Song')

        await page.keyboard.press('s')
        await expect(appPart(page, 'btn-shuffle')).toHaveAttribute('aria-pressed', 'true')
        await page.keyboard.press('s')
        await expect(appPart(page, 'btn-shuffle')).toHaveAttribute('aria-pressed', 'false')

        await page.keyboard.press('m')
        await expect(appPart(page, 'btn-mute')).toHaveAttribute('aria-pressed', 'true')
        await page.keyboard.press('m')
        await expect(appPart(page, 'btn-mute')).toHaveAttribute('aria-pressed', 'false')

        // The volume moves in steps of 5 %.
        const start = await volume(page)
        await page.keyboard.press('ArrowDown')
        await expect.poll(() => volume(page)).toBeCloseTo(Math.max(0, start - 0.05), 2)
        await page.keyboard.press('ArrowUp')
        await expect.poll(() => volume(page)).toBeCloseTo(start, 2)

        await page.keyboard.press('r')
        await expect(appPart(page, 'btn-repeat')).toHaveAttribute('data-state', 'one')
        await page.keyboard.press('r')
        await expect(appPart(page, 'btn-repeat')).toHaveAttribute('data-state', 'off')
    })

    test('do not apply while a field has the focus', async ({ page }) => {
        await playAlbumPaused(page)

        await searchInput(page).click()
        await page.keyboard.press('m')
        await page.keyboard.press('Space')

        await expect(searchInput(page)).toHaveValue('m ')
        await expect(appPart(page, 'btn-mute')).toHaveAttribute('aria-pressed', 'false')
        await expect(appPart(page, 'transport')).toHaveAttribute('data-state', /\bpaused\b/)
    })

    test('focus the search field with Ctrl+F', async ({ page }) => {
        await page.goto('/library/albums')
        await expect(appPart(page, 'tile').first()).toBeVisible()
        await blur(page)

        await page.keyboard.press('ControlOrMeta+f')

        await expect(searchInput(page)).toBeFocused()
    })

    test('show the full screen with F and leave it again', async ({ page }) => {
        await page.goto('/')
        await expect(appPart(page, 'content')).toBeVisible()
        await blur(page)

        await page.keyboard.press('f')
        await expect
            .poll(() => page.evaluate(() => document.fullscreenElement !== null))
            .toBe(true)

        await page.keyboard.press('f')
        await expect
            .poll(() => page.evaluate(() => document.fullscreenElement !== null))
            .toBe(false)
    })

    test('list every shortcut in the overview', async ({ page }) => {
        await page.goto('/')
        await expect(appPart(page, 'content')).toBeVisible()
        await blur(page)

        await page.keyboard.press('?')

        const overview = page.locator("[data-part='dialog'][data-item='shortcuts']")
        await expect(overview).toBeVisible()
        await expect(overview.locator("[data-part='dialog-title']")).toHaveText(
            'Keyboard shortcuts',
        )
        await expect(overview.locator("[data-part='shortcut-item']")).toHaveCount(13)
        await expect(
            overview.locator("[data-part='shortcut-item'][data-item='mute'] [data-part='shortcut-key']"),
        ).toHaveText('M')

        // The shortcuts rest while the overview is open; Escape closes it.
        await page.keyboard.press('m')
        await expect(appPart(page, 'btn-mute')).toHaveAttribute('aria-pressed', 'false')
        await page.keyboard.press('Escape')
        await expect(overview).toBeHidden()

        await chooseFromProfileMenu(page, 'shortcuts')
        await expect(overview).toBeVisible()
    })
})
