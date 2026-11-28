import { createPinia, setActivePinia } from 'pinia'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import type { Track, TrackPlayback, TrackVideoPlayback } from '@/api/generated/models'
import { FakeCastLayer } from '@/casting/__tests__/fakes'
import type { DeckSource, EngineEvents } from '@/player/engine'
import { getLyrics } from '@/lyrics/api'
import { getPlayback, getVideoPlayback, recordPlay } from '@/player/api'
import { castDependencies } from '../cast'
import { playerDependencies, usePlayerStore, type PlayerEngine } from '../player'
import { useSessionStore } from '../session'

vi.mock('@/lyrics/api', () => ({
    getLyrics: vi.fn(),
}))

vi.mock('@/player/api', () => ({
    getPlayback: vi.fn(),
    getVideoPlayback: vi.fn(),
    recordPlay: vi.fn(),
    reportNowPlaying: vi.fn(),
}))

/** The audio engine of the browser. */
class FakeEngine implements PlayerEngine {
    position = 0
    hasPrepared = false
    readonly loaded: (DeckSource & { position: number })[] = []
    readonly load = vi.fn((source: DeckSource, position = 0) => {
        this.loaded.push({ ...source, position })
    })
    readonly prepare = vi.fn()
    readonly play = vi.fn(() => {
        this.events.playing?.()
        return Promise.resolve()
    })
    readonly pause = vi.fn(() => this.events.paused?.())
    readonly stop = vi.fn()
    readonly seek = vi.fn()
    readonly setVolume = vi.fn()
    readonly setTrackGain = vi.fn()
    readonly setEqualizer = vi.fn()
    readonly fadeOut = vi.fn()
    readonly restoreOutput = vi.fn()
    equalizerAvailable = true
    analyser: AnalyserNode | null = null

    constructor(readonly events: EngineEvents) {}
}

const origin = 'https://music.example'

function track(id: string, extra: Partial<Track> = {}): Track {
    return {
        id,
        title: id.toUpperCase(),
        hasAudio: true,
        hasCover: true,
        durationMilliseconds: 200_000,
        artist: { id: 'ar', name: 'Band' },
        album: { id: 'al', title: 'Album' },
        ...extra,
    }
}

function playbackOf(id: string, target?: string): TrackPlayback {
    return {
        trackId: id,
        method: 'DirectPlay',
        format: 'Mp3',
        contentType: 'audio/mpeg',
        streamUrl: target === 'Cast' ? `/cast/${id}` : `/stream/${id}`,
        coverUrl: target === 'Cast' ? `/cover/${id}` : null,
        durationMilliseconds: 200_000,
        replayGain: { trackGain: -6, trackPeak: 0.5 },
    }
}

function videoOf(id: string): TrackVideoPlayback {
    return {
        trackId: id,
        method: 'DirectPlay',
        protocol: 'File',
        contentType: 'video/mp4',
        streamUrl: `/cast-video/${id}`,
        coverUrl: `/cover/${id}`,
        durationMilliseconds: 200_000,
    }
}

const abc = [track('a'), track('b'), track('c', { hasCover: false })]

describe('player store while casting', () => {
    let engine: FakeEngine
    let layer: FakeCastLayer
    const originalPlayer = { ...playerDependencies }
    const originalCast = { ...castDependencies }

    beforeEach(() => {
        setActivePinia(createPinia())
        window.localStorage.clear()
        vi.mocked(getPlayback)
            .mockReset()
            .mockImplementation((id, _formats, _sound, target) =>
                Promise.resolve(playbackOf(id, target)),
            )
        vi.mocked(getVideoPlayback)
            .mockReset()
            .mockImplementation((id) => Promise.resolve(videoOf(id)))
        vi.mocked(recordPlay).mockReset().mockResolvedValue(undefined)
        vi.mocked(getLyrics).mockReset().mockResolvedValue(null)
        layer = new FakeCastLayer()
        castDependencies.createLayer = () => layer
        playerDependencies.createEngine = (events) => (engine = new FakeEngine(events))
        playerDependencies.audioFormats = () => ['Mp3', 'Flac', 'OggOpus']
        playerDependencies.origin = () => origin
        playerDependencies.random = () => 0
        playerDependencies.mediaSession = false
    })

    afterEach(() => {
        Object.assign(playerDependencies, originalPlayer)
        Object.assign(castDependencies, originalCast)
    })

    /** Plays the queue in the browser and lets the first track reach a position. */
    async function playingLocally(at = 42) {
        const player = usePlayerStore()
        player.playTracks(abc, 0)
        await flushPromises()
        engine.events.timeupdate?.(at, 200)
        return player
    }

    it('moves the playing track to the device at its position', async () => {
        const player = await playingLocally()

        layer.connect('Living Room')
        await flushPromises()

        expect(engine.stop).toHaveBeenCalled()
        expect(getPlayback).toHaveBeenLastCalledWith('a', ['Mp3', 'Flac'], undefined, 'Cast')
        expect(layer.loads).toEqual([
            {
                media: {
                    url: `${origin}/cast/a`,
                    contentType: 'audio/mpeg',
                    kind: 'audio',
                    hls: false,
                    title: 'A',
                    artist: 'Band',
                    album: 'Album',
                    artworkUrl: `${origin}/cover/a`,
                    duration: 200,
                },
                position: 42,
                autoplay: true,
            },
        ])
        expect(player.casting).toBe(true)
        expect(player.state.playback).toBe('playing')
        expect(player.state.cast).toMatchObject({
            availability: 'available',
            connection: 'connected',
            device: 'Living Room',
        })
    })

    it('keeps a paused track paused on the device', async () => {
        const player = await playingLocally()
        player.pause()

        layer.connect()
        await flushPromises()

        expect(layer.loads[0]).toMatchObject({ position: 42, autoplay: false })
        expect(player.state.playback).toBe('paused')
    })

    it('sends the controls of the web app to the device', async () => {
        const player = await playingLocally()
        layer.connect()
        await flushPromises()

        player.pause()
        player.play()
        player.seek(80)
        player.setVolume(0.3)
        player.toggleMute()
        await flushPromises()

        expect(layer.calls).toEqual(['pause', 'play', 'seek 80', 'volume 0.3', 'muted true'])
        expect(player.state.volume).toBe(0.3)
        expect(player.state.muted).toBe(true)
        expect(engine.setVolume).not.toHaveBeenCalledWith(0.3)
    })

    it('loads the next track of the queue on the device when one ends, without the browser', async () => {
        const player = await playingLocally()
        layer.connect()
        await flushPromises()

        layer.events.timeupdate?.(190, 200)
        layer.events.ended?.()
        await flushPromises()

        expect(player.state.trackId).toBe('b')
        expect(layer.loads.map((load) => load.media.url)).toEqual([
            `${origin}/cast/a`,
            `${origin}/cast/b`,
        ])
        expect(engine.prepare).not.toHaveBeenCalledWith(
            expect.objectContaining({ url: '/stream/b' }),
        )

        player.next()
        await flushPromises()

        // The third track has no cover, so the device shows none.
        expect(layer.loads.at(-1)?.media.artworkUrl).toBeUndefined()
    })

    it('greys out the equalizer, crossfade, ReplayGain and the visualization while casting', async () => {
        const session = useSessionStore()
        session.playerViewMode = 'Visualization'
        const player = await playingLocally()
        expect(player.equalizerAvailable).toBe(true)
        expect(player.state.effects).toBe(true)

        layer.connect()
        await flushPromises()

        expect(player.equalizerAvailable).toBe(false)
        expect(player.state.effects).toBe(false)
        expect(player.stageView).toBe('cover')
    })

    it('casts the video while the video view is chosen and the track has one', async () => {
        const session = useSessionStore()
        session.playerViewMode = 'Video'
        const player = usePlayerStore()
        player.playTracks([track('v', { hasVideo: true })], 0)
        await flushPromises()

        layer.connect()
        await flushPromises()

        expect(getVideoPlayback).toHaveBeenLastCalledWith(
            'v',
            { containers: ['Mp4'], videoCodecs: ['H264'], audioCodecs: ['Aac'] },
            'Cast',
        )
        expect(layer.loads.at(-1)?.media).toMatchObject({
            kind: 'video',
            url: `${origin}/cast-video/v`,
            contentType: 'video/mp4',
        })
        expect(player.stageView).toBe('cover')

        // Leaving the video view casts the sound again.
        session.playerViewMode = 'Cover'
        await flushPromises()

        expect(layer.loads.at(-1)?.media).toMatchObject({ kind: 'audio', url: `${origin}/cast/v` })
    })

    it('goes back to the browser, paused at the position of the device, when it disconnects', async () => {
        const player = await playingLocally()
        layer.connect()
        await flushPromises()
        layer.events.timeupdate?.(120, 200)
        const loadsBefore = engine.loaded.length

        layer.disconnect()
        await flushPromises()

        expect(player.casting).toBe(false)
        expect(layer.calls).toContain('stop')
        expect(engine.loaded.slice(loadsBefore)).toEqual([
            expect.objectContaining({ url: '/stream/a', position: 120 }),
        ])
        expect(player.state.playback).toBe('paused')
        expect(player.state.effects).toBe(true)
    })

    it('opens the device selection with the current stream for AirPlay', async () => {
        const player = await playingLocally()

        player.requestCast()

        expect(layer.calls).toEqual(['request /stream/a'])
    })
})
