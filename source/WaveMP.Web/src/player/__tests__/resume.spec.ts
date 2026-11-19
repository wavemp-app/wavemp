import { describe, expect, it } from 'vitest'
import {
    PlaybackSourceKindObject,
    RepeatModeObject,
    type PlaybackState,
    type Track,
} from '@/api/generated/models'
import { playTracks, setShuffle, type Queue } from '../queue'
import {
    fromSavedState,
    isFromOtherDevice,
    savedStateSignature,
    savedWindow,
    toSavedState,
    type PlayerSnapshot,
} from '../resume'

function track(id: string, hasAudio = true): Track {
    return { id, title: id.toUpperCase(), hasAudio }
}

function ids(queue: Queue): string[] {
    return queue.items.map((item) => item.track.id ?? '')
}

function snapshot(queue: Queue, extra: Partial<PlayerSnapshot> = {}): PlayerSnapshot {
    return { queue, repeat: 'off', position: 0, source: null, ...extra }
}

const abcd = [track('a'), track('b'), track('c'), track('d')]

describe('toSavedState', () => {
    it('saves the songs, the current entry, the position in milliseconds and repeat', () => {
        const state = toSavedState(
            snapshot(playTracks(abcd, 2, false), {
                position: 61.2345,
                repeat: 'one',
                source: { kind: 'smart-playlist', id: 'p1' },
            }),
        )

        expect(state).toEqual({
            items: [{ trackId: 'a' }, { trackId: 'b' }, { trackId: 'c' }, { trackId: 'd' }],
            currentIndex: 2,
            positionMilliseconds: 61235,
            shuffle: false,
            originalOrder: [],
            repeat: RepeatModeObject.One,
            source: { kind: PlaybackSourceKindObject.SmartPlaylist, id: 'p1' },
        })
    })

    it('marks the songs autoplay added', () => {
        const queue = playTracks(abcd.slice(0, 2), 0, false)
        const items = [queue.items[0]!, { ...queue.items[1]!, autoplay: true }]

        expect(toSavedState(snapshot({ ...queue, items })).items).toEqual([
            { trackId: 'a' },
            { trackId: 'b', autoplay: true },
        ])
    })

    it('saves the order before shuffling as positions in the saved queue', () => {
        const queue = setShuffle(playTracks(abcd, 0, false), true, () => 0)
        const state = toSavedState(snapshot(queue))

        expect(state.shuffle).toBe(true)
        expect(state.originalOrder?.map((at) => state.items?.[at]?.trackId)).toEqual([
            'a',
            'b',
            'c',
            'd',
        ])
    })

    it('saves no position without a current entry', () => {
        const state = toSavedState(
            snapshot({ ...playTracks(abcd, 0, false), index: -1 }, { position: 12 }),
        )

        expect(state.currentIndex).toBe(-1)
        expect(state.positionMilliseconds).toBe(0)
    })

    it('keeps the entries around the current one of a long queue', () => {
        const tracks = Array.from({ length: 10 }, (_, at) => track(`t${at}`))
        const queue = setShuffle(playTracks(tracks, 8, false), true, () => 0)
        const state = toSavedState(snapshot(queue), 4)

        expect(state.items).toHaveLength(4)
        expect(state.items?.[state.currentIndex ?? -1]?.trackId).toBe(
            queue.items[queue.index]?.track.id,
        )
        expect([...(state.originalOrder ?? [])].sort()).toEqual([0, 1, 2, 3])
    })
})

describe('savedWindow', () => {
    it('keeps a short queue whole', () => {
        expect(savedWindow(10, 5, 20)).toEqual([0, 10])
    })

    it('starts 50 entries before the current one and stays inside the queue', () => {
        expect(savedWindow(3000, 1000, 2000)).toEqual([950, 2950])
        expect(savedWindow(3000, 10, 2000)).toEqual([0, 2000])
        expect(savedWindow(3000, 2990, 2000)).toEqual([1000, 3000])
        expect(savedWindow(3000, -1, 2000)).toEqual([0, 2000])
    })
})

describe('savedStateSignature', () => {
    it('ignores changes of the position below a second', () => {
        const queue = playTracks(abcd, 1, false)
        const first = toSavedState(snapshot(queue, { position: 10.1 }))
        const second = toSavedState(snapshot(queue, { position: 10.3 }))
        const later = toSavedState(snapshot(queue, { position: 12 }))

        expect(savedStateSignature(first)).toBe(savedStateSignature(second))
        expect(savedStateSignature(first)).not.toBe(savedStateSignature(later))
    })
})

function savedState(extra: Partial<PlaybackState> = {}): PlaybackState {
    return {
        items: abcd.map((value) => ({ track: value, autoplay: false })),
        currentIndex: 2,
        positionMilliseconds: 42_500,
        shuffle: false,
        originalOrder: [],
        repeat: RepeatModeObject.All,
        source: { kind: PlaybackSourceKindObject.Album, id: 'al1' },
        device: { id: 'd1', name: 'Firefox · Windows' },
        changedAt: new Date('2026-11-19T10:00:00Z'),
        ...extra,
    }
}

describe('fromSavedState', () => {
    it('restores the queue, the current entry, the position in seconds, repeat and source', () => {
        const restored = fromSavedState(savedState())

        expect(ids(restored.queue)).toEqual(['a', 'b', 'c', 'd'])
        expect(restored.queue.index).toBe(2)
        expect(restored.queue.shuffle).toBe(false)
        expect(restored.queue.original).toBeNull()
        expect(new Set(restored.queue.items.map((item) => item.key)).size).toBe(4)
        expect(restored.position).toBe(42.5)
        expect(restored.repeat).toBe('all')
        expect(restored.source).toEqual({ kind: 'album', id: 'al1' })
    })

    it('restores the autoplay marks and the order before shuffling', () => {
        const restored = fromSavedState(
            savedState({
                items: abcd.map((value, at) => ({ track: value, autoplay: at === 3 })),
                shuffle: true,
                originalOrder: [3, 2, 1, 0],
            }),
        )

        expect(restored.queue.items.map((item) => item.autoplay === true)).toEqual([
            false,
            false,
            false,
            true,
        ])
        expect(restored.queue.shuffle).toBe(true)
        expect(
            restored.queue.original?.map(
                (key) => restored.queue.items.find((item) => item.key === key)?.track.id,
            ),
        ).toEqual(['d', 'c', 'b', 'a'])
    })

    it('continues with the next song when the current one cannot play here', () => {
        const restored = fromSavedState(
            savedState({
                items: [track('a'), track('b', false), track('c')].map((value) => ({
                    track: value,
                })),
                currentIndex: 1,
                shuffle: true,
                originalOrder: [0, 1, 2],
            }),
        )

        expect(ids(restored.queue)).toEqual(['a', 'c'])
        expect(restored.queue.index).toBe(1)
        expect(restored.position).toBe(0)
        expect(restored.queue.original).toHaveLength(2)
    })

    it('waits at the last song when no song follows the current one', () => {
        const restored = fromSavedState(
            savedState({
                items: [track('a'), track('b', false)].map((value) => ({ track: value })),
                currentIndex: 1,
            }),
        )

        expect(restored.queue.index).toBe(0)
        expect(restored.position).toBe(0)
    })

    it('starts a song saved at its very end at its beginning', () => {
        const long = { ...track('a'), durationMilliseconds: 200_000 }
        const at = (positionMilliseconds: number) =>
            fromSavedState(
                savedState({ items: [{ track: long }], currentIndex: 0, positionMilliseconds }),
            ).position

        expect(at(199_000)).toBe(199)
        expect(at(199_800)).toBe(0)
        expect(at(250_000)).toBe(0)
    })

    it('restores an empty queue without current entry', () => {
        const restored = fromSavedState(savedState({ items: [], currentIndex: -1, source: null }))

        expect(restored.queue.items).toEqual([])
        expect(restored.queue.index).toBe(-1)
        expect(restored.position).toBe(0)
        expect(restored.source).toBeNull()
    })

    it('round-trips a saved state', () => {
        const queue = setShuffle(playTracks(abcd, 1, false), true, () => 0)
        const saved = toSavedState(snapshot(queue, { position: 30, repeat: 'all' }))
        const restored = fromSavedState({
            ...saved,
            items: saved.items?.map((item) => ({
                track: abcd.find((value) => value.id === item.trackId),
                autoplay: item.autoplay ?? false,
            })),
        })

        expect(ids(restored.queue)).toEqual(ids(queue))
        expect(restored.queue.index).toBe(queue.index)
        expect(restored.position).toBe(30)
        expect(
            restored.queue.original?.map(
                (key) => restored.queue.items.find((item) => item.key === key)?.track.id,
            ),
        ).toEqual(['a', 'b', 'c', 'd'])
    })
})

describe('isFromOtherDevice', () => {
    const state = savedState()
    const changedAt = state.changedAt!.getTime()

    it('takes over a state of another device that is not known yet', () => {
        expect(isFromOtherDevice(state, 'd2', null)).toBe(true)
        expect(isFromOtherDevice(state, 'd2', changedAt - 1000)).toBe(true)
    })

    it('ignores the own state and a state already taken over', () => {
        expect(isFromOtherDevice(state, 'd1', null)).toBe(false)
        expect(isFromOtherDevice(state, 'd2', changedAt)).toBe(false)
        expect(isFromOtherDevice({ ...state, device: null }, 'd2', null)).toBe(false)
    })
})
