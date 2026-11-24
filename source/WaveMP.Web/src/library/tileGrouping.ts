// Groups of the album and artist tiles of the library, the same as the server forms them
// (GET /api/v1/albums/groups, /api/v1/artists/groups): runs of consecutive entries with the same
// key, in the order of the list. Pure functions only; the views translate the texts.

import type { Album, Artist, ArtistReference } from '@/api/generated/models'
import { indexLetterOf } from './indexLetters'
import type { SortState } from './sorting'
import { effectiveSort } from './sorting'

/** What the entries of a group have in common. */
export type TileGroupKind = 'artist' | 'letter' | 'year'

/** The key of an entry and what its group shows. */
export interface TileGroupKey {
    /** Equal for the entries of one group. */
    readonly key: string
    readonly kind: TileGroupKind
    /** Album artist of a group of the kind "artist"; none for the albums without album artist. */
    readonly artist?: ArtistReference
    /** Index letter (A to Z or #) of a group of the kind "letter". */
    readonly letter?: string
    /** Year of a group of the kind "year"; none for the albums without year. */
    readonly year?: number
}

/** How the entries of a list are grouped: the key of each entry. */
export type TileGrouping<T> = (item: T) => TileGroupKey

export interface TileGroup<T> extends Omit<TileGroupKey, 'key'> {
    /** Unique in the list, also when a key appears in two runs. */
    readonly id: string
    /** Position of the first entry of the group in the whole list (0-based). */
    readonly position: number
    readonly items: readonly T[]
}

function albumByArtist(album: Album): TileGroupKey {
    const artist = album.artist?.id ? album.artist : null
    return artist
        ? { key: `artist:${artist.id}`, kind: 'artist', artist }
        : { key: 'artist:unknown', kind: 'artist' }
}

function byLetter(text: string | null | undefined): TileGroupKey {
    const letter = indexLetterOf(text)
    return { key: `letter:${letter}`, kind: 'letter', letter }
}

function albumByYear(album: Album): TileGroupKey {
    return typeof album.year === 'number'
        ? { key: `year:${album.year}`, kind: 'year', year: album.year }
        : { key: 'year:unknown', kind: 'year' }
}

/**
 * The grouping of the album tiles in the sort order: by album artist (Artist, the default), by
 * the letter of the title (Title) or by year (Year); none for the other orders.
 */
export function albumTileGrouping(
    state: SortState,
    hasSearch: boolean,
): TileGrouping<Album> | undefined {
    switch (effectiveSort('albums', state, hasSearch).sort) {
        case 'Artist':
            return albumByArtist
        case 'Title':
            return (album) => byLetter(album.title)
        case 'Year':
            return albumByYear
        default:
            return undefined
    }
}

/** The grouping of the artist tiles: by the letter of the name (Name, the default), else none. */
export function artistTileGrouping(
    state: SortState,
    hasSearch: boolean,
): TileGrouping<Artist> | undefined {
    return effectiveSort('artists', state, hasSearch).sort === 'Name'
        ? (artist) => byLetter(artist.name)
        : undefined
}

/**
 * Groups consecutive entries with the same key. start is the position of the first entry in the
 * whole list, when the entries are a window of it.
 */
export function groupTiles<T>(
    items: readonly T[],
    grouping: TileGrouping<T>,
    start = 0,
): TileGroup<T>[] {
    const runs: { key: string; group: Omit<TileGroup<T>, 'id' | 'items'>; items: T[] }[] = []
    items.forEach((item, index) => {
        const { key, ...group } = grouping(item)
        const last = runs[runs.length - 1]
        if (last && last.key === key) {
            last.items.push(item)
        } else {
            runs.push({ key, group: { ...group, position: start + index }, items: [item] })
        }
    })

    const seen = new Map<string, number>()
    return runs.map(({ key, group, items: groupItems }) => {
        const count = seen.get(key) ?? 0
        seen.set(key, count + 1)
        return { ...group, id: count === 0 ? key : `${key}:${count}`, items: groupItems }
    })
}
