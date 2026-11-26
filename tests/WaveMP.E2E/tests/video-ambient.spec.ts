import { expect, test, type Locator, type Page } from '@playwright/test'
import { createUser, deleteUser, findTrackId, uniqueSuffix, type TestUser } from './helpers/api'
import { chooseFromProfileMenu, homeUrl, profileTile } from './helpers/navigation'
import { emptyStorageState } from './helpers/storage'

// The ambient light of videos with the synthetic test media of scripts/generate-media.sh:
//   Loose Song: MP3 with a landscape VP9/Opus WebM video, which the browser plays as it is
//   Second Song: FLAC with a portrait video the server transcodes to HLS
// The ambient light belongs to the profile, so the tests select a profile of their own and switch
// it off without changing the profile the other tests share.

test.use({ storageState: emptyStorageState, locale: 'en-US' })

function part(page: Page, name: string): Locator {
    return page.locator(`[data-part='app'] [data-part='${name}']`)
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

function opacity(locator: Locator): Promise<number> {
    return locator.evaluate((element) => Number(getComputedStyle(element).opacity))
}

/**
 * Size of the current canvas of the ambient light and the brightest channel of its pixels. Reading
 * the pixels fails if the canvas is tainted, so this also checks that the signed URL of the video
 * lets the page read its picture.
 */
function currentPicture(page: Page) {
    return part(page, 'video-ambient')
        .locator("[data-part='video-ambient-canvas'][data-state='current']")
        .evaluate((canvas: HTMLCanvasElement) => {
            const context = canvas.getContext('2d')!
            const data = context.getImageData(0, 0, canvas.width, canvas.height).data
            let brightest = 0
            for (let index = 0; index < data.length; index += 4) {
                brightest = Math.max(brightest, data[index]!, data[index + 1]!, data[index + 2]!)
            }
            return { width: canvas.width, height: canvas.height, brightest }
        })
}

let user: TestUser

test.beforeEach(async ({ page, request }) => {
    user = await createUser(request, `Ambient ${uniqueSuffix()}`)
    await page.goto('/profiles')
    await profileTile(page, user.name).click()
    await expect(page).toHaveURL(homeUrl)
})

test.afterEach(async ({ request }) => {
    await deleteUser(request, user.id)
})

test('shows the ambient light around a playing video and switches it off', async ({
    page,
    request,
}) => {
    const id = await findTrackId(request, 'Loose Song')
    await playSong(page, id, 'Loose Song')
    await openNowPlaying(page)

    // A new profile has the ambient light on; it draws the playing video into its canvases.
    await expect(part(page, 'stage')).toHaveAttribute('data-state', 'video', {
        timeout: 15_000,
    })
    const ambient = part(page, 'video-ambient')
    await expect(ambient).toHaveAttribute('data-state', 'running', {
        timeout: 15_000,
    })
    await expect(ambient).toHaveAttribute('aria-hidden', 'true')
    await expect.poll(() => opacity(ambient)).toBeGreaterThan(0.3)
    await expect.poll(async () => (await currentPicture(page)).brightest).toBeGreaterThan(0)
    expect(await currentPicture(page)).toMatchObject({ width: 32, height: 24 })
    // The light lies between the backdrop and the picture of the video.
    const order = await part(page, 'video-stage').evaluate((stage) =>
        [...stage.children].map((child) => child.getAttribute('data-part')),
    )
    expect(order.indexOf('video-ambient')).toBeGreaterThan(order.indexOf('video-backdrop'))
    expect(order.indexOf('video-ambient')).toBeLessThan(order.indexOf('video'))

    // It pauses while the cover is shown and goes on with the video.
    await part(page, 'stage-actions').locator("[data-part='segment'][data-item='cover']").click()
    await expect(ambient).toHaveAttribute('data-state', 'paused')
    await part(page, 'stage-actions').locator("[data-part='segment'][data-item='video']").click()
    await expect(ambient).toHaveAttribute('data-state', 'running')

    // Switched off on the settings page, it stays off for the profile.
    await chooseFromProfileMenu(page, 'settings')
    const checkbox = page.locator("[data-field='video-ambient'] input")
    await expect(checkbox).toBeChecked()
    const saved = page.waitForResponse(
        (response) =>
            response.request().method() === 'PATCH' &&
            new URL(response.url()).pathname === '/api/v1/me/settings' &&
            response.request().postDataJSON()?.video?.ambientMode === false,
    )
    await checkbox.uncheck()
    expect((await saved).ok()).toBe(true)

    await openNowPlaying(page)
    await expect(ambient).toHaveAttribute('data-state', 'off')
    await expect.poll(() => opacity(ambient)).toBe(0)
    const session = await request.post('/api/v1/sessions', {
        data: { profileId: user.id },
    })
    expect(session.status()).toBe(201)
    const settings = await request.get('/api/v1/me/settings', {
        headers: { Authorization: `Bearer ${(await session.json()).token}` },
    })
    expect((await settings.json()).video).toEqual({ ambientMode: false })
})

test('keeps one static color for reduced motion', async ({ page, request }) => {
    await page.emulateMedia({ reducedMotion: 'reduce' })
    const id = await findTrackId(request, 'Loose Song')
    await playSong(page, id, 'Loose Song')
    await openNowPlaying(page)

    const ambient = part(page, 'video-ambient')
    await expect(ambient).toHaveAttribute('data-state', 'static', {
        timeout: 15_000,
    })
    await expect
        .poll(() =>
            ambient.evaluate((element) =>
                getComputedStyle(element).getPropertyValue('--wmp-video-ambient-color').trim(),
            ),
        )
        .toMatch(/^rgb\(\d+ \d+ \d+\)$/)
    // Nothing is drawn or animated: the canvases stay hidden.
    await expect(ambient.locator("[data-part='video-ambient-canvas']").first()).toBeHidden()
})

test('fills the sides of a portrait video and the screen in full screen', async ({
    page,
    request,
}, testInfo) => {
    test.skip(
        testInfo.project.name !== 'chromium',
        'Transcodes and uses full screen on the desktop.',
    )
    const id = await findTrackId(request, 'Second Song')
    await playSong(page, id, 'Second Song')
    await openNowPlaying(page)

    const ambient = part(page, 'video-ambient')
    await expect(part(page, 'video-stage')).toHaveAttribute('data-orientation', 'portrait', {
        timeout: 60_000,
    })
    await expect(ambient).toHaveAttribute('data-state', /^(running|paused)$/, {
        timeout: 60_000,
    })
    await expect.poll(async () => (await currentPicture(page)).width).toBe(24)
    expect((await currentPicture(page)).height).toBe(32)

    await part(page, 'video-stage').locator("[data-part='btn-fullscreen']").click()
    await expect(part(page, 'video-stage')).toHaveAttribute('data-state', 'fullscreen')
    const viewport = page.viewportSize()!
    const box = (await ambient.boundingBox())!
    expect(box.width).toBeCloseTo(viewport.width, 0)
    expect(box.height).toBeCloseTo(viewport.height, 0)
    await expect(ambient).not.toHaveAttribute('data-state', /^(off|waiting)$/)

    await part(page, 'video-stage').locator("[data-part='btn-fullscreen']").click()
    await expect(part(page, 'video-stage')).not.toHaveAttribute('data-state', 'fullscreen')
})

/** Box of a part of the full-screen player and the size of the picture its video shows. */
function videoLayout(page: Page) {
    return part(page, 'player-media').evaluate((media) => {
        const box = (element: Element | null) => {
            const rect = element!.getBoundingClientRect()
            return { width: rect.width, height: rect.height }
        }
        const video = media.querySelector<HTMLVideoElement>("[data-part='player-video']")!
        const area = box(video)
        const scale = Math.min(area.width / video.videoWidth, area.height / video.videoHeight)
        return {
            media: box(media),
            video: area,
            ambient: box(media.querySelector("[data-part='video-ambient']")),
            cover: box(media.querySelector("[data-part='cover']")),
            picture: { width: video.videoWidth * scale, height: video.videoHeight * scale },
            background: getComputedStyle(video).backgroundColor,
        }
    })
}

test('fills the room of the full-screen player around the video with the ambient light', async ({
    page,
    request,
    isMobile,
}) => {
    test.skip(!isMobile, 'The full-screen player exists on phones only.')
    const id = await findTrackId(request, 'Loose Song')
    await playSong(page, id, 'Loose Song')
    const transport = part(page, 'transport')
    await part(page, 'now-playing-mini').click()
    await expect(transport).toHaveAttribute('data-state', /\bexpanded\b/)

    // The video takes the whole width of the player, not the square of the cover; the ambient
    // light fills the room above and below its picture and the cover fades out under it.
    const media = part(page, 'player-media')
    await expect(media).toHaveAttribute('data-state', 'video', { timeout: 15_000 })
    const ambient = media.locator("> [data-part='video-ambient']")
    await expect(ambient).toHaveAttribute('data-state', 'running', { timeout: 15_000 })
    await expect.poll(() => opacity(ambient)).toBeGreaterThan(0.3)
    await expect.poll(() => opacity(media.locator("> [data-part='cover']"))).toBe(0)
    const portrait = await videoLayout(page)
    expect(portrait.video).toEqual(portrait.media)
    expect(portrait.ambient).toEqual(portrait.media)
    expect(portrait.video.width).toBeGreaterThan(portrait.cover.width)
    expect(portrait.picture.width).toBeCloseTo(portrait.media.width, 0)
    expect(portrait.picture.height).toBeLessThan(portrait.media.height)
    expect(portrait.background).toBe('rgba(0, 0, 0, 0)')

    // Held sideways, the cover area takes the left half of the screen and still shows the video.
    const size = page.viewportSize()!
    await page.setViewportSize({ width: size.height, height: size.width })
    await expect
        .poll(async () => (await videoLayout(page)).media.height)
        .toBeGreaterThan(size.width / 3)
    const landscape = await videoLayout(page)
    expect(landscape.media.width).toBeLessThan(size.height / 2)
    expect(landscape.ambient).toEqual(landscape.media)
    await page.setViewportSize(size)
})
