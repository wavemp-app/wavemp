// Requests of the own albums (admins only): the albums, their songs in their order and their
// covers. The only place of the web app that calls these endpoints, so components and tests can
// replace it as a whole (vi.mock('@/admin/ownAlbumApi')).

import { apiClient } from '@/api/client'
import type { OwnAlbumsRequestBuilderGetQueryParameters } from '@/api/generated/api/v1/ownAlbums'
import type {
    CreateOwnAlbum,
    OwnAlbum,
    OwnAlbumPage,
    OwnAlbumTrackPage,
    UpdateOwnAlbum,
} from '@/api/generated/models'

export type OwnAlbumQuery = OwnAlbumsRequestBuilderGetQueryParameters

/** Most songs an own album holds (the API checks it as well). */
export const maxOwnAlbumTracks = 1000

const ownAlbum = (id: string) => apiClient.api.v1.ownAlbums.byId(id)

export function listOwnAlbums(query: OwnAlbumQuery): Promise<OwnAlbumPage | undefined> {
    return apiClient.api.v1.ownAlbums.get({ queryParameters: query })
}

export function getOwnAlbum(id: string): Promise<OwnAlbum | undefined> {
    return ownAlbum(id).get()
}

/** Creates an own album without songs; no file changes. */
export function createOwnAlbum(album: CreateOwnAlbum): Promise<OwnAlbum | undefined> {
    return apiClient.api.v1.ownAlbums.post(album)
}

/** Changes the values of an own album; no file changes until it is applied. */
export function updateOwnAlbum(id: string, changes: UpdateOwnAlbum): Promise<OwnAlbum | undefined> {
    return ownAlbum(id).patch(changes)
}

/** Deletes the definition of an own album; its files and tags stay as they are. */
export async function deleteOwnAlbum(id: string): Promise<void> {
    await ownAlbum(id).delete()
}

/** The songs of an own album by position, then the songs taken out since the last apply. */
export function listOwnAlbumTracks(
    id: string,
    offset: number,
    limit: number,
): Promise<OwnAlbumTrackPage | undefined> {
    return ownAlbum(id).tracks.get({ queryParameters: { offset, limit } })
}

/** Replaces the songs of an own album with these, in this order. */
export function setOwnAlbumTracks(
    id: string,
    trackIds: readonly string[],
): Promise<OwnAlbum | undefined> {
    return ownAlbum(id).tracks.put({ trackIds: [...trackIds] })
}

/** Adds songs to the end of an own album. */
export function addOwnAlbumTracks(
    id: string,
    trackIds: readonly string[],
): Promise<OwnAlbum | undefined> {
    return ownAlbum(id).tracks.post({ trackIds: [...trackIds] })
}

/** The cover the admin uploaded for an own album, as it was uploaded. */
export function fetchOwnAlbumCover(id: string): Promise<ArrayBuffer | undefined> {
    return ownAlbum(id).cover.get()
}

/** Uploads a JPEG, PNG or WebP picture as the cover of an own album. */
export function uploadOwnAlbumCover(
    id: string,
    picture: ArrayBuffer,
    contentType: string,
): Promise<OwnAlbum | undefined> {
    return ownAlbum(id).cover.put(picture, contentType)
}

export async function deleteOwnAlbumCover(id: string): Promise<void> {
    await ownAlbum(id).cover.delete()
}
