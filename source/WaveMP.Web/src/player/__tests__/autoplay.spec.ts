import { describe, expect, it } from 'vitest'
import type { Track } from '@/api/generated/models'
import {
    applyAutoplayChange,
    autoplayContext,
    autoplayExcluded,
    autoplayMaxTracks,
    autoplaySignature,
    insertAutoplay,
    isEndOfList,
    isPlaybackFresh,
    toAutoplayEntries,
    toAutoplaySettings,
    withoutUpcomingAutoplay,
    type AutoplayEntry,
} from '../autoplay'
import { emptyQueue, type Queue, type QueueItem } from '../queue'

function track(id: string): Track {
    return { id, title: id.toUpperCase(), hasAudio: true }
}

function item(id: string, autoplay = false): QueueItem {
    return { key: `k-${id}`, track: track(id), ...(autoplay ? { autoplay: true } : {}) }
}

function queueOf(items: QueueItem[], index: number, original: string[] | null = null): Queue {
    return { items, index, shuffle: original !== null, original }
}

function entry(id: string): AutoplayEntry {
    return { key: `e-${id}`, track: track(id) }
}

function ids(queue: Queue): string[] {
    return queue.items.map((value) => `${value.track.id}${value.autoplay ? '*' : ''}`)
}

describe('autoplay settings', () => {
    it('reads complete settings and rejects incomplete ones', () => {
        expect(toAutoplaySettings({ enabled: false, includeVideoOnly: true })).toEqual({
            enabled: false,
            includeVideoOnly: true,
        })
        expect(toAutoplaySettings({ enabled: true })).toBeNull()
        expect(toAutoplaySettings(null)).toBeNull()
    })

    it('applies a change like the server: missing values stay', () => {
        const settings = { enabled: true, includeVideoOnly: true }

        expect(applyAutoplayChange(settings, { includeVideoOnly: false })).toEqual({
            enabled: true,
            includeVideoOnly: false,
        })
        expect(applyAutoplayChange(settings, { enabled: false, includeVideoOnly: null })).toEqual({
            enabled: false,
            includeVideoOnly: true,
        })
    })
})

describe('autoplay list', () => {
    it('takes the songs of the list without those of autoplay, the last ones of a long list', () => {
        expect(autoplayContext(queueOf([item('a'), item('x', true), item('b')], 0))).toEqual([
            'a',
            'b',
        ])

        const long = Array.from({ length: autoplayMaxTracks + 2 }, (_, at) => item(`t${at}`))
        const context = autoplayContext(queueOf(long, 0))
        expect(context).toHaveLength(autoplayMaxTracks)
        expect(context[0]).toBe('t2')
    })

    it('keeps the same signature when the list only moves', () => {
        expect(autoplaySignature(['b', 'a', 'b'])).toBe(autoplaySignature(['a', 'b', 'b']))
        expect(autoplaySignature(['a', 'b'])).not.toBe(autoplaySignature(['a', 'b', 'b']))
    })

    it('excludes removed, played and waiting songs of autoplay once each', () => {
        const queue = queueOf([item('a'), item('x', true)], 1)

        expect(autoplayExcluded(queue, [entry('y'), entry('x')], ['z'])).toEqual(['z', 'x', 'y'])
    })

    it('takes new songs from an answer, without songs it knows', () => {
        const entries = toAutoplayEntries(
            [
                { track: track('x'), playback: { trackId: 'x', streamUrl: '/x' } },
                { track: track('y') },
                { track: track('y') },
                { track: track('known') },
                { track: { title: 'No id' } },
                {},
            ],
            ['known'],
        )

        expect(entries.map((value) => value.track.id)).toEqual(['x', 'y'])
        expect(entries[0]?.playback?.streamUrl).toBe('/x')
        expect(entries[1]).not.toHaveProperty('playback')
        expect(new Set(entries.map((value) => value.key)).size).toBe(2)
    })
})

describe('autoplay in the queue', () => {
    it('adds a song at the end of the queue, marked as autoplay', () => {
        const queue = insertAutoplay(queueOf([item('a'), item('b')], 1), entry('x'))

        expect(ids(queue)).toEqual(['a', 'b', 'x*'])
        expect(queue.index).toBe(1)
    })

    it('adds a song right after the current entry, also in the order before shuffling', () => {
        const shuffled = queueOf([item('b'), item('a'), item('c')], 0, ['k-a', 'k-b', 'k-c'])

        const queue = insertAutoplay(shuffled, entry('x'), true)

        expect(ids(queue)).toEqual(['b', 'x*', 'a', 'c'])
        expect(queue.original).toEqual(['k-a', 'k-b', 'e-x', 'k-c'])
        expect(insertAutoplay(shuffled, entry('y')).original).toEqual(['k-a', 'k-b', 'k-c', 'e-y'])
    })

    it('takes back the songs of autoplay after the current entry only', () => {
        const queue = queueOf([item('a'), item('x', true), item('y', true), item('z', true)], 2, [
            'k-a',
            'k-x',
            'k-y',
            'k-z',
        ])

        const result = withoutUpcomingAutoplay(queue)

        expect(ids(result)).toEqual(['a', 'x*', 'y*'])
        expect(result.index).toBe(2)
        expect(result.original).toEqual(['k-a', 'k-x', 'k-y'])
        const unchanged = queueOf([item('a'), item('b')], 0)
        expect(withoutUpcomingAutoplay(unchanged)).toBe(unchanged)
    })

    it('knows the end of the list: only songs of autoplay or nothing follow', () => {
        expect(isEndOfList(queueOf([item('a'), item('b')], 1))).toBe(true)
        expect(isEndOfList(queueOf([item('a'), item('x', true)], 0))).toBe(true)
        expect(isEndOfList(queueOf([item('a'), item('x', true), item('b')], 1))).toBe(false)
        expect(isEndOfList(queueOf([item('a')], -1))).toBe(false)
        expect(isEndOfList(emptyQueue)).toBe(true)
    })

    it('uses a stream URL autoplay brought only while it stays valid a while', () => {
        const now = Date.parse('2026-11-19T20:00:00Z')
        const playback = (expiresAt: Date | null, streamUrl: string | null = '/x') => ({
            trackId: 'x',
            streamUrl,
            expiresAt,
        })

        expect(isPlaybackFresh(playback(new Date(now + 600_000)), now)).toBe(true)
        expect(isPlaybackFresh(playback(new Date(now + 30_000)), now)).toBe(false)
        expect(isPlaybackFresh(playback(null), now)).toBe(false)
        expect(isPlaybackFresh(playback(new Date(now + 600_000), null), now)).toBe(false)
        expect(isPlaybackFresh(undefined, now)).toBe(false)
    })
})
