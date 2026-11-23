// What a visualization reacts to, frame by frame: the levels of bass, mids and highs, the loudness,
// quiet passages and the beats. It reads the spectrum in decibels (getFloatFrequencyData), sums the
// power of each band and measures it within an automatic range learnt from the music itself (see
// range.ts), so a kick drum reaches about 1 and the gap after it about 0, whatever the volume and
// however loud the music is mastered. The levels follow rises fast and falls more slowly, so the
// picture does not flicker. Beats are onsets: a sudden rise of the bass (positive flux), not its
// mere loudness, so a held bass note is no beat. Pure: the caller hands in the spectrum of its
// analyser and the time; the visualizations share it with the beat detection.

import { bandEnergies, loudness, silentDecibels } from './bands'
import { BeatDetector, type BeatDetectorOptions } from './beat'
import { AdaptiveRange, adaptiveRangeDefaults, type AdaptiveRangeOptions } from './range'

export interface AudioFeatures {
    /** Level of the bass, 0 to 1, within the range of its recent past. */
    readonly bass: number
    /** Level of the mids, 0 to 1, within the range of their recent past. */
    readonly mid: number
    /** Level of the highs, 0 to 1, within the range of their recent past. */
    readonly treble: number
    /**
     * Loudness, 0 to 1, compared with the loud parts of the music heard so far: the loud parts are
     * about 1, quieter passages lower, silence 0.
     */
    readonly level: number
    /**
     * How quiet the last seconds are compared with the music heard so far, 0 to 1: 0 in the loud
     * parts, 1 in a quiet passage and in silence.
     */
    readonly quiet: number
    /** Strength of the rise of the bass in this frame (the onset of a kick), 0 to 1. */
    readonly onset: number
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
    /** The automatic range of bass, mids and highs. */
    readonly range?: AdaptiveRangeOptions
    readonly beat?: BeatDetectorOptions
}

const defaults = {
    attackSeconds: 0.02,
    releaseSeconds: 0.16,
}

/**
 * The highs fall back faster than the other levels: they come from short sounds (hi-hats, the
 * ticks of cymbals), and the picture should sparkle with each of them.
 */
const trebleRelease = 0.5

/** Longest frame the smoothing accounts for; longer gaps (a pause) count as this long. */
const maxFrameSeconds = 0.25

/** Decibels below the loud parts at which the level reaches 0. */
const levelRangeDb = 15

/** Seconds the long-term loudness needs to follow a rise and a fall (to about two thirds). */
const longRiseSeconds = 3
const longFallSeconds = 40

/** Seconds the loudness of the last moments needs to follow a change (about a second). */
const recentSeconds = 0.6

/** Decibels below the long-term loudness at which a passage starts to be quiet, and is quiet. */
const quietStartDb = 3
const quietFullDb = 9

/** How far back the onset compares the bass: a kick rises within a few frames. */
const onsetSeconds = 0.07

/** A rise of the bass by this many decibels within onsetSeconds is the strongest onset. */
const onsetFullDb = 12

/**
 * The beat detector works on the onset strength: a beat needs a rise of the bass by at least a
 * quarter of onsetFullDb (3 dB), so the small wobbles of a quiet passage are no beats.
 */
const onsetBeat: BeatDetectorOptions = { minRise: 0.25 }

export const silentFeatures: AudioFeatures = {
    bass: 0,
    mid: 0,
    treble: 0,
    level: 0,
    quiet: 1,
    onset: 0,
    beat: false,
    pulse: 0,
    bpm: null,
}

type Band = 'bass' | 'mid' | 'treble'

const bands: readonly Band[] = ['bass', 'mid', 'treble']

export class AudioFeatureTracker {
    private readonly options: typeof defaults
    private readonly silenceDb: number
    private readonly beats: BeatDetector
    private readonly range: AdaptiveRange
    private readonly energies = new Float32Array(3)
    private readonly relative = new Float32Array(3)
    private readonly smoothed: Record<Band, number> = { bass: 0, mid: 0, treble: 0 }
    /** Recent energies of the bass in decibels with their times, for the onset. */
    private readonly bassHistory: { time: number; energy: number }[] = []
    private level = 0
    /** Weighted power of the spectrum: following fast (loudness), of the last moments (recent). */
    private loudness = 0
    private recent = 0
    /** Loudness of the music heard so far in decibels. */
    private longTerm: number | null = null
    private lastTime: number | null = null

    constructor(options: AudioFeatureOptions = {}) {
        this.options = {
            attackSeconds: options.attackSeconds ?? defaults.attackSeconds,
            releaseSeconds: options.releaseSeconds ?? defaults.releaseSeconds,
        }
        this.silenceDb = options.range?.silenceDb ?? adaptiveRangeDefaults.silenceDb
        this.range = new AdaptiveRange(bands.length, options.range)
        this.beats = new BeatDetector({ ...onsetBeat, ...options.beat })
    }

    /**
     * The features of a frame: spectrum in decibels as getFloatFrequencyData writes it, the sample
     * rate of its audio context and the time in seconds.
     */
    update(spectrum: ArrayLike<number>, sampleRate: number, time: number): AudioFeatures {
        if (this.lastTime !== null && time < this.lastTime) {
            this.reset()
        }

        const seconds = this.lastTime === null ? 0 : Math.min(maxFrameSeconds, time - this.lastTime)
        this.lastTime = time

        const energies = bandEnergies(spectrum, sampleRate)
        this.energies[0] = energies.bass
        this.energies[1] = energies.mid
        this.energies[2] = energies.treble
        this.range.update(this.energies, seconds, this.relative)
        bands.forEach((band, index) => {
            this.smoothed[band] = this.follow(
                this.smoothed[band],
                this.relative[index] ?? 0,
                seconds,
                this.options.attackSeconds,
                this.options.releaseSeconds * (band === 'treble' ? trebleRelease : 1),
            )
        })

        const level = this.measureLoudness(loudness(spectrum, sampleRate), seconds)
        const onset = this.onset(energies.bass, time)
        const beat = this.beats.update(onset, time)
        return {
            bass: this.smoothed.bass,
            mid: this.smoothed.mid,
            treble: this.smoothed.treble,
            level: level.level,
            quiet: level.quiet,
            onset,
            beat: beat.beat,
            pulse: beat.pulse,
            bpm: beat.bpm,
        }
    }

    /** Forgets levels, ranges, loudness and beats, e.g. after the time went backwards. */
    reset(): void {
        for (const band of bands) {
            this.smoothed[band] = 0
        }

        this.range.reset()
        this.bassHistory.length = 0
        this.level = 0
        this.loudness = 0
        this.recent = 0
        this.longTerm = null
        this.lastTime = null
        this.beats.reset()
    }

    /**
     * Level and quietness of the loudness (decibels, see loudness) of this frame. The loudness is
     * averaged as power, not as decibels, so the hits of the drums count as much as the ear hears
     * them and the short gaps between them do not pull the music down.
     */
    private measureLoudness(total: number, seconds: number): { level: number; quiet: number } {
        const power = total > silentDecibels ? Math.pow(10, total / 10) : 0
        const first = seconds <= 0
        this.loudness = first
            ? power
            : this.follow(
                  this.loudness,
                  power,
                  seconds,
                  this.options.attackSeconds,
                  this.options.releaseSeconds,
              )
        this.recent = first
            ? power
            : this.follow(this.recent, power, seconds, recentSeconds, recentSeconds)
        const recent = decibels(this.recent)

        // The long-term loudness learns from the music only; a pause keeps it for later.
        if (recent > this.silenceDb) {
            this.longTerm =
                this.longTerm === null
                    ? recent
                    : this.follow(this.longTerm, recent, seconds, longRiseSeconds, longFallSeconds)
        }

        if (this.longTerm === null || recent <= this.silenceDb) {
            this.level = this.follow(this.level, 0, seconds, 0, this.options.releaseSeconds)
            return { level: this.level, quiet: 1 }
        }

        this.level = clamp01(1 - (this.longTerm - decibels(this.loudness)) / levelRangeDb)
        return {
            level: this.level,
            quiet: clamp01((this.longTerm - recent - quietStartDb) / (quietFullDb - quietStartDb)),
        }
    }

    /** How far the bass rose within the last onsetSeconds, 0 to 1. */
    private onset(bass: number, time: number): number {
        const history = this.bassHistory
        history.push({ time, energy: bass })
        while (history.length > 2 && (history[1]?.time ?? time) <= time - onsetSeconds) {
            history.shift()
        }

        let lowest = bass
        for (const entry of history) {
            lowest = Math.min(lowest, entry.energy)
        }

        if (bass <= this.silenceDb) {
            return 0
        }

        return clamp01((bass - Math.max(lowest, this.silenceDb)) / onsetFullDb)
    }

    /** Moves a value towards its target: within attack when it rises, release when it falls. */
    private follow(
        current: number,
        target: number,
        seconds: number,
        attack: number,
        release: number,
    ): number {
        if (seconds <= 0) {
            return target
        }

        const tau = target > current ? attack : release
        if (tau <= 0) {
            return target
        }

        return current + (target - current) * (1 - Math.exp(-seconds / tau))
    }
}

/** Power in decibels, silentDecibels for silence. */
function decibels(power: number): number {
    return power > 0 ? Math.max(silentDecibels, 10 * Math.log10(power)) : silentDecibels
}

function clamp01(value: number): number {
    return Math.min(1, Math.max(0, value))
}
