import { describe, expect, it, vi } from 'vitest'
import { AirPlayBackend } from '../airplay'
import { ChromecastBackend } from '../chromecast'
import { createCastLayer, NoCastLayer, type CastLayerFactories } from '../layer'
import type { CastEnvironment } from '../support'
import { noCastStatus } from '../types'
import { SimulatedAirPlayElement, SimulatedChromecast } from './fakes'

const chrome =
    'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36'

function factories(): CastLayerFactories {
    return {
        loadSdk: vi.fn(async () => new SimulatedChromecast().sdk()),
        createAirPlayElement: vi.fn(() => new SimulatedAirPlayElement()),
        browserFormats: vi.fn(() => ({
            audio: [],
            video: { containers: [], videoCodecs: [], audioCodecs: [] },
        })),
    }
}

function environment(extra: Partial<CastEnvironment>): CastEnvironment {
    return {
        userAgent: chrome,
        secureContext: true,
        webkitPlaybackTarget: false,
        chromeObject: true,
        ...extra,
    }
}

describe('casting layer', () => {
    it('uses Chromecast in Chrome and loads the SDK only on start', async () => {
        const parts = factories()
        const layer = createCastLayer(environment({}), parts)

        expect(layer).toBeInstanceOf(ChromecastBackend)
        expect(parts.loadSdk).not.toHaveBeenCalled()

        await layer.start()

        expect(parts.loadSdk).toHaveBeenCalledTimes(1)
        expect(layer.status.availability).toBe('available')
    })

    it('greys Chromecast out without HTTPS', async () => {
        const layer = createCastLayer(environment({ secureContext: false }), factories())

        await layer.start()

        expect(layer.status.availability).toBe('insecure')
    })

    it('uses AirPlay in Safari with the formats of the browser', async () => {
        const parts = factories()
        const layer = createCastLayer(
            environment({ webkitPlaybackTarget: true, chromeObject: false, secureContext: false }),
            parts,
        )

        await layer.start()

        expect(layer).toBeInstanceOf(AirPlayBackend)
        expect(parts.createAirPlayElement).toHaveBeenCalledTimes(1)
        expect(layer.status.availability).toBe('available')
        layer.formats()
        expect(parts.browserFormats).toHaveBeenCalled()
    })

    it('hides casting in other browsers', async () => {
        const parts = factories()
        const layer = createCastLayer(environment({ chromeObject: false }), parts)

        await layer.start()
        await layer.requestSession()

        expect(layer).toBeInstanceOf(NoCastLayer)
        expect(layer.technology).toBeNull()
        expect(layer.status).toEqual(noCastStatus)
        expect(parts.loadSdk).not.toHaveBeenCalled()
        await expect(
            layer.load(
                {
                    url: 'https://music.example/a',
                    contentType: 'audio/mpeg',
                    kind: 'audio',
                    hls: false,
                    duration: 0,
                },
                0,
                true,
            ),
        ).rejects.toThrow()
    })
})
