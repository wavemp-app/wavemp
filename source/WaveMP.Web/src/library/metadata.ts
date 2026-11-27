// Form logic of the metadata editor: the form holds every field as text and the artists as
// lists, the request contains only the fields the admin changed. Empty fields remove the value
// from the tags (null), an empty list of artists removes the artists.

import {
    ArtistRoleObject,
    type TrackMetadata,
    type UpdateTrackMetadata,
} from '@/api/generated/models'
import type { ArtistEntry } from './artists'

export type MetadataTextField = 'title' | 'album' | 'genre'
export type MetadataNumberField = 'year' | 'trackNumber' | 'discNumber'
export type MetadataArtistField = 'artists' | 'albumArtists'
export type MetadataField = MetadataTextField | MetadataNumberField | MetadataArtistField

/** The values of the form: the texts as entered, the artists in their order. */
export type MetadataForm = Record<MetadataTextField | MetadataNumberField, string> &
    Record<MetadataArtistField, ArtistEntry[]>

/** Errors of the form by field; "number" stands for a value that is no number from 1 to the maximum. */
export type MetadataFormErrors = Partial<Record<MetadataField, 'number'>>

/** Limits of the API (UpdateTrackMetadata). */
export const metadataLimits = {
    text: 512,
    genre: 128,
    /** Most artists of a song, and most album artists. */
    artists: 50,
    numberDigits: 4,
    minNumber: 1,
    maxNumber: 9999,
} as const

export const metadataTextFields: readonly MetadataTextField[] = ['title', 'album', 'genre']

export const metadataNumberFields: readonly MetadataNumberField[] = [
    'year',
    'trackNumber',
    'discNumber',
]

/** data-field of each field in the theme API. */
export const metadataDataFields: Readonly<Record<MetadataField, string>> = {
    title: 'title',
    artists: 'artist',
    album: 'album',
    albumArtists: 'album-artist',
    year: 'year',
    trackNumber: 'number',
    discNumber: 'disc',
    genre: 'genre',
}

export function emptyMetadataForm(): MetadataForm {
    return {
        title: '',
        artists: [],
        album: '',
        albumArtists: [],
        year: '',
        trackNumber: '',
        discNumber: '',
        genre: '',
    }
}

/** The form of the metadata of a track. */
export function toMetadataForm(metadata: TrackMetadata): MetadataForm {
    return {
        title: metadata.title ?? '',
        artists: artistEntries(metadata),
        album: metadata.album ?? '',
        albumArtists: albumArtistEntries(metadata),
        year: numberText(metadata.year),
        trackNumber: numberText(metadata.trackNumber),
        discNumber: numberText(metadata.discNumber),
        genre: metadata.genre ?? '',
    }
}

/**
 * The artists of the form: the list the server read (separate values of the tags, or the artist
 * field split at "feat." and ";"), else the artist field as one artist.
 */
function artistEntries(metadata: TrackMetadata): ArtistEntry[] {
    const artists = (metadata.artists ?? [])
        .filter((artist) => !!artist.name)
        .map((artist) => ({ name: artist.name!, role: artist.role ?? ArtistRoleObject.Main }))
    if (artists.length > 0) {
        return artists
    }

    return metadata.artist ? [{ name: metadata.artist, role: ArtistRoleObject.Main }] : []
}

function albumArtistEntries(metadata: TrackMetadata): ArtistEntry[] {
    const names = (metadata.albumArtists ?? []).filter((name) => !!name)
    if (names.length > 0) {
        return names.map((name) => ({ name, role: ArtistRoleObject.Main }))
    }

    return metadata.albumArtist ? [{ name: metadata.albumArtist, role: ArtistRoleObject.Main }] : []
}

/** Whether two lists name the same artists in the same order, with the same roles. */
export function sameArtistEntries(
    first: readonly ArtistEntry[],
    second: readonly ArtistEntry[],
    roles = true,
): boolean {
    return (
        first.length === second.length &&
        first.every((entry, index) => {
            const other = second[index]!
            return (
                normalizeText(entry.name) === normalizeText(other.name) &&
                (!roles || entry.role === other.role)
            )
        })
    )
}

/** A text as the server stores it: trimmed, white space joined to single spaces. */
export function normalizeText(value: string): string {
    return value.trim().replace(/\s+/g, ' ')
}

/** The number of a field: null if empty, undefined if it is no whole number in the limits. */
export function parseMetadataNumber(value: string): number | null | undefined {
    const text = value.trim()
    if (text === '') {
        return null
    }
    if (!/^\d+$/.test(text)) {
        return undefined
    }
    const number = Number(text)
    return number >= metadataLimits.minNumber && number <= metadataLimits.maxNumber
        ? number
        : undefined
}

/** The fields whose values cannot be sent. */
export function validateMetadataForm(form: MetadataForm): MetadataFormErrors {
    const errors: MetadataFormErrors = {}
    for (const field of metadataNumberFields) {
        if (parseMetadataNumber(form[field]) === undefined) {
            errors[field] = 'number'
        }
    }
    return errors
}

/**
 * The changed fields of the form compared with the loaded values; empty fields become null.
 * Unchanged fields are left out, so values the admin did not touch are never written. Changed
 * artists are sent as the whole list in its order (artists, albumArtists), never as text.
 */
export function metadataChanges(original: MetadataForm, form: MetadataForm): UpdateTrackMetadata {
    const changes: UpdateTrackMetadata = {}
    for (const field of metadataTextFields) {
        const value = normalizeText(form[field])
        if (value !== normalizeText(original[field])) {
            changes[field] = value === '' ? null : value
        }
    }
    for (const field of metadataNumberFields) {
        const value = parseMetadataNumber(form[field])
        if (value !== undefined && value !== parseMetadataNumber(original[field])) {
            changes[field] = value
        }
    }
    if (!sameArtistEntries(original.artists, form.artists)) {
        changes.artists = form.artists.map((entry) => ({
            name: normalizeText(entry.name),
            role: entry.role,
        }))
    }
    if (!sameArtistEntries(original.albumArtists, form.albumArtists, false)) {
        changes.albumArtists = form.albumArtists.map((entry) => ({
            name: normalizeText(entry.name),
        }))
    }
    return changes
}

function numberText(value: number | null | undefined): string {
    return value === null || value === undefined ? '' : String(value)
}
