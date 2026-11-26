// Presentation of the library: message keys and data-state tokens for tracks, artists and files.
// Pure functions only.

import type { ArtistReference, Track } from '@/api/generated/models'

/**
 * Message key for an artist without name: "Various artists" for compilations, otherwise "Unknown
 * artist". Null if the artist has a name to show.
 */
export function artistLabelKey(
    artist: ArtistReference | null | undefined,
    isCompilation = false,
): string | null {
    if (artist?.name) {
        return null
    }

    return isCompilation ? 'library.variousArtists' : 'library.unknownArtist'
}

/** The same artist: the same id if both have one, otherwise the same name; two unknown are the same. */
function sameArtist(
    first: ArtistReference | null | undefined,
    second: ArtistReference | null | undefined,
): boolean {
    if (first?.id && second?.id) {
        return first.id === second.id
    }

    const name = (artist: ArtistReference | null | undefined) =>
        (artist?.name ?? '').trim().toLocaleLowerCase()
    return name(first) === name(second)
}

/** What a list knows about the group of a track: the album artist and whether it is a compilation. */
export interface TrackArtistContext {
    readonly artist: ArtistReference | null
    readonly isCompilation: boolean
    /** The one group of a plain list, which has no album artist to go by. */
    readonly untitled?: boolean
}

/**
 * Whether a list names the artist of a track below its title (part list-subtitle with the state
 * "other-artist"), for phones, which leave out the column of the artist: in a compilation, in a
 * plain list and whenever the track has another artist than its group (the album artist).
 */
export function namesTrackArtist(track: Pick<Track, 'artist'>, group: TrackArtistContext): boolean {
    return group.untitled === true || group.isCompilation || !sameArtist(track.artist, group.artist)
}

/** Duration in seconds from milliseconds; null if unknown. */
export function durationSeconds(milliseconds: number | null | undefined): number | null {
    return typeof milliseconds === 'number' && Number.isFinite(milliseconds)
        ? Math.max(0, milliseconds / 1000)
        : null
}

/**
 * data-state tokens of the row and the video badge of a track: "has-video" for every track with a
 * video, additionally "video-only" if it has no audio.
 */
export function trackRowState(track: Pick<Track, 'hasAudio' | 'hasVideo'>): string[] {
    if (!track.hasVideo) {
        return []
    }

    return track.hasAudio ? ['has-video'] : ['has-video', 'video-only']
}

/**
 * data-state of a list of tracks: the given tokens plus "has-video" if one of the tracks has a
 * video, so the list keeps a column for the video badge before the titles. Undefined without tokens.
 */
export function trackListState(
    tracks: readonly Pick<Track, 'hasVideo'>[],
    state?: string,
): string | undefined {
    const tokens = (state ?? '').split(' ').filter((token) => token && token !== 'has-video')
    if (tracks.some((track) => track.hasVideo)) {
        tokens.push('has-video')
    }

    return tokens.length > 0 ? tokens.join(' ') : undefined
}

/** Message key of how a track can be played: audio and video, audio only or video only. */
export function mediaKey(track: Pick<Track, 'hasAudio' | 'hasVideo'>): string {
    if (track.hasAudio && track.hasVideo) {
        return 'track.media.audioAndVideo'
    }

    return track.hasVideo ? 'track.media.video' : 'track.media.audio'
}

const fileKinds: Record<string, string> = {
    Audio: 'audio',
    Video: 'video',
    Image: 'image',
    Lyrics: 'lyrics',
}

/** data-state token of the kind of a file: audio, video, image or lyrics. */
export function fileKindToken(kind: string | null | undefined): string {
    return fileKinds[kind ?? ''] ?? 'audio'
}

/** Message key of the kind of a file. */
export function fileKindKey(kind: string | null | undefined): string {
    return `files.kinds.${fileKindToken(kind)}`
}
