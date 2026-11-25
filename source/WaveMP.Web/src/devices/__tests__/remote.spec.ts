import { describe, expect, it } from 'vitest'
import {
    livePositionMilliseconds,
    mirrorKey,
    mirrorQueue,
    optimisticLive,
    positionOfKey,
    remoteTransportState,
    restoredFromLive,
    transportPlayback,
} from '../remote'
import { deviceQueue, live, track } from './fakes'

const abc = [track('a'), track('b'), track('c')]

describe('mirrorQueue', () => {
    it('keys every entry by queue version and position and takes the current entry', () => {
        const queue = mirrorQueue(deviceQueue(abc, { currentIndex: 1, shuffle: true }))

        expect(queue.items.map((item) => item.key)).toEqual(['r90-0', 'r90-1', 'r90-2'])
        expect(queue.items.map((item) => item.track.id)).toEqual(['a', 'b', 'c'])
        expect(queue.index).toBe(1)
        expect(queue.shuffle).toBe(true)
        expect(queue.original).toBeNull()
        expect(mirrorKey(7, 3)).toBe('r7-3')
    })

    it('follows the live current entry and ignores one outside the queue', () => {
        const source = deviceQueue(abc)
        expect(mirrorQueue(source, 2).index).toBe(2)
        expect(mirrorQueue(source, 5).index).toBe(-1)
    })

    it('keeps songs that left the library as unknown entries and marks autoplay', () => {
        const source = deviceQueue([track('a'), null])
        source.items[0] = { ...source.items[0]!, autoplay: true }
        const queue = mirrorQueue(source)

        expect(queue.items[1]?.track).toEqual({ id: 'missing-1', hasAudio: true })
        expect(queue.items[0]?.autoplay).toBe(true)
        expect(queue.items[1]?.autoplay).toBeUndefined()
    })

    it('is empty without a queue', () => {
        expect(mirrorQueue(null)).toEqual({ items: [], index: -1, shuffle: false, original: null })
    })
})

describe('positionOfKey', () => {
    it('finds the position of an entry in the live queue', () => {
        const queue = mirrorQueue(deviceQueue(abc))
        expect(positionOfKey(queue, 'r90-2')).toBe(2)
        expect(positionOfKey(queue, 'q1')).toBeNull()
    })
})

describe('livePositionMilliseconds', () => {
    it('adds the time since the state arrived while playing, up to the end', () => {
        const playing = live({
            status: 'Playing',
            positionMilliseconds: 10_000,
            durationMilliseconds: 12_000,
        })

        expect(livePositionMilliseconds(playing, 1000, 2500)).toBe(11_500)
        expect(livePositionMilliseconds(playing, 1000, 9000)).toBe(12_000)
        expect(livePositionMilliseconds(playing, 1000, 500)).toBe(10_000)
    })

    it('keeps the position while paused and without a duration has no end', () => {
        expect(
            livePositionMilliseconds(
                live({ status: 'Paused', positionMilliseconds: 4000 }),
                0,
                5000,
            ),
        ).toBe(4000)
        expect(
            livePositionMilliseconds(
                live({ status: 'Playing', positionMilliseconds: 4000 }),
                0,
                5000,
            ),
        ).toBe(9000)
    })
})

describe('transportPlayback', () => {
    it('maps the live status to the transport bar', () => {
        expect(transportPlayback('Playing')).toBe('playing')
        expect(transportPlayback('Loading')).toBe('playing')
        expect(transportPlayback('Paused')).toBe('paused')
        expect(transportPlayback('Idle')).toBe('idle')
    })
})

describe('remoteTransportState', () => {
    it('shows the live state with the song of the mirrored queue', () => {
        const queue = mirrorQueue(
            deviceQueue([track('a'), track('b', { isFavorite: true, rating: 4, hasCover: true })]),
            1,
        )
        const state = remoteTransportState(
            live({
                status: 'Loading',
                currentIndex: 1,
                currentTrackId: 'b',
                positionMilliseconds: 3000,
                durationMilliseconds: 0,
                shuffle: true,
                repeat: 'One',
                volume: 0.4,
                muted: true,
                crossfade: true,
                autoplay: true,
            }),
            queue,
            0,
            1000,
        )

        expect(state).toEqual({
            playback: 'playing',
            loading: true,
            trackId: 'b',
            hasCover: true,
            title: 'B',
            artist: 'Band',
            isFavorite: true,
            rating: 4,
            position: 3,
            duration: 200,
            shuffle: true,
            repeat: 'one',
            volume: 0.4,
            muted: true,
            crossfade: true,
            autoplay: true,
        })
    })

    it('shows no song of an outdated mirror', () => {
        const queue = mirrorQueue(deviceQueue([track('a')]), 0)
        const state = remoteTransportState(
            live({
                status: 'Paused',
                currentIndex: 0,
                currentTrackId: 'z',
                durationMilliseconds: 5000,
            }),
            queue,
            0,
            0,
        )

        expect(state.title).toBeUndefined()
        expect(state.trackId).toBe('z')
        expect(state.duration).toBe(5)
    })
})

describe('optimisticLive', () => {
    const playing = live({
        status: 'Playing',
        currentIndex: 0,
        positionMilliseconds: 1000,
        durationMilliseconds: 100_000,
    })

    it('pauses and plays at the position of now', () => {
        expect(
            optimisticLive(playing, { targetDeviceId: 'a', type: 'Pause' }, 0, 2000),
        ).toMatchObject({
            status: 'Paused',
            positionMilliseconds: 3000,
        })
        expect(
            optimisticLive(
                { ...playing, status: 'Paused' },
                { targetDeviceId: 'a', type: 'Play' },
                0,
                2000,
            ),
        ).toMatchObject({ status: 'Playing', positionMilliseconds: 1000 })
        expect(optimisticLive(live(), { targetDeviceId: 'a', type: 'Play' }, 0, 0)).toBeNull()
        expect(optimisticLive(live(), { targetDeviceId: 'a', type: 'Pause' }, 0, 0)).toBeNull()
    })

    it('shows seek, volume and the switches at once', () => {
        expect(
            optimisticLive(
                playing,
                { targetDeviceId: 'a', type: 'Seek', positionMilliseconds: 42_000 },
                0,
                0,
            )?.positionMilliseconds,
        ).toBe(42_000)
        expect(
            optimisticLive(
                playing,
                { targetDeviceId: 'a', type: 'SetVolume', volume: 0.2, muted: null },
                0,
                0,
            ),
        ).toMatchObject({ volume: 0.2, muted: false })
        expect(
            optimisticLive(
                playing,
                { targetDeviceId: 'a', type: 'SetVolume', volume: 1, muted: true },
                0,
                0,
            ),
        ).toMatchObject({ muted: true })
        expect(
            optimisticLive(
                playing,
                { targetDeviceId: 'a', type: 'SetShuffle', enabled: true },
                0,
                0,
            )?.shuffle,
        ).toBe(true)
        expect(
            optimisticLive(playing, { targetDeviceId: 'a', type: 'SetRepeat', repeat: 'All' }, 0, 0)
                ?.repeat,
        ).toBe('All')
        expect(
            optimisticLive(
                playing,
                { targetDeviceId: 'a', type: 'SetCrossfade', enabled: true },
                0,
                0,
            )?.crossfade,
        ).toBe(true)
        expect(
            optimisticLive(
                playing,
                { targetDeviceId: 'a', type: 'SetAutoplay', enabled: true },
                0,
                0,
            )?.autoplay,
        ).toBe(true)
    })

    it('leaves commands on the queue to the active device', () => {
        expect(optimisticLive(playing, { targetDeviceId: 'a', type: 'Next' }, 0, 0)).toBeNull()
        expect(
            optimisticLive(
                playing,
                { targetDeviceId: 'a', type: 'ClearQueue', expectedQueueVersion: 1 },
                0,
                0,
            ),
        ).toBeNull()
    })
})

describe('restoredFromLive', () => {
    it('takes over the live queue at the position of now with repeat and source', () => {
        const restored = restoredFromLive(
            live({
                status: 'Playing',
                currentIndex: 1,
                currentTrackId: 'b',
                positionMilliseconds: 10_000,
                durationMilliseconds: 200_000,
                repeat: 'All',
            }),
            deviceQueue(abc, { source: { kind: 'Album', id: 'al' } }),
            0,
            5000,
        )

        expect(restored?.queue.items.map((item) => item.track.id)).toEqual(['a', 'b', 'c'])
        expect(restored?.queue.index).toBe(1)
        expect(restored?.position).toBe(15)
        expect(restored?.repeat).toBe('all')
        expect(restored?.source).toEqual({ kind: 'album', id: 'al' })
        // New keys: the entries belong to this player now.
        expect(restored?.queue.items[0]?.key).not.toBe('r90-0')
    })

    it('leaves out songs that left the library and starts an outdated queue at its own entry', () => {
        const restored = restoredFromLive(
            live({
                status: 'Paused',
                queueVersion: 95,
                currentIndex: 2,
                positionMilliseconds: 9000,
            }),
            deviceQueue([track('a'), null, track('c')], { currentIndex: 0 }),
            0,
            0,
        )

        expect(restored?.queue.items.map((item) => item.track.id)).toEqual(['a', 'c'])
        expect(restored?.queue.index).toBe(0)
        expect(restored?.position).toBe(0)
    })

    it('has nothing to take over without a queue', () => {
        expect(restoredFromLive(live(), null, 0, 0)).toBeNull()
        expect(restoredFromLive(live(), deviceQueue([]), 0, 0)).toBeNull()
    })
})
