// Where the song lies in a file (see the song analysis of the server): a music video may have an
// intro and an outro, and its version of the song may lack parts or add some. The server describes
// that as pieces: from fileStart on, the file plays the song from songStart on, for a duration. The
// time of the song is the time of the audio file, which synchronized lyrics follow. A file without
// pieces is the song as a whole. Pure functions; positions in seconds.

import {
    PlayerViewModeObject,
    SongSegmentMethodObject,
    SoundSourceObject,
    type PlayerViewMode,
    type SongSegment,
    type SoundSource,
} from '@/api/generated/models'

/** A piece of a file that belongs to the song, in seconds. */
interface Piece {
    readonly file: number
    readonly song: number
    readonly length: number
}

function piecesOf(segment: SongSegment | null | undefined): Piece[] {
    return (segment?.pieces ?? [])
        .map((piece) => ({
            file: (piece.fileStartMilliseconds ?? 0) / 1000,
            song: (piece.songStartMilliseconds ?? 0) / 1000,
            length: (piece.durationMilliseconds ?? 0) / 1000,
        }))
        .filter((piece) => piece.length > 0)
        .sort((a, b) => a.file - b.file)
}

/**
 * The position of the song at a position of the file; NaN outside of it (intro, outro or a part the
 * song does not have). A file without pieces is the song as it is.
 */
export function songPosition(segment: SongSegment | null | undefined, position: number): number {
    const pieces = piecesOf(segment)
    if (pieces.length === 0) {
        return position
    }

    const piece = pieces.find((p) => position >= p.file && position < p.file + p.length)
    return piece ? piece.song + (position - piece.file) : Number.NaN
}

/**
 * The position of the song at a position of the file, or the nearest one outside of it: the start
 * of the song during the intro, its end during the outro and the nearer edge in an added part.
 */
export function nearestSongPosition(
    segment: SongSegment | null | undefined,
    position: number,
): number {
    const pieces = piecesOf(segment)
    if (pieces.length === 0) {
        return Math.max(0, position)
    }

    let best = pieces[0]!.song
    let distance = Number.POSITIVE_INFINITY
    for (const piece of pieces) {
        const end = piece.file + piece.length
        const inside = Math.min(Math.max(position, piece.file), end)
        const gap = Math.abs(position - inside)
        if (gap < distance) {
            distance = gap
            best = piece.song + (inside - piece.file)
        }
    }

    return best
}

/**
 * The position of the file that plays a position of the song; a part of the song the file lacks
 * goes to the nearest piece. Before the first piece (a file that starts late in the song) it is the
 * start of that piece, after the last one its end.
 */
export function filePosition(segment: SongSegment | null | undefined, song: number): number {
    const pieces = piecesOf(segment)
    if (pieces.length === 0) {
        return Math.max(0, song)
    }

    let best = pieces[0]!.file
    let distance = Number.POSITIVE_INFINITY
    for (const piece of pieces) {
        const inside = Math.min(Math.max(song, piece.song), piece.song + piece.length)
        const gap = Math.abs(song - inside)
        if (gap < distance) {
            distance = gap
            best = piece.file + (inside - piece.song)
        }
    }

    return best
}

/**
 * The position in the other source (e.g. the video) for a position in one source (e.g. the audio
 * file) of the same song, so both play the same moment; an intro or outro maps to the nearest
 * position of the song.
 */
export function mapPosition(
    from: SongSegment | null | undefined,
    to: SongSegment | null | undefined,
    position: number,
): number {
    return filePosition(to, nearestSongPosition(from, position))
}

/** The part of the file that is played when intro and outro are skipped. */
export interface SkipWindow {
    /** Where the song starts in the file. */
    readonly start: number
    /** Where the song ends in the file; undefined to the end of the file. */
    readonly end: number | undefined
}

/**
 * The song within its file when intro and outro are skipped: only with the setting, for a sure
 * result (or a correction of the admin) that found more than the whole file; null otherwise.
 */
export function skipWindow(
    segment: SongSegment | null | undefined,
    enabled: boolean,
): SkipWindow | null {
    if (!enabled || !segment?.confident || piecesOf(segment).length === 0) {
        return null
    }

    if (segment.method === SongSegmentMethodObject.WholeFile && !segment.manual) {
        return null
    }

    const start = Math.max(0, (segment.startMilliseconds ?? 0) / 1000)
    const end =
        typeof segment.endMilliseconds === 'number' ? segment.endMilliseconds / 1000 : undefined
    return end !== undefined && end <= start ? null : { start, end }
}

/**
 * Which sound the player wants for a view: the sound of the video while the video is shown, the
 * audio file otherwise. The server plays the other one when the wanted one is missing.
 */
export function soundForView(viewMode: PlayerViewMode, hasVideo: boolean): SoundSource {
    return viewMode === PlayerViewModeObject.Video && hasVideo
        ? SoundSourceObject.Video
        : SoundSourceObject.Audio
}

/** The factor of a gain in dB; 1 without one. */
export function loudnessMatchFactor(db: number | null | undefined): number {
    return typeof db === 'number' && Number.isFinite(db) ? 10 ** (db / 20) : 1
}
