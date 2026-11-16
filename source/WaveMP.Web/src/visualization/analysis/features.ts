// What a visualization reacts to, frame by frame: the levels of bass, mids and highs and the beats.
// The levels follow rises fast and falls slowly (so the picture does not flicker) and are measured
// against their own recent peaks, so quiet and loud music both move the picture. Pure: the caller
// hands in the spectrum of its analyser and the time; the visualizations share it with the beat
// detection.

import { bandLevels } from './bands'
import { BeatDetector, type BeatDetectorOptions } from './beat'

export interface AudioFeatures {
    /** Level of the bass, 0 to 1, relative to its recent peaks. */
    readonly bass: number
    /** Level of the mids, 0 to 1, relative to their recent peaks. */
    readonly mid: number
    /** Level of the highs, 0 to 1, relative to their recent peaks. */
    readonly treble: number
    /** Overall loudness, 0 to 1 (not relative: silence stays 0). */
    readonly level: number
    /** Whether a beat starts in this frame. */
    readonly beat: boolean
    /** The last beat fading out, 0 to 1. */
    readonly pulse: number
    /** Estimated tempo in beats per minute, or null. */
    readonly bpm: number | null
}

export interface AudioFeatureOptions {
    /** Seconds a level needs to follow a rise (to about two thirds). */
    readonly attackSeconds?: number
    /** Seconds a level needs to follow a fall (to about two thirds). */
    readonly releaseSeconds?: number
    /** Seconds after which a peak has fallen to half. */
    readonly peakHalfLifeSeconds?: number
    /** Lowest peak a level is measured against, so near silence stays low. */
    readonly peakFloor?: number
    readonly beat?: BeatDetectorOptions
}

const defaults = {
    attackSeconds: 0.04,
    releaseSeconds: 0.22,
    peakHalfLifeSeconds: 4,
    peakFloor: 0.18,
}

/** Longest frame the smoothing accounts for; longer gaps (a pause) count as this long. */
const maxFrameSeconds = 0.25

export const silentFeatures: AudioFeatures = {
    bass: 0,
    mid: 0,
    treble: 0,
    level: 0,
    beat: false,
    pulse: 0,
    bpm: null,
}

type Band = 'bass' | 'mid' | 'treble'

const bands: readonly Band[] = ['bass', 'mid', 'treble']

export class AudioFeatureTracker {
    private readonly options: typeof defaults
    private readonly beats: BeatDetector
    private readonly smoothed: Record<Band, number> = { bass: 0, mid: 0, treble: 0 }
    private readonly peaks: Record<Band, number> = { bass: 0, mid: 0, treble: 0 }
    private level = 0
    private lastTime: number | null = null

    constructor(options: AudioFeatureOptions = {}) {
        this.options = {
            attackSeconds: options.attackSeconds ?? defaults.attackSeconds,
            releaseSeconds: options.releaseSeconds ?? defaults.releaseSeconds,
            peakHalfLifeSeconds: options.peakHalfLifeSeconds ?? defaults.peakHalfLifeSeconds,
            peakFloor: options.peakFloor ?? defaults.peakFloor,
        }
        this.beats = new BeatDetector(options.beat)
    }

    /**
     * The features of a frame: spectrum as getByteFrequencyData writes it (bytes 0 to 255), the
     * sample rate of its audio context and the time in seconds.
     */
    update(spectrum: ArrayLike<number>, sampleRate: number, time: number): AudioFeatures {
        if (this.lastTime !== null && time < this.lastTime) {
            this.reset()
        }

        const seconds = this.lastTime === null ? 0 : Math.min(maxFrameSeconds, time - this.lastTime)
        this.lastTime = time

        const raw = bandLevels(spectrum, sampleRate)
        const decay = Math.pow(0.5, seconds / this.options.peakHalfLifeSeconds)
        const relative: Record<Band, number> = { bass: 0, mid: 0, treble: 0 }
        for (const band of bands) {
            this.peaks[band] = Math.max(raw[band], this.peaks[band] * decay)
            relative[band] = Math.min(
                1,
                raw[band] / Math.max(this.options.peakFloor, this.peaks[band]),
            )
            this.smoothed[band] = this.follow(this.smoothed[band], relative[band], seconds)
        }

        this.level = this.follow(this.level, (raw.bass + raw.mid + raw.treble) / 3, seconds)
        const beat = this.beats.update(raw.bass, time)
        return {
            bass: this.smoothed.bass,
            mid: this.smoothed.mid,
            treble: this.smoothed.treble,
            level: this.level,
            beat: beat.beat,
            pulse: beat.pulse,
            bpm: beat.bpm,
        }
    }

    /** Forgets levels, peaks and beats, e.g. after a pause. */
    reset(): void {
        for (const band of bands) {
            this.smoothed[band] = 0
            this.peaks[band] = 0
        }

        this.level = 0
        this.lastTime = null
        this.beats.reset()
    }

    /** Moves a level towards its target: fast when it rises, slowly when it falls. */
    private follow(current: number, target: number, seconds: number): number {
        if (seconds <= 0) {
            return target
        }

        const tau = target > current ? this.options.attackSeconds : this.options.releaseSeconds
        return current + (target - current) * (1 - Math.exp(-seconds / tau))
    }
}
