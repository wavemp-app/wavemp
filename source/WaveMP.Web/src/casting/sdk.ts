// Loads the Google Cast Web Sender SDK from Google at runtime. It is not part of the repository or of
// the image: Google only allows it to be loaded from its own servers. The SDK calls
// window.__onGCastApiAvailable once it knows whether the browser can cast.

import type { CastFrameworkApi, CastSdk, ChromeCastApi } from './chromecast'

/** URL of the SDK with the Cast Application Framework (CAF). */
export const castSdkUrl =
    'https://www.gstatic.com/cv/js/sender/v1/cast_sender.js?loadCastFramework=1'

/** Milliseconds the SDK may take to load before the browser counts as one that cannot cast. */
export const castSdkTimeout = 15_000

/** The globals the SDK sets and calls. */
export interface CastSdkWindow {
    __onGCastApiAvailable?: (available: boolean) => void
    cast?: { framework?: CastFrameworkApi }
    chrome?: { cast?: ChromeCastApi }
}

export interface CastSdkEnvironment {
    readonly window: CastSdkWindow
    /** Adds a script to the page; rejects if it cannot be loaded. */
    loadScript(url: string): Promise<void>
    setTimeout(callback: () => void, delay: number): unknown
}

function sdkOf(target: CastSdkWindow): CastSdk | null {
    const framework = target.cast?.framework
    const chromeCast = target.chrome?.cast
    return framework && chromeCast ? { framework, chromeCast } : null
}

/**
 * Loads the SDK and returns it, or null if the browser cannot cast with it (e.g. Brave), the script
 * cannot be loaded (offline, blocked) or it takes too long.
 */
export function loadCastSdk(environment: CastSdkEnvironment): Promise<CastSdk | null> {
    const target = environment.window
    const loaded = sdkOf(target)
    if (loaded) {
        return Promise.resolve(loaded)
    }

    return new Promise((resolve) => {
        let done = false
        const finish = (sdk: CastSdk | null): void => {
            if (!done) {
                done = true
                resolve(sdk)
            }
        }

        target.__onGCastApiAvailable = (available) => finish(available ? sdkOf(target) : null)
        environment.setTimeout(() => finish(null), castSdkTimeout)
        environment.loadScript(castSdkUrl).catch(() => finish(null))
    })
}

let browserSdk: Promise<CastSdk | null> | null = null

/** The SDK in this browser, loaded once with a script element on first use. */
export function loadBrowserCastSdk(): Promise<CastSdk | null> {
    browserSdk ??= loadCastSdk({
        window: window as unknown as CastSdkWindow,
        loadScript: (url) =>
            new Promise<void>((resolve, reject) => {
                const script = document.createElement('script')
                script.src = url
                script.async = true
                script.addEventListener('load', () => resolve())
                script.addEventListener('error', () => reject(new Error('Cast SDK unavailable')))
                document.head.append(script)
            }),
        setTimeout: (callback, delay) => window.setTimeout(callback, delay),
    })
    return browserSdk
}
