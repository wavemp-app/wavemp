import { describe, expect, it, vi } from 'vitest'
import { commandFor, runCommand, trackIdsOf, type CommandTarget } from '../commands'
import { mirrorQueue } from '../remote'
import { deviceQueue, live, received, track } from './fakes'

const abcd = [track('a'), track('b'), track('c'), track('d')]

function context(currentIndex = 1, extra: Parameters<typeof live>[0] = {}) {
    return {
        target: 'tv',
        live: live({
            activeDeviceId: 'tv',
            status: 'Playing',
            currentIndex,
            volume: 0.6,
            ...extra,
        }),
        mirror: mirrorQueue(deviceQueue(abcd), currentIndex),
        queueVersion: 90,
    }
}

describe('trackIdsOf', () => {
    it('sends the playable songs and moves the start with them', () => {
        const tracks = [track('a'), track('x', { hasAudio: false }), track('b'), { title: 'no id' }]
        expect(trackIdsOf(tracks, 2)).toEqual({ trackIds: ['a', 'b'], startIndex: 1 })
        expect(trackIdsOf(tracks, 1)).toEqual({ trackIds: ['a', 'b'], startIndex: 0 })
    })

    it('sends at most 2000 songs around the start', () => {
        const many = Array.from({ length: 2100 }, (_, at) => track(`t${at}`))
        const { trackIds, startIndex } = trackIdsOf(many, 2050)

        expect(trackIds).toHaveLength(2000)
        expect(trackIds[startIndex]).toBe('t2050')
    })
})

describe('commandFor', () => {
    it('sends the transport commands to the active device', () => {
        expect(commandFor({ kind: 'play' }, context())).toEqual({
            targetDeviceId: 'tv',
            type: 'Play',
        })
        expect(commandFor({ kind: 'next' }, context())?.type).toBe('Next')
        expect(commandFor({ kind: 'previous' }, context())?.type).toBe('Previous')
        expect(commandFor({ kind: 'stop' }, context())?.type).toBe('Stop')
        expect(commandFor({ kind: 'pause' }, context())?.type).toBe('Pause')
    })

    it('toggles play by the live status', () => {
        expect(commandFor({ kind: 'togglePlay' }, context())?.type).toBe('Pause')
        expect(commandFor({ kind: 'togglePlay' }, context(1, { status: 'Loading' }))?.type).toBe(
            'Pause',
        )
        expect(commandFor({ kind: 'togglePlay' }, context(1, { status: 'Paused' }))?.type).toBe(
            'Play',
        )
    })

    it('seeks in whole milliseconds within 24 hours', () => {
        expect(
            commandFor({ kind: 'seek', seconds: 61.2345 }, context())?.positionMilliseconds,
        ).toBe(61_235)
        expect(commandFor({ kind: 'seek', seconds: -3 }, context())?.positionMilliseconds).toBe(0)
        expect(commandFor({ kind: 'seek', seconds: 1e9 }, context())?.positionMilliseconds).toBe(
            86_400_000,
        )
    })

    it('sets the volume, unmutes when it goes up and toggles mute', () => {
        expect(commandFor({ kind: 'setVolume', volume: 0.3 }, context())).toMatchObject({
            type: 'SetVolume',
            volume: 0.3,
            muted: false,
        })
        expect(commandFor({ kind: 'setVolume', volume: 0 }, context())).toMatchObject({
            volume: 0,
            muted: null,
        })
        expect(commandFor({ kind: 'setVolume', volume: 3 }, context())?.volume).toBe(1)
        expect(commandFor({ kind: 'toggleMute' }, context())).toMatchObject({
            volume: 0.6,
            muted: true,
        })
        expect(commandFor({ kind: 'toggleMute' }, context(1, { muted: true }))?.muted).toBe(false)
    })

    it('switches shuffle, repeat, crossfade and autoplay from the live state', () => {
        expect(commandFor({ kind: 'toggleShuffle' }, context())).toMatchObject({
            type: 'SetShuffle',
            enabled: true,
        })
        expect(commandFor({ kind: 'cycleRepeat' }, context())).toMatchObject({
            type: 'SetRepeat',
            repeat: 'All',
        })
        expect(commandFor({ kind: 'cycleRepeat' }, context(1, { repeat: 'All' }))?.repeat).toBe(
            'One',
        )
        expect(commandFor({ kind: 'cycleRepeat' }, context(1, { repeat: 'One' }))?.repeat).toBe(
            'Off',
        )
        expect(
            commandFor({ kind: 'toggleCrossfade' }, context(1, { crossfade: true }))?.enabled,
        ).toBe(false)
        expect(commandFor({ kind: 'toggleAutoplay' }, context())).toMatchObject({
            type: 'SetAutoplay',
            enabled: true,
        })
    })

    it('plays and queues songs by their ids', () => {
        expect(
            commandFor(
                {
                    kind: 'playTracks',
                    tracks: abcd,
                    start: 2,
                    source: { kind: 'smart-playlist', id: 'sp' },
                },
                context(),
            ),
        ).toEqual({
            targetDeviceId: 'tv',
            type: 'PlayTracks',
            trackIds: ['a', 'b', 'c', 'd'],
            startIndex: 2,
            shuffle: null,
            source: { kind: 'SmartPlaylist', id: 'sp' },
        })
        expect(commandFor({ kind: 'playNext', tracks: [track('x')] }, context())).toMatchObject({
            type: 'PlayNext',
            trackIds: ['x'],
        })
        expect(commandFor({ kind: 'addToQueue', tracks: [track('x')] }, context())?.type).toBe(
            'AddToQueue',
        )
        expect(
            commandFor(
                { kind: 'addToQueue', tracks: [track('x', { hasAudio: false })] },
                context(),
            ),
        ).toBeNull()
        expect(commandFor({ kind: 'playTracks', tracks: [], start: 0 }, context())).toBeNull()
    })

    it('names entries of the queue by position with the queue version the remote saw', () => {
        const ctx = context()
        const key = (at: number) => ctx.mirror.items[at]!.key

        expect(commandFor({ kind: 'playItem', key: key(3) }, ctx)).toEqual({
            targetDeviceId: 'tv',
            type: 'PlayQueueEntry',
            index: 3,
            expectedQueueVersion: 90,
        })
        expect(commandFor({ kind: 'removeItem', key: key(2) }, ctx)).toMatchObject({
            type: 'RemoveQueueEntry',
            index: 2,
        })
        expect(commandFor({ kind: 'moveItem', key: key(2), offset: -1 }, ctx)).toMatchObject({
            type: 'MoveQueueEntry',
            index: 2,
            toIndex: 1,
        })
        expect(commandFor({ kind: 'moveItem', key: key(3), offset: 1 }, ctx)).toBeNull()
        expect(commandFor({ kind: 'clearQueue' }, ctx)).toMatchObject({
            type: 'ClearQueue',
            expectedQueueVersion: 90,
        })
        expect(commandFor({ kind: 'playItem', key: 'unknown' }, ctx)).toBeNull()
    })

    it('moves an entry after the current one like the player does', () => {
        const ctx = context(2)
        const key = (at: number) => ctx.mirror.items[at]!.key

        expect(commandFor({ kind: 'moveItemNext', key: key(0) }, ctx)).toMatchObject({
            index: 0,
            toIndex: 2,
        })
        expect(commandFor({ kind: 'moveItemNext', key: key(3) }, ctx)).toBeNull()
        expect(commandFor({ kind: 'moveItemNext', key: key(2) }, ctx)).toBeNull()
        expect(commandFor({ kind: 'moveItemNext', key: key(1) }, context(0))).toBeNull()
    })
})

function target(): CommandTarget {
    return {
        play: vi.fn(),
        pause: vi.fn(),
        stop: vi.fn(),
        next: vi.fn(),
        previous: vi.fn(),
        seek: vi.fn(),
        setVolume: vi.fn(),
        setMuted: vi.fn(),
        setShuffle: vi.fn(),
        setRepeat: vi.fn(),
        setCrossfade: vi.fn(),
        setAutoplay: vi.fn(),
        playTracks: vi.fn(),
        addToQueue: vi.fn(),
        playNext: vi.fn(),
        playAt: vi.fn(),
        removeAt: vi.fn(),
        moveTo: vi.fn(),
        clearQueue: vi.fn(),
    }
}

describe('runCommand', () => {
    it('runs the transport commands', () => {
        const player = target()
        for (const type of ['Play', 'Pause', 'Stop', 'Next', 'Previous'] as const) {
            expect(runCommand(received({ type }), player)).toBe(true)
        }

        expect(player.play).toHaveBeenCalledOnce()
        expect(player.pause).toHaveBeenCalledOnce()
        expect(player.stop).toHaveBeenCalledOnce()
        expect(player.next).toHaveBeenCalledOnce()
        expect(player.previous).toHaveBeenCalledOnce()
    })

    it('seeks in seconds and sets volume and mute', () => {
        const player = target()
        runCommand(received({ type: 'Seek', positionMilliseconds: 61_500 }), player)
        runCommand(received({ type: 'SetVolume', volume: 0.25 }), player)
        runCommand(received({ type: 'SetVolume', volume: 0.5, muted: true }), player)

        expect(player.seek).toHaveBeenCalledWith(61.5)
        expect(player.setVolume).toHaveBeenNthCalledWith(1, 0.25)
        expect(player.setMuted).toHaveBeenCalledOnce()
        expect(player.setMuted).toHaveBeenCalledWith(true)
    })

    it('sets the switches', () => {
        const player = target()
        runCommand(received({ type: 'SetShuffle', enabled: true }), player)
        runCommand(received({ type: 'SetCrossfade', enabled: false }), player)
        runCommand(received({ type: 'SetAutoplay', enabled: true }), player)
        runCommand(received({ type: 'SetRepeat', repeat: 'One' }), player)

        expect(player.setShuffle).toHaveBeenCalledWith(true)
        expect(player.setCrossfade).toHaveBeenCalledWith(false)
        expect(player.setAutoplay).toHaveBeenCalledWith(true)
        expect(player.setRepeat).toHaveBeenCalledWith('one')
    })

    it('plays and queues the songs the server passed on', () => {
        const player = target()
        const tracks = [track('a'), track('b')]
        runCommand(
            received({
                type: 'PlayTracks',
                tracks,
                startIndex: 1,
                shuffle: true,
                source: { kind: 'Album', id: 'al' },
            }),
            player,
        )
        runCommand(received({ type: 'PlayTracks', tracks }), player)
        runCommand(received({ type: 'AddToQueue', tracks }), player)
        runCommand(received({ type: 'PlayNext', tracks }), player)

        expect(player.playTracks).toHaveBeenNthCalledWith(1, tracks, 1, {
            shuffle: true,
            source: { kind: 'album', id: 'al' },
        })
        expect(player.playTracks).toHaveBeenNthCalledWith(2, tracks, 0, {})
        expect(player.addToQueue).toHaveBeenCalledWith(tracks)
        expect(player.playNext).toHaveBeenCalledWith(tracks)
    })

    it('maps positions of the published window to the queue of the player', () => {
        const player = target()
        runCommand(received({ type: 'PlayQueueEntry', index: 2 }), player, 100)
        runCommand(received({ type: 'RemoveQueueEntry', index: 0 }), player, 100)
        runCommand(received({ type: 'MoveQueueEntry', index: 3, toIndex: 1 }), player, 100)
        runCommand(received({ type: 'ClearQueue' }), player, 100)

        expect(player.playAt).toHaveBeenCalledWith(102)
        expect(player.removeAt).toHaveBeenCalledWith(100)
        expect(player.moveTo).toHaveBeenCalledWith(103, 101)
        expect(player.clearQueue).toHaveBeenCalledOnce()
    })

    it('ignores commands without what their type needs', () => {
        const player = target()
        expect(runCommand(received({ type: 'Seek' }), player)).toBe(false)
        expect(runCommand(received({ type: 'SetVolume' }), player)).toBe(false)
        expect(runCommand(received({ type: 'SetShuffle' }), player)).toBe(false)
        expect(runCommand(received({ type: 'SetRepeat' }), player)).toBe(false)
        expect(runCommand(received({ type: 'PlayTracks', tracks: [] }), player)).toBe(false)
        expect(runCommand(received({ type: 'AddToQueue' }), player)).toBe(false)
        expect(runCommand(received({ type: 'PlayQueueEntry' }), player)).toBe(false)
        expect(runCommand(received({ type: 'MoveQueueEntry', index: 1 }), player)).toBe(false)
        expect(Object.values(player).every((fn) => vi.mocked(fn).mock.calls.length === 0)).toBe(
            true,
        )
    })
})
