// The casting layer: the one entry of the web app to casting. It picks the technique of the browser
// (support.ts) and hides it behind the backend interface, so the player and the UI never know
// whether a Chromecast or an AirPlay device plays. Browsers without casting get a layer that reports
// "unsupported", and the cast button stays hidden.

import { AirPlayBackend, browserAirPlayElement, type AirPlayElement } from './airplay'
import { ChromecastBackend, type CastSdk } from './chromecast'
import { loadBrowserCastSdk } from './sdk'
import { browserCastEnvironment, castTechnologyOf, type CastEnvironment } from './support'
import {
    noCastStatus,
    type CastBackend,
    type CastFormats,
    type CastMedia,
    type CastPlaybackEvents,
    type CastStatus,
    type CastTechnology,
} from './types'

/** The casting layer; its technology is null in browsers without casting. */
export interface CastLayer extends Omit<CastBackend, 'technology'> {
    readonly technology: CastTechnology | null
}

/** A layer for browsers that cannot cast: it reports so and does nothing. */
export class NoCastLayer implements CastLayer {
    readonly technology = null
    readonly status: CastStatus = noCastStatus
    readonly position = 0

    onChange(): void {}

    setPlaybackEvents(): void {}

    async start(): Promise<void> {}

    async requestSession(): Promise<void> {}

    endSession(): void {}

    formats(): CastFormats {
        return { audio: [], video: { containers: [], videoCodecs: [], audioCodecs: [] } }
    }

    async load(): Promise<void> {
        throw new Error('This browser cannot cast')
    }

    async play(): Promise<void> {}

    pause(): void {}

    stop(): void {}

    seek(): void {}

    setVolume(): void {}

    setMuted(): void {}
}

/** What the layer needs to build the backend of a technique; tests replace it. */
export interface CastLayerFactories {
    readonly loadSdk: () => Promise<CastSdk | null>
    readonly createAirPlayElement: () => AirPlayElement
    /** The formats of the browser, which AirPlay hands on to the device. */
    readonly browserFormats: () => CastFormats
}

/** The layer for a browser: AirPlay, Chromecast or none. */
export function createCastLayer(
    environment: CastEnvironment,
    factories: CastLayerFactories,
): CastLayer {
    switch (castTechnologyOf(environment)) {
        case 'airplay':
            return new AirPlayBackend({
                createElement: factories.createAirPlayElement,
                formats: factories.browserFormats,
            })
        case 'chromecast':
            return new ChromecastBackend({
                secure: environment.secureContext,
                loadSdk: factories.loadSdk,
            })
        default:
            return new NoCastLayer()
    }
}

/** The layer of this browser. */
export function createBrowserCastLayer(browserFormats: () => CastFormats): CastLayer {
    return createCastLayer(browserCastEnvironment(), {
        loadSdk: loadBrowserCastSdk,
        createAirPlayElement: browserAirPlayElement,
        browserFormats,
    })
}

export type { CastFormats, CastMedia, CastPlaybackEvents, CastStatus }
