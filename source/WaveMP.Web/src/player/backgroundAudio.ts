// Background playback on iPhone and iPad. WebKit there interrupts the audio graph (Web Audio) as soon
// as the page goes to the background, and elements routed through it fall silent with it. So the
// player has two playback paths (see pathEngine.ts): the effects path through Web Audio with
// equalizer, crossfade and visualization, and the plain path through audio elements alone, which
// keep playing in the background. This module tells whether a browser needs the plain path and which
// path the page wants, from capabilities and events of the browser, never from its user agent.

/** The playback path: through Web Audio with all effects, or through plain audio elements. */
export type PlaybackPath = 'effects' | 'plain'

/** What the capability probe needs to know of the browser; tests replace it. */
export interface BackgroundAudioEnvironment {
    /** Whether the browser has Web Audio. */
    readonly webAudio: boolean
    /** Touch points of the device (navigator.maxTouchPoints). */
    readonly maxTouchPoints: number
    /** Whether the browser knows gesture events (only WebKit on Apple devices has them). */
    readonly gestureEvents: boolean
}

/** The capabilities of the running browser. */
export function browserBackgroundEnvironment(): BackgroundAudioEnvironment {
    if (typeof window === 'undefined') {
        return { webAudio: false, maxTouchPoints: 0, gestureEvents: false }
    }

    return {
        webAudio:
            'AudioContext' in window ||
            'webkitAudioContext' in (window as unknown as Record<string, unknown>),
        maxTouchPoints:
            typeof navigator === 'undefined' ? 0 : Math.max(0, navigator.maxTouchPoints || 0),
        gestureEvents: 'GestureEvent' in window,
    }
}

/**
 * Whether the browser needs the plain path in the background: Web Audio, a touch screen and gesture
 * events together are WebKit on an iPhone or iPad (a Mac has no touch points, other browsers on
 * Android and desktops no gesture events), where the audio graph stops in the background.
 */
export function backgroundPathSupported(
    environment: BackgroundAudioEnvironment = browserBackgroundEnvironment(),
): boolean {
    return environment.webAudio && environment.maxTouchPoints > 0 && environment.gestureEvents
}

/** What decides the wanted path. */
export interface PathConditions {
    /** Whether the browser needs the plain path in the background (backgroundPathSupported). */
    readonly supported: boolean
    /** Whether the page is in the background. */
    readonly hidden: boolean
    /** Whether the browser interrupted the audio graph while the page was in the background. */
    readonly interrupted: boolean
}

/**
 * The path the page wants: the plain one in the background where the browser needs it, or where it
 * interrupted the audio graph there anyway (a browser the probe did not recognize); otherwise the
 * effects path.
 */
export function wantedPath(conditions: PathConditions): PlaybackPath {
    return conditions.hidden && (conditions.supported || conditions.interrupted)
        ? 'plain'
        : 'effects'
}

/** The Audio Session API of WebKit (navigator.audioSession). */
interface AudioSessionNavigator {
    audioSession?: { type: string }
}

/**
 * Declares the audio of the page as music playback, where the browser has the Audio Session API
 * (WebKit): it then keeps playing in the background and with the ringer switched off. Returns
 * whether the browser took it.
 */
export function applyAudioSession(target: unknown = globalThis.navigator): boolean {
    const session = (target as AudioSessionNavigator | undefined)?.audioSession
    if (!session) {
        return false
    }

    try {
        session.type = 'playback'
        return session.type === 'playback'
    } catch {
        return false
    }
}

/** What pageLifecycle reads of the document; tests replace it. */
export interface LifecycleDocument extends EventTarget {
    readonly visibilityState: DocumentVisibilityState
}

/**
 * Follows whether the page is in the background: visibilitychange of the document, pagehide and
 * pageshow of the window (pageshow also after the page returned from the back/forward cache).
 * Reports every change, not the current state; returns a function that stops listening.
 */
export function pageLifecycle(
    document: LifecycleDocument,
    window: EventTarget,
    onChange: (hidden: boolean) => void,
): () => void {
    let hidden = document.visibilityState === 'hidden'
    const report = (now: boolean): void => {
        if (now !== hidden) {
            hidden = now
            onChange(now)
        }
    }
    const onVisibility = (): void => report(document.visibilityState === 'hidden')
    const onHide = (): void => report(true)
    document.addEventListener('visibilitychange', onVisibility)
    window.addEventListener('pagehide', onHide)
    window.addEventListener('pageshow', onVisibility)
    return () => {
        document.removeEventListener('visibilitychange', onVisibility)
        window.removeEventListener('pagehide', onHide)
        window.removeEventListener('pageshow', onVisibility)
    }
}
