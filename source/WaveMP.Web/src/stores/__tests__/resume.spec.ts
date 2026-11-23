import { createPinia, setActivePinia } from 'pinia'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import {
    PlaybackSourceKindObject,
    RepeatModeObject,
    type PlaybackState,
    type Track,
    type TrackPlayback,
    type UpdatePlaybackState,
} from '@/api/generated/models'
import type { EngineEvents } from '@/player/engine'
import {
    clearPlaybackState,
    getPlayback,
    getPlaybackState,
    recordPlay,
    reportNowPlaying,
    savePlaybackState,
} from '@/player/api'
import { playerDependencies, usePlayerStore, type PlayerEngine } from '../player'
import { resumeDependencies, useResumeStore } from '../resume'
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
    getPlaybackState: vi.fn(),
    savePlaybackState: vi.fn(),
    clearPlaybackState: vi.fn(),
}))

class FakeEngine implements PlayerEngine {
    position = 0
    hasPrepared = false
    loaded: { url: string; position: number }[] = []
    readonly load = vi.fn((source: { url: string }, position = 0) => {
        this.loaded.push({ url: source.url, position })
    })
    readonly prepare = vi.fn()
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
}

function track(id: string): Track {
    return { id, title: id.toUpperCase(), hasAudio: true, durationMilliseconds: 200_000 }
}

function direct(id: string): TrackPlayback {
    return {
        trackId: id,
        method: 'DirectPlay',
        format: 'Mp3',
        contentType: 'audio/mpeg',
        streamUrl: `/stream/${id}`,
        durationMilliseconds: 200_000,
    }
}

const abc = [track('a'), track('b'), track('c')]

function saved(extra: Partial<PlaybackState> = {}): PlaybackState {
    return {
        items: abc.map((value) => ({ track: value, autoplay: false })),
        currentIndex: 1,
        positionMilliseconds: 42_000,
        shuffle: false,
        originalOrder: [],
        repeat: RepeatModeObject.All,
        source: { kind: PlaybackSourceKindObject.Album, id: 'al1' },
        device: { id: 'this-device', name: 'Firefox · Windows' },
        changedAt: new Date('2026-11-19T10:00:00Z'),
        ...extra,
    }
}

function notFound(): Error {
    return Object.assign(new Error('not found'), { responseStatusCode: 404 })
}

/** The timers of the store, run by the tests. */
class ManualTimers {
    private next = 1
    readonly timeouts = new Map<number, () => void>()
    readonly intervals = new Map<number, () => void>()

    install(): void {
        resumeDependencies.setTimeout = (handler) => {
            const id = this.next++
            this.timeouts.set(id, handler)
            return id
        }
        resumeDependencies.clearTimeout = (id) => this.timeouts.delete(id)
        resumeDependencies.setInterval = (handler) => {
            const id = this.next++
            this.intervals.set(id, handler)
            return id
        }
        resumeDependencies.clearInterval = (id) => this.intervals.delete(id)
    }

    runTimeouts(): void {
        const handlers = [...this.timeouts.values()]
        this.timeouts.clear()
        handlers.forEach((handler) => handler())
    }

    tick(): void {
        this.intervals.forEach((handler) => handler())
    }
}

describe('resume store', () => {
    let engine: FakeEngine
    let timers: ManualTimers
    const originalPlayer = { ...playerDependencies }
    const originalResume = { ...resumeDependencies }

    function savedBodies(): UpdatePlaybackState[] {
        return vi.mocked(savePlaybackState).mock.calls.map(([body]) => body)
    }

    async function start(state: PlaybackState | Error) {
        if (state instanceof Error) {
            vi.mocked(getPlaybackState).mockRejectedValue(state)
        } else {
            vi.mocked(getPlaybackState).mockResolvedValue(state)
        }

        const session = useSessionStore()
        session.token = 'token'
        session.autoplay = { enabled: false, includeVideoOnly: false }
        const player = usePlayerStore()
        const resume = useResumeStore()
        await flushPromises()
        return { session, player, resume }
    }

    beforeEach(() => {
        setActivePinia(createPinia())
        window.localStorage.clear()
        vi.mocked(getPlayback).mockReset()
        vi.mocked(getPlayback).mockImplementation((id) => Promise.resolve(direct(id)))
        vi.mocked(recordPlay).mockReset().mockResolvedValue(undefined)
        vi.mocked(reportNowPlaying).mockReset().mockResolvedValue(undefined)
        vi.mocked(getPlaybackState).mockReset()
        vi.mocked(savePlaybackState).mockReset().mockResolvedValue(undefined)
        vi.mocked(clearPlaybackState).mockReset().mockResolvedValue(undefined)
        playerDependencies.createEngine = (events) => (engine = new FakeEngine(events))
        playerDependencies.audioFormats = () => ['Mp3']
        playerDependencies.random = () => 0
        playerDependencies.mediaSession = false
        resumeDependencies.deviceId = () => 'this-device'
        resumeDependencies.deviceName = () => 'Chrome · Linux'
        resumeDependencies.listen = false
        timers = new ManualTimers()
        timers.install()
    })

    afterEach(() => {
        Object.assign(playerDependencies, originalPlayer)
        Object.assign(resumeDependencies, originalResume)
    })

    describe('start', () => {
        it('shows the saved queue at the saved position without playing', async () => {
            const { player, resume } = await start(saved())

            expect(player.queue.items.map((item) => item.track.id)).toEqual(['a', 'b', 'c'])
            expect(player.current?.track.id).toBe('b')
            expect(player.position).toBe(42)
            expect(player.repeat).toBe('all')
            expect(player.source).toEqual({ kind: 'album', id: 'al1' })
            expect(player.playback).toBe('paused')
            expect(engine.loaded).toEqual([{ url: '/stream/b', position: 42 }])
            expect(engine.play).not.toHaveBeenCalled()
            // The own state of this device needs no notice and no new save.
            expect(resume.notice).toBeNull()
            expect(savePlaybackState).not.toHaveBeenCalled()
        })

        it('continues at the saved position on play', async () => {
            const { player } = await start(saved())

            player.play()
            await flushPromises()

            expect(engine.play).toHaveBeenCalledTimes(1)
            expect(engine.loaded).toHaveLength(1)
            expect(player.playback).toBe('playing')
            expect(player.current?.track.id).toBe('b')
        })

        it('starts empty without a saved state', async () => {
            const { player } = await start(notFound())

            expect(player.queue.items).toEqual([])
            expect(player.playback).toBe('idle')
        })

        it('keeps what the user started before the saved state arrived', async () => {
            let answer: (state: PlaybackState) => void = () => undefined
            vi.mocked(getPlaybackState).mockReturnValue(
                new Promise((resolve) => {
                    answer = resolve
                }),
            )
            const session = useSessionStore()
            session.token = 'token'
            session.autoplay = { enabled: false, includeVideoOnly: false }
            const player = usePlayerStore()
            useResumeStore()

            player.playTracks([track('x')], 0)
            answer(saved())
            await flushPromises()

            expect(player.queue.items.map((item) => item.track.id)).toEqual(['x'])
            expect(player.playback).toBe('playing')
        })

        it('offers to continue the state of another device', async () => {
            const { player, resume } = await start(
                saved({ device: { id: 'phone', name: 'Safari · iPhone' } }),
            )

            expect(resume.notice).toEqual({ device: 'Safari · iPhone' })

            resume.resume()
            await flushPromises()

            expect(resume.notice).toBeNull()
            expect(player.playback).toBe('playing')
            expect(engine.loaded).toEqual([{ url: '/stream/b', position: 42 }])
            // Playing makes this device the one the profile listens on.
            expect(savedBodies()).toHaveLength(1)
            expect(savedBodies()[0]).toMatchObject({
                currentIndex: 1,
                positionMilliseconds: 42000,
                device: { id: 'this-device', name: 'Chrome · Linux' },
            })
        })

        it('hides the notice when dismissed or when another song plays', async () => {
            const first = await start(saved({ device: { id: 'phone', name: 'Safari · iPhone' } }))
            first.resume.dismiss()
            expect(first.resume.notice).toBeNull()

            setActivePinia(createPinia())
            const second = await start(saved({ device: { id: 'phone', name: 'Safari · iPhone' } }))
            second.player.next()
            await flushPromises()
            expect(second.resume.notice).toBeNull()
        })

        it('loads the state of the next profile', async () => {
            const { session, player } = await start(saved())
            vi.mocked(getPlaybackState).mockResolvedValue(
                saved({ items: [{ track: track('z') }], currentIndex: 0, positionMilliseconds: 0 }),
            )

            session.token = 'other'
            await flushPromises()

            expect(player.queue.items.map((item) => item.track.id)).toEqual(['z'])
            expect(savePlaybackState).not.toHaveBeenCalled()
        })
    })

    describe('saving', () => {
        it('saves a new list at once with device and source', async () => {
            const { player } = await start(notFound())

            player.playTracks(abc, 0, { source: { kind: 'playlist', id: 'p1' } })
            await flushPromises()

            expect(savedBodies()).toEqual([
                {
                    items: [{ trackId: 'a' }, { trackId: 'b' }, { trackId: 'c' }],
                    currentIndex: 0,
                    positionMilliseconds: 0,
                    shuffle: false,
                    originalOrder: [],
                    repeat: RepeatModeObject.Off,
                    source: { kind: PlaybackSourceKindObject.Playlist, id: 'p1' },
                    device: { id: 'this-device', name: 'Chrome · Linux' },
                },
            ])
        })

        it('saves on a song change, on pause and every few seconds, not on every progress', async () => {
            const { player } = await start(notFound())
            player.playTracks(abc, 0)
            await flushPromises()
            vi.mocked(savePlaybackState).mockClear()

            for (let time = 1; time <= 9; time++) {
                engine.events.timeupdate?.(time, 200)
            }
            await flushPromises()
            expect(savePlaybackState).not.toHaveBeenCalled()

            timers.tick()
            await flushPromises()
            expect(savedBodies().map((body) => body.positionMilliseconds)).toEqual([9000])

            engine.events.timeupdate?.(12, 200)
            player.pause()
            await flushPromises()
            expect(savedBodies().at(-1)?.positionMilliseconds).toBe(12000)
            expect(timers.intervals.size).toBe(0)

            player.next()
            await flushPromises()
            expect(savedBodies().at(-1)).toMatchObject({ currentIndex: 1, positionMilliseconds: 0 })
        })

        it('saves an unchanged state only once', async () => {
            const { player } = await start(notFound())
            player.playTracks(abc, 0)
            await flushPromises()
            vi.mocked(savePlaybackState).mockClear()

            timers.tick()
            timers.tick()
            await flushPromises()

            expect(savePlaybackState).not.toHaveBeenCalled()
            expect(player.playback).toBe('playing')
        })

        it('saves changes of repeat and shuffle shortly after', async () => {
            const { player } = await start(notFound())
            player.playTracks(abc, 0)
            await flushPromises()
            vi.mocked(savePlaybackState).mockClear()

            player.cycleRepeat()
            player.toggleShuffle()
            await flushPromises()
            expect(savePlaybackState).not.toHaveBeenCalled()

            timers.runTimeouts()
            await flushPromises()
            expect(savedBodies()).toHaveLength(1)
            expect(savedBodies()[0]).toMatchObject({ repeat: RepeatModeObject.All, shuffle: true })
        })

        it('saves nothing before the saved state is loaded', async () => {
            vi.mocked(getPlaybackState).mockReturnValue(new Promise(() => undefined))
            const session = useSessionStore()
            session.token = 'token'
            const player = usePlayerStore()
            useResumeStore()

            player.playTracks(abc, 0)
            await flushPromises()
            timers.runTimeouts()
            await flushPromises()

            expect(savePlaybackState).not.toHaveBeenCalled()
        })
    })

    describe('other devices', () => {
        const outdated = () => Object.assign(new Error('outdated'), { responseStatusCode: 409 })

        it('takes over the newer state of another device when a save is too old', async () => {
            const { player, resume } = await start(notFound())
            player.playTracks(abc, 0)
            await flushPromises()
            player.pause()
            engine.events.timeupdate?.(5, 200)
            vi.mocked(savePlaybackState).mockRejectedValue(outdated())
            vi.mocked(getPlaybackState).mockResolvedValue(
                saved({ device: { id: 'phone', name: 'Safari · iPhone' } }),
            )

            timers.runTimeouts()
            await flushPromises()

            expect(player.current?.track.id).toBe('b')
            expect(player.position).toBe(42)
            expect(player.playback).toBe('paused')
            expect(resume.notice).toEqual({ device: 'Safari · iPhone' })
        })

        it('does not replace what plays here', async () => {
            const { player, resume } = await start(notFound())
            player.playTracks([track('x')], 0)
            await flushPromises()
            vi.mocked(getPlaybackState).mockResolvedValue(
                saved({ device: { id: 'phone', name: 'Safari · iPhone' } }),
            )

            await resume.checkOtherDevices()

            expect(player.current?.track.id).toBe('x')
            expect(resume.notice).toBeNull()
        })

        it('takes over a newer state of another device once while nothing plays', async () => {
            const { player, resume } = await start(saved())
            const phone = saved({
                device: { id: 'phone', name: 'Safari · iPhone' },
                currentIndex: 2,
                positionMilliseconds: 10_000,
                changedAt: new Date('2026-11-19T11:00:00Z'),
            })
            vi.mocked(getPlaybackState).mockResolvedValue(phone)

            await resume.checkOtherDevices()
            await flushPromises()
            expect(player.current?.track.id).toBe('c')
            expect(player.position).toBe(10)
            expect(resume.notice).toEqual({ device: 'Safari · iPhone' })

            resume.dismiss()
            await resume.checkOtherDevices()
            await flushPromises()
            expect(resume.notice).toBeNull()
            expect(engine.loaded).toHaveLength(2)
        })

        it('ignores its own newer state', async () => {
            const { player, resume } = await start(saved())
            vi.mocked(getPlaybackState).mockResolvedValue(
                saved({ currentIndex: 0, changedAt: new Date('2026-11-19T11:00:00Z') }),
            )

            await resume.checkOtherDevices()
            await flushPromises()

            expect(player.current?.track.id).toBe('b')
        })
    })

    describe('a restored song that cannot be loaded', () => {
        const videoOnly: Track = {
            id: 'v',
            title: 'V',
            hasAudio: false,
            hasVideo: true,
            durationMilliseconds: 200_000,
        }

        /** The state of another device with a song that has only a video. */
        function otherDevice(): PlaybackState {
            return saved({
                items: [track('a'), videoOnly, track('b'), track('c')].map((value) => ({
                    track: value,
                    autoplay: false,
                })),
                currentIndex: 0,
                positionMilliseconds: 42_000,
                device: { id: 'phone', name: 'Safari · iPhone' },
            })
        }

        function failure(status?: number): Error {
            return status === undefined
                ? new TypeError('Failed to fetch')
                : Object.assign(new Error('failed'), { responseStatusCode: status })
        }

        /** Answers the request of the first song with the given answer, all others as usual. */
        function answerFirst(answer: () => Promise<TrackPlayback>): void {
            vi.mocked(getPlayback).mockImplementation((id) =>
                id === 'a' ? answer() : Promise.resolve(direct(id)),
            )
        }

        it.each([
            ['is canceled without a status', () => Promise.reject(failure())],
            ['fails with a server error', () => Promise.reject(failure(500))],
            ['fails while the server is unavailable', () => Promise.reject(failure(503))],
            ['finds no song', () => Promise.reject(failure(404))],
            [
                'has no stream the browser plays',
                () =>
                    Promise.resolve<TrackPlayback>({
                        trackId: 'a',
                        method: 'Transcode',
                        transcodeReason: 'format_unsupported',
                    }),
            ],
        ])(
            'keeps the saved state when the request %s',
            async (_, answer: () => Promise<TrackPlayback>) => {
                answerFirst(answer)
                const { player, resume } = await start(otherDevice())

                // The delayed save and the saves when the page is left or hidden.
                timers.runTimeouts()
                resume.saveNow()
                await flushPromises()

                expect(savePlaybackState).not.toHaveBeenCalled()
                expect(player.state).toMatchObject({
                    trackId: 'a',
                    playback: 'paused',
                    loading: false,
                    position: 42,
                    message: null,
                })
                expect(player.queue.index).toBe(0)
                expect(resume.notice).toEqual({ device: 'Safari · iPhone' })
            },
        )

        it('keeps the saved state when the restored song fails to play again', async () => {
            const { player, resume } = await start(otherDevice())

            engine.events.error?.(new Error('expired'))
            await flushPromises()
            engine.events.error?.(new Error('broken'))
            await flushPromises()
            timers.runTimeouts()
            resume.saveNow()
            await flushPromises()

            expect(savePlaybackState).not.toHaveBeenCalled()
            expect(engine.stop).toHaveBeenCalled()
            expect(player.state).toMatchObject({
                trackId: 'a',
                playback: 'paused',
                loading: false,
                position: 42,
                message: null,
            })
            expect(resume.notice).toEqual({ device: 'Safari · iPhone' })
        })

        it('skips the song with a notice when it still fails on continue', async () => {
            answerFirst(() => Promise.reject(failure()))
            const { player, resume } = await start(otherDevice())

            resume.resume()
            await flushPromises()

            expect(player.state).toMatchObject({
                trackId: 'b',
                playback: 'playing',
                message: 'failed',
            })
            // Playing makes this device the one the profile listens on; the next song saves again.
            expect(
                savedBodies().map((body) => [
                    body.currentIndex,
                    body.positionMilliseconds,
                    body.device?.id,
                ]),
            ).toEqual([
                [0, 42000, 'this-device'],
                [1, 0, 'this-device'],
            ])
            expect(savedBodies()[1]?.items).toEqual([
                { trackId: 'a' },
                { trackId: 'b' },
                { trackId: 'c' },
            ])
        })

        it('requests the song again on play', async () => {
            vi.mocked(getPlayback).mockRejectedValueOnce(failure(503))
            const { player } = await start(otherDevice())

            player.play()
            await flushPromises()

            expect(engine.loaded).toEqual([{ url: '/stream/a', position: 42 }])
            expect(player.state).toMatchObject({
                trackId: 'a',
                playback: 'playing',
                message: null,
            })
            expect(savedBodies()).toHaveLength(1)
            expect(savedBodies()[0]).toMatchObject({
                currentIndex: 0,
                positionMilliseconds: 42000,
                device: { id: 'this-device', name: 'Chrome · Linux' },
            })
        })
    })

    describe('clearing', () => {
        it('empties the player and clears the saved state without saving it again', async () => {
            const { player, resume } = await start(
                saved({ device: { id: 'phone', name: 'iPhone' } }),
            )
            expect(resume.notice).not.toBeNull()
            player.play()
            await flushPromises()
            vi.mocked(savePlaybackState).mockClear()

            await resume.clear()
            timers.runTimeouts()
            timers.tick()
            await flushPromises()

            expect(clearPlaybackState).toHaveBeenCalledOnce()
            expect(player.queue.items).toEqual([])
            expect(player.playback).toBe('idle')
            expect(resume.notice).toBeNull()
            expect(timers.intervals.size).toBe(0)
            expect(savePlaybackState).not.toHaveBeenCalled()
        })

        it('waits for a save on its way before it clears the state', async () => {
            const { player, resume } = await start(notFound())
            let finishSave: () => void = () => undefined
            vi.mocked(savePlaybackState).mockImplementation(
                () => new Promise<void>((resolve) => (finishSave = resolve)),
            )
            player.playTracks(abc, 0)
            await flushPromises()
            expect(savePlaybackState).toHaveBeenCalledOnce()

            const cleared = resume.clear()
            await flushPromises()
            expect(clearPlaybackState).not.toHaveBeenCalled()

            finishSave()
            await cleared
            expect(clearPlaybackState).toHaveBeenCalledOnce()
        })

        it('saves the next list again after clearing', async () => {
            const { player, resume } = await start(saved())
            await resume.clear()
            vi.mocked(savePlaybackState).mockClear()

            player.playTracks(abc, 2)
            await flushPromises()

            expect(savedBodies().map((body) => body.currentIndex)).toEqual([2])
        })

        it('passes on a failure of the server', async () => {
            const { resume } = await start(notFound())
            vi.mocked(clearPlaybackState).mockRejectedValue(new Error('offline'))

            await expect(resume.clear()).rejects.toThrow('offline')
        })
    })
})
