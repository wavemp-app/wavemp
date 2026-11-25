import { describe, expect, it } from 'vitest'
import { castTechnologyOf, isCastChromium, type CastEnvironment } from '../support'

const chrome =
    'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36'
const edge = `${chrome} Edg/131.0.0.0`
const chromeAndroid =
    'Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) ' +
    'Chrome/131.0.0.0 Mobile Safari/537.36'
const opera = `${chrome} OPR/115.0.0.0`
const samsung =
    'Mozilla/5.0 (Linux; Android 14) AppleWebKit/537.36 (KHTML, like Gecko) SamsungBrowser/26.0 ' +
    'Chrome/122.0.0.0 Mobile Safari/537.36'
const firefox = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:133.0) Gecko/20100101 Firefox/133.0'
const safari =
    'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) ' +
    'Version/18.1 Safari/605.1.15'

function environment(userAgent: string, extra: Partial<CastEnvironment> = {}): CastEnvironment {
    return {
        userAgent,
        secureContext: true,
        webkitPlaybackTarget: false,
        chromeObject: true,
        ...extra,
    }
}

describe('casting support', () => {
    it.each([
        ['Chrome', chrome],
        ['Edge', edge],
        ['Chrome on Android', chromeAndroid],
    ])('casts with Chromecast in %s', (_name, userAgent) => {
        expect(isCastChromium(environment(userAgent))).toBe(true)
        expect(castTechnologyOf(environment(userAgent))).toBe('chromecast')
    })

    it('offers Chromecast also without HTTPS, so the button can say why it is greyed out', () => {
        expect(castTechnologyOf(environment(chrome, { secureContext: false }))).toBe('chromecast')
    })

    it.each([
        ['Opera', opera],
        ['Samsung Internet', samsung],
    ])('does not cast in %s, which has no Google Cast', (_name, userAgent) => {
        expect(castTechnologyOf(environment(userAgent))).toBeNull()
    })

    it('does not cast without the chrome object, e.g. in a test browser that only claims Chrome', () => {
        expect(castTechnologyOf(environment(chrome, { chromeObject: false }))).toBeNull()
    })

    it('casts with AirPlay where WebKit offers its playback target', () => {
        expect(
            castTechnologyOf(
                environment(safari, { webkitPlaybackTarget: true, chromeObject: false }),
            ),
        ).toBe('airplay')
        // Also without HTTPS: AirPlay needs none.
        expect(
            castTechnologyOf(
                environment(safari, {
                    webkitPlaybackTarget: true,
                    chromeObject: false,
                    secureContext: false,
                }),
            ),
        ).toBe('airplay')
    })

    it('hides casting in Firefox and in Safari without a playback target', () => {
        expect(castTechnologyOf(environment(firefox, { chromeObject: false }))).toBeNull()
        expect(castTechnologyOf(environment(safari, { chromeObject: false }))).toBeNull()
    })
})
