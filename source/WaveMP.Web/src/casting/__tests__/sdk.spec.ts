import { describe, expect, it, vi } from 'vitest'
import { castSdkTimeout, castSdkUrl, loadCastSdk, type CastSdkWindow } from '../sdk'
import { SimulatedChromecast } from './fakes'

function setup(script: (target: CastSdkWindow) => Promise<void>) {
    const target: CastSdkWindow = {}
    const timers: { callback: () => void; delay: number }[] = []
    const loadScript = vi.fn((url: string) => {
        void url
        return script(target)
    })
    const result = loadCastSdk({
        window: target,
        loadScript,
        setTimeout: (callback, delay) => timers.push({ callback, delay }),
    })
    return { target, timers, loadScript, result }
}

describe('loading the Google Cast SDK', () => {
    it('loads the SDK with the framework from Google and returns it once it is available', async () => {
        const sdk = new SimulatedChromecast().sdk()
        const { loadScript, result } = setup(async (target) => {
            target.cast = { framework: sdk.framework }
            target.chrome = { cast: sdk.chromeCast }
            target.__onGCastApiAvailable?.(true)
        })

        const loaded = await result

        expect(loadScript).toHaveBeenCalledWith(castSdkUrl)
        expect(castSdkUrl).toMatch(/^https:\/\/www\.gstatic\.com\/.*loadCastFramework=1$/)
        expect(loaded?.framework).toBe(sdk.framework)
        expect(loaded?.chromeCast).toBe(sdk.chromeCast)
    })

    it('returns null when the browser cannot cast with it', async () => {
        const { result } = setup(async (target) => target.__onGCastApiAvailable?.(false))

        await expect(result).resolves.toBeNull()
    })

    it('returns null when the script cannot be loaded, e.g. offline or blocked', async () => {
        const { result } = setup(() => Promise.reject(new Error('blocked')))

        await expect(result).resolves.toBeNull()
    })

    it('gives up when the SDK does not answer in time', async () => {
        const { timers, result } = setup(() => new Promise(() => undefined))

        expect(timers[0]?.delay).toBe(castSdkTimeout)
        timers[0]?.callback()

        await expect(result).resolves.toBeNull()
    })

    it('uses an SDK that is already loaded', async () => {
        const sdk = new SimulatedChromecast().sdk()
        const loadScript = vi.fn()
        const loaded = await loadCastSdk({
            window: { cast: { framework: sdk.framework }, chrome: { cast: sdk.chromeCast } },
            loadScript,
            setTimeout: vi.fn(),
        })

        expect(loaded?.framework).toBe(sdk.framework)
        expect(loadScript).not.toHaveBeenCalled()
    })
})
