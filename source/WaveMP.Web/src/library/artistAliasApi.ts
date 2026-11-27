// Requests of the aliases of artists, the other names the search finds an artist by. The only
// place of the web app that calls these endpoints, so components and tests can replace it as a
// whole (vi.mock('@/library/artistAliasApi')).

import { RetryHandlerOptions } from '@microsoft/kiota-http-fetchlibrary'
import { apiClient } from '@/api/client'
import type { ArtistAlias, ArtistAliasPage } from '@/api/generated/models'

/** Aliases per page; one page holds the aliases of an artist. */
export const artistAliasPageSize = 100

/** The longest alias the server takes. */
export const artistAliasMaxLength = 512

/**
 * The server already repeats a request that MusicBrainz refuses as busy and then answers itself
 * (artist.picture_lookup_unavailable); the client does not repeat it again.
 */
const once = { options: [new RetryHandlerOptions({ maxRetries: 0 })] }

/** The aliases of an artist, sorted by name. */
export function listArtistAliases(id: string): Promise<ArtistAliasPage | undefined> {
    return apiClient.api.v1.artists
        .byId(id)
        .aliases.get({ queryParameters: { offset: 0, limit: artistAliasPageSize } })
}

/** Adds an alias of the admin; the server cleans the name like names from tags. */
export function addArtistAlias(id: string, name: string): Promise<ArtistAlias | undefined> {
    return apiClient.api.v1.artists.byId(id).aliases.post({ name })
}

/** Removes an alias; one taken from MusicBrainz is not taken again by later lookups. */
export async function removeArtistAlias(id: string, aliasId: string): Promise<void> {
    await apiClient.api.v1.artists.byId(id).aliases.byAliasId(aliasId).delete()
}

/**
 * Takes the sort name and the aliases of the MusicBrainz artist of the artist, replacing those
 * taken before. Answers with all aliases of the artist.
 */
export function importArtistAliases(id: string): Promise<ArtistAliasPage | undefined> {
    return apiClient.api.v1.artists.byId(id).aliases.musicbrainzImports.post(once)
}
