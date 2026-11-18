import {
    expect,
    test,
    type APIRequestContext,
    type Locator,
    type Page,
    type Request,
} from '@playwright/test'
import {
    adminHeaders,
    createUser,
    deleteUser,
    findTrackId,
    uniqueSuffix,
    type TestUser,
} from './helpers/api'
import { homeUrl, profileTile } from './helpers/navigation'
import { adminStorageState, emptyStorageState } from './helpers/storage'

// The views of Now Playing (cover, video, visualization) with the synthetic test media of
// scripts/generate-media.sh:
//   Loose Song: 20 seconds of audio with a WebM video the browser plays as it is
//   First Song: audio without a video
//   Second Song: audio with a video; the video sync test changes its offset and resets it
// The chosen view belongs to the profile, so the tests that switch it select their own profile.

/**
 * How long a test waits for the server to store a view. A busy machine (several workers playing
 * video) can hold the saves of the page back for several seconds.
 */
const settleTimeout = 20_000

function part(page: Page, name: string): Locator {
    return page.locator(`[data-part='app'] [data-part='${name}']`)
}

/** A segment of the segmented control that chooses the view of the stage. */
function viewSegment(page: Page, item: string): Locator {
    return page.locator(
        `[data-part='app'] [data-part='stage-actions'] [data-part='segment'][data-item='${item}']`,
    )
}

function stageView(page: Page, kind: string): Locator {
    return page.locator(`[data-part='app'] [data-part='stage-view'][data-kind='${kind}']`)
}

/** Opens Now Playing without loading the page again, so the player keeps its track. */
async function openNowPlaying(page: Page): Promise<void> {
    await page.locator("[data-part='app'] a[href='/now-playing']").filter({ visible: true }).click()
    await expect(page).toHaveURL(/\/now-playing$/)
}

async function playSong(page: Page, id: string, title: string): Promise<void> {
    await page.goto(`/library/tracks/${id}`)
    await part(page, 'detail-actions').locator("[data-item='play']").click()
    await expect(part(page, 'now-playing-title')).toHaveText(title)
}

/** Position of the audio as the seek bar shows it, in seconds. */
function audioPosition(page: Page): Promise<number> {
    return page
        .locator("[data-part='app'] [data-part='seek'] [role='slider']")
        .evaluate((element) => Number(element.getAttribute('aria-valuenow')))
}

/** Distance in seconds between the video and the audio, read at the same moment. */
function videoDrift(page: Page): Promise<number> {
    return page.evaluate(() => {
        const slider = document.querySelector("[data-part='seek'] [role='slider']")
        const video = document.querySelector<HTMLVideoElement>("[data-part='video']")
        if (!slider || !video) {
            return Number.POSITIVE_INFINITY
        }
        return Math.abs(video.currentTime - Number(slider.getAttribute('aria-valuenow')))
    })
}

/** Waits until the audio has played on for one more second. */
async function playsOn(page: Page): Promise<void> {
    const from = await audioPosition(page)
    await expect.poll(() => audioPosition(page), { timeout: 10_000 }).toBeGreaterThan(from + 1)
}

/**
 * Waits until the video shows the position of the audio. The seek bar follows the audio a little
 * behind, so the distance may stay a few tenths of a second.
 */
async function expectVideoInSync(page: Page): Promise<void> {
    await expect.poll(() => videoDrift(page), { timeout: 10_000 }).toBeLessThan(0.75)
}

/**
 * Records the position of the audio in the page every 100 milliseconds, so a test can tell
 * afterwards whether the song ever started again or stopped.
 */
async function recordPositions(page: Page): Promise<void> {
    await page.evaluate(() => {
        const positions: number[] = []
        ;(window as unknown as { e2ePositions: number[] }).e2ePositions = positions
        window.setInterval(() => {
            const slider = document.querySelector("[data-part='seek'] [role='slider']")
            positions.push(Number(slider?.getAttribute('aria-valuenow') ?? Number.NaN))
        }, 100)
    })
}

function recordedPositions(page: Page): Promise<number[]> {
    return page.evaluate(() => (window as unknown as { e2ePositions: number[] }).e2ePositions)
}

/** The view a request of the page saves for the profile, if it is such a save. */
function viewModeOf(request: Request): string | undefined {
    if (request.method() !== 'PATCH' || new URL(request.url()).pathname !== '/api/v1/me/settings') {
        return undefined
    }
    return request.postDataJSON()?.playerViewMode ?? undefined
}

async function sessionHeaders(
    request: APIRequestContext,
    profileId: string,
): Promise<Record<string, string>> {
    const session = await request.post('/api/v1/sessions', { data: { profileId } })
    expect(session.status()).toBe(201)
    return { Authorization: `Bearer ${(await session.json()).token}` }
}

async function storedViewMode(request: APIRequestContext, profileId: string): Promise<string> {
    const response = await request.get('/api/v1/me/settings', {
        headers: await sessionHeaders(request, profileId),
    })
    expect(response.status()).toBe(200)
    return (await response.json()).playerViewMode
}

test.describe('with a profile of its own', () => {
    test.use({ storageState: emptyStorageState, locale: 'en-US' })

    let user: TestUser

    test.beforeEach(async ({ page, request }) => {
        user = await createUser(request, `Viewer ${uniqueSuffix()}`)
        await page.goto('/profiles')
        await profileTile(page, user.name).click()
        await expect(page).toHaveURL(homeUrl)
    })

    test.afterEach(async ({ request }) => {
        await deleteUser(request, user.id)
    })

    test('switches the view while the song plays on without a break', async ({ page, request }) => {
        const id = await findTrackId(request, 'Loose Song')
        await playSong(page, id, 'Loose Song')
        await openNowPlaying(page)
        await recordPositions(page)

        // A new profile starts with the video, which follows the audio.
        const stage = part(page, 'stage')
        await expect(stage).toHaveAttribute('data-state', 'video', { timeout: 15_000 })
        await expect(part(page, 'transport')).toHaveAttribute('data-state', /\bplaying\b/)
        await playsOn(page)
        await expectVideoInSync(page)

        await viewSegment(page, 'cover').click()
        await expect(stage).toHaveAttribute('data-state', 'cover')
        await expect(viewSegment(page, 'cover')).toHaveAttribute('aria-checked', 'true')
        await expect(stageView(page, 'cover')).toHaveAttribute('data-state', 'active')
        await expect(stageView(page, 'video')).not.toHaveAttribute('data-state', 'active')
        await expect.poll(() => storedViewMode(request, user.id)).toBe('Cover')
        await playsOn(page)

        await viewSegment(page, 'visualization').click()
        await expect(stage).toHaveAttribute('data-state', 'visualization')
        await expect(part(page, 'visualization')).toBeVisible()
        await expect.poll(() => storedViewMode(request, user.id)).toBe('Visualization')
        await playsOn(page)

        // Back to the video: it catches up with the audio, which went on in the meantime.
        await viewSegment(page, 'video').click()
        await expect(stage).toHaveAttribute('data-state', 'video')
        await expect(part(page, 'video')).toBeVisible()
        await expect.poll(() => storedViewMode(request, user.id)).toBe('Video')
        await expectVideoInSync(page)

        // The audio led all the time: it played on and never started again.
        await expect(part(page, 'transport')).toHaveAttribute('data-state', /\bplaying\b/)
        const positions = await recordedPositions(page)
        expect(positions.length).toBeGreaterThan(10)
        for (let index = 1; index < positions.length; index++) {
            expect(positions[index]).toBeGreaterThanOrEqual(positions[index - 1]!)
        }
        expect(positions.at(-1)!).toBeGreaterThan(3)
    })

    test('remembers the view of the profile and offers the video only for songs with one', async ({
        page,
        request,
    }) => {
        const firstSong = await findTrackId(request, 'First Song')
        const looseSong = await findTrackId(request, 'Loose Song')

        // First Song has no video: the stage shows its cover, and the video cannot be chosen.
        await playSong(page, firstSong, 'First Song')
        await openNowPlaying(page)
        await expect(part(page, 'stage')).toHaveAttribute('data-state', 'cover')
        await expect(viewSegment(page, 'video')).toBeDisabled()
        await expect(viewSegment(page, 'cover')).toHaveAttribute('aria-checked', 'true')
        await expect(stageView(page, 'video')).toHaveCount(0)
        await expect(
            page.locator("[data-part='app'] [data-part='stage-actions'] [role='radiogroup']"),
        ).toHaveAttribute('data-item', 'stage-view')

        // One tab stop; the arrow key skips the disabled video and checks the visualization.
        await expect(viewSegment(page, 'cover')).toHaveAttribute('tabindex', '0')
        await expect(viewSegment(page, 'visualization')).toHaveAttribute('tabindex', '-1')
        await viewSegment(page, 'cover').focus()
        await page.keyboard.press('ArrowRight')
        await expect(viewSegment(page, 'visualization')).toHaveAttribute('aria-checked', 'true')
        await expect(viewSegment(page, 'visualization')).toBeFocused()
        await expect(part(page, 'stage')).toHaveAttribute('data-state', 'visualization')
        await expect.poll(() => storedViewMode(request, user.id)).toBe('Visualization')

        // After loading the app again, the profile keeps its view, also for a song with a video.
        await page.reload()
        await playSong(page, looseSong, 'Loose Song')
        await openNowPlaying(page)
        await expect(part(page, 'stage')).toHaveAttribute('data-state', 'visualization')
        await expect(viewSegment(page, 'visualization')).toHaveAttribute('aria-checked', 'true')
        await expect(viewSegment(page, 'video')).toBeEnabled()
    })

    test.describe('without the service worker', () => {
        // Requests of the page must reach page.route, which does not see what the service worker
        // fetches.
        test.use({ serviceWorkers: 'block' })

        test('keeps the last of two quick view switches after loading the app again', async ({
            page,
            request,
        }) => {
            // Holds a save back and waits for the answers, which takes longer on a busy machine.
            test.slow()
            const id = await findTrackId(request, 'Loose Song')
            await playSong(page, id, 'Loose Song')
            await openNowPlaying(page)
            const stage = part(page, 'stage')
            await expect(stage).toHaveAttribute('data-state', 'video', { timeout: 15_000 })

            // The first save is held back, so a second save sent alongside it would overtake it,
            // be stored first and then be overwritten with the older view. `sent` keeps the order
            // in which the saves go on to the server. They go on from the browser itself
            // (route.continue): fetching them in the test process took several seconds when the
            // machine was busy.
            const sent: string[] = []
            let held = false
            await page.route('**/api/v1/me/settings', async (route) => {
                const mode = viewModeOf(route.request())
                if (mode && !held) {
                    held = true
                    await new Promise((resolve) => setTimeout(resolve, 750))
                }
                if (mode) {
                    sent.push(mode)
                }
                await route.continue()
            })

            // Wait for the answers of both saves instead of polling for a few seconds: under load,
            // they can take longer than the default timeout of expect.
            const answered = Promise.all([
                page.waitForResponse((response) => viewModeOf(response.request()) === 'Cover', {
                    timeout: settleTimeout,
                }),
                page.waitForResponse(
                    (response) => viewModeOf(response.request()) === 'Visualization',
                    { timeout: settleTimeout },
                ),
            ])
            await viewSegment(page, 'cover').click()
            await viewSegment(page, 'visualization').click()
            await expect(stage).toHaveAttribute('data-state', 'visualization')
            for (const response of await answered) {
                expect(response.status()).toBe(200)
            }
            expect(sent).toEqual(['Cover', 'Visualization'])
            expect(await storedViewMode(request, user.id)).toBe('Visualization')

            await page.unroute('**/api/v1/me/settings')
            await page.reload()
            await playSong(page, id, 'Loose Song')
            await openNowPlaying(page)
            await expect(stage).toHaveAttribute('data-state', 'visualization')
            await expect(viewSegment(page, 'visualization')).toHaveAttribute('aria-checked', 'true')
        })
    })
})

test.describe('video sync', () => {
    test.use({ storageState: adminStorageState, locale: 'en-US' })

    test('sets the video offset of a song through the API and the song page', async ({
        page,
        request,
    }, testInfo) => {
        test.skip(
            testInfo.project.name !== 'chromium',
            'Changes the song; runs in one project only',
        )
        const id = await findTrackId(request, 'Second Song')
        const headers = await adminHeaders(request)
        const offsetUrl = `/api/v1/tracks/${id}/video/offset`

        try {
            const outOfRange = await request.put(offsetUrl, {
                data: { offsetMilliseconds: 700_000 },
                headers,
            })
            expect(outOfRange.status()).toBe(400)
            expect(await outOfRange.text()).toContain('out_of_range')

            const stored = await request.put(offsetUrl, {
                data: { offsetMilliseconds: 1500 },
                headers,
            })
            expect(stored.status()).toBe(200)
            expect(await stored.json()).toMatchObject({ trackId: id, offsetMilliseconds: 1500 })

            // The players get the offset with the video.
            const playback = await request.get(
                `/api/v1/tracks/${id}/video/playback?videoContainers=Mp4&videoCodecs=H264&audioCodecs=Aac`,
                { headers },
            )
            expect(playback.status()).toBe(200)
            expect((await playback.json()).offsetMilliseconds).toBe(1500)

            await page.goto(`/library/tracks/${id}`)
            const section = page.locator("[data-part='app'] [data-item='video-sync']")
            await expect(section.locator("[data-part='section-title']")).toHaveText('Video sync')
            const field = section.locator("[data-field='video-offset'] input")
            await expect(field).toHaveValue('1500')
            await field.fill('500')
            await section.locator("[data-item='save-video-offset']").click()
            await expect(section.locator("[data-part='alert']")).toHaveText('Saved.')

            const response = await request.get(offsetUrl, { headers })
            expect(response.status()).toBe(200)
            expect((await response.json()).offsetMilliseconds).toBe(500)
        } finally {
            const reset = await request.put(offsetUrl, { data: { offsetMilliseconds: 0 }, headers })
            expect(reset.status()).toBe(200)
        }
    })

    test('keeps the video offset to admins', async ({ request }) => {
        const id = await findTrackId(request, 'Second Song')
        const user = await createUser(request, `Listener ${uniqueSuffix()}`)
        try {
            const headers = await sessionHeaders(request, user.id)
            const read = await request.get(`/api/v1/tracks/${id}/video/offset`, { headers })
            expect(read.status()).toBe(403)
            const write = await request.put(`/api/v1/tracks/${id}/video/offset`, {
                data: { offsetMilliseconds: 100 },
                headers,
            })
            expect(write.status()).toBe(403)
        } finally {
            await deleteUser(request, user.id)
        }
    })
})
