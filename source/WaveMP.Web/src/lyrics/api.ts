// Requests of the lyrics. The only place of the web app that calls the lyrics endpoints, so the
// player store and its tests can replace it as a whole (vi.mock('@/lyrics/api')).

import { apiClient } from '@/api/client'
import {
    LyricsPublicationObject,
    ProblemCodeObject,
    type LyricsPublication,
} from '@/api/generated/models'
import { problemCodeOf, statusOf } from '@/api/problems'
import { toTrackLyrics, type TrackLyrics } from './timing'

/**
 * The lyrics of a track, or null if it has none (lyrics.not_found). Other errors, also an unknown
 * track, reject.
 */
export async function getLyrics(trackId: string): Promise<TrackLyrics | null> {
    try {
        const lyrics = await apiClient.api.v1.tracks.byId(trackId).lyrics.get()
        return lyrics ? toTrackLyrics(trackId, lyrics) : null
    } catch (error) {
        if (statusOf(error) === 404 && problemCodeOf(error) === ProblemCodeObject.LyricsNot_found) {
            return null
        }

        throw error
    }
}

/**
 * Downloads the lyrics of a track from the online lyrics service (admins, while the download is
 * switched on) and returns the lyrics the track has afterwards. Errors carry the problem code, for
 * example lyrics.download_not_found or lyrics.service_unavailable.
 */
export async function downloadLyrics(trackId: string): Promise<TrackLyrics | null> {
    const lyrics = await apiClient.api.v1.tracks.byId(trackId).lyrics.download.post()
    return lyrics ? toTrackLyrics(trackId, lyrics) : null
}

/** A track of the online lyrics service an admin can choose. */
export interface LyricsMatch {
    /** Id of the track at the lyrics service. */
    readonly id: number
    readonly title: string
    readonly artist: string | null
    readonly album: string | null
    /** Duration in seconds, or null if unknown. */
    readonly duration: number | null
    /** Whether its lyrics have times. */
    readonly synchronized: boolean
    /** Whether its duration fits the song; otherwise only its plain lyrics are kept. */
    readonly fitsDuration: boolean
}

/** What a search of the online lyrics service looked for and found. */
export interface LyricsSearch {
    readonly title: string
    readonly artist: string | null
    readonly matches: readonly LyricsMatch[]
}

/**
 * Searches the online lyrics service for the lyrics of a track (admins, while the download is
 * switched on). Without title and artist the server takes those of the track and names them in the
 * answer; an empty artist searches by title only. Errors carry the problem code.
 */
export async function searchLyrics(
    trackId: string,
    names?: { readonly title: string; readonly artist: string },
): Promise<LyricsSearch> {
    const found = await apiClient.api.v1.tracks
        .byId(trackId)
        .lyrics.onlineMatches.get(
            names
                ? { queryParameters: { title: names.title.trim(), artist: names.artist.trim() } }
                : undefined,
        )
    return {
        title: found?.title ?? names?.title ?? '',
        artist: found?.artist ?? null,
        matches: (found?.matches ?? []).flatMap<LyricsMatch>((match) =>
            typeof match.id === 'number'
                ? [
                      {
                          id: match.id,
                          title: match.title ?? '',
                          artist: match.artist ?? null,
                          album: match.album ?? null,
                          duration: match.durationSeconds ?? null,
                          synchronized: match.synchronized === true,
                          fitsDuration: match.fitsDuration !== false,
                      },
                  ]
                : [],
        ),
    }
}

/**
 * Gives a track the lyrics of a track of the online lyrics service an admin chose; they replace a
 * lyrics file. Returns the lyrics the track has afterwards.
 */
export async function chooseLyrics(trackId: string, matchId: number): Promise<TrackLyrics | null> {
    const lyrics = await apiClient.api.v1.tracks.byId(trackId).lyrics.onlineMatch.put({ matchId })
    return lyrics ? toTrackLyrics(trackId, lyrics) : null
}

/** What an upload of lyrics did. */
export interface LyricsUploadResult {
    readonly lyrics: TrackLyrics | null
    readonly publication: LyricsPublication
}

/**
 * Saves own lyrics of a track (LRC with times or plain text) for admins; they replace a lyrics
 * file. On request they are also published to the online lyrics service.
 */
export async function uploadLyrics(
    trackId: string,
    text: string,
    publish: boolean,
): Promise<LyricsUploadResult> {
    const upload = await apiClient.api.v1.tracks.byId(trackId).lyrics.put({ text, publish })
    return {
        lyrics: upload?.lyrics ? toTrackLyrics(trackId, upload.lyrics) : null,
        publication: upload?.publication ?? LyricsPublicationObject.NotRequested,
    }
}
