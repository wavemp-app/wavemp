import { expect, type Locator, type Page } from '@playwright/test'

// Helpers for the tests of playback in the background. A browser under test never really goes to
// the background, so the tests let the page believe it: the visibility of the document is replaced
// by one the test switches, and the events of the browser (visibilitychange, pagehide, pageshow)
// are sent the way a switch to another app or a locked screen sends them.

/** One sample of the recording: what the page showed and told the system at one moment. */
export interface Sample {
    /** Moment of the sample on the clock of the page (performance.now()), in milliseconds. */
    readonly time: number
    /** Position of the audio as the seek bar shows it, in seconds. */
    readonly position: number
    /**
     * Moment the seek bar began to show this position, on the clock of the page, in milliseconds.
     * A sample may come later than that: the timer of the recording waits while the page is busy.
     */
    readonly positionTime: number
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
 * that last only two seconds. The moment the seek bar changes its position is noted right away, as
 * the timer of the recording may wait while the page is busy (loading the other playback path).
 */
export async function startRecording(page: Page): Promise<void> {
    await page.evaluate(() => {
        const state = window as unknown as BackgroundWindow
        state.e2eSamples = []
        const text = (name: string): string =>
            document
                .querySelector(`[data-part='app'] [data-part='${name}']`)
                ?.textContent?.trim() ?? ''
        const shownPosition = (): number =>
            Number(
                document
                    .querySelector("[data-part='seek'] [role='slider']")
                    ?.getAttribute('aria-valuenow') ?? Number.NaN,
            )
        let position = shownPosition()
        let positionTime = performance.now()
        const notePosition = (): void => {
            const now = shownPosition()
            if (!Object.is(now, position)) {
                position = now
                positionTime = performance.now()
            }
        }
        new MutationObserver(notePosition).observe(document.documentElement, {
            subtree: true,
            attributes: true,
            attributeFilter: ['aria-valuenow'],
        })
        window.setInterval(() => {
            // Also a seek bar the observer missed, e.g. one that was rendered anew.
            notePosition()
            const bar = document.querySelector("[data-part='app'] [data-part='transport']")
            const session = 'mediaSession' in navigator ? navigator.mediaSession : null
            state.e2eSamples.push({
                time: performance.now(),
                position,
                positionTime,
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
 * (the song started again, or the other playback path began earlier) and never further ahead than
 * the time that passed on the clock of the page plus `allowedLead` seconds (a part of the song was
 * skipped). The time that passed is taken from the moments the seek bar changed, not from the
 * samples, so a page that is busy for a while (and records late) does not look like a skip.
 */
export function expectContinuous(samples: readonly Sample[], allowedLead = 0.5): void {
    const known = samples.filter((sample) => Number.isFinite(sample.position))
    expect(known.length).toBeGreaterThan(10)
    const start = known[0]!.time
    const at = (time: number): string => `${Math.round(time - start)} ms`
    const timeline = known
        .map(
            (sample) =>
                `${at(sample.time)}: ${sample.position} (shown since ${at(sample.positionTime)}), ` +
                sample.state,
        )
        .join('\n')
    for (let index = 1; index < known.length; index++) {
        const previous = known[index - 1]!
        const sample = known[index]!
        const step = sample.position - previous.position
        const passed = (sample.positionTime - previous.positionTime) / 1000
        const message = `sample ${index} of\n${timeline}\n`
        expect(step, `${message}step back`).toBeGreaterThan(-0.5)
        expect(step - passed, `${message}step ahead of the time that passed`).toBeLessThan(
            allowedLead,
        )
    }
}
