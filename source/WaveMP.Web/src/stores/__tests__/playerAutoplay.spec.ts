import { createPinia, setActivePinia } from 'pinia'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import type { AutoplayResult, Track, TrackPlayback } from '@/api/generated/models'
import type { EngineEvents } from '@/player/engine'
import { getAutoplayTracks, getPlayback, recordPlay, reportNowPlaying } from '@/player/api'
import { playerDependencies, usePlayerStore, type PlayerEngine } from '../player'
import { useSessionStore } from '../session'

vi.mock('@/lyrics/api', () => ({
    getLyrics: vi.fn().mockResolvedValue(null),
}))

vi.mock('@/player/api', () => ({
    getAutoplayTracks: vi.fn(),
    getPlayback: vi.fn(),
    getVideoPlayback: vi.fn(),
    recordPlay: vi.fn(),
    reportNowPlaying: vi.fn(),
}))

class FakeEngine implements PlayerEngine {
    position = 0
    hasPrepared = false
    loaded: { url: string; gain: number; position: number }[] = []
    prepared: ({ url: string; gain: number } | null)[] = []
    readonly load = vi.fn((source: { url: string; gain: number }, position = 0) => {
        this.loaded.push({ ...source, position })
    })
    readonly prepare = vi.fn((source: { url: string; gain: number } | null) => {
        this.prepared.push(source)
        this.hasPrepared = source !== null
    })
    readonly play = vi.fn(() => {
        this.events.playing?.()
        return Promise.resolve()
    })
    readonly pause = vi.fn()
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

    get lastUrl(): string | undefined {
        return this.loaded.at(-1)?.url
    }
}

const now = Date.parse('2026-11-19T20:00:00Z')

function track(id: string, extra: Partial<Track> = {}): Track {
    return {
        id,
        title: id.toUpperCase(),
        hasAudio: true,
        durationMilliseconds: 200_000,
        artist: { id: 'ar', name: 'Band' },
        ...extra,
    }
}

function direct(id: string, extra: Partial<TrackPlayback> = {}): TrackPlayback {
    return {
        trackId: id,
        method: 'DirectPlay',
        format: 'Mp3',
        contentType: 'audio/mpeg',
        streamUrl: `/stream/${id}`,
        durationMilliseconds: 200_000,
        expiresAt: new Date(now + 3_600_000),
        ...extra,
    }
}

/** An answer of autoplay with its own stream URLs, so tests see which playback was used. */
function answer(...ids: string[]): AutoplayResult {
    return {
        items: ids.map((id) => ({
            track: track(id),
            playback: direct(id, { streamUrl: `/autoplay/${id}` }),
        })),
    }
}

const ab = [track('a'), track('b')]

describe('player store: autoplay', () => {
    let engine: FakeEngine
    const original = { ...playerDependencies }

    beforeEach(() => {
        setActivePinia(createPinia())
        window.localStorage.clear()
        vi.mocked(getPlayback)
            .mockReset()
            .mockImplementation((id) => Promise.resolve(direct(id)))
        vi.mocked(getAutoplayTracks)
            .mockReset()
            .mockResolvedValue(answer('x', 'y', 'z'))
        vi.mocked(recordPlay).mockReset().mockResolvedValue(undefined)
        vi.mocked(reportNowPlaying).mockReset().mockResolvedValue(undefined)
        playerDependencies.createEngine = (events) => (engine = new FakeEngine(events))
        playerDependencies.audioFormats = () => ['Mp3']
        playerDependencies.random = () => 0
        playerDependencies.now = () => now
        playerDependencies.mediaSession = false
    })

    afterEach(() => {
        Object.assign(playerDependencies, original)
    })

    function titles(player: ReturnType<typeof usePlayerStore>): string[] {
        return player.autoplay.map((entry) => entry.track.id ?? '')
    }

    function queued(player: ReturnType<typeof usePlayerStore>): string[] {
        return player.queue.items.map((item) => `${item.track.id}${item.autoplay ? '*' : ''}`)
    }

    it('chooses the songs after a new list and shows them waiting', async () => {
        const player = usePlayerStore()

        player.playTracks(ab, 0)
        await flushPromises()

        expect(getAutoplayTracks).toHaveBeenCalledExactlyOnceWith(['a', 'b'], [], ['Mp3'], 10)
        expect(titles(player)).toEqual(['x', 'y', 'z'])
        expect(player.autoplayActive).toBe(true)
        expect(player.autoplayLoading).toBe(false)
        expect(queued(player)).toEqual(['a', 'b'])
    })

    it('chooses nothing while autoplay is off for the profile or repeat is on', async () => {
        const session = useSessionStore()
        session.autoplay = { enabled: false, includeVideoOnly: true }
        const player = usePlayerStore()

        player.playTracks(ab, 0)
        await flushPromises()
        expect(getAutoplayTracks).not.toHaveBeenCalled()
        expect(player.autoplayActive).toBe(false)

        session.autoplay = { enabled: true, includeVideoOnly: true }
        player.cycleRepeat()
        await flushPromises()
        expect(getAutoplayTracks).not.toHaveBeenCalled()

        player.cycleRepeat()
        player.cycleRepeat()
        await flushPromises()
        expect(getAutoplayTracks).toHaveBeenCalledOnce()
        expect(titles(player)).toEqual(['x', 'y', 'z'])
    })

    it('does not choose again when the list is only moved or shuffled', async () => {
        const player = usePlayerStore()
        player.playTracks([...ab, track('c')], 0)
        await flushPromises()

        player.moveItem(player.queue.items[2]!.key, -1)
        player.toggleShuffle()
        await flushPromises()

        expect(getAutoplayTracks).toHaveBeenCalledOnce()
    })

    it('plays the first waiting song when the list has ended, with the playback it brought', async () => {
        const player = usePlayerStore()
        player.playTracks(ab, 1)
        await flushPromises()

        engine.events.ended?.()
        await flushPromises()

        expect(player.state.trackId).toBe('x')
        expect(player.state.playback).toBe('playing')
        expect(engine.lastUrl).toBe('/autoplay/x')
        expect(getPlayback).not.toHaveBeenCalledWith('x', expect.anything())
        expect(queued(player)).toEqual(['a', 'b', 'x*'])
        expect(titles(player)).toEqual(['y', 'z'])
    })

    it('goes on from song to song of autoplay until the user stops', async () => {
        const player = usePlayerStore()
        player.playTracks(ab, 1)
        await flushPromises()

        engine.events.ended?.()
        await flushPromises()
        engine.events.ended?.()
        await flushPromises()

        expect(player.state.trackId).toBe('y')
        expect(queued(player)).toEqual(['a', 'b', 'x*', 'y*'])
    })

    it('prepares the song of autoplay before the list ends, so it follows without a gap', async () => {
        const player = usePlayerStore()
        player.playTracks(ab, 1)
        await flushPromises()

        engine.events.timeupdate?.(185, 200)
        await flushPromises()

        expect(engine.prepared.at(-1)).toEqual({ url: '/autoplay/x', gain: 1 })
        engine.events.advanced?.()
        expect(player.state.trackId).toBe('x')
        expect(queued(player)).toEqual(['a', 'b', 'x*'])
    })

    it('takes a new stream URL when the one autoplay brought expires soon', async () => {
        const player = usePlayerStore()
        vi.mocked(getAutoplayTracks).mockResolvedValue({
            items: [
                { track: track('x'), playback: direct('x', { expiresAt: new Date(now + 30_000) }) },
            ],
        })
        player.playTracks(ab, 1)
        await flushPromises()

        engine.events.ended?.()
        await flushPromises()

        expect(getPlayback).toHaveBeenCalledWith('x', ['Mp3'], 'Audio')
        expect(engine.lastUrl).toBe('/stream/x')
    })

    it('waits for the songs of autoplay that are still being chosen when the list ends', async () => {
        let resolve: (value: AutoplayResult) => void = () => undefined
        vi.mocked(getAutoplayTracks).mockReturnValue(
            new Promise((done) => {
                resolve = done
            }),
        )
        const player = usePlayerStore()
        player.playTracks(ab, 1)
        await flushPromises()
        expect(player.autoplayLoading).toBe(true)

        engine.events.ended?.()
        await flushPromises()
        expect(player.state.loading).toBe(true)
        expect(player.state.trackId).toBe('b')

        resolve(answer('x'))
        await flushPromises()
        expect(player.state.trackId).toBe('x')
        expect(player.state.playback).toBe('playing')
    })

    it('stops at the end of the list when autoplay has no songs', async () => {
        vi.mocked(getAutoplayTracks).mockResolvedValue({ items: [] })
        const player = usePlayerStore()
        player.playTracks(ab, 1)
        await flushPromises()

        engine.events.ended?.()
        await flushPromises()

        expect(player.state.trackId).toBe('b')
        expect(player.state.playback).toBe('paused')
        expect(queued(player)).toEqual(['a', 'b'])
    })

    it('stops at the end of the list when the request fails', async () => {
        vi.mocked(getAutoplayTracks).mockRejectedValue(new Error('offline'))
        const player = usePlayerStore()
        player.playTracks(ab, 1)
        await flushPromises()

        engine.events.ended?.()
        await flushPromises()

        expect(player.state.playback).toBe('paused')
        expect(player.autoplayLoading).toBe(false)
    })

    it('skips to the song of autoplay at the end of a playing list, but not while paused', async () => {
        const player = usePlayerStore()
        player.playTracks(ab, 1)
        await flushPromises()

        player.pause()
        player.next()
        await flushPromises()
        expect(queued(player)).toEqual(['a', 'b'])
        expect(player.state.playback).toBe('idle')

        player.playItem(player.queue.items[1]!.key)
        await flushPromises()
        player.next()
        await flushPromises()
        expect(player.state.trackId).toBe('x')
        expect(queued(player)).toEqual(['a', 'b', 'x*'])
    })

    it('loads more songs when few are left, without those it already chose', async () => {
        vi.mocked(getAutoplayTracks)
            .mockResolvedValueOnce(answer('x', 'y'))
            .mockResolvedValueOnce(answer('y', 'z', 'w'))
        const player = usePlayerStore()
        player.playTracks(ab, 1)
        await flushPromises()

        engine.events.ended?.()
        await flushPromises()

        expect(getAutoplayTracks).toHaveBeenLastCalledWith(['a', 'b'], ['x', 'y'], ['Mp3'], 10)
        expect(titles(player)).toEqual(['y', 'z', 'w'])
    })

    it('stops asking for more once the list has no more songs', async () => {
        vi.mocked(getAutoplayTracks)
            .mockResolvedValueOnce(answer('x'))
            .mockResolvedValueOnce({ items: [] })
        const player = usePlayerStore()
        player.playTracks(ab, 1)
        await flushPromises()

        engine.events.ended?.()
        await flushPromises()
        player.removeAutoplayEntry('none')
        await flushPromises()

        expect(getAutoplayTracks).toHaveBeenCalledTimes(2)
    })

    it('ignores the answer for a list that was replaced meanwhile', async () => {
        let first: (value: AutoplayResult) => void = () => undefined
        vi.mocked(getAutoplayTracks)
            .mockReturnValueOnce(
                new Promise((done) => {
                    first = done
                }),
            )
            .mockResolvedValueOnce(answer('n'))
        const player = usePlayerStore()
        player.playTracks(ab, 0)
        await flushPromises()

        player.playTracks([track('c')], 0)
        await flushPromises()
        first(answer('old'))
        await flushPromises()

        expect(titles(player)).toEqual(['n'])
    })

    it('puts songs the user adds before those of autoplay and chooses again', async () => {
        vi.mocked(getAutoplayTracks)
            .mockResolvedValueOnce(answer('x', 'y', 'z'))
            .mockResolvedValueOnce(answer('w'))
            .mockResolvedValueOnce(answer('m', 'n', 'o'))
        const player = usePlayerStore()
        player.playTracks(ab, 1)
        await flushPromises()
        engine.events.timeupdate?.(185, 200)
        await flushPromises()
        expect(queued(player)).toEqual(['a', 'b', 'x*'])

        player.addToQueue([track('c')])
        await flushPromises()

        expect(queued(player)).toEqual(['a', 'b', 'c'])
        expect(engine.prepared.at(-1)).toBeNull()
        expect(getAutoplayTracks).toHaveBeenLastCalledWith(['a', 'b', 'c'], [], ['Mp3'], 10)
        expect(titles(player)).toEqual(['m', 'n', 'o'])
    })

    it('takes back the songs of autoplay after the current one when it is turned off', async () => {
        const session = useSessionStore()
        const setEnabled = vi.spyOn(session, 'setAutoplayEnabled').mockResolvedValue()
        const player = usePlayerStore()
        player.playTracks(ab, 1)
        await flushPromises()
        engine.events.ended?.()
        await flushPromises()
        engine.events.timeupdate?.(185, 200)
        await flushPromises()
        expect(queued(player)).toEqual(['a', 'b', 'x*', 'y*'])
        expect(player.state.autoplay).toBe(true)

        player.toggleAutoplay()
        expect(setEnabled).toHaveBeenCalledWith(false)
        session.autoplay = { enabled: false, includeVideoOnly: true }
        await flushPromises()

        expect(queued(player)).toEqual(['a', 'b', 'x*'])
        expect(player.state.trackId).toBe('x')
        expect(player.autoplay).toEqual([])
        expect(player.state.autoplay).toBe(false)

        engine.events.ended?.()
        await flushPromises()
        expect(player.state.playback).toBe('paused')
    })

    it('chooses again when songs with a video only are switched on or off', async () => {
        const session = useSessionStore()
        const player = usePlayerStore()
        player.playTracks(ab, 0)
        await flushPromises()

        session.autoplay = { enabled: true, includeVideoOnly: false }
        await flushPromises()

        expect(getAutoplayTracks).toHaveBeenCalledTimes(2)
    })

    it('plays a waiting song at once and skips those before it', async () => {
        const player = usePlayerStore()
        player.playTracks(ab, 0)
        await flushPromises()

        player.playAutoplayEntry(player.autoplay[1]!.key)
        await flushPromises()

        expect(player.state.trackId).toBe('y')
        expect(player.state.playback).toBe('playing')
        expect(queued(player)).toEqual(['a', 'y*', 'b'])
        expect(titles(player)).toEqual(['z'])
        expect(getAutoplayTracks).toHaveBeenLastCalledWith(['a', 'b'], ['x', 'y', 'z'], ['Mp3'], 10)
    })

    it('removes a waiting song', async () => {
        vi.mocked(getAutoplayTracks)
            .mockResolvedValueOnce(answer('x', 'y', 'z', 'w'))
            .mockResolvedValueOnce(answer('v'))
        const player = usePlayerStore()
        player.playTracks(ab, 0)
        await flushPromises()

        player.removeAutoplayEntry(player.autoplay[0]!.key)
        await flushPromises()
        expect(titles(player)).toEqual(['y', 'z', 'w'])
        expect(getAutoplayTracks).toHaveBeenCalledOnce()

        player.removeAutoplayEntry(player.autoplay[0]!.key)
        await flushPromises()
        expect(titles(player)).toEqual(['z', 'w', 'v'])
        expect(getAutoplayTracks).toHaveBeenLastCalledWith(
            ['a', 'b'],
            ['x', 'y', 'z', 'w'],
            ['Mp3'],
            10,
        )
    })

    it('plays songs that only have a music video', async () => {
        vi.mocked(getAutoplayTracks).mockResolvedValue({
            items: [
                {
                    track: track('clip', { hasAudio: false, hasVideo: true }),
                    playback: direct('clip', { method: 'Transcode', streamUrl: '/hls/clip' }),
                },
            ],
        })
        const player = usePlayerStore()
        player.playTracks(ab, 1)
        await flushPromises()

        engine.events.ended?.()
        await flushPromises()

        expect(player.state.trackId).toBe('clip')
        expect(engine.loaded.at(-1)).toEqual({ url: '/hls/clip', gain: 1, hls: true, position: 0 })
    })

    it('lets the sleep timer stop at the end of the list, not after the songs of autoplay', async () => {
        vi.useFakeTimers()
        try {
            const player = usePlayerStore()
            player.playTracks(ab, 1)
            await flushPromises()
            player.startSleepTimer('endOfQueue')

            engine.events.timeupdate?.(185, 200)
            await flushPromises()
            expect(engine.prepared.filter(Boolean)).toEqual([])

            engine.events.ended?.()
            await flushPromises()
            expect(player.state.trackId).toBe('b')
            expect(player.state.playback).toBe('paused')
            expect(queued(player)).toEqual(['a', 'b'])
        } finally {
            vi.useRealTimers()
        }
    })

    it('forgets the waiting songs when the queue is emptied', async () => {
        const player = usePlayerStore()
        player.playTracks(ab, 0)
        await flushPromises()

        player.clear()
        await flushPromises()

        expect(player.autoplay).toEqual([])
        expect(player.autoplayActive).toBe(false)
    })
})
