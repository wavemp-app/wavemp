// Presentation of the song segments of a track for the admin (see SongSegmentFields): the
// values of the form in seconds, the strip that shows where the song lies in the file, the status
// of the analysis and the parts a preview plays. Pure functions.

import {
    SongAnalysisStatusObject,
    SongSegmentMethodObject,
    type FileSongSegment,
} from '@/api/generated/models'

/** Earliest start of a song in milliseconds: the song may start up to 10 minutes before the file. */
export const minSongStartMilliseconds = -600_000

/** Seconds a preview plays before the start of the song and after its end. */
export const previewLeadSeconds = 3

/** Seconds a preview plays of the song after its start and before its end. */
export const previewSongSeconds = 5

/**
 * A value of the form in milliseconds: null for an empty field (the analysis decides), undefined
 * for text that is no number. Seconds with a decimal point or comma, e.g. "12.5" or "-1,2".
 */
export function parseSeconds(text: string): number | null | undefined {
    const value = text.trim().replace(',', '.')
    if (value === '') {
        return null
    }

    if (!/^-?\d+(\.\d{0,3})?$/.test(value) && !/^-?\.\d{1,3}$/.test(value)) {
        return undefined
    }

    const milliseconds = Math.round(Number(value) * 1000)
    return Number.isFinite(milliseconds) ? milliseconds : undefined
}

/** Milliseconds as seconds for the form, with up to three decimals and without trailing zeros. */
export function secondsText(milliseconds: number | null | undefined): string {
    if (typeof milliseconds !== 'number' || !Number.isFinite(milliseconds)) {
        return ''
    }

    return String(Math.round(milliseconds) / 1000)
}

/** A part of the strip of a file, as shares of its duration. */
export interface StripPiece {
    readonly start: number
    readonly length: number
}

function share(value: number, duration: number): number {
    return Math.min(1, Math.max(0, value / duration))
}

/**
 * The parts of the file that belong to the song, as shares of the file: the pieces of the segment
 * the players use, or the whole file without pieces. Empty while the duration is unknown.
 */
export function stripPieces(file: FileSongSegment): StripPiece[] {
    const duration = file.durationMilliseconds ?? 0
    if (duration <= 0) {
        return []
    }

    const pieces = file.effective?.pieces ?? []
    if (pieces.length === 0) {
        return [{ start: 0, length: 1 }]
    }

    return pieces
        .map((piece) => {
            const start = share(piece.fileStartMilliseconds ?? 0, duration)
            const end = share(
                (piece.fileStartMilliseconds ?? 0) + (piece.durationMilliseconds ?? 0),
                duration,
            )
            return { start, length: Math.max(0, end - start) }
        })
        .filter((piece) => piece.length > 0)
}

/** How sure the players can be of a file: data-state of the song segment and its status text. */
export type SongSegmentState =
    'pending' | 'corrected' | 'whole-file' | 'sure' | 'unsure' | 'mismatch' | 'no-sound' | 'failed'

export function segmentState(file: FileSongSegment): SongSegmentState {
    const effective = file.effective
    if (effective?.manual) {
        return 'corrected'
    }

    if (file.pending) {
        return 'pending'
    }

    if (file.status === SongAnalysisStatusObject.NoSound) {
        return 'no-sound'
    }

    if (file.status === SongAnalysisStatusObject.Failed) {
        return 'failed'
    }

    if (effective?.mismatch) {
        return 'mismatch'
    }

    if (effective?.method === SongSegmentMethodObject.WholeFile) {
        return 'whole-file'
    }

    return effective?.confident ? 'sure' : 'unsure'
}

/** Message key of the method the analysis found the song with. */
export function methodLabelKey(file: FileSongSegment): string {
    switch (file.detected?.method) {
        case SongSegmentMethodObject.Alignment:
            return 'trackSongSegments.methods.alignment'
        case SongSegmentMethodObject.Lyrics:
            return 'trackSongSegments.methods.lyrics'
        case SongSegmentMethodObject.Structure:
            return 'trackSongSegments.methods.structure'
        default:
            return 'trackSongSegments.methods.wholeFile'
    }
}

/** The confidence of the analysis in percent; null without one. */
export function confidencePercent(file: FileSongSegment): number | null {
    const confidence = file.detected?.confidence
    return typeof confidence === 'number' && Number.isFinite(confidence)
        ? Math.round(Math.min(1, Math.max(0, confidence)) * 100)
        : null
}

/** What a preview plays, in seconds of the file. */
export interface PreviewRange {
    readonly from: number
    readonly to: number
}

/**
 * The part of the file a preview plays: around the start of the song (a little before and the
 * first seconds of the song) or around its end (the last seconds and a little after).
 */
export function previewRange(
    edge: 'start' | 'end',
    startMilliseconds: number,
    endMilliseconds: number | null,
    durationMilliseconds: number,
): PreviewRange {
    const duration = Math.max(0, durationMilliseconds / 1000)
    const clamp = (value: number): number => Math.min(duration, Math.max(0, value))
    if (edge === 'start') {
        const start = startMilliseconds / 1000
        return {
            from: clamp(start - previewLeadSeconds),
            to: clamp(start + previewSongSeconds),
        }
    }

    const end = (endMilliseconds ?? durationMilliseconds) / 1000
    return { from: clamp(end - previewSongSeconds), to: clamp(end + previewLeadSeconds) }
}
