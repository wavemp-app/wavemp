// Requests of the smart playlists. The only place of the web app that calls their endpoints, so
// components and tests can replace it as a whole (vi.mock('@/playlists/smartApi')).

import { apiClient } from '@/api/client'
import type { SmartPlaylistsRequestBuilderGetQueryParameters } from '@/api/generated/api/v1/me/smartPlaylists'
import type {
    Playlist,
    SaveSmartPlaylist,
    SmartPlaylist,
    SmartPlaylistPage,
    SmartPlaylistTemplatePage,
    TrackPage,
} from '@/api/generated/models'

export type SmartPlaylistQuery = SmartPlaylistsRequestBuilderGetQueryParameters

function smartPlaylist(id: string) {
    return apiClient.api.v1.me.smartPlaylists.byId(id)
}

export function listSmartPlaylists(
    query: SmartPlaylistQuery,
): Promise<SmartPlaylistPage | undefined> {
    return apiClient.api.v1.me.smartPlaylists.get({ queryParameters: query })
}

export function getSmartPlaylist(id: string): Promise<SmartPlaylist | undefined> {
    return smartPlaylist(id).get()
}

export function createSmartPlaylist(body: SaveSmartPlaylist): Promise<SmartPlaylist | undefined> {
    return apiClient.api.v1.me.smartPlaylists.post(body)
}

export function updateSmartPlaylist(
    id: string,
    body: SaveSmartPlaylist,
): Promise<SmartPlaylist | undefined> {
    return smartPlaylist(id).put(body)
}

export async function deleteSmartPlaylist(id: string): Promise<void> {
    await smartPlaylist(id).delete()
}

/** The songs of the smart playlist as the server computes them now, in its order. */
export function listSmartPlaylistTracks(
    id: string,
    offset: number,
    limit: number,
): Promise<TrackPage | undefined> {
    return smartPlaylist(id).tracks.get({ queryParameters: { offset, limit } })
}

/** Turns the smart playlist into a normal playlist with its songs of now; the smart one is gone. */
export function convertSmartPlaylist(id: string): Promise<Playlist | undefined> {
    return smartPlaylist(id).conversions.post()
}

export function listSmartPlaylistTemplates(
    offset: number,
    limit: number,
): Promise<SmartPlaylistTemplatePage | undefined> {
    return apiClient.api.v1.smartPlaylistTemplates.get({ queryParameters: { offset, limit } })
}
