import { expect, test, type Locator, type Page } from '@playwright/test'
import { adminHeaders, findTrackId } from './helpers/api'

// The muted video follows the music. On a slow network every seek of the video costs a new request
// and the video lands behind the music again; a follower that seeks again at once ends in a loop of
// seeks and the video stutters. This spec plays the video of Loose Song (VP9 WebM played as it is,
// 20 seconds, see scripts/generate-media.sh) over a throttled network and counts the seeks.

/** Key of the developer setting of the device that shows the diagnostics of the video. */
const diagnosticsStorageKey = 'wavemp.player.video-diagnostics.v1'

/** Seconds of the video the spec plays over the throttled network. */
const playedSeconds = 10

/**
 * Seeks the video may take in that time: one to show the picture at the position of the music
 * and a few to catch up after waiting for the slow network, but no loop of seeks.
 */
const maxSeeks = 4

function part(page: Page, name: string): Locator {
    return page.locator(`[data-part='app'] [data-part='${name}']`)
}

/** The value of a field of the diagnostics overlay on the stage. */
function diagnosticsFigure(page: Page, field: string): Locator {
    return part(page, 'video-diagnostics').locator(
        `[data-part='diagnostics-value'][data-field='${field}'] [data-part='diagnostics-figure']`,
    )
}

/** The first number of a text, for example 2 of "2 (0 missed in a row)". */
function firstNumber(text: string): number {
    const match = /\d+/.exec(text)
    expect(match, `a number in "${text}"`).not.toBeNull()
    return Number(match![0])
}

test.beforeAll(async ({ request }) => {
    const response = await request.patch('/api/v1/me/settings', {
        data: { playerViewMode: 'Video' },
        headers: await adminHeaders(request),
    })
    expect(response.status()).toBe(200)
})

test('follows the music on a slow network with few seeks', async ({ page, request }, testInfo) => {
    test.skip(testInfo.project.name !== 'chromium', 'Throttles the network of Chromium only.')
    test.setTimeout(120_000)
    const id = await findTrackId(request, 'Loose Song')
    await page.addInitScript((key) => {
        window.localStorage.setItem(key, 'true')
        // Media events do not bubble; a listener in the capture phase sees those of every video.
        const counter = window as unknown as { wavempVideoSeeks: number }
        counter.wavempVideoSeeks = 0
        document.addEventListener(
            'seeking',
            (event) => {
                if (event.target instanceof HTMLVideoElement) {
                    counter.wavempVideoSeeks++
                }
            },
            true,
        )
    }, diagnosticsStorageKey)
    await page.goto(`/library/tracks/${id}`)
    await expect(part(page, 'detail-actions').locator("[data-item='play']")).toBeVisible()

    // About 800 kbit/s with a long round trip: enough for the music (128 kbit/s) and the video
    // (200 kbit/s) together, but every request of a seek takes its time.
    const network = await page.context().newCDPSession(page)
    await network.send('Network.enable')
    await network.send('Network.emulateNetworkConditions', {
        offline: false,
        latency: 300,
        downloadThroughput: 100_000,
        uploadThroughput: 100_000,
    })

    await part(page, 'detail-actions').locator("[data-item='play']").click()
    await page.locator("[data-part='app'] a[href='/now-playing']").filter({ visible: true }).click()
    await expect(page).toHaveURL(/\/now-playing$/)

    const video = part(page, 'video')
    await expect(video).toBeVisible()
    await expect(part(page, 'video-diagnostics')).toBeVisible()
    await expect(part(page, 'video-diagnostics')).toHaveAttribute('data-delivery', 'file')
    await expect
        .poll(() => video.evaluate((element: HTMLVideoElement) => element.currentTime), {
            timeout: 90_000,
        })
        .toBeGreaterThanOrEqual(playedSeconds)

    const seeks = await page.evaluate(
        () => (window as unknown as { wavempVideoSeeks: number }).wavempVideoSeeks,
    )
    const recorded = firstNumber((await diagnosticsFigure(page, 'seeks').textContent()) ?? '')
    const log = await part(page, 'diagnostics-log').textContent()
    expect(seeks, `seeks of the video; newest diagnostics: ${log}`).toBeLessThanOrEqual(maxSeeks)
    expect(recorded, 'seeks the diagnostics counted').toBeLessThanOrEqual(seeks)
    // The video still follows the music: the diagnostics show its distance in milliseconds.
    await expect
        .poll(
            async () => {
                const text = (await diagnosticsFigure(page, 'drift').textContent()) ?? ''
                const match = /[-+]?\d+/.exec(text)
                return match ? Math.abs(Number(match[0])) : Number.POSITIVE_INFINITY
            },
            { timeout: 15_000 },
        )
        .toBeLessThan(1000)
})
