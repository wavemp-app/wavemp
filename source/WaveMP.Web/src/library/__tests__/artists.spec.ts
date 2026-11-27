import { describe, expect, it } from 'vitest'
import type { ArtistCredit } from '@/api/generated/models'
import {
    artistParts,
    artistsOf,
    artistText,
    hasArtist,
    moveEntry,
    nameKey,
    plainArtistSeparators,
    sameArtists,
} from '../artists'

const bob: ArtistCredit = { id: 'bob', name: 'Bob Sinclar', role: 'Main' }
const simon: ArtistCredit = { id: 'simon', name: 'Simon & Garfunkel', role: 'Main' }
const kflay: ArtistCredit = { id: 'kflay', name: 'K.Flay', role: 'Featured' }
const sia: ArtistCredit = { id: 'sia', name: 'Sia', role: 'Featured' }

describe('artistsOf', () => {
    it('takes every artist in its order', () => {
        expect(
            artistsOf({ artists: [bob, kflay], artist: { id: 'bob', name: 'Bob Sinclar' } }),
        ).toEqual([bob, kflay])
    })

    it('falls back to the first artist alone and is empty without one', () => {
        expect(artistsOf({ artist: { id: 'acdc', name: 'AC/DC' } })).toEqual([
            { id: 'acdc', name: 'AC/DC', role: 'Main' },
        ])
        expect(artistsOf({ artists: [], artist: null })).toEqual([])
        expect(artistsOf(null)).toEqual([])
    })
})

describe('artistText', () => {
    it.each([
        [[bob], 'Bob Sinclar'],
        [[bob, kflay], 'Bob Sinclar feat. K.Flay'],
        [[bob, simon, kflay, sia], 'Bob Sinclar, Simon & Garfunkel feat. K.Flay, Sia'],
        // Main artists come first, whatever the order of the list.
        [[kflay, bob], 'Bob Sinclar feat. K.Flay'],
        // Only guests: no "feat." in front of the first one.
        [[kflay, sia], 'K.Flay, Sia'],
        [[], ''],
    ])('joins %j to "%s"', (artists, expected) => {
        expect(artistText(artists, plainArtistSeparators)).toBe(expected)
    })

    it('names the separator in front of every artist', () => {
        expect(artistParts([bob, simon, kflay]).map((part) => part.separator)).toEqual([
            null,
            'list',
            'featuring',
        ])
    })
})

describe('nameKey', () => {
    it.each([
        ['K.Flay', 'kflay'],
        ['K/DA', 'kda'],
        ['Beyoncé', 'beyonce'],
        ['  Guns N’ Roses ', 'gunsnroses'],
        ['Mötley Crüe', 'motleycrue'],
        ['...', ''],
    ])('finds "%s" by "%s"', (name, key) => {
        expect(nameKey(name)).toBe(key)
    })
})

describe('sameArtists', () => {
    it('compares ids where both have one, otherwise the normalized names', () => {
        // The role does not count: a guest is the same artist as a main one.
        const mainKflay: ArtistCredit = { ...kflay, role: 'Main' }
        expect(sameArtists([bob, kflay], [bob, mainKflay])).toBe(true)
        expect(sameArtists([{ name: 'K.Flay' }], [{ name: 'kflay' }])).toBe(true)
        expect(sameArtists([bob, kflay], [kflay, bob])).toBe(false)
        expect(sameArtists([bob], [bob, kflay])).toBe(false)
        expect(sameArtists([], [])).toBe(true)
    })
})

describe('hasArtist', () => {
    it('finds the same id or the same normalized name', () => {
        const entries = [{ name: 'K.Flay', role: 'Main' as const, id: 'kflay' }]

        expect(hasArtist(entries, { name: 'kflay', role: 'Main' })).toBe(true)
        expect(hasArtist(entries, { name: 'Other spelling', role: 'Main', id: 'kflay' })).toBe(true)
        expect(hasArtist(entries, { name: 'K/DA', role: 'Main' })).toBe(false)
    })
})

describe('moveEntry', () => {
    it('moves an entry and keeps the others in order', () => {
        expect(moveEntry(['a', 'b', 'c'], 0, 2)).toEqual(['b', 'c', 'a'])
        expect(moveEntry(['a', 'b', 'c'], 2, 0)).toEqual(['c', 'a', 'b'])
        expect(moveEntry(['a', 'b'], 5, 0)).toEqual(['a', 'b'])
    })
})
