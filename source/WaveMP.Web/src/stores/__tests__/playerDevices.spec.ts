import { flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { PlaybackState, TrackPlayback } from '@/api/generated/models'
import { getHandoverState } from '@/devices/api'
import {
    device,
    deviceQueue,
    FakeHubConnection,
    FakeTimers,
    live,
    received,
    track,
} from '@/devices/__tests__/fakes'
import type { DeviceCommand, DeviceSessionSnapshot, LivePlayback } from '@/devices/protocol'
import type { EngineEvents } from '@/player/engine'
import { getLyrics } from '@/lyrics/api'
import {
    getPlayback,
    getPlaybackState,
    recordPlay,
    reportNowPlaying,
    savePlaybackState,
} from '@/player/api'
import { devicesDependencies, useDevicesStore } from '../devices'
import { playerDependencies, usePlayerStore, type PlayerEngine } from '../player'
import { resumeDependencies, useResumeStore } from '../resume'
import { useSessionStore } from '../session'

vi.mock('@/lyrics/api', () => ({
    getLyrics: vi.fn(),
}))

vi.mock('@/player/api', () => ({
    getPlayback: vi.fn(),
    getVideoPlayback: vi.fn(),
    recordPlay: vi.fn(),
    reportNowPlaying: vi.fn(),
    getAutoplayTracks: vi.fn(() => Promise.resolve(undefined)),
    getPlaybackState: vi.fn(),
    savePlaybackState: vi.fn(),
    clearPlaybackState: vi.fn(),
}))

vi.mock('@/devices/api', () => ({
    createHubTicket: vi.fn(() => Promise.resolve('ticket')),
    getHandoverState: vi.fn(),
}))

/** The audio engine of the browser, with the unlock of a gesture. */
class FakeEngine implements PlayerEngine {
    position = 0
    hasPrepared = false
    readonly load = vi.fn()
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
    readonly unlock = vi.fn()
    equalizerAvailable = true
    analyser: AnalyserNode | null = null

    constructor(readonly events: EngineEvents) {}
}

/** Songs of the hub have UUIDs, which the decoder of the generated client checks. */
const id = (letter: string): string => `00000000-0000-4000-8000-0000000000${letter}${letter}`
const [idA, idB, idC] = ['a', 'b', 'c'].map(id) as [string, string, string]
const abc = [track(idA), track(idB), track(idC)]

function direct(trackId: string): TrackPlayback {
    return {
        trackId,
        method: 'DirectPlay',
        format: 'Mp3',
        contentType: 'audio/mpeg',
        streamUrl: `/stream/${trackId}`,
        durationMilliseconds: 200_000,
    }
}

/** The television plays and allows remote control. */
function tvPlaying(extra: Partial<LivePlayback> = {}): LivePlayback {
    return live({
        activeDeviceId: 'tv',
        status: 'Playing',
        currentIndex: 1,
        currentTrackId: idB,
        queueLength: 3,
        positionMilliseconds: 30_000,
        durationMilliseconds: 200_000,
        volume: 0.7,
        ...extra,
    })
}

describe('player with devices', () => {
    const originalPlayer = { ...playerDependencies }
    const originalDevices = { ...devicesDependencies }
    const originalResume = { ...resumeDependencies }
    let engine: FakeEngine | undefined
    let timers: FakeTimers
    let hub: FakeHubConnection
    let now: number
    let registerAnswer: DeviceSessionSnapshot

    beforeEach(() => {
        setActivePinia(createPinia())
        window.localStorage.clear()
        engine = undefined
        timers = new FakeTimers()
        now = 1_000_000
        registerAnswer = {
            deviceId: 'me',
            devices: [device('me'), device('tv', { remoteControl: true })],
            playback: live(),
        }
        vi.mocked(getPlayback)
            .mockReset()
            .mockImplementation((trackId) => Promise.resolve(direct(trackId)))
        vi.mocked(recordPlay).mockReset().mockResolvedValue(undefined)
        vi.mocked(reportNowPlaying).mockReset().mockResolvedValue(undefined)
        vi.mocked(getLyrics).mockReset().mockResolvedValue(null)
        vi.mocked(getPlaybackState).mockReset().mockRejectedValue(new Error('none'))
        vi.mocked(savePlaybackState).mockReset().mockResolvedValue(undefined)
        vi.mocked(getHandoverState).mockReset()
        playerDependencies.createEngine = (events) => (engine = new FakeEngine(events))
        playerDependencies.audioFormats = () => ['Mp3']
        playerDependencies.random = () => 0
        playerDependencies.mediaSession = false
        resumeDependencies.deviceId = () => 'me'
        resumeDependencies.deviceName = () => 'Firefox · Windows'
        resumeDependencies.listen = false
        resumeDependencies.setTimeout = timers.setTimeout
        resumeDependencies.clearTimeout = timers.clearTimeout
        resumeDependencies.setInterval = timers.setInterval
        resumeDependencies.clearInterval = timers.clearInterval
        devicesDependencies.deviceId = () => 'me'
        devicesDependencies.deviceName = () => 'Firefox · Windows'
        devicesDependencies.ticket = () => Promise.resolve('ticket')
        devicesDependencies.listen = () => () => undefined
        devicesDependencies.now = () => now
        devicesDependencies.setTimeout = timers.setTimeout
        devicesDependencies.clearTimeout = timers.clearTimeout
        devicesDependencies.setInterval = timers.setInterval
        devicesDependencies.clearInterval = timers.clearInterval
        devicesDependencies.createConnection = (accessToken) => {
            hub = new FakeHubConnection(accessToken)
            hub.answers.set('Register', () => ({
                status: 'Registered',
                errors: [],
                snapshot: registerAnswer,
            }))
            hub.answers.set('PublishPlayback', () => ({
                status: 'Accepted',
                version: 200,
                queueVersion: 200,
                errors: [],
            }))
            hub.answers.set('SendCommand', () => ({
                status: 'Accepted',
                version: 100,
                queueVersion: 90,
                errors: [],
            }))
            hub.answers.set('SetRemoteControl', () => ({
                status: 'Accepted',
                version: 100,
                queueVersion: 90,
                errors: [],
            }))
            hub.answers.set('GetQueue', () => ({
                status: 'Accepted',
                queue: deviceQueue(abc, { currentIndex: 1, source: { kind: 'Album', id: idC } }),
            }))
            return hub
        }
    })

    afterEach(() => {
        Object.assign(playerDependencies, originalPlayer)
        Object.assign(devicesDependencies, originalDevices)
        Object.assign(resumeDependencies, originalResume)
    })

    async function start() {
        const session = useSessionStore()
        session.token = 'token'
        session.autoplay = { enabled: false, includeVideoOnly: false }
        const player = usePlayerStore()
        const resume = useResumeStore()
        const devices = useDevicesStore()
        devices.start()
        await flushPromises()
        return { session, player, resume, devices }
    }

    function commands(): DeviceCommand[] {
        return hub.callsOf('SendCommand') as DeviceCommand[]
    }

    function publications(): { claim: boolean; playback: { status: string; queue: unknown } }[] {
        return hub.callsOf('PublishPlayback') as never
    }

    describe('as a remote', () => {
        async function remote() {
            registerAnswer = { ...registerAnswer, playback: tvPlaying() }
            const started = await start()
            await flushPromises()
            return started
        }

        it('shows the live playback and queue of the active device; the local engine rests', async () => {
            const { player } = await remote()

            expect(player.remote).toBe(true)
            expect(player.queue.items.map((item) => item.track.id)).toEqual([idA, idB, idC])
            expect(player.current?.track.id).toBe(idB)
            expect(player.state).toMatchObject({
                playback: 'playing',
                title: idB.toUpperCase(),
                position: 30,
                volume: 0.7,
                effects: false,
                message: null,
            })
            expect(player.equalizerAvailable).toBe(false)
            expect(player.stageView).toBe('cover')
            expect(getPlayback).not.toHaveBeenCalled()

            now += 2000
            expect(player.livePosition()).toBe(32)
        })

        it('stops its own playback when it becomes a remote', async () => {
            const { player, devices } = await start()
            player.playTracks([track('x'), track('y')], 0)
            await flushPromises()
            expect(player.playback).toBe('playing')
            player.pause()
            await flushPromises()
            const stops = engine!.stop.mock.calls.length

            // Newer than the version of its own publications.
            hub.emit('PlaybackChanged', tvPlaying({ version: 250 }))
            await flushPromises()

            expect(devices.role).toBe('remote')
            expect(engine!.stop.mock.calls.length).toBeGreaterThan(stops)
            expect(player.queue.items.map((item) => item.track.id)).toEqual([idA, idB, idC])
            expect(player.playback).toBe('idle')
        })

        it('sends every command to the active device instead of playing', async () => {
            const { player } = await remote()
            const key = (at: number) => player.queue.items[at]!.key

            player.play()
            player.pause()
            player.togglePlay()
            player.stop()
            player.next()
            player.previous()
            player.seek(12)
            player.setVolume(0.2)
            player.toggleMute()
            player.toggleShuffle()
            player.cycleRepeat()
            player.toggleCrossfade()
            player.toggleAutoplay()
            player.playTracks([track(idA)], 0, { source: { kind: 'album', id: idC } })
            player.playNext([track(idB)])
            player.addToQueue([track(idC)])
            player.playItem(key(2))
            player.removeItem(key(2))
            player.moveItem(key(2), -1)
            player.moveItemNext(key(0))
            player.clearQueue()
            await flushPromises()

            expect(commands().map((command) => command.type)).toEqual([
                'Play',
                'Pause',
                'Pause',
                'Stop',
                'Next',
                'Previous',
                'Seek',
                'SetVolume',
                'SetVolume',
                'SetShuffle',
                'SetRepeat',
                'SetCrossfade',
                'SetAutoplay',
                'PlayTracks',
                'PlayNext',
                'AddToQueue',
                'PlayQueueEntry',
                'RemoveQueueEntry',
                'MoveQueueEntry',
                'MoveQueueEntry',
                'ClearQueue',
            ])
            expect(commands().every((command) => command.targetDeviceId === 'tv')).toBe(true)
            expect(engine?.play.mock.calls.length ?? 0).toBe(0)
            expect(getPlayback).not.toHaveBeenCalled()
            expect(player.queue.items).toHaveLength(3)
        })

        it('keeps the sleep timer to the device that plays', async () => {
            const { player } = await remote()
            player.startSleepTimer('duration', 10)

            expect(player.sleepTimer.mode).toBe('off')
        })

        it('saves no playback state and takes none over', async () => {
            const { player, resume } = await remote()
            vi.mocked(getPlaybackState)
                .mockReset()
                .mockResolvedValue({
                    items: [{ track: track(idC), autoplay: false }],
                    currentIndex: 0,
                    positionMilliseconds: 0,
                    shuffle: false,
                    originalOrder: [],
                    repeat: 'Off',
                    device: { id: 'laptop', name: 'Laptop' },
                    changedAt: new Date(),
                })
            player.seek(5)
            timers.runTimeouts()
            resume.saveNow()
            await resume.checkOtherDevices()
            await flushPromises()

            expect(savePlaybackState).not.toHaveBeenCalled()
            expect(getPlaybackState).not.toHaveBeenCalled()
            expect(player.queue.items.map((item) => item.track.id)).toEqual([idA, idB, idC])
            expect(resume.notice).toBeNull()
        })

        it('waits paused with the state of the active device when it closes', async () => {
            const { player, resume, devices } = await remote()
            now += 4000

            hub.emit('DevicesChanged', {
                devices: [
                    device('me'),
                    device('tv', { name: 'Living room', remoteControl: false, online: false }),
                ],
            })
            await flushPromises()

            expect(devices.role).toBe('local')
            expect(player.remote).toBe(false)
            expect(player.queue.items.map((item) => item.track.id)).toEqual([idA, idB, idC])
            expect(player.current?.track.id).toBe(idB)
            expect(player.playback).toBe('paused')
            expect(player.position).toBe(34)
            expect(player.source).toEqual({ kind: 'album', id: idC })
            expect(resume.notice).toEqual({ device: 'Living room' })
            expect(engine?.play).not.toHaveBeenCalled()
            expect(savePlaybackState).not.toHaveBeenCalled()

            // Play goes on here.
            resume.resume()
            await flushPromises()
            expect(player.playback).toBe('playing')
        })
    })

    describe('as the active device', () => {
        async function active() {
            registerAnswer = { ...registerAnswer, playback: live({ activeDeviceId: 'me' }) }
            return start()
        }

        it('publishes play, pause and a new song at once and the queue shortly after', async () => {
            const { player } = await active()
            const before = publications().length
            player.playTracks(abc, 0)
            await flushPromises()

            expect(publications().length).toBeGreaterThan(before)
            expect(publications().at(-1)).toMatchObject({
                claim: false,
                playback: { status: 'Playing' },
            })

            const count = publications().length
            player.pause()
            await flushPromises()
            expect(publications()).toHaveLength(count + 1)
            expect(publications().at(-1)?.playback.status).toBe('Paused')

            player.addToQueue([track(idA)])
            await flushPromises()
            expect(publications()).toHaveLength(count + 1)
            timers.runTimeouts()
            await flushPromises()
            expect(publications()).toHaveLength(count + 2)
        })

        it('publishes every 10 seconds while it plays', async () => {
            const { player } = await active()
            player.playTracks(abc, 0)
            await flushPromises()
            timers.runTimeouts()
            await flushPromises()
            const count = publications().length

            timers.tickIntervals()
            await flushPromises()
            expect(publications().length).toBeGreaterThan(count)

            player.pause()
            await flushPromises()
            const paused = publications().length
            timers.tickIntervals()
            await flushPromises()
            expect(publications()).toHaveLength(paused)
        })

        it('runs the commands of remotes through its own player', async () => {
            const { player } = await active()
            player.playTracks(abc, 0)
            await flushPromises()

            hub.emit('CommandReceived', received({ type: 'Pause' }))
            await flushPromises()
            expect(engine!.pause).toHaveBeenCalled()
            expect(player.playback).toBe('paused')

            hub.emit('CommandReceived', received({ type: 'SetVolume', volume: 0.25, muted: true }))
            expect(player.volume).toBe(0.25)
            expect(player.muted).toBe(true)

            hub.emit('CommandReceived', received({ type: 'MoveQueueEntry', index: 2, toIndex: 1 }))
            expect(player.queue.items.map((item) => item.track.id)).toEqual([idA, idC, idB])

            hub.emit('CommandReceived', received({ type: 'SetRepeat', repeat: 'All' }))
            expect(player.repeat).toBe('all')

            hub.emit(
                'CommandReceived',
                received({
                    type: 'PlayTracks',
                    tracks: [{ id: idC, title: 'New', hasAudio: true }],
                    startIndex: 0,
                }),
            )
            await flushPromises()
            expect(player.queue.items.map((item) => item.track.title)).toEqual(['New'])
            expect(player.playback).toBe('playing')
        })

        it('pauses and becomes a remote when another device takes over, only with permission', async () => {
            const { player, devices } = await active()
            player.playTracks(abc, 0)
            await flushPromises()

            hub.emit('PlaybackChanged', tvPlaying({ version: 300 }))
            await flushPromises()
            expect(player.playback).toBe('playing')
            expect(player.remote).toBe(false)

            player.playTracks(abc, 0)
            await flushPromises()
            devices.allowRemoteControl(true)
            hub.emit('PlaybackChanged', live({ version: 301, activeDeviceId: 'me' }))
            hub.emit('PlaybackChanged', tvPlaying({ version: 302 }))
            await flushPromises()

            expect(player.remote).toBe(true)
            expect(player.state.title).toBe(idB.toUpperCase())
        })
    })

    describe('taking playback over', () => {
        it('claims playback when its user starts it while another device is active', async () => {
            registerAnswer = {
                ...registerAnswer,
                devices: [device('me'), device('tv', { remoteControl: false })],
                playback: tvPlaying(),
            }
            const { player, devices } = await start()
            expect(player.remote).toBe(false)

            player.playTracks(abc, 0)
            await flushPromises()

            // The song still loads when the claim goes out; the next publication shows it playing.
            expect(publications()[0]).toMatchObject({
                claim: true,
                playback: { status: 'Loading' },
            })
            expect(
                publications()
                    .slice(1)
                    .every((publication) => !publication.claim),
            ).toBe(true)
            expect(publications().at(-1)?.playback.status).toBe('Playing')
            expect(devices.isActive).toBe(true)
        })

        it('loads the saved state and plays when playback is handed over to it', async () => {
            registerAnswer = { ...registerAnswer, playback: tvPlaying() }
            const saved: PlaybackState = {
                items: abc.map((value) => ({ track: value, autoplay: false })),
                currentIndex: 2,
                positionMilliseconds: 42_000,
                shuffle: false,
                originalOrder: [],
                repeat: 'Off',
                device: { id: 'me', name: 'Firefox · Windows' },
                changedAt: new Date(),
            }
            vi.mocked(getHandoverState).mockResolvedValue(saved)
            const { player, resume } = await start()
            expect(player.remote).toBe(true)

            hub.emit(
                'PlaybackChanged',
                tvPlaying({
                    version: 101,
                    activeDeviceId: 'me',
                    status: 'Paused',
                    handover: { fromDeviceId: 'tv', play: true },
                }),
            )
            await flushPromises()

            expect(player.remote).toBe(false)
            expect(player.current?.track.id).toBe(idC)
            expect(engine!.load).toHaveBeenLastCalledWith(expect.anything(), 42)
            expect(player.playback).toBe('playing')
            expect(resume.notice).toBeNull()
            expect(publications().at(-1)).toMatchObject({
                claim: false,
                playback: { status: 'Playing' },
            })
        })

        it('stops when it hands its playback over', async () => {
            registerAnswer = { ...registerAnswer, playback: live({ activeDeviceId: 'me' }) }
            const { player } = await start()
            player.playTracks(abc, 0)
            await flushPromises()

            hub.emit(
                'PlaybackChanged',
                tvPlaying({ version: 300, handover: { fromDeviceId: 'me', play: true } }),
            )
            await flushPromises()

            expect(engine!.pause).toHaveBeenCalled()
            expect(player.remote).toBe(true)
        })
    })

    it('unlocks the audio of the browser when remote control is allowed', async () => {
        const { devices } = await start()
        devices.allowRemoteControl(true)

        expect(engine!.unlock).toHaveBeenCalledOnce()
    })
})
