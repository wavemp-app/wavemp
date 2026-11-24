// Artist pictures as the web app shows them: the picture of an artist (its own, from its folder,
// uploaded or taken over from an online service), otherwise a collage of the covers of its albums,
// otherwise the icon; the order of the sources for the admin dialog and the credit of an online
// picture. Pure functions only.

import {
    ArtistPictureKindObject,
    ArtistPictureProviderObject,
    ProblemCodeObject,
    type ArtistPicture,
    type ArtistPictureCandidate,
    type ArtistPictureCredit,
    type ArtistPictureCrop,
    type ArtistPictureProvider,
    type MusicBrainzArtistMatch,
} from '@/api/generated/models'
import type { AvatarCrop, PictureSize } from '@/profiles/avatarCrop'
import { cropRectangle } from '@/profiles/avatarCrop'

/** Most album covers a collage shows. */
export const maxCollageCovers = 4

/** What the picture of an artist shows. */
export type ArtistPictureView =
    | { readonly kind: 'picture'; readonly version: string }
    | { readonly kind: 'albums'; readonly albumIds: readonly string[] }
    | { readonly kind: 'none' }

/**
 * The view of the picture summary of an artist: its picture with the version of the API (a new
 * version is a new picture), otherwise the covers of up to four albums, otherwise nothing (the
 * icon). Unknown or incomplete summaries show the icon.
 */
export function artistPictureViewOf(picture: ArtistPicture | null | undefined): ArtistPictureView {
    switch (picture?.kind) {
        case ArtistPictureKindObject.Local:
        case ArtistPictureKindObject.Uploaded:
        case ArtistPictureKindObject.Online:
            return picture.version
                ? { kind: 'picture', version: picture.version }
                : { kind: 'none' }
        case ArtistPictureKindObject.Albums: {
            const albumIds = [
                ...new Set((picture.albumIds ?? []).filter((id): id is string => !!id)),
            ]
            return albumIds.length > 0
                ? { kind: 'albums', albumIds: albumIds.slice(0, maxCollageCovers) }
                : { kind: 'none' }
        }
        default:
            return { kind: 'none' }
    }
}

/** The sources of an artist picture in the order of their priority. */
export const pictureSources = ['local', 'uploaded', 'online', 'albums'] as const

export type PictureSource = (typeof pictureSources)[number]

/**
 * State of a source: active (the picture viewers see), available (stored or possible, but a source
 * before it wins) or missing.
 */
export type PictureSourceState = 'active' | 'available' | 'missing'

export interface PictureSourceRow {
    readonly source: PictureSource
    readonly state: PictureSourceState
    /** For a stored picture that another one hides: the source that wins. */
    readonly hiddenBy: PictureSource | null
}

function activeSource(picture: ArtistPicture | null | undefined): PictureSource | null {
    switch (picture?.kind) {
        case ArtistPictureKindObject.Local:
            return 'local'
        case ArtistPictureKindObject.Uploaded:
            return 'uploaded'
        case ArtistPictureKindObject.Online:
            return 'online'
        case ArtistPictureKindObject.Albums:
            return 'albums'
        default:
            return null
    }
}

function isStored(picture: ArtistPicture | null | undefined, source: PictureSource): boolean {
    switch (source) {
        case 'local':
            return picture?.hasLocalPicture ?? false
        case 'uploaded':
            return picture?.hasUploadedPicture ?? false
        case 'online':
            return picture?.hasOnlinePicture ?? false
        default:
            // The collage needs no picture of its own; it is missing only when the artist has no
            // album with a cover (kind None).
            return picture?.kind !== ArtistPictureKindObject.None
    }
}

/**
 * The sources of the picture of an artist in their real order, each with its state, so the admin
 * sees which picture is shown and why a stored one is not: the first stored source is active, the
 * stored ones after it are hidden by it.
 */
export function pictureSourceRows(picture: ArtistPicture | null | undefined): PictureSourceRow[] {
    const active = activeSource(picture)
    return pictureSources.map((source) => {
        if (source === active) {
            return { source, state: 'active', hiddenBy: null }
        }

        if (!isStored(picture, source)) {
            return { source, state: 'missing', hiddenBy: null }
        }

        const winner = active && pictureSources.indexOf(active) < pictureSources.indexOf(source)
        return { source, state: 'available', hiddenBy: winner ? active : null }
    })
}

/** The URL if it is an absolute https address, otherwise null; links never get other schemes. */
export function httpsUrlOf(value: string | null | undefined): string | null {
    if (!value) {
        return null
    }

    try {
        const url = new URL(value)
        return url.protocol === 'https:' && url.hostname !== '' ? url.href : null
    } catch {
        return null
    }
}

/** Plain text of a credit field: white space collapsed, empty values missing. */
function textOf(value: string | null | undefined): string | null {
    const text = (value ?? '').replace(/\s+/g, ' ').trim()
    return text === '' ? null : text
}

/** Message key of the name of a service that online pictures come from. */
export function providerKey(provider: ArtistPictureProvider | null | undefined): string {
    return provider === ArtistPictureProviderObject.FanartTv
        ? 'artistPictures.providers.fanartTv'
        : 'artistPictures.providers.wikimedia'
}

/**
 * The parts of the credit of an online picture: author and license as plain text (the server
 * already turned them into text; the web app only ever shows them as text), the links only for
 * https addresses, and the message key that joins the parts that exist.
 */
export interface CreditParts {
    readonly key: string
    readonly author: string | null
    readonly license: string | null
    readonly licenseUrl: string | null
    readonly providerKey: string
    readonly sourceUrl: string | null
}

export function creditPartsOf(
    credit: ArtistPictureCredit | ArtistPictureCandidate | null | undefined,
): CreditParts | null {
    if (!credit) {
        return null
    }

    const author = textOf(credit.author)
    const license = textOf(credit.license)
    const key =
        author && license
            ? 'artistPictures.credit.authorLicense'
            : author
              ? 'artistPictures.credit.author'
              : license
                ? 'artistPictures.credit.license'
                : 'artistPictures.credit.source'
    return {
        key,
        author,
        license,
        licenseUrl: license ? httpsUrlOf(credit.licenseUrl) : null,
        providerKey: providerKey(credit.provider),
        sourceUrl: httpsUrlOf(credit.sourceUrl),
    }
}

/**
 * The crop of the API for a square chosen on a preview: its left and top edge as fractions of the
 * width and height and its side as fraction of the shorter side. The preview has the aspect ratio
 * of the full picture, so the server cuts the same square from it.
 */
export function takeOverCropOf(crop: AvatarCrop, picture: PictureSize): ArtistPictureCrop | null {
    if (!(picture.width > 0 && picture.height > 0)) {
        return null
    }

    const rectangle = cropRectangle(crop, picture)
    const shorter = Math.min(picture.width, picture.height)
    const round = (value: number) => Math.min(1, Math.max(0, Math.round(value * 10000) / 10000))
    return {
        x: round(rectangle.x / picture.width),
        y: round(rectangle.y / picture.height),
        size: Math.max(0.0001, round(rectangle.side / shorter)),
    }
}

/** Life span of a MusicBrainz artist as years, e.g. "1991" to "2004"; null without a start. */
export function lifeSpanOf(match: MusicBrainzArtistMatch): {
    readonly begin: string
    readonly end: string | null
} | null {
    const year = (value: string | null | undefined) => /^\d{4}/.exec(value ?? '')?.[0] ?? null
    const begin = year(match.lifeSpanBegin)
    return begin ? { begin, end: year(match.lifeSpanEnd) } : null
}

/** Message key of a failed action of the artist picture dialog by the problem code. */
export function pictureFailureKey(code: string | null | undefined): string {
    switch (code) {
        case ProblemCodeObject.ArtistPicture_too_large:
            return 'artistPictures.errors.size'
        case ProblemCodeObject.ArtistPicture_unsupported_format:
            return 'artistPictures.errors.type'
        case ProblemCodeObject.ArtistPicture_unreadable:
            return 'artistPictures.errors.unreadable'
        case ProblemCodeObject.ArtistNot_found:
            return 'artistPictures.errors.artistNotFound'
        case ProblemCodeObject.ArtistPicture_lookup_disabled:
            return 'artistPictures.online.disabled'
        case ProblemCodeObject.ArtistPicture_lookup_unavailable:
            return 'artistPictures.errors.unavailable'
        case ProblemCodeObject.ArtistPicture_candidate_not_found:
            return 'artistPictures.errors.candidateExpired'
        case ProblemCodeObject.ServiceUnavailable:
            return 'artistPictures.errors.processing'
        default:
            return 'artistPictures.errors.saveFailed'
    }
}
