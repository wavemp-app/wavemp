// Texts of the lyrics: the API sends codes, the web app translates them.

import {
    LyricsPublicationObject,
    ProblemCodeObject,
    type LyricsPublication,
} from '@/api/generated/models'

interface Labeled {
    readonly labelKey: string
}

const types: Record<string, Labeled> = {
    LineSynchronized: { labelKey: 'trackLyrics.types.lineSynchronized' },
    WordSynchronized: { labelKey: 'trackLyrics.types.wordSynchronized' },
    Static: { labelKey: 'trackLyrics.types.static' },
}

/** Message key of how lyrics follow the music. */
export function lyricsTypeKey(type: string | null | undefined): string {
    return (types[type ?? ''] ?? { labelKey: 'trackLyrics.types.static' }).labelKey
}

const sources: Record<string, Labeled> = {
    File: { labelKey: 'trackLyrics.sources.file' },
    Download: { labelKey: 'trackLyrics.sources.download' },
    Upload: { labelKey: 'trackLyrics.sources.upload' },
    Tags: { labelKey: 'trackLyrics.sources.tags' },
}

/** Message key of where lyrics come from. */
export function lyricsSourceKey(source: string | null | undefined): string {
    return (sources[source ?? ''] ?? { labelKey: 'trackLyrics.sources.file' }).labelKey
}

interface DownloadFailure extends Labeled {
    /** Whether the download worked but found nothing, which is no error. */
    readonly notice?: boolean
}

const downloadFailures: Record<string, DownloadFailure> = {
    [ProblemCodeObject.LyricsDownload_disabled]: {
        labelKey: 'trackLyrics.errors.downloadDisabled',
    },
    [ProblemCodeObject.LyricsFile_exists]: { labelKey: 'trackLyrics.errors.fileExists' },
    [ProblemCodeObject.LyricsDownload_not_found]: {
        labelKey: 'trackLyrics.errors.notFound',
        notice: true,
    },
    [ProblemCodeObject.LyricsInstrumental]: {
        labelKey: 'trackLyrics.errors.instrumental',
        notice: true,
    },
    [ProblemCodeObject.LyricsService_unavailable]: {
        labelKey: 'trackLyrics.errors.serviceUnavailable',
    },
    [ProblemCodeObject.LyricsRate_limited]: { labelKey: 'trackLyrics.errors.rateLimited' },
    [ProblemCodeObject.LyricsFile_not_writable]: { labelKey: 'trackLyrics.errors.fileNotWritable' },
    [ProblemCodeObject.LyricsMatch_not_found]: { labelKey: 'trackLyrics.errors.matchNotFound' },
    [ProblemCodeObject.TrackNot_found]: { labelKey: 'trackLyrics.errors.trackNotFound' },
}

const unknownFailure: DownloadFailure = { labelKey: 'trackLyrics.errors.failed' }

/** Message key and tone of a failed download, from the problem code of the answer. */
export function downloadFailureOf(code: string | null | undefined): {
    readonly labelKey: string
    readonly tone: 'info' | 'error'
} {
    const failure = downloadFailures[code ?? ''] ?? unknownFailure
    return { labelKey: failure.labelKey, tone: failure.notice ? 'info' : 'error' }
}

const lookupNotes: Record<string, Labeled> = {
    NotFound: { labelKey: 'trackLyrics.lookup.notFound' },
    Instrumental: { labelKey: 'trackLyrics.lookup.instrumental' },
}

/**
 * Message key of the stored lookup of a song that found no lyrics, or null for results with
 * lyrics, which the lyrics summary already describes.
 */
export function lookupNoteKey(status: string | null | undefined): string | null {
    return lookupNotes[status ?? '']?.labelKey ?? null
}

const minutesPerHour = 60
const minutesPerDay = 24 * minutesPerHour

/**
 * The wait of the lyrics download before it looks up a song without lyrics again, as a number and
 * a unit for Intl.NumberFormat: whole days if it is at least a day, otherwise whole hours (at least
 * one); null if it never looks such a song up again.
 */
export function lookUpAgainWait(
    minutes: number | null | undefined,
): { readonly value: number; readonly unit: 'day' | 'hour' } | null {
    if (minutes === null || minutes === undefined || minutes <= 0) {
        return null
    }

    if (minutes >= minutesPerDay) {
        return { value: Math.round(minutes / minutesPerDay), unit: 'day' }
    }

    return { value: Math.max(1, Math.round(minutes / minutesPerHour)), unit: 'hour' }
}

/**
 * Message key of forgetting the lookup of a song, from the problem code of a failed answer: a
 * result that is already gone counts as forgotten (null).
 */
export function forgetFailureKey(code: string | null | undefined): string | null {
    if (code === ProblemCodeObject.LyricsLookup_not_found) {
        return null
    }

    return code === ProblemCodeObject.LyricsLookup_found_lyrics
        ? 'trackLyrics.lookup.errors.foundLyrics'
        : 'trackLyrics.lookup.errors.failed'
}

const publications: Partial<Record<LyricsPublication, DownloadFailure>> = {
    [LyricsPublicationObject.Published]: {
        labelKey: 'trackLyrics.upload.publication.published',
        notice: true,
    },
    [LyricsPublicationObject.Disabled]: { labelKey: 'trackLyrics.upload.publication.disabled' },
    [LyricsPublicationObject.MissingMetadata]: {
        labelKey: 'trackLyrics.upload.publication.missingMetadata',
    },
    [LyricsPublicationObject.Rejected]: { labelKey: 'trackLyrics.upload.publication.rejected' },
    [LyricsPublicationObject.RateLimited]: {
        labelKey: 'trackLyrics.upload.publication.rateLimited',
    },
    [LyricsPublicationObject.Unavailable]: {
        labelKey: 'trackLyrics.upload.publication.unavailable',
    },
}

/**
 * Message key and tone of what publishing uploaded lyrics did, or null if they were not to be
 * published: success for published lyrics, otherwise an error (the lyrics stay saved).
 */
export function publicationMessageOf(publication: LyricsPublication | null | undefined): {
    readonly labelKey: string
    readonly tone: 'success' | 'error'
} | null {
    const message = publications[publication ?? LyricsPublicationObject.NotRequested]
    return message
        ? { labelKey: message.labelKey, tone: message.notice ? 'success' : 'error' }
        : null
}
