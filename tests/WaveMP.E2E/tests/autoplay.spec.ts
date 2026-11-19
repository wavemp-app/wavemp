import { expect, test, type APIRequestContext, type Locator, type Page } from '@playwright/test'
import { createUser, deleteUser, uniqueSuffix, type TestUser } from './helpers/api'
import { homeUrl, profileTile } from './helpers/navigation'
import { emptyStorageState } from './helpers/storage'

// Autoplay with the synthetic test media of scripts/generate-media.sh: E2E Album of E2E Artist
// holds First Song and Second Song (two seconds each); the only other song of E2E Artist is Video
// Only, a music video without audio file. So when the album has ended, autoplay goes on with Video
// Only and then has no more songs.
// Autoplay belongs to the profile, so the tests select their own profile: turning it off must not
// change the player of the tests that run with the built-in admin at the same time.

function part(page: Page, name: string): Locator {
    return page.locator(`[data-part='app'] [data-part='${name}']`)
}

async function openAlbum(page: Page): Promise<void> {
    await page.goto('/library/albums')
    await part(page, 'tile')
        .filter({
            has: page.locator("[data-part='tile-title']", {
                hasText: 'E2E Album',
            }),
        })
        .click()
    await expect(part(page, 'detail-title')).toHaveText('E2E Album')
}

/** Opens Now Playing without loading the page again, so the player keeps its queue. */
async function openNowPlaying(page: Page): Promise<void> {
    await page.locator("[data-part='app'] a[href='/now-playing']").filter({ visible: true }).click()
    await expect(page).toHaveURL(/\/now-playing$/)
}

async function storedAutoplay(request: APIRequestContext, profileId: string): Promise<unknown> {
    const session = await request.post('/api/v1/sessions', {
        data: { profileId },
    })
    expect(session.status()).toBe(201)
    const response = await request.get('/api/v1/me/settings', {
        headers: { Authorization: `Bearer ${(await session.json()).token}` },
    })
    expect(response.status()).toBe(200)
    return (await response.json()).autoplay
}

test.describe('autoplay', () => {
    test.use({ storageState: emptyStorageState, locale: 'en-US' })

    let user: TestUser

    test.beforeEach(async ({ page, request }) => {
        user = await createUser(request, `Autoplay ${uniqueSuffix()}`)
        await page.goto('/profiles')
        await profileTile(page, user.name).click()
        await expect(page).toHaveURL(homeUrl)
    })

    test.afterEach(async ({ request }) => {
        await deleteUser(request, user.id)
    })

    test('goes on with a song of the artist when the album has ended', async ({ page }) => {
        await openAlbum(page)
        await part(page, 'detail-actions').locator("[data-item='play']").click()
        await expect(part(page, 'now-playing-title')).toHaveText('First Song')
        await openNowPlaying(page)

        // Autoplay is on for a new profile: the button is pressed, and the song that follows the
        // album waits below the queue.
        await expect(part(page, 'btn-autoplay')).toHaveAttribute('aria-pressed', 'true')
        const autoplay = part(page, 'autoplay')
        await expect(autoplay.locator("[data-part='section-title']")).toHaveText('Autoplay')
        await expect(autoplay.locator("[data-part='autoplay-item-title']")).toHaveText([
            'Video Only',
        ])
        await expect(autoplay.locator("[data-part='autoplay-item']")).toHaveAttribute(
            'data-state',
            'video-only',
        )

        // The album ends: Video Only moves into the queue, marked as autoplay, and plays.
        await expect(part(page, 'now-playing-title')).toHaveText('Video Only', {
            timeout: 30_000,
        })
        const added = part(page, 'queue-item').filter({
            hasText: 'Video Only',
        })
        await expect(added).toHaveAttribute('data-state', /\bautoplay\b/)
        await expect(added).toHaveAttribute('aria-current', 'true')
        await expect(part(page, 'queue-item').locator("[data-part='queue-item-title']")).toHaveText(
            ['First Song', 'Second Song', 'Video Only'],
        )
        await expect(autoplay).toHaveAttribute('data-state', 'empty')
        await expect(autoplay.locator("[data-part='autoplay-status']")).toContainText(
            'No similar songs left',
        )

        // Without more songs, the player pauses at the end of the song of autoplay.
        await expect(part(page, 'btn-play')).toHaveAttribute('data-state', 'paused', {
            timeout: 30_000,
        })
        await expect(part(page, 'now-playing-title')).toHaveText('Video Only')
    })

    test('turns autoplay off below the queue and keeps it off for the profile', async ({
        page,
        request,
    }) => {
        await openAlbum(page)
        await part(page, 'detail-actions').locator("[data-item='play']").click()
        await part(page, 'btn-play').click()
        await expect(part(page, 'btn-play')).toHaveAttribute('data-state', 'paused')
        await openNowPlaying(page)

        const autoplay = part(page, 'autoplay')
        await expect(autoplay.locator("[data-part='autoplay-item-title']")).toHaveText([
            'Video Only',
        ])
        await autoplay.locator("[data-item='turn-off-autoplay']").click()

        await expect(autoplay).toHaveCount(0)
        await expect(part(page, 'btn-autoplay')).toHaveAttribute('aria-pressed', 'false')
        await expect
            .poll(() => storedAutoplay(request, user.id), { timeout: 15_000 })
            .toEqual({ enabled: false, includeVideoOnly: true })

        // The album plays to its end and stops there.
        await part(page, 'btn-play').click()
        await expect(part(page, 'now-playing-title')).toHaveText('Second Song', {
            timeout: 15_000,
        })
        await expect(part(page, 'btn-play')).toHaveAttribute('data-state', 'paused', {
            timeout: 15_000,
        })
        await expect(part(page, 'now-playing-title')).toHaveText('Second Song')
        await expect(part(page, 'queue-item')).toHaveCount(2)
    })
})
