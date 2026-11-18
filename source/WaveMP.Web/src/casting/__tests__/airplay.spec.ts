import { describe, expect, it, vi } from 'vitest'
import { AirPlayBackend } from '../airplay'
import type { CastFormats, CastMedia } from '../types'
import { SimulatedAirPlayElement } from './fakes'

const safariFormats: CastFormats = {
    audio: ['Mp3', 'Mp4Aac', 'Mp4Alac'],
    video: { containers: ['Mp4'], videoCodecs: ['H264'], audioCodecs: ['Aac'] },
}

const song: CastMedia = {
    url: 'https://music.example/api/v1/tracks/t1/stream?sig=x',
    contentType: 'audio/mpeg',
    kind: 'audio',
    hls: false,
    title: 'Song',
    duration: 200,
}

async function started() {
    const element = new SimulatedAirPlayElement()
    const backend = new AirPlayBackend({
        createElement: () => element,
        formats: () => safariFormats,
    })
    const events = {
        timeupdate: vi.fn(),
        playing: vi.fn(),
        paused: vi.fn(),
        waiting: vi.fn(),
        ended: vi.fn(),
        error: vi.fn(),
    }
    backend.setPlaybackEvents(events)
    await backend.start()
    return { backend, element, events }
}

describe('AirPlay backend', () => {
    it('is available without HTTPS and prepares its element for AirPlay', async () => {
        const { backend, element } = await started()

        expect(backend.status).toMatchObject({
            technology: 'airplay',
            availability: 'available',
            devicesFound: true,
            connection: 'disconnected',
            deviceName: null,
        })
        expect(element.attributes.get('x-webkit-airplay')).toBe('allow')
        expect(element.preload).toBe('none')
    })

    it('follows whether WebKit finds devices', async () => {
        const { backend, element } = await started()

        element.emit('webkitplaybacktargetavailabilitychanged', { availability: 'not-available' })
        expect(backend.status.devicesFound).toBe(false)

        element.emit('webkitplaybacktargetavailabilitychanged', { availability: 'available' })
        expect(backend.status.devicesFound).toBe(true)
    })

    it('opens the AirPlay menu with the current stream as its source and connects', async () => {
        const { backend, element } = await started()
        const changed = vi.fn()
        backend.onChange(changed)

        await backend.requestSession('/api/v1/tracks/t1/stream?sig=x')

        expect(element.pickerOpened).toBe(1)
        expect(element.src).toBe('/api/v1/tracks/t1/stream?sig=x')
        expect(backend.status.connection).toBe('connected')
        expect(changed).toHaveBeenCalled()
    })

    it('stays disconnected when the menu is closed without a device', async () => {
        const { backend, element } = await started()
        element.menuChoice = 'cancel'

        await backend.requestSession(null)

        expect(backend.status.connection).toBe('disconnected')
    })

    it('plays the media on the device at the position and passes its events on', async () => {
        const { backend, element, events } = await started()
        await backend.requestSession(null)

        await backend.load(song, 30, true)
        element.loadMetadata(200)
        element.currentTime = 31
        element.emit('timeupdate')

        expect(element.src).toBe(song.url)
        expect(element.paused).toBe(false)
        expect(events.playing).toHaveBeenCalled()
        expect(events.timeupdate).toHaveBeenLastCalledWith(31, 200)

        backend.pause()
        backend.seek(50)
        expect(events.paused).toHaveBeenCalled()
        expect(backend.position).toBe(50)

        element.emit('ended')
        expect(events.ended).toHaveBeenCalledTimes(1)
    })

    it('sets the position once the metadata is known', async () => {
        const { backend, element } = await started()
        await backend.requestSession(null)

        await backend.load(song, 30, false)
        expect(element.currentTime).toBe(0)
        expect(element.paused).toBe(true)

        element.loadMetadata(200)
        expect(element.currentTime).toBe(30)
    })

    it('reports errors of the device', async () => {
        const { backend, element, events } = await started()
        await backend.requestSession(null)
        await backend.load(song, 0, true)

        element.emit('error')

        expect(events.error).toHaveBeenCalled()
    })

    it('cannot load without a device and passes no events of the preview on', async () => {
        const { backend, element, events } = await started()

        await expect(backend.load(song, 0, true)).rejects.toThrow()
        element.emit('playing')
        element.emit('ended')

        expect(events.playing).not.toHaveBeenCalled()
        expect(events.ended).not.toHaveBeenCalled()
    })

    it('keeps the source on stop so the element keeps its route to the device', async () => {
        const { backend, element } = await started()
        await backend.requestSession(null)
        await backend.load(song, 0, true)

        backend.stop()

        expect(element.src).toBe(song.url)
        expect(element.paused).toBe(true)
        expect(backend.status.connection).toBe('connected')
    })

    it('ends through the AirPlay menu and pauses the element back on this device', async () => {
        const { backend, element } = await started()
        await backend.requestSession(null)
        await backend.load(song, 0, true)

        backend.endSession()

        expect(element.pickerOpened).toBe(2)
        expect(element.paused).toBe(true)
        expect(backend.status.connection).toBe('disconnected')
    })

    it('controls the volume of the element and follows it', async () => {
        const { backend, element } = await started()

        backend.setVolume(1.5)
        backend.setMuted(true)
        element.emit('volumechange')

        expect(element.volume).toBe(1)
        expect(backend.status).toMatchObject({ volume: 1, muted: true })
    })

    it('hands the formats of the browser on to the device', async () => {
        const { backend } = await started()

        expect(backend.formats()).toBe(safariFormats)
    })
})
