// Requests of the stored results of the online lyrics lookup, for admins. The only place of the web
// app that calls these endpoints, so components and their tests can replace it as a whole
// (vi.mock('@/lyrics/lookupApi')).

import { apiClient } from '@/api/client'
import { ProblemCodeObject } from '@/api/generated/models'
import type { LyricsLookup, LyricsLookupSummary } from '@/api/generated/models'
import { problemCodeOf, statusOf } from '@/api/problems'

/** How many songs have a stored lookup that found no lyrics, and the wait before the next one. */
export async function getLyricsLookupSummary(): Promise<LyricsLookupSummary> {
    const summary = await apiClient.api.v1.lyricsLookups.summary.get()
    if (!summary) {
        throw new Error('The summary of the lyrics lookups is missing.')
    }

    return summary
}

/** Forgets the lookups of all songs that found no lyrics. */
export async function forgetLyricsLookups(): Promise<void> {
    await apiClient.api.v1.lyricsLookups.delete()
}

/**
 * The stored result of the last lookup of a song, or null if the song was never looked up or its
 * result was forgotten (lyrics.lookup_not_found). Other errors, also an unknown song, reject.
 */
export async function getLyricsLookup(trackId: string): Promise<LyricsLookup | null> {
    try {
        return (await apiClient.api.v1.tracks.byId(trackId).lyrics.lookup.get()) ?? null
    } catch (error) {
        if (
            statusOf(error) === 404 &&
            problemCodeOf(error) === ProblemCodeObject.LyricsLookup_not_found
        ) {
            return null
        }

        throw error
    }
}

/**
 * Forgets the lookup of a song that found no lyrics. Errors carry the problem code, for example
 * lyrics.lookup_found_lyrics.
 */
export async function forgetLyricsLookup(trackId: string): Promise<void> {
    await apiClient.api.v1.tracks.byId(trackId).lyrics.lookup.delete()
}
