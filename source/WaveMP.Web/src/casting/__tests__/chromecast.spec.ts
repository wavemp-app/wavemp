import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import { ChromecastBackend, CastError } from '../chromecast'
import { chromecastFormats } from '../formats'
import type { CastMedia } from '../types'
import { SimulatedChromecast } from './fakes'

const song: CastMedia = {
    url: 'https://music.example/api/v1/tracks/t1/stream?uid=u&sid=s&exp=1&sig=x',
    contentType: 'audio/flac',
    kind: 'audio',
    hls: false,
    title: 'Song',
    artist: 'Band',
    album: 'Album',
    artworkUrl: 'https://music.example/api/v1/tracks/t1/cover?size=1000&sig=y',
    duration: 200,
}

async function connected(device = new SimulatedChromecast()) {
    const backend = new ChromecastBackend({ secure: true, loadSdk: async () => device.sdk() })
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
    await backend.requestSession()
    return { backend, device, events }
}

describe('Chromecast backend', () => {
    it('is greyed out without HTTPS and never loads the SDK there', async () => {
        const loadSdk = vi.fn()
        const backend = new ChromecastBackend({ secure: false, loadSdk })

        await backend.start()

        expect(backend.status.availability).toBe('insecure')
        expect(backend.status.technology).toBe('chromecast')
        expect(loadSdk).not.toHaveBeenCalled()
    })

    it('stays hidden while the SDK loads and when the browser cannot cast', async () => {
        const backend = new ChromecastBackend({ secure: true, loadSdk: async () => null })
        expect(backend.status.availability).toBe('unsupported')

        await backend.start()

        expect(backend.status.availability).toBe('unsupported')
    })

    it('uses the Default Media Receiver and reports the devices the browser finds', async () => {
        const device = new SimulatedChromecast()
        device.state = 'NO_DEVICES_AVAILABLE'
        const backend = new ChromecastBackend({ secure: true, loadSdk: async () => device.sdk() })
        const changed = vi.fn()
        backend.onChange(changed)

        await backend.start()

        expect(device.options).toEqual([
            { receiverApplicationId: 'CC1AD845', autoJoinPolicy: 'origin_scoped' },
        ])
        expect(backend.status).toMatchObject({ availability: 'available', devicesFound: false })

        device.setDevices(true)

        expect(backend.status.devicesFound).toBe(true)
        expect(changed).toHaveBeenCalled()
    })

    it('connects through the dialog of the browser and names the device', async () => {
        const { backend, device } = await connected()

        expect(device.requestedSessions).toBe(1)
        expect(backend.status).toMatchObject({
            connection: 'connected',
            deviceName: 'Living Room',
            volume: 0.5,
            muted: false,
        })
    })

    it('treats a dialog closed without a choice as no error', async () => {
        const device = new SimulatedChromecast()
        device.dialogChoice = 'cancel'

        const { backend } = await connected(device)

        expect(backend.status.connection).toBe('disconnected')
    })

    it('loads a song with title, artist, album and cover at a position', async () => {
        const { backend, device, events } = await connected()

        await backend.load(song, 42, true)

        const load = device.loads[0]
        expect(load?.info.contentId).toBe(song.url)
        expect(load?.info.contentUrl).toBe(song.url)
        expect(load?.info.contentType).toBe('audio/flac')
        expect(load?.info.streamType).toBe('BUFFERED')
        expect(load?.info.duration).toBe(200)
        expect(load?.info.hlsSegmentFormat).toBeUndefined()
        expect(load?.info.metadata).toMatchObject({
            type: 'music',
            title: 'Song',
            artist: 'Band',
            albumName: 'Album',
            images: [{ url: song.artworkUrl }],
        })
        expect(load?.request).toMatchObject({ autoplay: true, currentTime: 42 })
        expect(events.playing).toHaveBeenCalled()
    })

    it('tells the receiver the segments of transcoded HLS streams', async () => {
        const { backend, device } = await connected()

        await backend.load(
            { ...song, hls: true, contentType: 'application/vnd.apple.mpegurl' },
            0,
            true,
        )
        await backend.load(
            { ...song, kind: 'video', hls: true, contentType: 'application/vnd.apple.mpegurl' },
            0,
            false,
        )

        expect(device.loads[0]?.info.hlsSegmentFormat).toBe('ts_aac')
        expect(device.loads[1]?.info.hlsSegmentFormat).toBe('ts')
        expect(device.loads[1]?.info.hlsVideoSegmentFormat).toBe('mpeg2_ts')
        expect(device.loads[1]?.info.metadata).toMatchObject({
            type: 'generic',
            title: 'Song',
            subtitle: 'Band',
        })
        expect(device.loads[1]?.request.autoplay).toBe(false)
    })

    it('sends no image without a cover', async () => {
        const { backend, device } = await connected()

        await backend.load({ ...song, artworkUrl: undefined }, 0, true)

        expect(device.loads[0]?.info.metadata).toMatchObject({ images: [] })
    })

    it('fails to load without a device and with the error code of a device', async () => {
        const backend = new ChromecastBackend({
            secure: true,
            loadSdk: async () => new SimulatedChromecast().sdk(),
        })
        await backend.start()
        await expect(backend.load(song, 0, true)).rejects.toEqual(new CastError('session_missing'))

        const { backend: other, device } = await connected()
        device.failNextLoad = 'LOAD_FAILED'
        await expect(other.load(song, 0, true)).rejects.toEqual(new CastError('LOAD_FAILED'))
    })

    it('controls the device: pause, play, seek, volume and mute', async () => {
        const { backend, device, events } = await connected()
        await backend.load(song, 0, true)

        backend.pause()
        await backend.play()
        backend.seek(80)
        backend.setVolume(0.3)
        backend.setMuted(true)
        backend.setMuted(true)

        expect(device.controls).toEqual([
            'playOrPause',
            'playOrPause',
            'seek 80',
            'volume 0.3',
            'muteOrUnmute',
        ])
        expect(events.paused).toHaveBeenCalled()
        expect(backend.position).toBe(80)
        expect(backend.status).toMatchObject({ volume: 0.3, muted: true })
    })

    it('ignores play and pause in the state the device already has', async () => {
        const { backend, device } = await connected()
        await backend.load(song, 0, true)

        await backend.play()
        backend.pause()
        backend.pause()

        expect(device.controls).toEqual(['playOrPause'])
    })

    it('reports the position, the end and errors of the media', async () => {
        const { backend, device, events } = await connected()
        await backend.load(song, 0, true)

        device.progress(12)
        expect(events.timeupdate).toHaveBeenLastCalledWith(12, 200)

        device.finish()
        expect(events.ended).toHaveBeenCalledTimes(1)

        await backend.load(song, 0, true)
        device.fail()
        expect(events.error).toHaveBeenCalledWith(new CastError('media_error'))
    })

    it('does not report the end of the previous media while the next one loads', async () => {
        const { backend, device, events } = await connected()
        await backend.load(song, 0, true)

        const next = backend.load({ ...song, title: 'Next' }, 0, true)
        device.finish()
        await next

        expect(events.ended).not.toHaveBeenCalled()
    })

    it('follows the volume set on the device', async () => {
        const { backend, device } = await connected()

        device.changeVolume(0.8)

        expect(backend.status.volume).toBe(0.8)
    })

    it('stops the media and ends the session', async () => {
        const { backend, device } = await connected()
        await backend.load(song, 0, true)

        backend.stop()
        backend.endSession()
        await flushPromises()

        expect(device.controls).toEqual(['stop'])
        expect(device.endedSessions).toBe(1)
        expect(backend.status).toMatchObject({ connection: 'disconnected', deviceName: null })
    })

    it('plays the formats of the Default Media Receiver', () => {
        const backend = new ChromecastBackend({ secure: true, loadSdk: async () => null })

        expect(backend.formats()).toBe(chromecastFormats)
        expect(chromecastFormats.audio).toContain('Flac')
        expect(chromecastFormats.audio).not.toContain('Mp4Alac')
        expect(chromecastFormats.video.videoCodecs).toContain('H264')
    })
})
