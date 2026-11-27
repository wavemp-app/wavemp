import { expect, test, type APIRequestContext, type Page } from '@playwright/test'
import { send, sessionHeaders } from '../../screenshots/helpers'
import { createUser, deleteUser, findTrackId, uniqueSuffix, type TestUser } from '../helpers/api'
import {
    attachRecording,
    controlLockScreen,
    controlVisibility,
    expectContinuous,
    hasToken,
    part,
    playsOn,
    pressOnLockScreen,
    recordedSamples,
    setHidden,
    startRecording,
    traceMediaElements,
    transport,
} from '../helpers/background'
import { homeUrl, profileTile } from '../helpers/navigation'
import { emptyStorageState } from '../helpers/storage'

// Playback in the background on iPhone and iPad, in WebKit with the screen of an iPhone and an
// iPad (projects "webkit-iphone" and "webkit-ipad"). WebKit there stops the audio graph (Web Audio)
// in the background, so the player hands the song over to plain audio elements while the page is
// hidden and back to the audio graph with all effects when it returns. The tests simulate the
// switch to the background and the buttons of the lock screen (see helpers/background.ts) and check
// what the listener notices: the music goes on at the same place, the transport bar marks the plain
// path with the state token "plain-audio", the media session (lock screen) shows the song, and the
// next song follows while the page is hidden.
//
// Every test starts from the same state, whatever ran before or runs at the same time: a profile of
// its own (the built-in admin is shared by the tests of all projects, with its saved queue, its
// settings and its devices), autoplay off, and a known queue that waits paused at its first song on
// the device of the page (the saved playback state of the profile), so nothing plays before the
// test starts it. The tests of this file run one after another in one worker per project (serial),
// so two pages of the same device never play at once and take the CPU from each other's audio.
// They wait for the states the page settles in, never for a fixed time.
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

test.describe.configure({ mode: 'serial' })
test.use({ storageState: emptyStorageState, locale: 'en-US' })

/** Touch points of an iPhone and an iPad. */
const appleTouchPoints = 5

/** Key of the id of the device in the local storage of the web app (src/player/device.ts). */
const deviceIdKey = 'wavemp.device-id'

/**
 * How long to wait for a change of the playback path: a handover waits up to three seconds
 * each for the audio graph and for the data of the other path (handoverTimeout in pathEngine.ts),
 * longer than an assertion waits by default.
 */
const pathChange = { timeout: 15_000 }

/** How long to wait for the page to load the profile and its queue. */
const loadTime = { timeout: 15_000 }

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

/** The profile of the running test. */
let listener: TestUser | null = null

/**
 * Selects the profile of the test with autoplay off and the songs as its queue, which waits paused
 * at the first song on the device of the page, so play starts there. Without songs the player
 * stays empty.
 */
async function startWith(
    page: Page,
    request: APIRequestContext,
    profile: TestUser,
    titles: readonly string[] = [],
): Promise<void> {
    const headers = await sessionHeaders(request, profile.id)
    await send(request, headers, 'patch', '/api/v1/me/settings', {
        language: 'en',
        autoplay: { enabled: false },
    })
    const device = { id: `webkit-background-${uniqueSuffix()}`, name: 'Safari · iPad' }
    if (titles.length > 0) {
        const trackIds: string[] = []
        for (const title of titles) {
            trackIds.push(await findTrackId(request, title))
        }

        await send(request, headers, 'put', '/api/v1/me/playback-state', {
            items: trackIds.map((trackId) => ({ trackId })),
            currentIndex: 0,
            positionMilliseconds: 0,
            shuffle: false,
            originalOrder: [],
            repeat: 'Off',
            device,
        })
    }

    // The saved queue belongs to this device, so the player takes it over without a notice.
    await page.addInitScript(([key, value]) => localStorage.setItem(key, value), [
        deviceIdKey,
        device.id,
    ] as const)
    await page.goto('/profiles', { waitUntil: 'domcontentloaded' })
    await profileTile(page, profile.name).click()
    // Selecting the profile creates its session and loads its settings and its saved queue, which
    // takes a while in a busy container.
    await expect(page).toHaveURL(homeUrl, loadTime)
    if (titles.length > 0) {
        await expect(part(page, 'now-playing-title')).toHaveText(titles[0]!, loadTime)
        await expect(part(page, 'btn-play')).toHaveAttribute('data-state', 'paused')
        await expect(transport(page)).not.toHaveAttribute('data-state', /\bloading\b/, loadTime)
    }
}

test.beforeEach(async ({ page, request }) => {
    // WebKit in a busy container loads pages and starts the playback paths slowly, and a test
    // waits for up to two path changes and three times for the music to go on (playsOn).
    test.setTimeout(60_000)
    await emulateAppleDevice(page)
    await controlVisibility(page)
    await controlLockScreen(page)
    await traceMediaElements(page)

    // The pages are opened without waiting for their load event: WebKit in a busy container
    // sometimes fired it only after half a minute (pictures still loading) although the app was
    // ready. The tests wait for what they check instead.
    await page.goto('/profiles', { waitUntil: 'domcontentloaded' })
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
    listener = await createUser(request, `Background ${uniqueSuffix()}`)
})

test.afterEach(async ({ page, request }, testInfo) => {
    await attachRecording(page, testInfo)
    if (listener) {
        await deleteUser(request, listener.id)
        listener = null
    }
})

test('tells about the paused effects in the settings', async ({ page, request }) => {
    await startWith(page, request, listener!)
    await page.goto('/settings', { waitUntil: 'domcontentloaded' })
    await expect(
        page.locator("[data-part='app'] [data-part='form-note'][data-item='background-effects']"),
    ).toBeVisible()
})

test('keeps a song playing in the background and brings the effects back', async ({
    page,
    request,
}) => {
    await startWith(page, request, listener!, ['Loose Song'])
    await part(page, 'btn-play').click()
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
    request,
}) => {
    // The album waits paused at its first song, so neither of its two short songs can end before
    // the test plays them.
    await startWith(page, request, listener!, ['First Song', 'Second Song'])

    // The lock screen shows title, artist, album and the cover in several sizes.
    await expect
        .poll(async () => (await sessionMetadata(page))?.sizes.length ?? 0, { timeout: 15_000 })
        .toBeGreaterThanOrEqual(3)
    expect(await sessionMetadata(page)).toMatchObject({
        title: 'First Song',
        artist: 'E2E Artist',
        album: 'E2E Album',
    })

    // The screen locks: the paused song moves to the plain path at once. Play on the lock screen
    // starts it there, the first song ends and the second one follows on the plain path.
    await startRecording(page)
    await setHidden(page, true)
    await expect(transport(page)).toHaveAttribute('data-state', /\bplain-audio\b/, pathChange)
    await pressOnLockScreen(page, 'play')
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

    // Without autoplay the album ends with its second song, which stays paused at its end.
    await expect(part(page, 'btn-play')).toHaveAttribute('data-state', 'paused', pathChange)
    await expect(part(page, 'now-playing-title')).toHaveText('Second Song')

    const samples = await recordedSamples(page)
    const played = samples.filter((sample) => hasToken(sample, 'playing'))
    expect(played.filter((sample) => !hasToken(sample, 'plain-audio'))).toEqual([])
    expect(played.some((sample) => sample.title === 'First Song')).toBe(true)
    // Nothing but the album played: no song of autoplay and none of another test.
    expect(
        samples.filter((sample) => !['First Song', 'Second Song'].includes(sample.title)),
    ).toEqual([])

    // Back to the foreground: the ended album moves to the effects path again.
    await setHidden(page, false)
    await expect(transport(page)).not.toHaveAttribute('data-state', /\bplain-audio\b/, pathChange)
})
