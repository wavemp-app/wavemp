import { describe, expect, it } from 'vitest'
import { playTracks, type Queue } from '@/player/queue'
import {
    liveAfterPublication,
    liveStatus,
    maxLiveMilliseconds,
    toLiveQueue,
    toPublication,
    type PlayerLiveSnapshot,
} from '../publication'
import { track } from './fakes'

function snapshot(queue: Queue, extra: Partial<PlayerLiveSnapshot> = {}): PlayerLiveSnapshot {
    return {
        queue,
        repeat: 'off',
        position: 12.3456,
        duration: 200,
        playback: 'playing',
        loading: false,
        volume: 0.5,
        muted: false,
        crossfade: false,
        autoplay: true,
        source: null,
        ...extra,
    }
}

const abc = [track('a'), track('b'), track('c')]

describe('liveStatus', () => {
    it('maps the player to the live status', () => {
        expect(liveStatus('playing', false)).toBe('Playing')
        expect(liveStatus('playing', true)).toBe('Loading')
        expect(liveStatus('paused', true)).toBe('Paused')
        expect(liveStatus('idle', false)).toBe('Idle')
    })
})

describe('toLiveQueue', () => {
    it('publishes the songs, autoplay, shuffle, the order before shuffling and the source', () => {
        const queue = playTracks(abc, 1, true, () => 0)
        const marked = {
            ...queue,
            items: queue.items.map((item, at) => (at === 2 ? { ...item, autoplay: true } : item)),
        }
        const [live, start] = toLiveQueue(marked, { kind: 'playlist', id: 'pl' })

        expect(start).toBe(0)
        expect(live.items.map((item) => item.trackId)).toEqual(
            marked.items.map((item) => item.track.id),
        )
        expect(live.items.map((item) => item.autoplay)).toEqual([false, false, true])
        expect(live.shuffle).toBe(true)
        expect([...live.originalOrder!].sort()).toEqual([0, 1, 2])
        expect(live.source).toEqual({ kind: 'Playlist', id: 'pl' })
    })

    it('publishes a window of 2000 entries around the current one of a longer queue', () => {
        const many = Array.from({ length: 2500 }, (_, at) => track(`t${at}`))
        const [middle, middleStart] = toLiveQueue(playTracks(many, 1000, false), null)
        const [end, endStart] = toLiveQueue(playTracks(many, 2400, false), null)

        expect(middle.items).toHaveLength(2000)
        expect(middleStart).toBe(500)
        expect(middle.items[0]?.trackId).toBe('t500')
        expect(endStart).toBe(500)
        const [early, earlyStart] = toLiveQueue(playTracks(many, 10, false), null)
        expect(earlyStart).toBe(0)
        expect(early.items[10]?.trackId).toBe('t10')
        expect(end.items.at(-1)?.trackId).toBe('t2499')
    })
})

describe('toPublication', () => {
    it('names the queue only when it changed since the last publication', () => {
        const queue = playTracks(abc, 1, false)
        const first = toPublication(snapshot(queue), null)
        const again = toPublication(snapshot(queue), first.queueSignature)
        const claimed = toPublication(snapshot(queue), first.queueSignature, true)

        expect(first.input.queue?.items).toHaveLength(3)
        expect(again.input.queue).toBeNull()
        expect(claimed.input.queue).not.toBeNull()
        expect(again.queueSignature).toBe(first.queueSignature)
    })

    it('rounds and limits the numbers the hub checks', () => {
        const queue = playTracks(abc, 1, false)
        const { input } = toPublication(
            snapshot(queue, { position: 1e9, duration: Number.NaN, volume: 1.4, repeat: 'one' }),
            null,
        )

        expect(input).toMatchObject({
            status: 'Playing',
            currentIndex: 1,
            positionMilliseconds: maxLiveMilliseconds,
            durationMilliseconds: 0,
            volume: 1,
            repeat: 'One',
            autoplay: true,
        })
        expect(
            toPublication(snapshot(queue, { position: 12.3456 }), null).input.positionMilliseconds,
        ).toBe(12_346)
    })

    it('publishes an empty player as idle', () => {
        const { input } = toPublication(
            snapshot({ items: [], index: -1, shuffle: false, original: null }),
            null,
        )

        expect(input).toMatchObject({ status: 'Idle', currentIndex: -1, positionMilliseconds: 0 })
        expect(input.queue?.items).toEqual([])
    })
})

describe('liveAfterPublication', () => {
    it('makes this device the active one with the published state', () => {
        const queue = playTracks(abc, 2, false)
        const { input } = toPublication(snapshot(queue), null)
        const live = liveAfterPublication(input, input.queue!, 'me', 7, 5)

        expect(live).toMatchObject({
            version: 7,
            queueVersion: 5,
            activeDeviceId: 'me',
            status: 'Playing',
            currentIndex: 2,
            currentTrackId: 'c',
            queueLength: 3,
            handover: null,
        })
    })
})
