// Groups of tracks for the list grouped by album. Pure functions only; the views translate the
// texts (unknown album, discs).

import type { Album, ArtistCredit, ArtistReference, Track } from '@/api/generated/models'
import { artistsOf } from './artists'

/** Cover shown for a group: the cover of its album, or of one of its tracks without album. */
export interface GroupCover {
    readonly kind: 'album' | 'track'
    readonly id: string
    readonly available: boolean
}

export interface TrackGroup {
    /** Stable key: the album id, otherwise the artist of tracks without album. */
    readonly id: string
    /**
     * Position of the first track of the group in the whole list (0-based), also when only a
     * window of the list is loaded; a group cut at the start of the window starts at its first
     * loaded track.
     */
    readonly position: number
    readonly albumId?: string
    /** Album title; null for tracks without album (the view shows "Unknown album"). */
    readonly title: string | null
    /** Disc number of a group of one disc of an album (the view shows "Disc n"). */
    readonly disc?: number
    /** The one group of a plain list: it has no heading and no cover or details. */
    readonly untitled?: boolean
    /** Album artist, for tracks without album the artist of the first track. */
    readonly artist: ArtistReference | null
    /** All album artists, for tracks without album all artists of the first track. */
    readonly artists: readonly ArtistCredit[]
    readonly isCompilation: boolean
    readonly year: number | null
    readonly genre: string | null
    readonly cover: GroupCover | null
    readonly tracks: readonly Track[]
}

function groupKey(track: Track): string {
    if (track.album?.id) {
        return `album:${track.album.id}`
    }

    return track.artist?.id ? `artist:${track.artist.id}` : 'unknown'
}

function createGroup(key: string, track: Track, position: number): TrackGroup {
    // A missing album may arrive as an object without id.
    const album = track.album?.id ? track.album : null
    return {
        id: key,
        position,
        albumId: album?.id ?? undefined,
        title: album?.title ?? null,
        artist: album ? (album.artist ?? null) : (track.artist ?? null),
        artists: artistsOf(album ?? track),
        isCompilation: album?.isCompilation ?? false,
        year: track.year ?? null,
        genre: track.genre ?? null,
        cover: null,
        tracks: [],
    }
}

function coverOf(group: TrackGroup, tracks: readonly Track[]): GroupCover | null {
    const withCover = tracks.find((track) => track.hasCover)
    if (group.albumId) {
        return { kind: 'album', id: group.albumId, available: !!withCover }
    }

    return withCover?.id ? { kind: 'track', id: withCover.id, available: true } : null
}

/**
 * Groups consecutive tracks of the same album; tracks without album are grouped by their artist.
 * The order of the tracks stays, so a list sorted by album gets one group per album. Year and genre
 * of a group come from its first track that has one. start is the position of the first track in
 * the whole list, when the tracks are a window of it.
 */
export function groupTracksByAlbum(tracks: readonly Track[], start = 0): TrackGroup[] {
    const groups: { group: TrackGroup; tracks: Track[] }[] = []
    tracks.forEach((track, index) => {
        const key = groupKey(track)
        const last = groups[groups.length - 1]
        if (last && last.group.id === key) {
            last.tracks.push(track)
        } else {
            groups.push({ group: createGroup(key, track, start + index), tracks: [track] })
        }
    })

    // Keys repeat when an album appears twice in a list sorted otherwise; the ids must stay unique.
    const seen = new Map<string, number>()
    return groups.map(({ group, tracks: groupTracks }) => {
        const count = seen.get(group.id) ?? 0
        seen.set(group.id, count + 1)
        return {
            ...group,
            id: count === 0 ? group.id : `${group.id}:${count}`,
            year: groupTracks.find((track) => track.year)?.year ?? null,
            genre: groupTracks.find((track) => track.genre)?.genre ?? null,
            cover: coverOf(group, groupTracks),
            tracks: groupTracks,
        }
    })
}

/**
 * The tracks of an album page: one group with the album title, or one group per disc as soon as
 * the album has more than one disc, in the order of the discs. The groups take the artist and the
 * compilation flag of the album, if given, so the list can tell the songs of other artists.
 */
export function groupTracksByDisc(
    tracks: readonly Track[],
    albumTitle: string,
    album?: Pick<Album, 'artist' | 'artists' | 'isCompilation'>,
): TrackGroup[] {
    const discs = [...new Set(tracks.map((track) => track.discNumber ?? 1))]
    const base: Omit<TrackGroup, 'id' | 'position' | 'tracks'> = {
        title: albumTitle,
        artist: album?.artist ?? null,
        artists: artistsOf(album),
        isCompilation: album?.isCompilation ?? false,
        year: null,
        genre: null,
        cover: null,
    }

    if (discs.length < 2) {
        return [{ ...base, id: 'album', position: 0, tracks }]
    }

    // The position counts the tracks of the discs shown before, in the order of the discs.
    let position = 0
    return discs
        .sort((a, b) => a - b)
        .map((disc) => {
            const discTracks = tracks.filter((track) => (track.discNumber ?? 1) === disc)
            const group = { ...base, id: `disc:${disc}`, position, disc, tracks: discTracks }
            position += discTracks.length
            return group
        })
}

/**
 * The tracks as a plain list: one group without heading, cover and details that holds all tracks
 * in their order (the view "List" of the view options). No group for an empty list. start is the
 * position of the first track in the whole list, when the tracks are a window of it.
 */
export function groupTracksAsList(tracks: readonly Track[], start = 0): TrackGroup[] {
    if (tracks.length === 0) {
        return []
    }

    return [
        {
            id: 'list',
            position: start,
            title: null,
            untitled: true,
            artist: null,
            artists: [],
            isCompilation: false,
            year: null,
            genre: null,
            cover: null,
            tracks,
        },
    ]
}
