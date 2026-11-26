import { expect, test, type APIRequestContext, type Locator, type Page } from '@playwright/test'
import { createUser, deleteUser, findTrackId, uniqueSuffix, type TestUser } from './helpers/api'
import { homeUrl, profileTile } from './helpers/navigation'
import { emptyStorageState } from './helpers/storage'

// Resuming playback with the synthetic test media of scripts/generate-media.sh: E2E Album holds
// First Song and Second Song, two seconds each. The playback state belongs to the profile, so the
// tests select a profile of their own: the tests of the built-in admin play at the same time.

/** A second device of the profile: Safari on an iPhone, so its name is known. */
const phoneUserAgent =
    'Mozilla/5.0 (iPhone; CPU iPhone OS 18_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) ' +
    'Version/18.1 Mobile/15E148 Safari/604.1'

function part(page: Page, name: string): Locator {
    return page.locator(`[data-part='app'] [data-part='${name}']`)
}

async function enterAs(page: Page, user: TestUser): Promise<void> {
    await page.goto('/profiles')
    await profileTile(page, user.name).click()
    await expect(page).toHaveURL(homeUrl)
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

/** Position of the player in seconds, as the seek slider tells it. */
async function position(page: Page): Promise<number> {
    return Number(await part(page, 'seek').locator("[role='slider']").getAttribute('aria-valuenow'))
}

interface SavedState {
    currentIndex: number
    positionMilliseconds: number
    items: { track: { title: string } }[]
    device: { id: string; name: string }
}

/** Authorization header of a new session of the profile. */
async function profileAuthorization(
    request: APIRequestContext,
    user: TestUser,
): Promise<Record<string, string>> {
    const session = await request.post('/api/v1/sessions', {
        data: { profileId: user.id },
    })
    expect(session.status()).toBe(201)
    return { Authorization: `Bearer ${(await session.json()).token}` }
}

async function savedState(
    request: APIRequestContext,
    user: TestUser,
): Promise<SavedState | undefined> {
    const response = await request.get('/api/v1/me/playback-state', {
        headers: await profileAuthorization(request, user),
    })
    return response.status() === 200 ? ((await response.json()) as SavedState) : undefined
}

/** The current song and the position in whole seconds of the saved state. */
async function savedPoint(request: APIRequestContext, user: TestUser) {
    const state = await savedState(request, user)
    return state
        ? {
              title: state.items[state.currentIndex]?.track.title,
              seconds: Math.floor(state.positionMilliseconds / 1000),
          }
        : null
}

test.describe('resume playback', () => {
    test.use({ storageState: emptyStorageState, locale: 'en-US' })

    let user: TestUser

    test.beforeEach(async ({ page, request }) => {
        user = await createUser(request, `Resume ${uniqueSuffix()}`)
        await enterAs(page, user)
    })

    test.afterEach(async ({ request }) => {
        await deleteUser(request, user.id)
    })

    test('shows the queue and the position after a reload and goes on there', async ({
        page,
        request,
        isMobile,
    }) => {
        await openAlbum(page)
        // The songs last two seconds: pausing right away keeps the first one from ending.
        await part(page, 'detail-actions').locator("[data-item='play']").click()
        await part(page, 'btn-play').click()
        await expect(part(page, 'btn-play')).toHaveAttribute('data-state', 'paused')
        await expect(part(page, 'now-playing-title')).toHaveText('First Song')
        await expect(part(page, 'transport')).not.toHaveAttribute('data-state', /\bloading\b/)
        if (isMobile) {
            // The seek slider of the full-screen player.
            await part(page, 'now-playing-mini').click()
            await expect(part(page, 'transport')).toHaveAttribute('data-state', /\bexpanded\b/)
        }

        // Seeking while paused saves shortly after. The position holds, even right after the start.
        await part(page, 'seek').getByRole('slider').focus()
        await page.keyboard.press('Home')
        await page.keyboard.press('ArrowRight')
        expect(await position(page)).toBe(1)
        await page.waitForTimeout(1000)
        expect(await position(page)).toBe(1)
        await expect
            .poll(() => savedPoint(request, user), { timeout: 15_000 })
            .toEqual({ title: 'First Song', seconds: 1 })

        await page.reload()

        // The player shows the saved queue at the saved position and does not play by itself.
        await expect(part(page, 'now-playing-title')).toHaveText('First Song')
        await expect(part(page, 'btn-play')).toHaveAttribute('data-state', 'paused')
        await expect(part(page, 'transport')).not.toHaveAttribute('data-state', /\bloading\b/)
        await expect.poll(() => position(page)).toBe(1)
        await expect(part(page, 'resume-notice')).toHaveCount(0)
        await page
            .locator("[data-part='app'] a[href='/now-playing']")
            .filter({ visible: true })
            .click()
        await expect(
            part(page, 'queue-item')
                .filter({ hasNot: page.locator("[data-state~='autoplay']") })
                .locator("[data-part='queue-item-title']"),
        ).toHaveText(['First Song', 'Second Song'])

        // Play goes on at the saved position, not at the start of the song.
        await part(page, 'btn-play').click()
        await expect(part(page, 'transport')).toHaveAttribute('data-state', /\bplaying\b/)
        await part(page, 'btn-play').click()
        await expect(part(page, 'btn-play')).toHaveAttribute('data-state', 'paused')
        expect(await position(page)).toBeGreaterThanOrEqual(1)
    })

    test('follows the profile to another device', async ({ page, request, browser, isMobile }) => {
        await openAlbum(page)
        await part(page, 'detail-actions').locator("[data-item='play']").click()
        await part(page, 'btn-play').click()
        await expect(part(page, 'btn-play')).toHaveAttribute('data-state', 'paused')
        if (isMobile) {
            await part(page, 'now-playing-mini').click()
            await expect(part(page, 'transport')).toHaveAttribute('data-state', /\bexpanded\b/)
        }
        await part(page, 'btn-next').click()
        await expect(part(page, 'now-playing-title')).toHaveText('Second Song')
        await expect(part(page, 'btn-play')).toHaveAttribute('data-state', 'paused')
        await expect
            .poll(() => savedPoint(request, user), { timeout: 15_000 })
            .toEqual({ title: 'Second Song', seconds: 0 })
        const first = (await savedState(request, user))!.device.name

        // The phone takes over the state and names the device it comes from.
        const phone = await browser.newContext({
            storageState: emptyStorageState,
            locale: 'en-US',
            userAgent: phoneUserAgent,
            viewport: { width: 390, height: 844 },
            isMobile: true,
            hasTouch: true,
        })
        try {
            const other = await phone.newPage()
            await enterAs(other, user)
            const notice = part(other, 'resume-notice')
            await expect(notice).toBeVisible()
            await expect(notice.locator(":scope > [data-part='label']")).toHaveText(
                `Continue listening from ${first}`,
            )
            await expect(part(other, 'now-playing-title')).toHaveText('Second Song')
            await expect(part(other, 'btn-play')).toHaveAttribute('data-state', 'paused')

            // Continue plays there; the phone now has the latest state.
            await notice.locator("[data-part='btn-resume']").click()
            await expect(notice).toHaveCount(0)
            await expect(part(other, 'transport')).toHaveAttribute('data-state', /\bplaying\b/)
            await part(other, 'btn-play').click()
            await expect(part(other, 'btn-play')).toHaveAttribute('data-state', 'paused')
            await expect
                .poll(async () => (await savedState(request, user))?.device.name, {
                    timeout: 15_000,
                })
                .toBe('Safari · iPhone')

            // Back on the first device, the newer state of the phone wins.
            await page.reload()
            const back = part(page, 'resume-notice')
            await expect(back.locator(":scope > [data-part='label']")).toHaveText(
                'Continue listening from Safari · iPhone',
            )
            await back.locator("[data-part='btn-dismiss-resume']").click()
            await expect(back).toHaveCount(0)
        } finally {
            await phone.close()
        }
    })

    test('keeps the state of another device when the restored song cannot be loaded', async ({
        page,
        request,
    }) => {
        const first = await findTrackId(request, 'First Song')
        // The player leaves songs with only a video out of its queue; the saved state keeps them.
        const videoOnly = await findTrackId(request, 'Video Only')
        const second = await findTrackId(request, 'Second Song')
        const saved = await request.put('/api/v1/me/playback-state', {
            headers: await profileAuthorization(request, user),
            data: {
                items: [{ trackId: first }, { trackId: videoOnly }, { trackId: second }],
                currentIndex: 0,
                positionMilliseconds: 1000,
                device: { id: 'e2e-other-device', name: 'E2E Device' },
            },
        })
        expect(saved.status()).toBe(204)

        // The request for the restored song fails, like one that a reload or navigation cancels.
        const playback = `**/api/v1/tracks/${first}/playback**`
        await page.route(playback, (route) => route.abort())
        let saves = 0
        page.on('request', (sent) => {
            if (sent.method() === 'PUT' && sent.url().includes('/api/v1/me/playback-state')) {
                saves++
            }
        })

        await page.reload()

        // The song waits paused at its position; nothing moves on by itself.
        await expect(part(page, 'now-playing-title')).toHaveText('First Song')
        await expect(part(page, 'btn-play')).toHaveAttribute('data-state', 'paused')
        await expect(part(page, 'transport')).not.toHaveAttribute('data-state', /\bloading\b/)
        await expect(part(page, 'resume-notice')).toBeVisible()
        // Longer than the delay of a save; leaving the page saves what changed, too.
        await page.waitForTimeout(3000)
        await page.goto('/settings')
        await expect(part(page, 'now-playing-title')).toHaveText('First Song')
        await expect(part(page, 'btn-play')).toHaveAttribute('data-state', 'paused')
        await page.waitForTimeout(3000)

        expect(saves).toBe(0)
        const state = await savedState(request, user)
        expect(state?.device.id).toBe('e2e-other-device')
        expect(state?.currentIndex).toBe(0)
        expect(state?.positionMilliseconds).toBe(1000)
        expect(state?.items.map((item) => item.track.title)).toEqual([
            'First Song',
            'Video Only',
            'Second Song',
        ])

        // Play requests the song again and plays; this device now has the latest state.
        await page.unroute(playback)
        await part(page, 'btn-play').click()
        await expect(part(page, 'transport')).toHaveAttribute('data-state', /\bplaying\b/)
        await expect
            .poll(async () => (await savedState(request, user))?.device.id, { timeout: 15_000 })
            .not.toBe('e2e-other-device')
    })

    test('makes room at the end of the page for the notice on phones', async ({
        page,
        request,
    }, testInfo) => {
        test.skip(
            testInfo.project.name !== 'mobile',
            'The notice floats over the end of the page above the mini player',
        )
        const headers = await profileAuthorization(request, user)
        const trackIds = [
            await findTrackId(request, 'First Song'),
            await findTrackId(request, 'Second Song'),
            await findTrackId(request, 'Video Only'),
        ]
        const saved = await request.put('/api/v1/me/playback-state', {
            headers,
            data: {
                items: [{ trackId: trackIds[0] }],
                currentIndex: 0,
                positionMilliseconds: 0,
                device: { id: 'e2e-other-device', name: 'E2E Device' },
            },
        })
        expect(saved.status()).toBe(204)
        const created = await request.post('/api/v1/me/playlists', {
            headers,
            data: { name: `Resume ${uniqueSuffix()}`, trackIds },
        })
        expect(created.status()).toBe(201)
        const { id } = await created.json()

        await page.goto(`/playlists/${id}`)
        const notice = part(page, 'resume-notice')
        await expect(notice).toBeVisible()
        const rows = part(page, 'list-row')
        await expect(rows).toHaveCount(3)

        // At the end of the page the last row ends above the notice, and its button takes the tap.
        await part(page, 'content').evaluate((content) => content.scrollTo(0, content.scrollHeight))
        const button = rows.last().locator("[data-part='btn-item-menu']")
        await expect(button).toBeVisible()
        const buttonBox = (await button.boundingBox())!
        const noticeBox = (await notice.boundingBox())!
        expect(buttonBox.y + buttonBox.height).toBeLessThanOrEqual(noticeBox.y)
        await button.tap()
        await expect(
            page.locator("[data-part='context-menu']:popover-open:not([data-kind='submenu'])"),
        ).toHaveAttribute('aria-label', 'Song actions')
        await page.keyboard.press('Escape')

        // Without the notice the room goes again.
        await expect(part(page, 'resume-notice-space')).toHaveCount(1)
        await notice.locator("[data-part='btn-dismiss-resume']").click()
        await expect(notice).toHaveCount(0)
        await expect(part(page, 'resume-notice-space')).toHaveCount(0)
    })
})
