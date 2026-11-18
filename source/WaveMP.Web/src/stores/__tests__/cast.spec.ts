import { createPinia, setActivePinia } from 'pinia'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { FakeCastLayer } from '@/casting/__tests__/fakes'
import { NoCastLayer } from '@/casting/layer'
import { castDependencies, useCastStore } from '../cast'

describe('cast store', () => {
    let layer: FakeCastLayer
    const original = { ...castDependencies }

    beforeEach(() => {
        setActivePinia(createPinia())
        layer = new FakeCastLayer()
        castDependencies.createLayer = () => layer
    })

    afterEach(() => {
        Object.assign(castDependencies, original)
    })

    it('shows the cast button where the browser can cast', async () => {
        const cast = useCastStore()
        await flushPromises()

        expect(cast.visible).toBe(true)
        expect(cast.technology).toBe('chromecast')
        expect(cast.availability).toBe('available')
        expect(cast.connected).toBe(false)
    })

    it('hides it where the browser cannot cast', () => {
        castDependencies.createLayer = () => new NoCastLayer()

        const cast = useCastStore()

        expect(cast.visible).toBe(false)
        expect(cast.technology).toBeNull()
    })

    it('follows the device as it connects and goes', () => {
        const cast = useCastStore()

        layer.connect('Kitchen')
        expect(cast.connected).toBe(true)
        expect(cast.connection).toBe('connected')
        expect(cast.deviceName).toBe('Kitchen')

        layer.disconnect()
        expect(cast.connected).toBe(false)
        expect(cast.deviceName).toBeNull()
    })

    it('opens the device selection only where casting is available', () => {
        const cast = useCastStore()

        cast.requestSession('/stream/a')
        layer.update({ availability: 'insecure' })
        cast.requestSession('/stream/a')

        expect(layer.calls).toEqual(['request /stream/a'])
        expect(cast.connected).toBe(false)
    })

    it('sets the volume of the device and ends the session', () => {
        const cast = useCastStore()

        cast.setVolume(0.4)
        cast.setMuted(true)
        cast.endSession()

        expect(layer.calls).toEqual(['volume 0.4', 'muted true', 'end'])
        expect(cast.volume).toBe(0.4)
        expect(cast.muted).toBe(true)
    })
})
