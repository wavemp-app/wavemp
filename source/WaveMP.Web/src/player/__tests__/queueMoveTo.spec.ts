import { describe, expect, it } from 'vitest'
import type { Track } from '@/api/generated/models'
import { moveTo, playTracks } from '../queue'

function track(id: string): Track {
    return { id, title: id, hasAudio: true }
}

const tracks = ['a', 'b', 'c', 'd'].map(track)

function ids(queue: ReturnType<typeof playTracks>): string[] {
    return queue.items.map((item) => item.track.id ?? '')
}

describe('moveTo', () => {
    it('moves an entry to a new index and keeps the current entry current', () => {
        const queue = playTracks(tracks, 1, false)

        const down = moveTo(queue, 0, 3)
        expect(ids(down)).toEqual(['b', 'c', 'd', 'a'])
        expect(down.items[down.index]?.track.id).toBe('b')

        const up = moveTo(queue, 3, 0)
        expect(ids(up)).toEqual(['d', 'a', 'b', 'c'])
        expect(up.items[up.index]?.track.id).toBe('b')

        const current = moveTo(queue, 1, 2)
        expect(ids(current)).toEqual(['a', 'c', 'b', 'd'])
        expect(current.index).toBe(2)
    })

    it('keeps the order before shuffling', () => {
        const shuffled = playTracks(tracks, 0, true, () => 0)
        expect(moveTo(shuffled, 1, 2).original).toBe(shuffled.original)
    })

    it('changes nothing for indexes outside the queue', () => {
        const queue = playTracks(tracks, 0, false)
        expect(moveTo(queue, 1, 1)).toBe(queue)
        expect(moveTo(queue, -1, 1)).toBe(queue)
        expect(moveTo(queue, 1, 4)).toBe(queue)
    })
})
