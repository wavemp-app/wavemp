// Texts of the lyrics: the API sends codes, the web app translates them.

import { ProblemCodeObject } from '@/api/generated/models'

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
