// Requests of the artist pictures. The only place of the web app that calls the endpoints of the
// artist pictures and their settings, so components and tests can replace it as a whole
// (vi.mock('@/library/artistPictureApi')).

import { MultipartBody } from '@microsoft/kiota-abstractions'
import { RetryHandlerOptions } from '@microsoft/kiota-http-fetchlibrary'
import { apiClient } from '@/api/client'
import type {
    Artist,
    ArtistPictureCandidatePage,
    ArtistPictureCrop,
    ArtistPictureSettings,
    MusicBrainzArtistMatchPage,
    UpdateArtistPictureSettings,
} from '@/api/generated/models'
import type { CoverSize } from './covers'

/**
 * The server already repeats a request that a service refuses as busy and then answers itself
 * (artist.picture_lookup_unavailable). The client does not repeat that answer, otherwise every
 * busy answer would cause several further requests to the services and keep the admin waiting.
 */
const once = { options: [new RetryHandlerOptions({ maxRetries: 0 })] }

/** Matches of MusicBrainz per page; the API allows up to 25. */
export const artistMatchPageSize = 10

/** Candidates per page; one page holds all pictures the services know of an artist. */
export const pictureCandidatePageSize = 50

/** The picture an artist shows (its own or the picture of its folder), as WebP in the size. */
export function fetchArtistPicture(id: string, size: CoverSize): Promise<ArrayBuffer | undefined> {
    return apiClient.api.v1.artists.byId(id).picture.get({ queryParameters: { size } })
}

/** The uploaded picture of an artist, also while a picture in its folder hides it (admins). */
export function fetchUploadedArtistPicture(
    id: string,
    size: CoverSize,
): Promise<ArrayBuffer | undefined> {
    return apiClient.api.v1.artists.byId(id).uploadedPicture.get({ queryParameters: { size } })
}

/** The online picture of an artist, also while another picture hides it (admins). */
export function fetchOnlineArtistPicture(
    id: string,
    size: CoverSize,
): Promise<ArrayBuffer | undefined> {
    return apiClient.api.v1.artists.byId(id).onlinePicture.get({ queryParameters: { size } })
}

/** Uploads a JPEG, PNG or WebP picture of an artist; the server stores a square of it. */
export async function uploadArtistPicture(
    id: string,
    picture: Blob,
    fileName: string,
): Promise<Artist | undefined> {
    const body = new MultipartBody()
    body.addOrReplacePart('file', picture.type, await picture.arrayBuffer(), undefined, fileName)
    return apiClient.api.v1.artists.byId(id).uploadedPicture.put(body)
}

/** Removes the uploaded picture of an artist; nothing happens if it has none. */
export async function removeUploadedArtistPicture(id: string): Promise<void> {
    await apiClient.api.v1.artists.byId(id).uploadedPicture.delete()
}

/** Removes the online picture of an artist; nothing happens if it has none. */
export async function removeOnlineArtistPicture(id: string): Promise<void> {
    await apiClient.api.v1.artists.byId(id).onlinePicture.delete()
}

/** Artists of MusicBrainz whose name matches the artist, the best first. */
export function listMusicBrainzMatches(
    id: string,
    offset = 0,
    limit = artistMatchPageSize,
): Promise<MusicBrainzArtistMatchPage | undefined> {
    return apiClient.api.v1.artists
        .byId(id)
        .musicbrainzMatches.get({ ...once, queryParameters: { offset, limit } })
}

/**
 * Pictures of the MusicBrainz artist on Wikimedia Commons and (with a key) fanart.tv. Without a
 * MusicBrainz artist the server uses the one remembered for the artist.
 */
export function listPictureCandidates(
    id: string,
    musicBrainzArtistId?: string | null,
): Promise<ArtistPictureCandidatePage | undefined> {
    return apiClient.api.v1.artists.byId(id).onlinePictureCandidates.get({
        ...once,
        queryParameters: {
            limit: pictureCandidatePageSize,
            ...(musicBrainzArtistId ? { musicBrainzArtistId } : {}),
        },
    })
}

/** The preview of a candidate as WebP (300 or 500 pixels), downloaded by the server. */
export function fetchCandidatePreview(
    id: string,
    candidateId: string,
    size: number,
): Promise<ArrayBuffer | undefined> {
    return apiClient.api.v1.artists
        .byId(id)
        .onlinePictureCandidates.byCandidateId(candidateId)
        .preview.get({ ...once, queryParameters: { size } })
}

/** Takes a candidate over as the online picture, optionally cropped to a square of it. */
export function takeOverPicture(
    id: string,
    candidateId: string,
    crop: ArtistPictureCrop | null,
): Promise<Artist | undefined> {
    return apiClient.api.v1.artists
        .byId(id)
        .onlinePicture.put({ candidateId, ...(crop ? { crop } : {}) }, once)
}

export function getArtistPictureSettings(): Promise<ArtistPictureSettings | undefined> {
    return apiClient.api.v1.settings.artistPictures.get()
}

/**
 * Changes the settings of the online lookup. A fanart.tv key is only ever sent, never read: the
 * answer only tells whether one is stored; an empty key removes it.
 */
export function updateArtistPictureSettings(
    changes: UpdateArtistPictureSettings,
): Promise<ArtistPictureSettings | undefined> {
    return apiClient.api.v1.settings.artistPictures.patch(changes)
}
