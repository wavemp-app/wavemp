import { describe, expect, it } from 'vitest'
import type { Favorite } from '@/api/generated/models'
import {
    favoriteItem,
    isMyMusic,
    isValidRating,
    nextRating,
    ratingStars,
    starState,
    sourceOf,
    withMyMusic,
    withSource,
} from '../presentation'

describe('ratings', () => {
    it('accepts 0 to 5 whole stars', () => {
        expect([0, 1, 5].map(isValidRating)).toEqual([true, true, true])
        expect([-1, 6, 2.5, Number.NaN].map(isValidRating)).toEqual([false, false, false, false])
        expect(ratingStars).toEqual([1, 2, 3, 4, 5])
    })

    it('rates with the clicked star and removes the rating with the same star', () => {
        expect(nextRating(0, 3)).toBe(3)
        expect(nextRating(3, 5)).toBe(5)
        expect(nextRating(3, 1)).toBe(1)
        expect(nextRating(3, 3)).toBe(0)
    })

    it('marks the stars up to the rating', () => {
        expect(ratingStars.map((star) => starState(3, star))).toEqual([
            'on',
            'on',
            'on',
            'off',
            'off',
        ])
        expect(ratingStars.map((star) => starState(0, star))).not.toContain('on')
    })
})

describe('favoriteItem', () => {
    it('returns the item of the type of the favorite', () => {
        const track = { id: 't', title: 'Song' }
        const album = { id: 'a', title: 'Album' }
        const artist = { id: 'r', name: 'Band' }

        expect(favoriteItem({ type: 'Track', track })).toEqual({ type: 'Track', track })
        expect(favoriteItem({ type: 'Album', album, track })).toEqual({ type: 'Album', album })
        expect(favoriteItem({ type: 'Artist', artist })).toEqual({ type: 'Artist', artist })
    })

    it('returns nothing for a favorite without its item', () => {
        expect(favoriteItem({ type: 'Track', track: null })).toBeNull()
        expect(favoriteItem({ type: 'Album' })).toBeNull()
        expect(favoriteItem({} as Favorite)).toBeNull()
    })
})

describe('my music in the query', () => {
    it('is on only for myMusic=true', () => {
        expect(isMyMusic({ myMusic: 'true' })).toBe(true)
        expect(isMyMusic({ myMusic: ['true', 'false'] })).toBe(true)
        expect(isMyMusic({ myMusic: 'false' })).toBe(false)
        expect(isMyMusic({ myMusic: '1' })).toBe(false)
        expect(isMyMusic({})).toBe(false)
    })

    it('is switched without touching the other parameters', () => {
        expect(withMyMusic({ q: 'rock', sort: 'Title' }, true)).toEqual({
            q: 'rock',
            sort: 'Title',
            myMusic: 'true',
        })
        expect(withMyMusic({ q: 'rock', myMusic: 'true' }, false)).toEqual({ q: 'rock' })
    })
})

describe('filter by media folder', () => {
    it('reads the media folder from the query', () => {
        expect(sourceOf({ source: 'abc' })).toBe('abc')
        expect(sourceOf({ source: ['abc', 'def'] })).toBe('abc')
        expect(sourceOf({ source: '' })).toBeUndefined()
        expect(sourceOf({})).toBeUndefined()
    })

    it('sets and removes the media folder and keeps the rest of the query', () => {
        expect(withSource({ q: 'rock', myMusic: 'true' }, 'abc')).toEqual({
            q: 'rock',
            myMusic: 'true',
            source: 'abc',
        })
        expect(withSource({ q: 'rock', source: 'abc' }, undefined)).toEqual({ q: 'rock' })
    })
})
