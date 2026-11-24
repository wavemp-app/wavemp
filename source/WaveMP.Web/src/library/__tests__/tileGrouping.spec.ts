import { describe, expect, it } from 'vitest'
import type { Album, Artist } from '@/api/generated/models'
import { albumTileGrouping, artistTileGrouping, groupTiles } from '../tileGrouping'

const alpha = { id: 'alpha', name: 'Alpha Band' }
const beta = { id: 'beta', name: 'Beta Singer' }

function album(id: string, values: Partial<Album> = {}): Album {
    return { id, title: id, ...values }
}

function artist(name: string): Artist {
    return { id: name, name }
}

describe('albumTileGrouping', () => {
    it('groups by album artist by default and puts albums without one together', () => {
        const grouping = albumTileGrouping({}, false)!
        const groups = groupTiles(
            [
                album('one', { artist: alpha }),
                album('two', { artist: alpha }),
                album('three', { artist: beta }),
                album('loose', { artist: null }),
                album('no-id', { artist: { name: 'Nobody' } }),
            ],
            grouping,
        )

        expect(groups.map((group) => [group.id, group.kind, group.position])).toEqual([
            ['artist:alpha', 'artist', 0],
            ['artist:beta', 'artist', 2],
            ['artist:unknown', 'artist', 3],
        ])
        expect(groups[0]?.artist).toEqual(alpha)
        expect(groups[0]?.items.map((item) => item.id)).toEqual(['one', 'two'])
        expect(groups[2]?.items).toHaveLength(2)
        expect(groups[2]?.artist).toBeUndefined()
    })

    it('groups by the index letter of the title when sorted by title', () => {
        const grouping = albumTileGrouping({ sort: 'Title' }, false)!
        const groups = groupTiles(
            [album('Abbey'), album('Ärger'), album('Blue'), album('1999'), album('')],
            grouping,
        )

        expect(groups.map((group) => [group.letter, group.items.length])).toEqual([
            ['A', 2],
            ['B', 1],
            ['#', 2],
        ])
        expect(groups.every((group) => group.kind === 'letter')).toBe(true)
    })

    it('groups by year when sorted by year, albums without year together', () => {
        const grouping = albumTileGrouping({ sort: 'Year', direction: 'Desc' }, false)!
        const groups = groupTiles(
            [album('a', { year: 2001 }), album('b', { year: 2001 }), album('c', { year: null })],
            grouping,
        )

        expect(groups.map((group) => [group.kind, group.year, group.position])).toEqual([
            ['year', 2001, 0],
            ['year', undefined, 2],
        ])
    })

    it('does not group the other orders', () => {
        expect(albumTileGrouping({ sort: 'FirstSeenAt' }, false)).toBeUndefined()
        expect(albumTileGrouping({}, true)).toBeUndefined()
        expect(albumTileGrouping({ sort: 'Relevance' }, true)).toBeUndefined()
        expect(albumTileGrouping({ sort: 'Artist' }, true)).toBeDefined()
    })
})

describe('artistTileGrouping', () => {
    it('groups by the index letter of the name when sorted by name', () => {
        const groups = groupTiles(
            [artist('Abba'), artist('Élan'), artist('Eagles'), artist('2Pac')],
            artistTileGrouping({}, false)!,
        )

        expect(groups.map((group) => [group.id, group.letter, group.position])).toEqual([
            ['letter:A', 'A', 0],
            ['letter:E', 'E', 1],
            ['letter:#', '#', 3],
        ])
    })

    it('does not group the other orders', () => {
        expect(artistTileGrouping({ sort: 'FirstSeenAt' }, false)).toBeUndefined()
        expect(artistTileGrouping({}, true)).toBeUndefined()
    })
})

describe('groupTiles', () => {
    it('counts positions from the start of the window', () => {
        const groups = groupTiles(
            [artist('Bee'), artist('Cello'), artist('Cat')],
            artistTileGrouping({}, false)!,
            740,
        )

        expect(groups.map((group) => group.position)).toEqual([740, 741])
    })

    it('keeps the ids unique when a key appears in two runs', () => {
        const groups = groupTiles(
            [artist('Abba'), artist('Bee'), artist('Air')],
            artistTileGrouping({ sort: 'Name' }, false)!,
        )

        expect(groups.map((group) => group.id)).toEqual(['letter:A', 'letter:B', 'letter:A:1'])
    })

    it('has no group for an empty list', () => {
        expect(groupTiles([], artistTileGrouping({}, false)!)).toEqual([])
    })
})
