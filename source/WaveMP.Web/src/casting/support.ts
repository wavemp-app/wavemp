// Which technique of casting a browser offers. Pure functions over what the browser tells, so the
// rules are tested without a browser.

import type { CastTechnology } from './types'

/** What the detection needs from the browser. */
export interface CastEnvironment {
    readonly userAgent: string
    /** Whether the page runs in a secure context (HTTPS or localhost). */
    readonly secureContext: boolean
    /** Whether WebKit offers its playback target picker (AirPlay). */
    readonly webkitPlaybackTarget: boolean
    /** Whether the global chrome object of Chromium exists. */
    readonly chromeObject: boolean
}

/** Browsers based on Chromium that do not cast with the Google Cast SDK. */
const chromiumWithoutCast = /\b(OPR|OPiOS|SamsungBrowser|YaBrowser|Electron)\//

/** Whether the browser is Chrome or Edge (desktop or Android), which cast with the Google Cast SDK. */
export function isCastChromium(environment: CastEnvironment): boolean {
    return (
        environment.chromeObject &&
        /\bChrome\/\d/.test(environment.userAgent) &&
        !chromiumWithoutCast.test(environment.userAgent)
    )
}

/**
 * The technique of the browser: AirPlay where WebKit offers its playback target (Safari on Mac,
 * iPhone and iPad), Chromecast in Chrome and Edge, otherwise none (e.g. Firefox).
 */
export function castTechnologyOf(environment: CastEnvironment): CastTechnology | null {
    if (environment.webkitPlaybackTarget) {
        return 'airplay'
    }

    return isCastChromium(environment) ? 'chromecast' : null
}

/** What this browser tells about casting. */
export function browserCastEnvironment(): CastEnvironment {
    if (typeof window === 'undefined' || typeof navigator === 'undefined') {
        return {
            userAgent: '',
            secureContext: false,
            webkitPlaybackTarget: false,
            chromeObject: false,
        }
    }

    return {
        userAgent: navigator.userAgent,
        secureContext: window.isSecureContext === true,
        webkitPlaybackTarget: 'WebKitPlaybackTargetAvailabilityEvent' in window,
        chromeObject: 'chrome' in window,
    }
}
