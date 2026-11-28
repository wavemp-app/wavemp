// Lyrics as the player shows them: times in seconds and the pure rules that pick the current line
// and word for a position of the music. The lyrics view (LyricsStage) only renders these results.

import {
    LyricsTypeObject,
    type Lyrics,
    type LyricsSource,
    type LyricsType,
} from '@/api/generated/models'

/** A word or syllable of a word-synchronized line; its text keeps its spaces. */
export interface LyricsWordTiming {
    /** When it is sung, in seconds. */
    readonly start: number
    /** When the next word or line starts, in seconds; null for the last word without an end. */
    readonly end: number | null
    readonly text: string
}

/** A line; an empty text marks a part without singing. */
export interface LyricsLineTiming {
    /** When it starts, in seconds; null for static lyrics. */
    readonly start: number | null
    /** When the next line starts, in seconds; null for the last line and static lyrics. */
    readonly end: number | null
    readonly text: string
    /** The words with their times; empty without word timestamps. */
    readonly words: readonly LyricsWordTiming[]
}

/** The lyrics of a track. */
export interface TrackLyrics {
    readonly trackId: string
    readonly type: LyricsType
    readonly source: LyricsSource | undefined
    readonly lines: readonly LyricsLineTiming[]
    /**
     * Whether the lyrics belong to another version of the song (another duration), so they are not
     * synchronized to this one.
     */
    readonly fromOtherVersion?: boolean
}

/** How long the last line counts as sung when neither it nor the track has an end, in seconds. */
export const lastLineSeconds = 5

/** Where a line or word stands against the position of the music. */
export type LyricsItemState = 'past' | 'active' | 'upcoming'

function seconds(milliseconds: number | null | undefined): number | null {
    return typeof milliseconds === 'number' && Number.isFinite(milliseconds)
        ? milliseconds / 1000
        : null
}

/**
 * The lyrics of the API in seconds; null without lines. Lyrics whose lines have no times count as
 * static, whatever their type says.
 */
export function toTrackLyrics(trackId: string, lyrics: Lyrics): TrackLyrics | null {
    const lines = (lyrics.lines ?? []).map<LyricsLineTiming>((line) => ({
        start: seconds(line.startMilliseconds),
        end: seconds(line.endMilliseconds),
        text: line.text ?? '',
        words: (line.words ?? []).flatMap<LyricsWordTiming>((word) => {
            const start = seconds(word.startMilliseconds)
            return start === null
                ? []
                : [{ start, end: seconds(word.endMilliseconds), text: word.text ?? '' }]
        }),
    }))
    if (lines.length === 0) {
        return null
    }

    const timed = lines.some((line) => line.start !== null)
    const type =
        !timed || !lyrics.type || lyrics.type === LyricsTypeObject.Static
            ? LyricsTypeObject.Static
            : lyrics.type
    return {
        trackId,
        type,
        source: lyrics.source ?? undefined,
        fromOtherVersion: lyrics.fromOtherVersion === true,
        lines:
            type === LyricsTypeObject.Static
                ? lines.map((line) => ({ ...line, start: null, end: null, words: [] }))
                : lines,
    }
}

/** True if the lyrics follow the music. */
export function isSynchronized(lyrics: TrackLyrics | null | undefined): boolean {
    return lyrics !== null && lyrics !== undefined && lyrics.type !== LyricsTypeObject.Static
}

/**
 * Index of the line sung at a position (in seconds): the last line that started; -1 before the
 * first line and for static lyrics. The lines are sorted by their start.
 */
export function activeLineIndex(lines: readonly LyricsLineTiming[], position: number): number {
    let low = 0
    let high = lines.length - 1
    let found = -1
    while (low <= high) {
        const middle = Math.floor((low + high) / 2)
        const start = lines[middle]?.start
        if (start !== null && start !== undefined && start <= position) {
            found = middle
            low = middle + 1
        } else {
            high = middle - 1
        }
    }

    return found
}

/** Index of the word sung at a position: the last word that started; -1 before the first. */
export function activeWordIndex(words: readonly LyricsWordTiming[], position: number): number {
    let found = -1
    for (const [index, word] of words.entries()) {
        if (word.start > position) {
            break
        }

        found = index
    }

    return found
}

/**
 * The end of a line: the start of the next line, otherwise the end of the track, otherwise a few
 * seconds after its start.
 */
export function lineEnd(line: LyricsLineTiming, duration: number): number {
    const start = line.start ?? 0
    if (line.end !== null) {
        return line.end
    }

    return duration > start ? duration : start + lastLineSeconds
}

/** The end of a word: the start of the next word, otherwise the end of its line. */
export function wordEnd(word: LyricsWordTiming, lineEndTime: number): number {
    return word.end ?? Math.max(word.start, lineEndTime)
}

/** Share of the time between start and end that has passed at a position, between 0 and 1. */
export function progressBetween(start: number, end: number, position: number): number {
    if (position <= start) {
        return 0
    }

    if (end <= start || position >= end) {
        return 1
    }

    return (position - start) / (end - start)
}

/** State of the line or word with an index against the active index. */
export function itemStateOf(index: number, active: number): LyricsItemState {
    if (index === active) {
        return 'active'
    }

    return index < active ? 'past' : 'upcoming'
}

/**
 * How far static lyrics have scrolled at a position: the share of the track that has passed, so
 * the text moves calmly from the top to the bottom over the whole track.
 */
export function staticScrollShare(position: number, duration: number): number {
    return duration > 0 ? progressBetween(0, duration, position) : 0
}
