// Requests of the library. The only place of the web app that calls the library endpoints, so
// components and tests can replace it as a whole (vi.mock('@/library/api')).

import { apiClient } from '@/api/client'
import type { AlbumsRequestBuilderGetQueryParameters } from '@/api/generated/api/v1/albums'
import type { GroupsRequestBuilderGetQueryParameters as AlbumGroupQuery } from '@/api/generated/api/v1/albums/groups'
import type * as albumTracks from '@/api/generated/api/v1/albums/item/tracks'
import type { ArtistsRequestBuilderGetQueryParameters } from '@/api/generated/api/v1/artists'
import type { GroupsRequestBuilderGetQueryParameters as ArtistGroupQuery } from '@/api/generated/api/v1/artists/groups'
import type * as artistAlbums from '@/api/generated/api/v1/artists/item/albums'
import type { GenresRequestBuilderGetQueryParameters } from '@/api/generated/api/v1/genres'
import type { TracksRequestBuilderGetQueryParameters } from '@/api/generated/api/v1/tracks'
import type { GroupsRequestBuilderGetQueryParameters as TrackGroupQuery } from '@/api/generated/api/v1/tracks/groups'
import type { FilesRequestBuilderGetQueryParameters } from '@/api/generated/api/v1/tracks/item/files'
import type { YearsRequestBuilderGetQueryParameters } from '@/api/generated/api/v1/years'
import type {
    Album,
    AlbumPage,
    Artist,
    ArtistPage,
    AlbumCoverFallbackOption,
    AlbumCoverFallbackRule,
    CoverRule,
    CoverRuleOption,
    GenrePage,
    ListGroupPage,
    MediaFilePage,
    SearchResult,
    Track,
    TrackMetadata,
    SongAnalysesStarted,
    TrackPage,
    TrackSongSegments,
    UpdateTrackMetadata,
    YearPage,
} from '@/api/generated/models'
import type { CoverSize } from './covers'

/** The file of a track whose song segment is meant. */
export type SongSegmentKind = 'audio' | 'video'

export type TrackQuery = TracksRequestBuilderGetQueryParameters
export type AlbumQuery = AlbumsRequestBuilderGetQueryParameters
export type ArtistQuery = ArtistsRequestBuilderGetQueryParameters
export type GenreQuery = GenresRequestBuilderGetQueryParameters
export type YearQuery = YearsRequestBuilderGetQueryParameters
type AlbumTrackQuery = albumTracks.TracksRequestBuilderGetQueryParameters
type ArtistAlbumQuery = artistAlbums.AlbumsRequestBuilderGetQueryParameters
export type TrackFileQuery = FilesRequestBuilderGetQueryParameters
export type { AlbumGroupQuery, ArtistGroupQuery, TrackGroupQuery }

export function listTracks(query: TrackQuery): Promise<TrackPage | undefined> {
    return apiClient.api.v1.tracks.get({ queryParameters: query })
}

/**
 * The groups of the track list with the same query (without its offset and limit): runs of the
 * same album with their position in the list.
 */
export function listTrackGroups(query: TrackGroupQuery): Promise<ListGroupPage | undefined> {
    return apiClient.api.v1.tracks.groups.get({ queryParameters: query })
}

export function getTrack(id: string): Promise<Track | undefined> {
    return apiClient.api.v1.tracks.byId(id).get()
}

export function listTrackFiles(
    id: string,
    query: TrackFileQuery,
): Promise<MediaFilePage | undefined> {
    return apiClient.api.v1.tracks.byId(id).files.get({ queryParameters: query })
}

/** The cover of a track, scaled by the API to fit into a square of the size (see coverSizes). */
export function fetchTrackCover(id: string, size: CoverSize): Promise<ArrayBuffer | undefined> {
    return apiClient.api.v1.tracks.byId(id).cover.get({ queryParameters: { size } })
}

export function getTrackMetadata(id: string): Promise<TrackMetadata | undefined> {
    return apiClient.api.v1.tracks.byId(id).metadata.get()
}

export function updateTrackMetadata(
    id: string,
    changes: UpdateTrackMetadata,
): Promise<TrackMetadata | undefined> {
    return apiClient.api.v1.tracks.byId(id).metadata.patch(changes)
}

/**
 * Where the song lies in the audio file and the video of a track: what the song analysis found,
 * the corrections of the admin and what the players use (admins only).
 */
export function getTrackSongSegments(id: string): Promise<TrackSongSegments | undefined> {
    return apiClient.api.v1.tracks.byId(id).songSegments.get()
}

/**
 * Corrects where the song starts and ends in a file of a track, in milliseconds of the file; null
 * leaves the value to the analysis (admins only).
 */
export function setTrackSongSegment(
    id: string,
    kind: SongSegmentKind,
    startMilliseconds: number | null,
    endMilliseconds: number | null,
): Promise<TrackSongSegments | undefined> {
    return apiClient.api.v1.tracks
        .byId(id)
        .songSegments.byKind(kind)
        .put({ startMilliseconds, endMilliseconds })
}

/** Removes the correction of a file of a track; the analysis decides again (admins only). */
export function resetTrackSongSegment(
    id: string,
    kind: SongSegmentKind,
): Promise<TrackSongSegments | undefined> {
    return apiClient.api.v1.tracks.byId(id).songSegments.byKind(kind).delete()
}

/** Lets the song analysis look at the files of a track again in the background (admins only). */
export function analyzeTrackSong(id: string): Promise<void> {
    return apiClient.api.v1.tracks.byId(id).songAnalyses.post()
}

/** Lets the song analysis look at every file of the library again (admins only). */
export function analyzeAllSongs(): Promise<SongAnalysesStarted | undefined> {
    return apiClient.api.v1.songAnalyses.post()
}

export function listAlbums(query: AlbumQuery): Promise<AlbumPage | undefined> {
    return apiClient.api.v1.albums.get({ queryParameters: query })
}

/** The groups of the album list with the same query: by album artist, letter or year. */
export function listAlbumGroups(query: AlbumGroupQuery): Promise<ListGroupPage | undefined> {
    return apiClient.api.v1.albums.groups.get({ queryParameters: query })
}

export function getAlbum(id: string): Promise<Album | undefined> {
    return apiClient.api.v1.albums.byId(id).get()
}

export function listAlbumTracks(
    id: string,
    query: AlbumTrackQuery,
): Promise<TrackPage | undefined> {
    return apiClient.api.v1.albums.byId(id).tracks.get({ queryParameters: query })
}

/** The cover of an album, scaled by the API to fit into a square of the size (see coverSizes). */
export function fetchAlbumCover(id: string, size: CoverSize): Promise<ArrayBuffer | undefined> {
    return apiClient.api.v1.albums.byId(id).cover.get({ queryParameters: { size } })
}

export function listArtists(query: ArtistQuery): Promise<ArtistPage | undefined> {
    return apiClient.api.v1.artists.get({ queryParameters: query })
}

/** The groups of the artist list with the same query: by the letter of the name. */
export function listArtistGroups(query: ArtistGroupQuery): Promise<ListGroupPage | undefined> {
    return apiClient.api.v1.artists.groups.get({ queryParameters: query })
}

export function getArtist(id: string): Promise<Artist | undefined> {
    return apiClient.api.v1.artists.byId(id).get()
}

export function listArtistAlbums(
    id: string,
    query: ArtistAlbumQuery,
): Promise<AlbumPage | undefined> {
    return apiClient.api.v1.artists.byId(id).albums.get({ queryParameters: query })
}

export function listGenres(query: GenreQuery): Promise<GenrePage | undefined> {
    return apiClient.api.v1.genres.get({ queryParameters: query })
}

export function listYears(query: YearQuery): Promise<YearPage | undefined> {
    return apiClient.api.v1.years.get({ queryParameters: query })
}

export function searchLibrary(q: string, limit: number): Promise<SearchResult | undefined> {
    return apiClient.api.v1.search.get({ queryParameters: { q, limit } })
}

/** What a cover rule belongs to: an album or a track. */
export type CoverRuleOwner = 'album' | 'track'

function coverRuleOf(owner: CoverRuleOwner, id: string) {
    return owner === 'album'
        ? apiClient.api.v1.albums.byId(id).coverRule
        : apiClient.api.v1.tracks.byId(id).coverRule
}

/** The cover rule of a song or of the songs of an album with the rule it inherits (admins only). */
export function getCoverRule(owner: CoverRuleOwner, id: string): Promise<CoverRule | undefined> {
    return coverRuleOf(owner, id).get()
}

/** Sets the cover rule of a song or of the songs of an album; Inherit removes it (admins only). */
export function setCoverRule(
    owner: CoverRuleOwner,
    id: string,
    rule: CoverRuleOption,
): Promise<CoverRule | undefined> {
    return coverRuleOf(owner, id).put({ rule })
}

/** The cover an album or track would show with the rule, without changing it (admins only). */
export function fetchCoverRulePreview(
    owner: CoverRuleOwner,
    id: string,
    rule: CoverRuleOption,
    size: CoverSize,
): Promise<ArrayBuffer | undefined> {
    return coverRuleOf(owner, id).preview.get({ queryParameters: { rule, size } })
}

/** What an album shows without a cover of its own, with the rule it inherits (admins only). */
export function getAlbumCoverFallback(id: string): Promise<AlbumCoverFallbackRule | undefined> {
    return apiClient.api.v1.albums.byId(id).coverFallback.get()
}

/** Sets what an album shows without a cover of its own; Inherit removes its exception (admins only). */
export function setAlbumCoverFallback(
    id: string,
    fallback: AlbumCoverFallbackOption,
): Promise<AlbumCoverFallbackRule | undefined> {
    return apiClient.api.v1.albums.byId(id).coverFallback.put({ fallback })
}
