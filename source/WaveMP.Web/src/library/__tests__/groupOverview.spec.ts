import { describe, expect, it } from 'vitest'
import type { ListGroup } from '@/api/generated/models'
import { groupTracksByDisc } from '../grouping'
import {
    discOverviewGroups,
    gridMove,
    groupIndexAt,
    letterBarMinimum,
    overviewLetters,
    showsLetterBar,
    tileOverviewGroups,
    trackOverviewGroups,
    type OverviewGroup,
} from '../groupOverview'

const album = { id: 'album-1', title: 'Blue', artist: { id: 'artist-1', name: 'Band' } }

describe('trackOverviewGroups', () => {
    it('makes albums, songs without album of an artist and unknown songs', () => {
        const groups: ListGroup[] = [
            { kind: 'Album', position: 0, count: 12, album, hasCover: true, letter: 'B' },
            {
                kind: 'Artist',
                position: 12,
                count: 3,
                artist: { id: 'artist-2', name: 'Solo' },
            },
            { kind: 'Unknown', position: 15, count: 1 },
        ]

        expect(trackOverviewGroups(groups)).toEqual([
            {
                id: 'group-0-0',
                kind: 'album',
                position: 0,
                count: 12,
                album,
                hasCover: true,
                letter: 'B',
            },
            {
                id: 'group-12-1',
                kind: 'unknown',
                unknown: 'album',
                position: 12,
                count: 3,
                artist: { id: 'artist-2', name: 'Solo' },
                letter: undefined,
            },
            {
                id: 'group-15-2',
                kind: 'unknown',
                unknown: 'album',
                position: 15,
                count: 1,
                artist: undefined,
                letter: undefined,
            },
        ])
    })
})

describe('tileOverviewGroups', () => {
    it('makes artists, letters and years, and names what an unknown group misses', () => {
        const groups: ListGroup[] = [
            { kind: 'Artist', position: 0, count: 2, artist: { id: 'a', name: 'A band' } },
            { kind: 'Letter', position: 2, count: 4, letter: 'C' },
            { kind: 'Year', position: 6, count: 1, year: 1999 },
            { kind: 'Unknown', position: 7, count: 5 },
        ]

        const byArtist = tileOverviewGroups(groups, 'artist')
        expect(byArtist.map((group) => [group.kind, group.position, group.count])).toEqual([
            ['artist', 0, 2],
            ['letter', 2, 4],
            ['year', 6, 1],
            ['unknown', 7, 5],
        ])
        expect(byArtist[0]?.artist?.name).toBe('A band')
        expect(byArtist[1]?.letter).toBe('C')
        expect(byArtist[2]?.year).toBe(1999)
        expect(byArtist[3]?.unknown).toBe('artist')
        expect(tileOverviewGroups(groups, 'year')[3]?.unknown).toBe('year')
    })

    it('treats an artist or year group without its value as unknown', () => {
        const groups: ListGroup[] = [
            { kind: 'Artist', position: 0, count: 2 },
            { kind: 'Year', position: 2, count: 1 },
        ]

        expect(tileOverviewGroups(groups, 'artist').map((group) => group.unknown)).toEqual([
            'artist',
            'year',
        ])
    })
})

describe('discOverviewGroups', () => {
    it('makes one group per disc with its position and number of songs', () => {
        const tracks = [
            { id: 'a', discNumber: 1 },
            { id: 'b', discNumber: 2 },
            { id: 'c', discNumber: 1 },
            { id: 'd', discNumber: 2 },
            { id: 'e', discNumber: 2 },
        ]

        expect(
            discOverviewGroups(groupTracksByDisc(tracks, 'Album')).map((group) => [
                group.kind,
                group.disc,
                group.position,
                group.count,
            ]),
        ).toEqual([
            ['disc', 1, 0, 2],
            ['disc', 2, 2, 3],
        ])
    })
})

function groupsAt(...ranges: [number, number][]): OverviewGroup[] {
    return ranges.map(([position, count], index) => ({
        id: `g${index}`,
        kind: 'letter',
        position,
        count,
    }))
}

describe('groupIndexAt', () => {
    it('finds the group whose entries contain the position', () => {
        const groups = groupsAt([0, 3], [3, 1], [4, 10], [14, 2])
        expect(groupIndexAt(groups, 0)).toBe(0)
        expect(groupIndexAt(groups, 2)).toBe(0)
        expect(groupIndexAt(groups, 3)).toBe(1)
        expect(groupIndexAt(groups, 9)).toBe(2)
        expect(groupIndexAt(groups, 15)).toBe(3)
    })

    it('finds nothing outside the groups or without a position', () => {
        const groups = groupsAt([0, 3], [3, 1])
        expect(groupIndexAt(groups, 4)).toBe(-1)
        expect(groupIndexAt(groups, undefined)).toBe(-1)
        expect(groupIndexAt([], 0)).toBe(-1)
    })

    it('stays fast for thousands of groups', () => {
        const groups = groupsAt(
            ...Array.from({ length: 5000 }, (_, i): [number, number] => [i * 3, 3]),
        )
        expect(groupIndexAt(groups, 14_999)).toBe(4999)
        expect(groupIndexAt(groups, 7_500)).toBe(2500)
    })
})

function lettered(count: number, letters: string): OverviewGroup[] {
    return Array.from({ length: count }, (_, index) => ({
        id: `g${index}`,
        kind: 'album',
        position: index,
        count: 1,
        letter: letters[index % letters.length],
    }))
}

describe('showsLetterBar', () => {
    it('needs 30 groups with at least two letters', () => {
        expect(letterBarMinimum).toBe(30)
        expect(showsLetterBar(lettered(29, 'AB'))).toBe(false)
        expect(showsLetterBar(lettered(30, 'AB'))).toBe(true)
        expect(showsLetterBar(lettered(30, 'A'))).toBe(false)
        expect(
            showsLetterBar(groupsAt(...Array.from({ length: 40 }, (): [number, number] => [0, 1]))),
        ).toBe(false)
    })
})

describe('overviewLetters', () => {
    it('lists A to Z and # with the first group of each letter', () => {
        const groups = lettered(6, 'AAC#C#')
        const letters = overviewLetters(groups)

        expect(letters.map((entry) => entry.letter).join('')).toBe('ABCDEFGHIJKLMNOPQRSTUVWXYZ#')
        expect(letters.find((entry) => entry.letter === 'A')?.index).toBe(0)
        expect(letters.find((entry) => entry.letter === 'B')?.index).toBe(-1)
        expect(letters.find((entry) => entry.letter === 'C')?.index).toBe(2)
        expect(letters.find((entry) => entry.letter === '#')?.index).toBe(3)
    })
})

describe('gridMove', () => {
    const layout = { count: 10, columns: 3, rows: 2 }

    it('moves by tiles and rows with the arrow keys and stays at the edges', () => {
        expect(gridMove('ArrowRight', 4, layout)).toBe(5)
        expect(gridMove('ArrowLeft', 4, layout)).toBe(3)
        expect(gridMove('ArrowDown', 4, layout)).toBe(7)
        expect(gridMove('ArrowUp', 4, layout)).toBe(1)
        expect(gridMove('ArrowLeft', 0, layout)).toBe(0)
        expect(gridMove('ArrowRight', 9, layout)).toBe(9)
        expect(gridMove('ArrowUp', 1, layout)).toBe(1)
        // Below the tile 8 there is none: it stays.
        expect(gridMove('ArrowDown', 8, layout)).toBe(8)
    })

    it('moves to the ends and by the rows in view', () => {
        expect(gridMove('Home', 5, layout)).toBe(0)
        expect(gridMove('End', 5, layout)).toBe(9)
        expect(gridMove('PageDown', 1, layout)).toBe(7)
        expect(gridMove('PageDown', 7, layout)).toBe(9)
        expect(gridMove('PageUp', 8, layout)).toBe(2)
        expect(gridMove('PageUp', 2, layout)).toBe(0)
    })

    it('ignores other keys and empty grids', () => {
        expect(gridMove('Enter', 1, layout)).toBeNull()
        expect(gridMove('ArrowDown', 0, { count: 0, columns: 1, rows: 1 })).toBeNull()
    })
})
