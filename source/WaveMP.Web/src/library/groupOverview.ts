// The overview of the groups of a list (the quick jump): its groups made from the groups of the
// API or of a list that is loaded completely, the group a position belongs to, the letter bar and
// the keyboard movement in its grid. Pure functions only; the overview translates the texts.

import type { AlbumReference, ArtistReference, ListGroup } from '@/api/generated/models'
import type { TrackGroup } from './grouping'
import { indexLetters } from './indexLetters'

/** What the entries of a group have in common (data-kind of group-tile). */
export type OverviewGroupKind = 'album' | 'artist' | 'letter' | 'year' | 'disc' | 'unknown'

/** What an unknown group is unknown for: the album, the album artist or the year. */
export type UnknownGroupKind = 'album' | 'artist' | 'year'

/** What the entries of the list are, for the number of entries of a group. */
export type OverviewUnit = 'songs' | 'albums' | 'artists'

export interface OverviewGroup {
    /** Unique in the overview. */
    readonly id: string
    readonly kind: OverviewGroupKind
    /** Position of the first entry of the group in the whole list (0-based). */
    readonly position: number
    /** Number of entries of the group. */
    readonly count: number
    /** The album of a group of the kind "album". */
    readonly album?: AlbumReference
    /** Whether the album of the group has a cover. */
    readonly hasCover?: boolean
    /**
     * The artist of a group of the kind "artist", or the artist of the songs without album of an
     * "unknown" group of a list of songs.
     */
    readonly artist?: ArtistReference
    /** Index letter (A to Z or #) of the text the list is sorted by, for the letter bar. */
    readonly letter?: string
    readonly year?: number
    readonly disc?: number
    /** What an "unknown" group is unknown for. */
    readonly unknown?: UnknownGroupKind
}

/** Groups from this number on get the letter bar (if their letters differ). */
export const letterBarMinimum = 30

function base(group: ListGroup, index: number) {
    const position = group.position ?? 0
    return {
        id: `group-${position}-${index}`,
        position,
        count: group.count ?? 0,
        letter: group.letter ?? undefined,
    }
}

/**
 * The groups of a list of songs (GET /api/v1/tracks/groups): albums, the songs without album of
 * one artist (shown as unknown album with the artist) and the songs without album and artist.
 */
export function trackOverviewGroups(groups: readonly ListGroup[]): OverviewGroup[] {
    return groups.map((group, index) => {
        const common = base(group, index)
        if (group.kind === 'Album' && group.album) {
            return {
                ...common,
                kind: 'album',
                album: group.album,
                hasCover: group.hasCover ?? false,
            }
        }

        return {
            ...common,
            kind: 'unknown',
            unknown: 'album',
            artist: group.kind === 'Artist' ? (group.artist ?? undefined) : undefined,
        }
    })
}

/**
 * The groups of album or artist tiles (GET /api/v1/albums/groups, /api/v1/artists/groups): album
 * artists, letters or years. unknown tells what a group of the kind Unknown misses, after the sort
 * order of the list (the album artist when sorted by artist, the year when sorted by year).
 */
export function tileOverviewGroups(
    groups: readonly ListGroup[],
    unknown: UnknownGroupKind,
): OverviewGroup[] {
    return groups.map((group, index): OverviewGroup => {
        const common = base(group, index)
        switch (group.kind) {
            case 'Artist':
                return group.artist
                    ? { ...common, kind: 'artist', artist: group.artist }
                    : { ...common, kind: 'unknown', unknown: 'artist' }
            case 'Letter':
                return { ...common, kind: 'letter' }
            case 'Year':
                return typeof group.year === 'number'
                    ? { ...common, kind: 'year', year: group.year }
                    : { ...common, kind: 'unknown', unknown: 'year' }
            default:
                return { ...common, kind: 'unknown', unknown }
        }
    })
}

/** The discs of an album page, all of whose songs are loaded. */
export function discOverviewGroups(groups: readonly TrackGroup[]): OverviewGroup[] {
    return groups.map((group) => ({
        id: `group-${group.position}`,
        kind: 'disc',
        position: group.position,
        count: group.tracks.length,
        disc: group.disc,
    }))
}

/** Index of the group the entry at the position belongs to; -1 if none (groups in list order). */
export function groupIndexAt(
    groups: readonly OverviewGroup[],
    position: number | undefined,
): number {
    if (position === undefined) {
        return -1
    }

    let low = 0
    let high = groups.length - 1
    while (low <= high) {
        const middle = Math.floor((low + high) / 2)
        const group = groups[middle]!
        if (position < group.position) {
            high = middle - 1
        } else if (position >= group.position + Math.max(group.count, 1)) {
            low = middle + 1
        } else {
            return middle
        }
    }

    return -1
}

/** Whether the overview shows the letter bar: many groups with at least two different letters. */
export function showsLetterBar(groups: readonly OverviewGroup[]): boolean {
    if (groups.length < letterBarMinimum) {
        return false
    }

    const letters = new Set(groups.flatMap((group) => (group.letter ? [group.letter] : [])))
    return letters.size >= 2
}

export interface OverviewLetter {
    /** A to Z or #. */
    readonly letter: string
    /** Index of the first group with the letter; -1 if no group has it. */
    readonly index: number
}

/** The letters of the letter bar, A to Z and #, each with the first group that has it. */
export function overviewLetters(groups: readonly OverviewGroup[]): OverviewLetter[] {
    const first = new Map<string, number>()
    groups.forEach((group, index) => {
        if (group.letter && !first.has(group.letter)) {
            first.set(group.letter, index)
        }
    })

    return indexLetters.map((letter) => ({ letter, index: first.get(letter) ?? -1 }))
}

/** The layout of the grid of the overview, for the arrow keys. */
export interface GridLayout {
    /** Number of groups. */
    readonly count: number
    /** Tiles per row (at least 1). */
    readonly columns: number
    /** Rows that fit into the visible part of the grid (at least 1), for Page Up and Page Down. */
    readonly rows: number
}

/**
 * The tile a key moves the focus to in the grid of the overview: the arrow keys to the neighbor
 * (up and down by a row), Home and End to the first and last tile, Page Up and Page Down by the
 * rows that fit. Stays at the edges; null for any other key.
 */
export function gridMove(key: string, index: number, layout: GridLayout): number | null {
    const last = layout.count - 1
    if (last < 0) {
        return null
    }

    const columns = Math.max(1, layout.columns)
    const page = columns * Math.max(1, layout.rows)
    const clamp = (value: number) => Math.min(last, Math.max(0, value))
    switch (key) {
        case 'ArrowRight':
            return clamp(index + 1)
        case 'ArrowLeft':
            return clamp(index - 1)
        case 'ArrowDown':
            return index + columns <= last ? index + columns : index
        case 'ArrowUp':
            return index - columns >= 0 ? index - columns : index
        case 'Home':
            return 0
        case 'End':
            return last
        case 'PageDown':
            return clamp(index + page)
        case 'PageUp':
            return clamp(index - page)
        default:
            return null
    }
}
