// Requests of the lyrics. The only place of the web app that calls the lyrics endpoints, so the
// player store and its tests can replace it as a whole (vi.mock('@/lyrics/api')).

import { apiClient } from '@/api/client'
import { ProblemCodeObject } from '@/api/generated/models'
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
