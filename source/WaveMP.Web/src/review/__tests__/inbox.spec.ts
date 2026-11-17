import { describe, expect, it } from 'vitest'
import type { TrackReview } from '@/api/generated/models'
import {
    isMissingTagsFilter,
    isReviewed,
    isReviewSort,
    missingTagHints,
    missingTagsParameter,
    nextOpenId,
    openIds,
    reviewPosition,
    selectionOf,
    toggled,
} from '../inbox'

function item(trackId: string, status: 'Unreviewed' | 'Reviewed' = 'Unreviewed'): TrackReview {
    return { trackId, title: trackId, status }
}

describe('review inbox', () => {
    it('knows its filters and sort orders', () => {
        expect(isMissingTagsFilter('missing')).toBe(true)
        expect(isMissingTagsFilter('Missing')).toBe(false)
        expect(isReviewSort('Artist')).toBe(true)
        expect(isReviewSort('Duration')).toBe(false)
    })

    it('asks the API for songs with or without missing tags only when filtered', () => {
        expect(missingTagsParameter('all')).toBeUndefined()
        expect(missingTagsParameter('missing')).toBe(true)
        expect(missingTagsParameter('complete')).toBe(false)
    })

    it('names the missing tags with the fields of the metadata editor', () => {
        const hints = missingTagHints(['Title', 'TrackNumber', 'Genre'])

        expect(hints.map((hint) => hint.field)).toEqual(['title', 'number', 'genre'])
        expect(hints.map((hint) => hint.labelKey)).toEqual([
            'reviewInbox.tags.title',
            'reviewInbox.tags.trackNumber',
            'reviewInbox.tags.genre',
        ])
        expect(missingTagHints(null)).toEqual([])
    })

    it('leaves out tags it does not know', () => {
        // A newer server may check more tags; the hint shows the ones it knows.
        const hints = missingTagHints(['Year', 'Composer' as never])

        expect(hints.map((hint) => hint.tag)).toEqual(['Year'])
    })

    it('treats songs reviewed by the API or in this tab as reviewed', () => {
        const reviewed = new Set(['b'])

        expect(isReviewed(item('a', 'Reviewed'), reviewed)).toBe(true)
        expect(isReviewed(item('b'), reviewed)).toBe(true)
        expect(isReviewed(item('c'), reviewed)).toBe(false)
        expect(openIds([item('a', 'Reviewed'), item('b'), item('c')], reviewed)).toEqual(['c'])
    })

    it('keeps only open songs of the list in the selection', () => {
        const items = [item('a'), item('b'), item('c')]
        const selected = toggled(toggled(new Set(), 'a', true), 'b', true)

        expect([...selected]).toEqual(['a', 'b'])
        expect([...toggled(selected, 'a', false)]).toEqual(['b'])
        expect([...selectionOf(new Set(['a', 'b', 'x']), items, new Set(['b']))]).toEqual(['a'])
    })

    it('tells the position and the neighbours of a song in the list', () => {
        const ids = ['a', 'b', 'c']

        expect(reviewPosition(ids, 'a')).toEqual({ index: 0, previous: null, next: 'b' })
        expect(reviewPosition(ids, 'b')).toEqual({ index: 1, previous: 'a', next: 'c' })
        expect(reviewPosition(ids, 'c')).toEqual({ index: 2, previous: 'b', next: null })
        expect(reviewPosition(ids, 'x')).toEqual({ index: -1, previous: null, next: null })
    })

    it('goes on to the next unreviewed song after the current one, never back', () => {
        const ids = ['a', 'b', 'c', 'd']

        expect(nextOpenId(ids, 'a', new Set(['a']))).toBe('b')
        expect(nextOpenId(ids, 'a', new Set(['a', 'b', 'c']))).toBe('d')
        // Songs before it were skipped or reviewed; the way ends at the end of the list.
        expect(nextOpenId(ids, 'c', new Set(['c', 'd']))).toBeNull()
        expect(nextOpenId(ids, 'd', new Set())).toBeNull()
        // A song that is not in the list starts at the beginning.
        expect(nextOpenId(ids, 'x', new Set(['a']))).toBe('b')
    })
})
