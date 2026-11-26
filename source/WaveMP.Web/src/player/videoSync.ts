// How the muted video of the Now Playing stage follows the audio, which always leads. The video
// should show the position of the audio plus the offset of the track; small differences are made
// up by playing the video a little faster or slower in a few steps, larger ones by seeking. A seek
// waits until the video can play and the last seek is done, aims ahead by the time seeks take and
// waits longer after each seek that missed, so a slow stream never ends up in a loop of seeks.
// Pure functions, so the rules are tested without a browser.

import { PlayerViewModeObject, type PlayerViewMode } from '@/api/generated/models'

/** What the stage shows. */
export type StageView = 'cover' | 'video' | 'visualization' | 'lyrics'

/** State of the video of the current track: not loaded, loading or positioning, ready, failed. */
export type VideoState = 'idle' | 'loading' | 'ready' | 'failed'

/** Seconds of difference from which a video file seeks instead of changing its speed. */
export const hardSyncThreshold = 0.3

/**
 * Seconds of difference from which an HLS stream seeks: a seek of a stream may have to load
 * segments first, so it puts up with a larger difference before it jumps.
 */
export const hlsHardSyncThreshold = 0.5

/** Seconds of difference below which the video is in sync. */
export const softSyncThreshold = 0.04

/**
 * Seconds of difference from which the video starts to change its speed. Clearly above the
 * threshold of the sync, so the small jitter of the positions never changes the speed; once it
 * runs faster or slower, it does so until the difference is below softSyncThreshold (hysteresis).
 */
export const rateStartThreshold = 0.08

/**
 * The few speeds a correction uses, as a share of the normal speed, each from a difference in
 * seconds on. A new speed with every check makes some browsers (Safari above all) drop pictures,
 * so the speed only changes when the difference moves into another step.
 */
export const rateSteps: readonly { readonly from: number; readonly change: number }[] = [
    { from: 0, change: 0.02 },
    { from: 0.1, change: 0.05 },
    { from: 0.2, change: 0.08 },
]

/** Largest change of the speed of the video, as a share of the normal speed. */
export const maxRateCorrection = 0.08

/** Seconds below the start of its step a difference has to fall to take the smaller step. */
export const rateStepHysteresis = 0.03

/** Seconds between two seeks of a video file. */
export const minSeekInterval = 1

/** Seconds between two seeks of an HLS stream. */
export const hlsMinSeekInterval = 3

/** Most seconds between two seeks once seeks did not bring the video into sync. */
export const maxSeekInterval = 16

/**
 * Seconds of difference from which the video seeks after the shortest interval even after seeks
 * that missed: the audio jumped (a seek of the user), which the video follows at once.
 */
export const jumpThreshold = 5

/** Most seconds a seek of a playing video aims ahead of the audio to make up for its own time. */
export const maxSeekLead = 2

/** Seconds before the end of the video at which it stops on its last picture. */
export const endMargin = 0.05

export interface SyncInput {
    /** Position of the audio in seconds. */
    readonly audioPosition: number
    /** Whether the audio plays. */
    readonly playing: boolean
    /** Position of the video in seconds. */
    readonly videoPosition: number
    /** Offset of the video against the audio in seconds. */
    readonly offset: number
    /** Duration of the video in seconds; 0 while unknown. */
    readonly videoDuration: number
    /** Seconds since the last seek of the video. */
    readonly sinceLastSeek: number
    /** Whether the video is an HLS stream (larger threshold, longer interval between seeks). */
    readonly hls?: boolean
    /** The speed the video plays at now; 1 is the normal speed. */
    readonly rate?: number
    /**
     * Whether the shown video waits for data: it cannot play on at its position (readyState below
     * HAVE_FUTURE_DATA). A seek then only makes it wait longer.
     */
    readonly buffering?: boolean
    /** Whether the last seek is not done yet: the video did not play again since. */
    readonly settling?: boolean
    /** Seeks in a row after which the video still was not in sync; each doubles the interval. */
    readonly missedSeeks?: number
    /** Seconds a seek of the video took so far (from the seek until it played again). */
    readonly seekLatency?: number
}

export type SyncAction =
    /** The video waits paused at this position: it starts later or already ended. */
    | { readonly kind: 'hold'; readonly at: number }
    /** The video jumps to this position. */
    | { readonly kind: 'seek'; readonly to: number }
    /** The video plays (or waits) at this speed; 1 is the normal speed. */
    | { readonly kind: 'rate'; readonly rate: number }
    /** The video plays on at the normal speed until it can play again (buffering or seeking). */
    | { readonly kind: 'wait' }

/** The position of the video that belongs to a position of the audio. */
export function videoTarget(audioPosition: number, offset: number): number {
    return audioPosition + offset
}

function clamp(value: number, min: number, max: number): number {
    return Math.min(max, Math.max(min, value))
}

/** Seconds of difference from which the video seeks. */
export function hardThresholdOf(hls: boolean): number {
    return hls ? hlsHardSyncThreshold : hardSyncThreshold
}

/**
 * Seconds that have to pass between two seeks: the shortest interval of the kind of video, doubled
 * with every seek that missed, up to maxSeekInterval. A jump of the audio needs no back-off.
 */
export function seekIntervalOf(hls: boolean, missedSeeks: number, distance = 0): number {
    const base = hls ? hlsMinSeekInterval : minSeekInterval
    if (distance > jumpThreshold || missedSeeks <= 0) {
        return base
    }

    return Math.min(maxSeekInterval, base * 2 ** Math.min(missedSeeks, 8))
}

/** How many seeks in a row missed, after a seek is done with this distance to the audio. */
export function missedSeeksAfter(missedSeeks: number, distance: number, hls: boolean): number {
    return distance > hardThresholdOf(hls) ? missedSeeks + 1 : 0
}

/**
 * The time of a seek measured so far, updated by a new measurement in seconds: the mean of both,
 * so a single slow seek does not make the next one aim too far ahead.
 */
export function nextSeekLatency(previous: number | null, measured: number): number {
    const value = clamp(measured, 0, maxSeekLead * 2)
    return previous === null ? value : (previous + value) / 2
}

/** Index of the step of the speed for a difference in seconds. */
function stepIndexOf(distance: number): number {
    let index = 0
    rateSteps.forEach((step, candidate) => {
        if (distance >= step.from) {
            index = candidate
        }
    })
    return index
}

/** Index of the step a speed belongs to; -1 for the normal speed or a speed of no step. */
function stepIndexOfRate(rate: number): number {
    const change = Math.abs(rate - 1)
    return rateSteps.findIndex((step) => Math.abs(step.change - change) < 1e-6)
}

/**
 * The speed that makes up a difference: one of a few steps, chosen with hysteresis, so the speed
 * changes rarely. Positive drift: the video is ahead of the audio.
 */
export function rateFor(drift: number, currentRate: number): number {
    const distance = Math.abs(drift)
    if (distance <= softSyncThreshold) {
        return 1
    }

    // 1: the video has to run faster, -1: slower.
    const direction = drift < 0 ? 1 : -1
    const current = Math.sign(Math.round((currentRate - 1) * 1e6))
    if (current === 0) {
        return distance < rateStartThreshold
            ? 1
            : 1 + direction * rateSteps[stepIndexOf(distance)]!.change
    }

    // It went past the position of the audio: the normal speed again.
    if (current !== direction) {
        return 1
    }

    const wanted = stepIndexOf(distance)
    const step = stepIndexOfRate(currentRate)
    if (step < 0 || wanted > step) {
        return 1 + direction * rateSteps[wanted]!.change
    }

    // A smaller step only once the difference is clearly below the start of the current one.
    const lower = stepIndexOf(distance + rateStepHysteresis)
    return 1 + direction * rateSteps[Math.min(lower, step)]!.change
}

/** What the video does to show the position of the audio. */
export function syncAction(input: SyncInput): SyncAction {
    const target = videoTarget(input.audioPosition, input.offset)
    if (target < 0) {
        return { kind: 'hold', at: 0 }
    }

    const end = Math.max(0, input.videoDuration - endMargin)
    if (input.videoDuration > 0 && target >= end) {
        return { kind: 'hold', at: end }
    }

    const hls = input.hls === true
    // Positive: the video is ahead of the audio.
    const drift = input.videoPosition - target
    const distance = Math.abs(drift)
    if (!input.playing) {
        // A paused video cannot catch up by its speed: it shows the exact picture.
        const mayJump = !input.settling && input.sinceLastSeek >= seekIntervalOf(hls, 0)
        return distance > softSyncThreshold && mayJump
            ? { kind: 'seek', to: target }
            : { kind: 'rate', rate: 1 }
    }

    // A video that waits for data or for its last seek is not moved again: it would only start
    // to wait anew (the loop of seeks that makes a video stutter).
    if (input.buffering || input.settling) {
        return { kind: 'wait' }
    }

    if (distance > hardThresholdOf(hls)) {
        const interval = seekIntervalOf(hls, input.missedSeeks ?? 0, distance)
        if (input.sinceLastSeek >= interval) {
            // While the video seeks, the audio plays on: the seek aims ahead by the time seeks take.
            const lead = clamp(input.seekLatency ?? 0, 0, maxSeekLead)
            const to = input.videoDuration > 0 ? Math.min(end, target + lead) : target + lead
            return { kind: 'seek', to }
        }

        return { kind: 'rate', rate: 1 + (drift < 0 ? 1 : -1) * maxRateCorrection }
    }

    return { kind: 'rate', rate: rateFor(drift, input.rate ?? 1) }
}

/**
 * The view the stage shows for the chosen mode: the video only when the track has one and it is
 * ready at the right position, the lyrics only when the track has lyrics, otherwise the cover.
 */
export function stageViewOf(
    mode: PlayerViewMode,
    hasVideo: boolean,
    video: VideoState,
    hasLyrics = false,
): StageView {
    if (mode === PlayerViewModeObject.Visualization) {
        return 'visualization'
    }

    if (mode === PlayerViewModeObject.Lyrics) {
        return hasLyrics ? 'lyrics' : 'cover'
    }

    return mode === PlayerViewModeObject.Video && hasVideo && video === 'ready' ? 'video' : 'cover'
}
