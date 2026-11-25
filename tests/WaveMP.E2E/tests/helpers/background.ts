import { expect, type Locator, type Page } from '@playwright/test'

// Helpers for the tests of playback in the background. A browser under test never really goes to
// the background, so the tests let the page believe it: the visibility of the document is replaced
// by one the test switches, and the events of the browser (visibilitychange, pagehide, pageshow)
// are sent the way a switch to another app or a locked screen sends them.

/** One sample of the recording: what the page showed and told the system at one moment. */
export interface Sample {
    /** Position of the audio as the seek bar shows it, in seconds. */
    readonly position: number
    /** State tokens of the transport bar. */
    readonly state: string
    /** Title of the current song in the transport bar. */
    readonly title: string
    /** Title and playback state the media session shows (lock screen, notification). */
    readonly sessionTitle: string
    readonly sessionState: string
}

/** State the helpers keep in the page. */
interface BackgroundWindow {
    e2eHidden: boolean
    e2eSamples: Sample[]
}

/**
 * Replaces the visibility of the document before the app starts, so the player reads it from its
 * first moment on. The page starts visible.
 */
export async function controlVisibility(page: Page): Promise<void> {
    await page.addInitScript(() => {
        const state = window as unknown as BackgroundWindow
        state.e2eHidden = false
        Object.defineProperty(document, 'visibilityState', {
            configurable: true,
            get: () => (state.e2eHidden ? 'hidden' : 'visible'),
        })
        Object.defineProperty(document, 'hidden', {
            configurable: true,
            get: () => state.e2eHidden,
        })
    })
}

/**
 * Lets the page go to the background (visibilitychange, then pagehide, as when the screen locks)
 * or come back (visibilitychange, then pageshow).
 */
export async function setHidden(page: Page, hidden: boolean): Promise<void> {
    await page.evaluate((now) => {
        ;(window as unknown as BackgroundWindow).e2eHidden = now
        document.dispatchEvent(new Event('visibilitychange'))
        window.dispatchEvent(
            new PageTransitionEvent(now ? 'pagehide' : 'pageshow', { persisted: false }),
        )
    }, hidden)
}

export function part(page: Page, name: string): Locator {
    return page.locator(`[data-part='app'] [data-part='${name}']`)
}

export function transport(page: Page): Locator {
    return part(page, 'transport')
}

/** Plays one song from its page. */
export async function playSong(page: Page, id: string, title: string): Promise<void> {
    await page.goto(`/library/tracks/${id}`, { waitUntil: 'domcontentloaded' })
    await part(page, 'detail-actions').locator("[data-item='play']").click()
    await expect(part(page, 'now-playing-title')).toHaveText(title)
}

/** Position of the audio as the seek bar shows it, in seconds. */
export function audioPosition(page: Page): Promise<number> {
    return page
        .locator("[data-part='app'] [data-part='seek'] [role='slider']")
        .first()
        .evaluate((element) => Number(element.getAttribute('aria-valuenow')))
}

/** Waits until the audio has played on for one more second. */
export async function playsOn(page: Page): Promise<void> {
    const from = await audioPosition(page)
    await expect.poll(() => audioPosition(page), { timeout: 15_000 }).toBeGreaterThan(from + 1)
}

/**
 * Records every 100 milliseconds what the page shows and tells the system, so a test can tell
 * afterwards whether the song ever jumped, stopped or switched the playback path, also for songs
 * that last only two seconds.
 */
export async function startRecording(page: Page): Promise<void> {
    await page.evaluate(() => {
        const state = window as unknown as BackgroundWindow
        state.e2eSamples = []
        const text = (name: string): string =>
            document
                .querySelector(`[data-part='app'] [data-part='${name}']`)
                ?.textContent?.trim() ?? ''
        window.setInterval(() => {
            const slider = document.querySelector("[data-part='seek'] [role='slider']")
            const bar = document.querySelector("[data-part='app'] [data-part='transport']")
            const session = 'mediaSession' in navigator ? navigator.mediaSession : null
            state.e2eSamples.push({
                position: Number(slider?.getAttribute('aria-valuenow') ?? Number.NaN),
                state: bar?.getAttribute('data-state') ?? '',
                title: text('now-playing-title'),
                sessionTitle: session?.metadata?.title ?? '',
                sessionState: session?.playbackState ?? '',
            })
        }, 100)
    })
}

export function recordedSamples(page: Page): Promise<Sample[]> {
    return page.evaluate(() => (window as unknown as BackgroundWindow).e2eSamples)
}

/** Whether the state tokens of a sample contain a token. */
export function hasToken(sample: Sample, token: string): boolean {
    return sample.state.split(/\s+/).includes(token)
}

/**
 * Checks that the recorded positions went on without a jump: never back by more than half a second
 * (the song started again, or the other playback path began earlier) and never ahead by more than
 * a second and a half between two samples a tenth of a second apart (a part of the song was
 * skipped).
 */
export function expectContinuous(samples: readonly Sample[]): void {
    const known = samples.map((sample) => sample.position).filter(Number.isFinite)
    expect(known.length).toBeGreaterThan(10)
    for (let index = 1; index < known.length; index++) {
        const step = known[index]! - known[index - 1]!
        expect(step, `step ${index} of ${known.join(', ')}`).toBeGreaterThan(-0.5)
        expect(step, `step ${index} of ${known.join(', ')}`).toBeLessThan(1.5)
    }
}
