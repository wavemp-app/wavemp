import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import { CastEngine } from '../engine'
import type { CastMedia } from '../types'
import { FakeCastLayer } from './fakes'

const media: CastMedia = {
    url: 'https://music.example/stream/a',
    contentType: 'audio/mpeg',
    kind: 'audio',
    hls: false,
    duration: 200,
}

function setup() {
    const layer = new FakeCastLayer()
    const events = {
        timeupdate: vi.fn(),
        playing: vi.fn(),
        paused: vi.fn(),
        waiting: vi.fn(),
        ended: vi.fn(),
        error: vi.fn(),
    }
    const engine = new CastEngine(layer, events)
    return { layer, events, engine }
}

describe('cast engine', () => {
    it('loads and starts the media with one request when play follows the load', async () => {
        const { layer, engine, events } = setup()

        engine.load({ url: media.url, gain: 1, cast: media }, 30)
        await engine.play()

        expect(layer.loads).toEqual([{ media, position: 30, autoplay: true }])
        expect(layer.calls).not.toContain('play')
        expect(events.playing).toHaveBeenCalled()
    })

    it('loads the media paused without play', async () => {
        const { layer, engine } = setup()

        engine.load({ url: media.url, gain: 1, cast: media }, 0)
        await flushPromises()

        expect(layer.loads).toEqual([{ media, position: 0, autoplay: false }])
    })

    it('loads the media paused when a pause follows play before the request leaves', async () => {
        const { layer, engine } = setup()

        engine.load({ url: media.url, gain: 1, cast: media })
        const playing = engine.play()
        engine.pause()
        await playing

        expect(layer.loads[0]?.autoplay).toBe(false)
    })

    it('pauses the media again when a pause comes while the device loads it', async () => {
        const { layer, engine } = setup()

        engine.load({ url: media.url, gain: 1, cast: media })
        const playing = engine.play()
        await Promise.resolve()
        engine.pause()
        await playing

        expect(layer.loads[0]?.autoplay).toBe(true)
        expect(layer.calls.at(-1)).toBe('pause')
    })

    it('sends only the last of several loads in a row', async () => {
        const { layer, engine } = setup()

        engine.load({ url: 'first', gain: 1, cast: { ...media, url: 'first' } })
        engine.load({ url: media.url, gain: 1, cast: media })
        await flushPromises()

        expect(layer.loads.map((load) => load.media.url)).toEqual([media.url])
    })

    it('reports a source without media for the device and failed loads as errors', async () => {
        const { layer, engine, events } = setup()

        engine.load({ url: media.url, gain: 1 })
        expect(events.error).toHaveBeenCalledTimes(1)

        layer.failLoad = true
        engine.load({ url: media.url, gain: 1, cast: media })
        await engine.play()
        expect(events.error).toHaveBeenCalledTimes(2)
    })

    it('passes play, pause, stop, seek and the position to the device', async () => {
        const { layer, engine } = setup()
        engine.load({ url: media.url, gain: 1, cast: media })
        await flushPromises()

        await engine.play()
        engine.pause()
        engine.seek(12)
        engine.stop()

        expect(layer.calls).toEqual(['play', 'pause', 'seek 12', 'stop'])
        expect(engine.position).toBe(12)
    })

    it('passes the events of the device on', () => {
        const { layer, events } = setup()

        layer.events.timeupdate?.(5, 200)
        layer.events.waiting?.()
        layer.events.ended?.()

        expect(events.timeupdate).toHaveBeenCalledWith(5, 200)
        expect(events.waiting).toHaveBeenCalled()
        expect(events.ended).toHaveBeenCalled()
    })

    it('has no equalizer, crossfade, gain or visualization', () => {
        const { layer, engine } = setup()

        engine.prepare()
        engine.setVolume()
        engine.setTrackGain()
        engine.setEqualizer()
        engine.fadeOut()
        engine.restoreOutput()

        expect(engine.equalizerAvailable).toBe(false)
        expect(engine.hasPrepared).toBe(false)
        expect(engine.analyser).toBeNull()
        expect(layer.calls).toEqual([])
    })
})
