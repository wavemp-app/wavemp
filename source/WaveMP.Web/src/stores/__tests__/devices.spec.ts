import { flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import {
    device,
    deviceQueue,
    FakeHubConnection,
    FakeTimers,
    live,
    received,
    track,
} from '@/devices/__tests__/fakes'
import type { DeviceSessionSnapshot, LivePlayback } from '@/devices/protocol'
import type { PlayerLiveSnapshot } from '@/devices/publication'
import { playTracks } from '@/player/queue'
import { devicesDependencies, useDevicesStore, type DevicePlayer } from '../devices'
import { useSessionStore } from '../session'

/** Songs of the hub have UUIDs, which the decoder of the generated client checks. */
const id = (letter: string): string => `00000000-0000-4000-8000-0000000000${letter}${letter}`
const [idA, idB, idC] = ['a', 'b', 'c'].map(id) as [string, string, string]

/** A player for the store: it plays or not, and records what the store asks of it. */
function fakePlayer(state: Partial<PlayerLiveSnapshot> = {}) {
    const snapshot: PlayerLiveSnapshot = {
        queue: playTracks([track('a'), track('b')], 0, false),
        repeat: 'off',
        position: 5,
        duration: 200,
        playback: 'paused',
        loading: false,
        volume: 0.8,
        muted: false,
        crossfade: false,
        autoplay: false,
        source: null,
        ...state,
    }
    return {
        snapshot: vi.fn((): PlayerLiveSnapshot => snapshot),
        playing: vi.fn((): boolean => snapshot.playback === 'playing'),
        execute: vi.fn(),
        handover: vi.fn(() => Promise.resolve()),
        yieldPlayback: vi.fn(),
        unlockAudio: vi.fn(),
        publishNow: vi.fn(),
    } satisfies DevicePlayer
}

function snapshot(extra: Partial<DeviceSessionSnapshot> = {}): DeviceSessionSnapshot {
    return {
        deviceId: 'me',
        devices: [device('me'), device('tv', { remoteControl: true })],
        playback: live(),
        ...extra,
    }
}

describe('devices store', () => {
    const original = { ...devicesDependencies }
    let timers: FakeTimers
    let connections: FakeHubConnection[]
    let now: number
    let registerAnswer: DeviceSessionSnapshot

    /** The hub of the current connection. */
    function hub(): FakeHubConnection {
        return connections.at(-1)!
    }

    beforeEach(() => {
        setActivePinia(createPinia())
        window.localStorage.clear()
        timers = new FakeTimers()
        connections = []
        now = 1_000_000
        registerAnswer = snapshot()
        devicesDependencies.deviceId = () => 'me'
        devicesDependencies.deviceName = () => 'Firefox · Windows'
        devicesDependencies.hints = () => ({ userAgent: 'Windows', maxTouchPoints: 0 })
        devicesDependencies.ticket = () => Promise.resolve('ticket')
        devicesDependencies.listen = () => () => undefined
        devicesDependencies.now = () => now
        devicesDependencies.setTimeout = timers.setTimeout
        devicesDependencies.clearTimeout = timers.clearTimeout
        devicesDependencies.setInterval = timers.setInterval
        devicesDependencies.clearInterval = timers.clearInterval
        devicesDependencies.createConnection = (accessToken) => {
            const created = new FakeHubConnection(accessToken)
            created.answers.set('Register', () => ({
                status: 'Registered',
                errors: [],
                snapshot: registerAnswer,
            }))
            created.answers.set('PublishPlayback', () => ({
                status: 'Accepted',
                version: 200,
                queueVersion: 200,
                errors: [],
            }))
            created.answers.set('SendCommand', () => ({
                status: 'Accepted',
                version: 100,
                queueVersion: 90,
                errors: [],
            }))
            created.answers.set('TransferPlayback', () => ({
                status: 'Accepted',
                version: 101,
                queueVersion: 90,
                errors: [],
            }))
            created.answers.set('SetRemoteControl', () => ({
                status: 'Accepted',
                version: 100,
                queueVersion: 90,
                errors: [],
            }))
            created.answers.set('GetQueue', () => ({
                status: 'Accepted',
                queue: deviceQueue([track(idA), track(idB), track(idC)], { currentIndex: 1 }),
            }))
            connections.push(created)
            return created
        }
    })

    afterEach(() => {
        Object.assign(devicesDependencies, original)
    })

    /** Starts the store with a session and waits until it registered. */
    async function connected(player = fakePlayer()) {
        const devices = useDevicesStore()
        devices.attachPlayer(player)
        useSessionStore().token = 'token'
        devices.start()
        await flushPromises()
        return { devices, player }
    }

    describe('connection and registration', () => {
        it('connects only after start and with a profile', async () => {
            const devices = useDevicesStore()
            await flushPromises()
            expect(connections).toHaveLength(0)

            devices.start()
            await flushPromises()
            expect(connections).toHaveLength(0)

            useSessionStore().token = 'token'
            await flushPromises()
            expect(connections).toHaveLength(1)
            expect(devices.status).toBe('connected')
        })

        it('registers with id, name, kind and the permission of the page', async () => {
            const { devices } = await connected()

            expect(hub().callsOf('Register')).toEqual([
                {
                    deviceId: 'me',
                    name: 'Firefox · Windows',
                    type: 'Desktop',
                    remoteControl: false,
                },
            ])
            expect(devices.registered).toBe(true)
            expect(devices.devices.map((entry) => entry.id)).toEqual(['me', 'tv'])
            expect(devices.live).toEqual(registerAnswer.playback)
        })

        it('registers again after a reconnect and when the page comes back, with the permission', async () => {
            const { devices } = await connected()
            devices.allowRemoteControl(true)
            hub().reconnecting()
            expect(devices.registered).toBe(true)
            hub().reconnected()
            await flushPromises()

            expect(hub().callsOf('Register').at(-1)).toMatchObject({ remoteControl: true })
        })

        it('stops taking part when the connection is lost or replaced', async () => {
            const { devices } = await connected()
            hub().emit('DeviceReplaced', {})
            expect(devices.registered).toBe(false)
            expect(devices.replaced).toBe(true)

            hub().reconnected()
            await flushPromises()
            expect(devices.registered).toBe(true)
            expect(devices.replaced).toBe(false)

            hub().close()
            expect(devices.status).toBe('offline')
            expect(devices.registered).toBe(false)
        })

        it('starts anew without permission for another profile', async () => {
            const { devices } = await connected()
            devices.allowRemoteControl(true)

            useSessionStore().token = 'other'
            await flushPromises()

            expect(connections).toHaveLength(2)
            expect(connections[0]!.stops).toBe(1)
            expect(devices.remoteControl).toBe(false)
            expect(hub().callsOf('Register')).toEqual([
                expect.objectContaining({ remoteControl: false }),
            ])

            useSessionStore().token = null
            await flushPromises()
            expect(hub().stops).toBe(1)
            expect(devices.status).toBe('offline')
        })

        it('publishes at once when it registers as the active device', async () => {
            registerAnswer = snapshot({ playback: live({ activeDeviceId: 'me' }) })
            const { player } = await connected()

            expect(player.publishNow).toHaveBeenCalledOnce()
        })

        it('claims playback when it plays and no device is active', async () => {
            await connected(fakePlayer({ playback: 'playing' }))

            expect(hub().callsOf('PublishPlayback')).toEqual([
                expect.objectContaining({ claim: true }),
            ])
        })
    })

    describe('remote control permission', () => {
        it('lasts for the page only: in memory, never stored, off in a new store', async () => {
            const { devices, player } = await connected()
            const stored = { ...window.localStorage }

            devices.allowRemoteControl(true)
            await flushPromises()

            expect(devices.remoteControl).toBe(true)
            expect(player.unlockAudio).toHaveBeenCalledOnce()
            expect(hub().callsOf('SetRemoteControl')).toEqual([{ allowed: true }])
            expect({ ...window.localStorage }).toEqual(stored)
            expect({ ...window.sessionStorage }).toEqual({})

            setActivePinia(createPinia())
            expect(useDevicesStore().remoteControl).toBe(false)
        })

        it('forbids it again without unlocking audio', async () => {
            const { devices, player } = await connected()
            devices.allowRemoteControl(true)
            devices.allowRemoteControl(false)

            expect(player.unlockAudio).toHaveBeenCalledOnce()
            expect(hub().callsOf('SetRemoteControl')).toEqual([
                { allowed: true },
                { allowed: false },
            ])
        })
    })

    describe('live playback', () => {
        const tvPlaying = (extra: Partial<LivePlayback> = {}) =>
            live({
                activeDeviceId: 'tv',
                status: 'Playing',
                currentIndex: 1,
                currentTrackId: idB,
                queueLength: 3,
                positionMilliseconds: 10_000,
                durationMilliseconds: 200_000,
                ...extra,
            })

        it('ignores states older than the known one', async () => {
            registerAnswer = snapshot({ playback: tvPlaying({ version: 100 }) })
            const { devices } = await connected()

            hub().emit('PlaybackChanged', tvPlaying({ version: 99, volume: 0.1 }))
            expect(devices.live?.volume).toBe(1)

            hub().emit('PlaybackChanged', tvPlaying({ version: 101, volume: 0.2 }))
            expect(devices.live?.volume).toBe(0.2)
        })

        it('is a remote of an active device that allows remote control and mirrors its queue', async () => {
            registerAnswer = snapshot({ playback: tvPlaying() })
            const { devices } = await connected()

            expect(devices.role).toBe('remote')
            expect(hub().callsOf('GetQueue')).toHaveLength(1)
            expect(devices.mirror.items.map((item) => item.track.id)).toEqual([idA, idB, idC])
            expect(devices.mirror.index).toBe(1)
            expect(devices.transport).toMatchObject({
                playback: 'playing',
                title: idB.toUpperCase(),
                position: 10,
            })

            // The position moves while the device plays.
            now += 1500
            timers.tickIntervals()
            expect(devices.transport?.position).toBe(11.5)

            // A new current entry of the same queue needs no new queue.
            hub().emit(
                'PlaybackChanged',
                tvPlaying({ version: 101, currentIndex: 2, currentTrackId: idC }),
            )
            await flushPromises()
            expect(hub().callsOf('GetQueue')).toHaveLength(1)
            expect(devices.mirror.index).toBe(2)
            expect(devices.transport?.title).toBe(idC.toUpperCase())

            // A new queue version reads the queue again.
            hub().emit('PlaybackChanged', tvPlaying({ version: 102, queueVersion: 102 }))
            await flushPromises()
            expect(hub().callsOf('GetQueue')).toHaveLength(2)
        })

        it('is no remote of a device that does not allow remote control', async () => {
            registerAnswer = snapshot({
                devices: [device('me'), device('tv', { remoteControl: false })],
                playback: tvPlaying(),
            })
            const { devices } = await connected()

            expect(devices.role).toBe('local')
            expect(devices.transport).toBeNull()
        })

        it('pauses when another device takes playback over, only with permission', async () => {
            registerAnswer = snapshot({
                playback: live({ activeDeviceId: 'me', status: 'Playing' }),
            })
            const { devices, player } = await connected()

            hub().emit('PlaybackChanged', tvPlaying({ version: 101 }))
            expect(player.yieldPlayback).not.toHaveBeenCalled()

            devices.allowRemoteControl(true)
            hub().emit('PlaybackChanged', live({ version: 102, activeDeviceId: 'me' }))
            hub().emit('PlaybackChanged', tvPlaying({ version: 103 }))
            expect(player.yieldPlayback).toHaveBeenCalledOnce()
        })

        it('loads and plays a hand-over to this device, and stops when playback leaves it', async () => {
            registerAnswer = snapshot({ playback: tvPlaying() })
            const { devices, player } = await connected()
            let finish: () => void = () => undefined
            player.handover.mockImplementation(
                () => new Promise<void>((resolve) => (finish = resolve)),
            )

            hub().emit(
                'PlaybackChanged',
                tvPlaying({
                    version: 101,
                    activeDeviceId: 'me',
                    status: 'Paused',
                    handover: { fromDeviceId: 'tv', play: true },
                }),
            )
            expect(player.handover).toHaveBeenCalledWith(true)
            expect(devices.handingOver).toBe(true)

            // Nothing is published until the state was loaded.
            await devices.publish()
            expect(hub().callsOf('PublishPlayback')).toHaveLength(0)
            finish()
            await flushPromises()
            expect(devices.handingOver).toBe(false)
            expect(player.publishNow).toHaveBeenCalled()

            hub().emit(
                'PlaybackChanged',
                tvPlaying({ version: 102, handover: { fromDeviceId: 'me', play: false } }),
            )
            expect(player.yieldPlayback).toHaveBeenCalledOnce()
        })

        it('runs commands only as the active device, with the window of the queue', async () => {
            registerAnswer = snapshot({ playback: live({ activeDeviceId: 'me' }) })
            const { player } = await connected()

            hub().emit('CommandReceived', received({ type: 'Pause' }))
            expect(player.execute).toHaveBeenCalledWith(
                expect.objectContaining({ type: 'Pause' }),
                0,
            )

            hub().emit('PlaybackChanged', live({ version: 101, activeDeviceId: 'tv' }))
            hub().emit('CommandReceived', received({ type: 'Play' }))
            expect(player.execute).toHaveBeenCalledOnce()
        })

        it('takes the state of the active device over when it stops being a remote', async () => {
            registerAnswer = snapshot({ playback: tvPlaying() })
            const { devices } = await connected()
            await flushPromises()
            now += 2000

            const state = devices.mirrorState()
            expect(state?.queue.items.map((item) => item.track.id)).toEqual([idA, idB, idC])
            expect(state?.position).toBe(12)

            const handler = vi.fn(() => true)
            devices.onResumeFromRemote(handler)
            expect(devices.resumeFromRemote(state!, 'tv')).toBe(true)
            expect(handler).toHaveBeenCalledWith(state, 'tv')
        })
    })

    describe('publishing', () => {
        it('publishes only as the active device, and the queue only when it changed', async () => {
            const player = fakePlayer()
            const { devices } = await connected(player)
            await devices.publish()
            expect(hub().callsOf('PublishPlayback')).toHaveLength(0)

            hub().emit('PlaybackChanged', live({ version: 101, activeDeviceId: 'me' }))
            await devices.publish()
            await devices.publish()
            const calls = hub().callsOf('PublishPlayback') as {
                claim: boolean
                playback: { queue: unknown }
            }[]

            expect(calls).toHaveLength(2)
            expect(calls[0]?.claim).toBe(false)
            expect(calls[0]?.playback.queue).not.toBeNull()
            expect(calls[1]?.playback.queue).toBeNull()
            expect(devices.live).toMatchObject({
                version: 200,
                activeDeviceId: 'me',
                currentTrackId: 'a',
            })
        })

        it('claims playback and becomes the active device', async () => {
            registerAnswer = snapshot({
                devices: [device('me'), device('tv', { remoteControl: false })],
                playback: live({ activeDeviceId: 'tv', status: 'Playing' }),
            })
            const { devices } = await connected()
            await devices.publish(true)

            expect(hub().callsOf('PublishPlayback')).toEqual([
                expect.objectContaining({ claim: true }),
            ])
            expect(devices.isActive).toBe(true)
        })

        it('names the queue again after a publication was refused', async () => {
            registerAnswer = snapshot({ playback: live({ activeDeviceId: 'me' }) })
            const { devices } = await connected()
            hub().answers.set('PublishPlayback', () => ({
                status: 'NotActive',
                version: 1,
                queueVersion: 1,
                errors: [],
            }))
            await devices.publish()
            await devices.publish()
            const calls = hub().callsOf('PublishPlayback') as { playback: { queue: unknown } }[]

            expect(calls.every((call) => call.playback.queue !== null)).toBe(true)
        })
    })

    describe('commands and hand-over', () => {
        async function remote() {
            registerAnswer = snapshot({
                playback: live({
                    activeDeviceId: 'tv',
                    status: 'Playing',
                    currentIndex: 1,
                    currentTrackId: idB,
                    positionMilliseconds: 1000,
                }),
            })
            const result = await connected()
            await flushPromises()
            return result
        }

        it('sends the actions of a remote to the active device and shows simple ones at once', async () => {
            const { devices } = await remote()
            await devices.sendAction({ kind: 'setVolume', volume: 0.3 })
            await devices.sendAction({ kind: 'playItem', key: devices.mirror.items[2]!.key })

            expect(hub().callsOf('SendCommand')).toEqual([
                { targetDeviceId: 'tv', type: 'SetVolume', volume: 0.3, muted: false },
                {
                    targetDeviceId: 'tv',
                    type: 'PlayQueueEntry',
                    index: 2,
                    expectedQueueVersion: 90,
                },
            ])
            expect(devices.live?.volume).toBe(0.3)
        })

        it('reads the queue again after a conflict and reports refusals', async () => {
            const { devices } = await remote()
            hub().answers.set('SendCommand', () => ({
                status: 'Conflict',
                version: 100,
                queueVersion: 95,
                errors: [],
            }))
            const status = await devices.sendAction({ kind: 'clearQueue' })
            await flushPromises()

            expect(status).toBe('Conflict')
            expect(devices.lastStatus).toBe('Conflict')
            expect(hub().callsOf('GetQueue')).toHaveLength(2)
            // No retry of its own.
            expect(hub().callsOf('SendCommand')).toHaveLength(1)

            devices.dismissStatus()
            expect(devices.lastStatus).toBeNull()
        })

        it('sends nothing while it is not a remote', async () => {
            const { devices } = await connected()
            expect(await devices.sendAction({ kind: 'play' })).toBeNull()
            expect(hub().callsOf('SendCommand')).toHaveLength(0)
        })

        it('hands playback over with the version it saw and plays if the device plays', async () => {
            const { devices } = await remote()
            await devices.transfer('me')

            expect(hub().callsOf('TransferPlayback')).toEqual([
                { targetDeviceId: 'me', play: true, expectedVersion: 100 },
            ])
        })

        it('publishes the latest position before it hands its own playback over', async () => {
            registerAnswer = snapshot({ playback: live({ activeDeviceId: 'me' }) })
            const { devices } = await connected(fakePlayer({ playback: 'playing' }))
            hub().invocations.length = 0
            await devices.transfer('tv')

            expect(hub().invocations.map((call) => call.method)).toEqual([
                'PublishPlayback',
                'TransferPlayback',
            ])
            expect(hub().callsOf('TransferPlayback')).toEqual([
                { targetDeviceId: 'tv', play: true, expectedVersion: 200 },
            ])
        })
    })
})
