// The online search for album info (admins only): requests of the matches of a song or album in
// MusicBrainz, the preview of release covers from the Cover Art Archive and applying the fields
// the admin chose, plus the logic that compares current and suggested values. The only place of
// the web app that calls these endpoints, so components and tests can replace the requests
// (vi.mock('@/library/onlineMetadata')).

import { RetryHandlerOptions } from '@microsoft/kiota-http-fetchlibrary'
import { apiClient } from '@/api/client'
import {
    OnlineCoverSizeObject,
    type OnlineAlbumApplyResult,
    type OnlineAlbumMatch,
    type OnlineAlbumMatches,
    type OnlineAlbumValues,
    type OnlineMetadataSettings,
    type OnlineRelease,
    type OnlineTrackApplyResult,
    type OnlineTrackMatch,
    type OnlineTrackMatches,
    type TrackMetadata,
    type UpdateAlbumTags,
    type UpdateOnlineMetadataSettings,
    type UpdateTrackMetadata,
} from '@/api/generated/models'

/** What the search is about: a song or an album. */
export type OnlineOwner = 'track' | 'album'

/** The fields a match suggests for a song; an album only uses the album fields. */
export type OnlineField =
    'title' | 'artist' | 'album' | 'albumArtist' | 'year' | 'trackNumber' | 'discNumber' | 'genre'

export const onlineTrackFields: readonly OnlineField[] = [
    'title',
    'artist',
    'album',
    'albumArtist',
    'year',
    'trackNumber',
    'discNumber',
    'genre',
]

export const onlineAlbumFields: readonly OnlineField[] = ['album', 'albumArtist', 'year', 'genre']

/** data-field of each field in the theme API, like the metadata editor. */
export const onlineDataFields: Readonly<Record<OnlineField, string>> = {
    title: 'title',
    artist: 'artist',
    album: 'album',
    albumArtist: 'album-artist',
    year: 'year',
    trackNumber: 'number',
    discNumber: 'disc',
    genre: 'genre',
}

/** A value of a field: text, number or nothing. */
export type OnlineValue = string | number | null

/** The values of the fields, current or suggested. */
export type OnlineValues = Partial<Record<OnlineField, OnlineValue>>

/**
 * How a suggested value relates to the current one: missing locally (the match fills it), other,
 * the same, or the match does not know it.
 */
export type OnlineDifference = 'missing' | 'different' | 'same' | 'unknown'

/** A field of the comparison: the current and the suggested value and how they differ. */
export interface OnlineCompareRow {
    field: OnlineField
    difference: OnlineDifference
    current: OnlineValue | undefined
    suggested: OnlineValue | undefined
}

/**
 * The preview of the release cover: no release, loading, shown, the release has none, or it could
 * not be loaded.
 */
export type OnlineCoverState = 'none' | 'loading' | 'ready' | 'missing' | 'error'

/** A match, the same for songs and albums. */
export interface OnlineMatch {
    /** Unique within the list of matches. */
    key: string
    release: OnlineRelease | null
    /** Agreement with the song or album, 0 to 100. */
    agreement: number
    values: OnlineValues
}

// --- Requests ---

/**
 * The server already repeats a request that MusicBrainz or the Cover Art Archive refuses as busy
 * (503) and then answers 503 itself (lookup.unavailable). The client does not repeat that answer,
 * otherwise every busy answer would cause several further requests to the services and keep the
 * admin waiting.
 */
const once = { options: [new RetryHandlerOptions({ maxRetries: 0 })] }

export function getOnlineMetadataSettings(): Promise<OnlineMetadataSettings | undefined> {
    return apiClient.api.v1.settings.onlineMetadata.get()
}

export function updateOnlineMetadataSettings(
    changes: UpdateOnlineMetadataSettings,
): Promise<OnlineMetadataSettings | undefined> {
    return apiClient.api.v1.settings.onlineMetadata.patch(changes)
}

export function searchTrackOnline(id: string): Promise<OnlineTrackMatches | undefined> {
    return apiClient.api.v1.tracks.byId(id).onlineMatches.get(once)
}

export function searchAlbumOnline(id: string): Promise<OnlineAlbumMatches | undefined> {
    return apiClient.api.v1.albums.byId(id).onlineMatches.get(once)
}

/** The front cover of a release as preview (500 pixels). */
export function fetchReleaseCover(releaseId: string): Promise<ArrayBuffer | undefined> {
    return apiClient.api.v1.onlineReleases
        .byReleaseId(releaseId)
        .cover.get({ ...once, queryParameters: { size: OnlineCoverSizeObject.Large } })
}

export function applyTrackOnline(
    id: string,
    tags: UpdateTrackMetadata | null,
    coverReleaseId: string | null,
): Promise<OnlineTrackApplyResult | undefined> {
    return apiClient.api.v1.tracks.byId(id).onlineMetadata.post({ tags, coverReleaseId }, once)
}

export function applyAlbumOnline(
    id: string,
    tags: UpdateAlbumTags | null,
    coverReleaseId: string | null,
): Promise<OnlineAlbumApplyResult | undefined> {
    return apiClient.api.v1.albums.byId(id).onlineMetadata.post({ tags, coverReleaseId }, once)
}

// --- Logic ---

/** The current values of a song as the metadata editor reads them. */
export function trackValues(metadata: TrackMetadata | null | undefined): OnlineValues {
    return {
        title: metadata?.title ?? null,
        artist: metadata?.artist ?? null,
        album: metadata?.album ?? null,
        albumArtist: metadata?.albumArtist ?? null,
        year: metadata?.year ?? null,
        trackNumber: metadata?.trackNumber ?? null,
        discNumber: metadata?.discNumber ?? null,
        genre: metadata?.genre ?? null,
    }
}

/** The values of an album. */
export function albumValues(values: OnlineAlbumValues | null | undefined): OnlineValues {
    return {
        album: values?.album ?? null,
        albumArtist: values?.albumArtist ?? null,
        year: values?.year ?? null,
        genre: values?.genre ?? null,
    }
}

export function fromTrackMatch(match: OnlineTrackMatch, index: number): OnlineMatch {
    return {
        key: `${match.recordingId ?? 'recording'}-${match.release?.id ?? 'none'}-${index}`,
        release: match.release ?? null,
        agreement: clampAgreement(match.agreement),
        values: {
            title: match.title ?? null,
            artist: match.artist ?? null,
            album: match.album ?? null,
            albumArtist: match.albumArtist ?? null,
            year: match.year ?? null,
            trackNumber: match.trackNumber ?? null,
            discNumber: match.discNumber ?? null,
            genre: match.genre ?? null,
        },
    }
}

export function fromAlbumMatch(match: OnlineAlbumMatch, index: number): OnlineMatch {
    return {
        key: `${match.release?.id ?? 'release'}-${index}`,
        release: match.release ?? null,
        agreement: clampAgreement(match.agreement),
        values: albumValues(match.values),
    }
}

function clampAgreement(value: number | null | undefined): number {
    return Number.isFinite(value) ? Math.min(100, Math.max(0, Math.round(value as number))) : 0
}

function isEmpty(value: OnlineValue | undefined): boolean {
    return value === null || value === undefined || (typeof value === 'string' && !value.trim())
}

/** Texts compare without regard to surrounding and repeated white space. */
function same(left: OnlineValue | undefined, right: OnlineValue | undefined): boolean {
    if (typeof left === 'string' && typeof right === 'string') {
        return left.trim().replace(/\s+/g, ' ') === right.trim().replace(/\s+/g, ' ')
    }

    return left === right
}

export function differenceOf(
    current: OnlineValue | undefined,
    suggested: OnlineValue | undefined,
): OnlineDifference {
    if (isEmpty(suggested)) {
        return 'unknown'
    }
    if (isEmpty(current)) {
        return 'missing'
    }

    return same(current, suggested) ? 'same' : 'different'
}

/** The rows of the comparison of the current values with a match. */
export function compareRows(
    fields: readonly OnlineField[],
    current: OnlineValues,
    suggested: OnlineValues,
): OnlineCompareRow[] {
    return fields.map((field) => ({
        field,
        difference: differenceOf(current[field], suggested[field]),
        current: current[field],
        suggested: suggested[field],
    }))
}

/** Whether the field can be applied: the match knows a value other than the current one. */
export function isApplicable(difference: OnlineDifference): boolean {
    return difference === 'missing' || difference === 'different'
}

/**
 * The fields chosen when a match is selected: those the song or album is missing. Other values are
 * only applied when the admin chooses them.
 */
export function defaultSelection(
    fields: readonly OnlineField[],
    current: OnlineValues,
    suggested: OnlineValues,
): Set<OnlineField> {
    return new Set(
        fields.filter((field) => differenceOf(current[field], suggested[field]) === 'missing'),
    )
}

/** The request of the chosen fields; null if none can be applied. */
export function tagsOf(
    fields: readonly OnlineField[],
    selected: ReadonlySet<OnlineField>,
    current: OnlineValues,
    suggested: OnlineValues,
): UpdateTrackMetadata | null {
    const tags: Record<string, OnlineValue> = {}
    for (const field of fields) {
        if (selected.has(field) && isApplicable(differenceOf(current[field], suggested[field]))) {
            tags[field] = suggested[field] ?? null
        }
    }

    return Object.keys(tags).length > 0 ? (tags as UpdateTrackMetadata) : null
}

/** The year of a release date (YYYY, YYYY-MM or YYYY-MM-DD). */
export function releaseYear(release: OnlineRelease | null | undefined): number | null {
    const year = Number.parseInt(release?.date?.slice(0, 4) ?? '', 10)
    return Number.isFinite(year) && year > 0 ? year : null
}
