import { expect, test, type Page } from '@playwright/test'
import { findTrackId } from '../helpers/api'
import {
    controlVisibility,
    expectContinuous,
    hasToken,
    part,
    playSong,
    playsOn,
    recordedSamples,
    setHidden,
    startRecording,
    transport,
} from '../helpers/background'
import { withoutSavedPlayback } from '../helpers/player'

// Playback in the background on iPhone and iPad, in WebKit with the screen of an iPhone and an
// iPad (projects "webkit-iphone" and "webkit-ipad"). WebKit there stops the audio graph (Web Audio)
// in the background, so the player hands the song over to plain audio elements while the page is
// hidden and back to the audio graph with all effects when it returns. The tests simulate the
// switch to the background (see helpers/background.ts) and check what the listener notices: the
// music goes on at the same place, the transport bar marks the plain path with the state token
// "plain-audio", the media session (lock screen) shows the song, and the next song follows while
// the page is hidden.
//
// The player recognizes iPhone and iPad by what the browser can do, not by its name: Web Audio, a
// touch screen and the gesture events of WebKit. Playwright's WebKit emulates the device without
// reporting its touch points (navigator.maxTouchPoints stays 0) and has no gesture events on every
// platform, so the init script adds both as an iPhone and an iPad report them. Playwright's WebKit
// on Windows has neither Web Audio nor the Media Session API; there the tests are skipped, and
// `npm run test:webkit` runs them in the Playwright image of the CI (WebKit of Linux).
//
// Test media of scripts/generate-media.sh: "Loose Song" lasts 20 seconds, the songs of
// "E2E Album" ("First Song", "Second Song", with the album cover) two seconds each.

/** Touch points of an iPhone and an iPad. */
const appleTouchPoints = 5

/**
 * How long to wait for a change of the playback path: a handover waits up to three seconds
 * each for the audio graph and for the data of the other path (handoverTimeout in pathEngine.ts),
 * longer than an assertion waits by default.
 */
const pathChange = { timeout: 15_000 }

/**
 * Seconds the seek bar may move ahead of the time that passed. WebKit of Linux reports the position
 * of an element that plays through the audio graph about a second ahead as soon as it starts, also
 * after a completed seek and on the first start of a song (1.0 to 1.2 seconds measured in the
 * Playwright image), so the return to the effects path moves the seek bar forward by that much
 * once.
 */
const effectsPathLead = 2

async function emulateAppleDevice(page: Page): Promise<void> {
    await page.addInitScript((touchPoints) => {
        if (!('GestureEvent' in window)) {
            class GestureEvent extends UIEvent {}
            Object.defineProperty(window, 'GestureEvent', {
                configurable: true,
                writable: true,
                value: GestureEvent,
            })
        }
        if (!navigator.maxTouchPoints) {
            Object.defineProperty(Navigator.prototype, 'maxTouchPoints', {
                configurable: true,
                get: () => touchPoints,
            })
        }
    }, appleTouchPoints)
}

/** The metadata the media session shows on the lock screen. */
function sessionMetadata(page: Page) {
    return page.evaluate(() => {
        const metadata = navigator.mediaSession.metadata
        return metadata
            ? {
                  title: metadata.title,
                  artist: metadata.artist,
                  album: metadata.album,
                  sizes: [...new Set(metadata.artwork.map((picture) => picture.sizes ?? ''))],
              }
            : null
    })
}

test.beforeEach(async ({ page }) => {
    // WebKit in a busy container loads pages and starts the playback paths slowly, and a test
    // waits for up to two path changes and three times for the music to go on (playsOn).
    test.setTimeout(60_000)
    await withoutSavedPlayback(page)
    await emulateAppleDevice(page)
    await controlVisibility(page)

    // The pages are opened without waiting for their load event: WebKit in a busy container
    // sometimes fired it only after half a minute (pictures still loading) although the app was
    // ready. The tests wait for what they check instead.
    await page.goto('/', { waitUntil: 'domcontentloaded' })
    const capable = await page.evaluate(
        () => 'AudioContext' in window && 'mediaSession' in navigator,
    )
    if (!process.env.CI) {
        test.skip(
            !capable,
            'This WebKit has no Web Audio or Media Session (WebKit of Windows); ' +
                'npm run test:webkit runs the tests in the Playwright image',
        )
    }
    expect(capable, 'WebKit with Web Audio and the Media Session API').toBe(true)
})

test('tells about the paused effects in the settings', async ({ page }) => {
    await page.goto('/settings', { waitUntil: 'domcontentloaded' })
    await expect(
        page.locator("[data-part='app'] [data-part='form-note'][data-item='background-effects']"),
    ).toBeVisible()
})

test('keeps a song playing in the background and brings the effects back', async ({
    page,
    request,
}) => {
    const id = await findTrackId(request, 'Loose Song')
    await playSong(page, id, 'Loose Song')
    await expect(transport(page)).toHaveAttribute('data-state', /\bplaying\b/)
    await expect(transport(page)).not.toHaveAttribute('data-state', /\bplain-audio\b/)
    await playsOn(page)
    await startRecording(page)

    // To the background: the plain audio elements take over at the same place and play on.
    await setHidden(page, true)
    await expect(transport(page)).toHaveAttribute('data-state', /\bplain-audio\b/, pathChange)
    await expect(transport(page)).toHaveAttribute('data-state', /\bplaying\b/)
    await playsOn(page)
    await expect
        .poll(() => sessionMetadata(page))
        .toMatchObject({ title: 'Loose Song', artist: 'Loose Artist' })
    expect(await page.evaluate(() => navigator.mediaSession.playbackState)).toBe('playing')

    // Back again: the audio graph with its effects takes over, and the song still plays on.
    await setHidden(page, false)
    await expect(transport(page)).not.toHaveAttribute('data-state', /\bplain-audio\b/, pathChange)
    await expect(transport(page)).toHaveAttribute('data-state', /\bplaying\b/)
    await playsOn(page)

    const samples = await recordedSamples(page)
    expect(samples.some((sample) => hasToken(sample, 'plain-audio'))).toBe(true)
    expect(samples.filter((sample) => !hasToken(sample, 'playing'))).toEqual([])
    expectContinuous(samples, effectsPathLead)
})

test('shows the album on the lock screen and plays the next song in the background', async ({
    page,
}) => {
    await page.goto('/library/albums', { waitUntil: 'domcontentloaded' })
    await part(page, 'tile')
        .filter({ has: page.locator("[data-part='tile-title']", { hasText: 'E2E Album' }) })
        .click()
    await expect(part(page, 'detail-title')).toHaveText('E2E Album')

    // The songs last two seconds: pausing right away keeps the first one from ending meanwhile.
    await part(page, 'detail-actions').locator("[data-item='play']").click()
    await part(page, 'btn-play').click()
    await expect(part(page, 'btn-play')).toHaveAttribute('data-state', 'paused')
    await expect(part(page, 'now-playing-title')).toHaveText('First Song')

    // The lock screen shows title, artist, album and the cover in several sizes.
    await expect
        .poll(async () => (await sessionMetadata(page))?.sizes.length ?? 0, { timeout: 15_000 })
        .toBeGreaterThanOrEqual(3)
    expect(await sessionMetadata(page)).toMatchObject({
        title: 'First Song',
        artist: 'E2E Artist',
        album: 'E2E Album',
    })

    // A paused song moves to the plain path at once; play stands for the button of the lock
    // screen, the first song ends there and the second one follows on the plain path.
    await startRecording(page)
    await setHidden(page, true)
    await expect(transport(page)).toHaveAttribute('data-state', /\bplain-audio\b/, pathChange)
    await part(page, 'btn-play').click()
    await expect
        .poll(
            async () =>
                (await recordedSamples(page)).some(
                    (sample) =>
                        sample.title === 'Second Song' &&
                        sample.sessionTitle === 'Second Song' &&
                        hasToken(sample, 'playing'),
                ),
            { timeout: 15_000 },
        )
        .toBe(true)

    const samples = await recordedSamples(page)
    const played = samples.filter(
        (sample) =>
            hasToken(sample, 'playing') && ['First Song', 'Second Song'].includes(sample.title),
    )
    expect(played.filter((sample) => !hasToken(sample, 'plain-audio'))).toEqual([])
    expect(played.some((sample) => sample.title === 'First Song')).toBe(true)

    await setHidden(page, false)
    await expect(transport(page)).not.toHaveAttribute('data-state', /\bplain-audio\b/, pathChange)
})
