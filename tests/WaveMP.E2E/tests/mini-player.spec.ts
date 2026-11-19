import { expect, test, type Locator, type Page } from '@playwright/test'
import { withoutSavedPlayback } from './helpers/player'

// The mini player (compact mode) with the synthetic test media of scripts/generate-media.sh:
// E2E Album holds First Song and Second Song, two seconds each. The compact mode button exists on
// the desktop only. Where the browser has the Document Picture-in-Picture API (Chromium), the
// mini player opens in a floating window; the fallback for other browsers, the compact view in
// the window of the app, is tested by hiding the API from the page.

function appPart(page: Page, name: string): Locator {
    return page.locator(`[data-part='app'] [data-part='${name}']`)
}

function miniPart(page: Page, name: string): Locator {
    return page.locator(`[data-part='compact-player'] [data-part='${name}']`)
}

/** Plays E2E Album with repeat, so the songs of two seconds never run out, and pauses it. */
async function playAlbumPaused(page: Page): Promise<void> {
    await page.goto('/library/albums')
    await appPart(page, 'tile')
        .filter({
            has: page.locator("[data-part='tile-title']", {
                hasText: 'E2E Album',
            }),
        })
        .click()
    await expect(appPart(page, 'detail-title')).toHaveText('E2E Album')
    await appPart(page, 'detail-actions').locator("[data-item='play']").click()
    await appPart(page, 'btn-play').click()
    await expect(appPart(page, 'btn-play')).toHaveAttribute('data-state', 'paused')
    await expect(appPart(page, 'now-playing-title')).toHaveText('First Song')
    await appPart(page, 'btn-repeat').click()
    await expect(appPart(page, 'btn-repeat')).toHaveAttribute('data-state', 'all')
}

// The queue, shuffle and repeat start empty and off: the tests of the built-in admin share one
// profile, so the saved playback state of another test must not be taken over.
test.beforeEach(async ({ page }) => {
    await withoutSavedPlayback(page)
})

test.describe('mini player', () => {
    test.beforeEach(({ isMobile }) => {
        test.skip(isMobile, 'The compact mode exists on the desktop only.')
    })

    test.describe('in the window of the app', () => {
        test.beforeEach(async ({ page }) => {
            // Like Firefox and Safari: no floating windows.
            await page.addInitScript(() => {
                delete (window as { documentPictureInPicture?: unknown }).documentPictureInPicture
            })
        })

        test('opens, controls the playback and closes', async ({ page }) => {
            await playAlbumPaused(page)
            const compact = appPart(page, 'btn-compact')
            await expect(compact).toHaveAttribute('aria-pressed', 'false')

            await compact.click()

            // Only the player shows: the app is hidden until the mini player closes.
            const player = page.locator("[data-part='compact-player']")
            await expect(player).toBeVisible()
            await expect(player).toHaveAttribute('data-kind', 'inline')
            await expect(page.locator("[data-part='app']")).toBeHidden()
            await expect(miniPart(page, 'compact-player-title')).toHaveText('First Song')
            await expect(miniPart(page, 'compact-player-artist')).toHaveText('E2E Artist')
            await expect(miniPart(page, 'cover')).toBeVisible()
            await expect(miniPart(page, 'btn-play')).toBeFocused()

            await miniPart(page, 'btn-play').click()
            await expect(player).toHaveAttribute('data-state', /\bplaying\b/)
            await expect(appPart(page, 'transport')).toHaveAttribute('data-state', /\bplaying\b/)

            await miniPart(page, 'btn-play').click()
            await expect(player).toHaveAttribute('data-state', /\bpaused\b/)

            await miniPart(page, 'btn-next').click()
            await expect(miniPart(page, 'compact-player-title')).toHaveText('Second Song')
            await expect(appPart(page, 'now-playing-title')).toHaveText('Second Song')

            await miniPart(page, 'btn-mute').click()
            await expect(miniPart(page, 'btn-mute')).toHaveAttribute('aria-pressed', 'true')
            await expect(appPart(page, 'btn-mute')).toHaveAttribute('aria-pressed', 'true')
            await miniPart(page, 'btn-mute').click()
            await expect(appPart(page, 'btn-mute')).toHaveAttribute('aria-pressed', 'false')

            await miniPart(page, 'btn-compact-close').click()
            await expect(player).toHaveCount(0)
            await expect(page.locator("[data-part='app']")).toBeVisible()
            await expect(compact).toHaveAttribute('aria-pressed', 'false')
            await expect(compact).toBeFocused()
            await expect(appPart(page, 'now-playing-title')).toHaveText('Second Song')
        })

        test('closes with Escape', async ({ page }) => {
            await page.goto('/')
            await appPart(page, 'btn-compact').click()
            const player = page.locator("[data-part='compact-player']")
            await expect(player).toBeVisible()
            await expect(miniPart(page, 'compact-player-title')).toHaveText('Nothing playing')

            await page.keyboard.press('Escape')

            await expect(player).toHaveCount(0)
            await expect(page.locator("[data-part='app']")).toBeVisible()
        })
    })

    test('opens in a floating window that controls the playback', async ({ page, context }) => {
        await playAlbumPaused(page)
        const compact = appPart(page, 'btn-compact')

        const opened = context.waitForEvent('page')
        await compact.click()
        const floating = await opened

        // The window shows the mini player with the theme of the app; the app stays as it is.
        const player = floating.locator("[data-part='compact-player']")
        await expect(player).toHaveAttribute('data-kind', 'window')
        await expect(floating.locator(':root')).toHaveAttribute(
            'data-theme',
            (await page.locator(':root').getAttribute('data-theme')) ?? '',
        )
        await expect(compact).toHaveAttribute('aria-pressed', 'true')
        await expect(page.locator("[data-part='app']")).toBeVisible()
        await expect(page.locator("[data-part='compact-player']")).toHaveCount(0)
        await expect(floating.locator("[data-part='compact-player-title']")).toHaveText(
            'First Song',
        )

        // The music plays in the window of the app.
        await floating.locator("[data-part='btn-play']").click()
        await expect(appPart(page, 'transport')).toHaveAttribute('data-state', /\bplaying\b/)
        await floating.locator("[data-part='btn-play']").click()
        await expect(appPart(page, 'transport')).toHaveAttribute('data-state', /\bpaused\b/)
        await floating.locator("[data-part='btn-next']").click()
        await expect(appPart(page, 'now-playing-title')).toHaveText('Second Song')
        await expect(floating.locator("[data-part='compact-player-title']")).toHaveText(
            'Second Song',
        )

        await floating.locator("[data-part='btn-compact-close']").click()
        await expect.poll(() => floating.isClosed()).toBe(true)
        await expect(compact).toHaveAttribute('aria-pressed', 'false')
    })
})
