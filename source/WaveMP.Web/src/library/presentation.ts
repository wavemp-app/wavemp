// Presentation of the library: message keys and data-state tokens for tracks, artists and files.
// Pure functions only.

import type { ArtistCredit, ArtistReference, Track } from '@/api/generated/models'
import { artistsOf, artistText, sameArtists, type ArtistSubject } from './artists'

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

/** What a list knows about the group of a track: the album artist and whether it is a compilation. */
export interface TrackArtistContext {
    readonly artist: ArtistReference | null
    /** All album artists of the group; the artist alone if missing. */
    readonly artists?: readonly ArtistCredit[]
    readonly isCompilation: boolean
    /** The one group of a plain list, which has no album artist to go by. */
    readonly untitled?: boolean
}

/**
 * Whether a list names the artist of a track below its title (part list-subtitle with the state
 * "other-artist"), for phones, which leave out the column of the artist: in a compilation, in a
 * plain list and whenever the track has other artists than its group (the album artists), also
 * when a guest joins them.
 */
export function namesTrackArtist(
    track: Pick<Track, 'artist' | 'artists'>,
    group: TrackArtistContext,
): boolean {
    if (group.untitled === true || group.isCompilation) {
        return true
    }

    const groupArtists = group.artists?.length ? group.artists : artistsOf({ artist: group.artist })
    return !sameArtists(artistsOf(track), groupArtists)
}

/**
 * The artists of a song or album as one text, e.g. "Simon & Garfunkel, AC/DC feat. K.Flay", for
 * places that show text only; "Unknown artist" ("Various artists" for compilations) without one.
 */
export function artistLine(
    subject: ArtistSubject | null | undefined,
    translate: (key: string) => string,
    isCompilation = false,
): string {
    const artists = artistsOf(subject)
    if (artists.length === 0) {
        return translate(isCompilation ? 'library.variousArtists' : 'library.unknownArtist')
    }

    return artistText(artists, {
        list: translate('artists.separator'),
        featuring: translate('artists.featuring'),
    })
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
