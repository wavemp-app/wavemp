// Beat detection on the energy of a band (usually the bass), frame by frame. The detector compares
// the energy with its own recent history: a beat is a rise clearly above the average of the last
// moment (by a multiple of the standard deviation), at least a short pause after the previous one.
// From the intervals between beats it estimates the tempo. It knows nothing of Web Audio or
// drawing, so every visualization can use it and tests can drive it with plain numbers.

export interface BeatDetectorOptions {
    /** Seconds of history the threshold adapts to. */
    readonly windowSeconds?: number
    /** How far above the average energy a beat must rise, in standard deviations. */
    readonly sensitivity?: number
    /** Lowest rise above the average that counts as a beat, so silence and noise give none. */
    readonly minRise?: number
    /** Shortest time between two beats in seconds (0.25 s: 240 beats per minute). */
    readonly minIntervalSeconds?: number
    /** Seconds after which the pulse of a beat has faded to about a third. */
    readonly pulseSeconds?: number
}

/** The result of one frame. */
export interface BeatState {
    /** Whether a beat starts in this frame. */
    readonly beat: boolean
    /** Strength of the last beat, 0 to 1: the share of its energy above the average. */
    readonly strength: number
    /** The last beat fading out: its strength in its frame, falling towards 0 afterwards. */
    readonly pulse: number
    /** Seconds since the last beat, or null before the first one. */
    readonly sinceBeat: number | null
    /** Estimated tempo in beats per minute, or null while there are too few beats. */
    readonly bpm: number | null
}

const defaults: Required<BeatDetectorOptions> = {
    windowSeconds: 1.2,
    sensitivity: 1.4,
    minRise: 0.04,
    minIntervalSeconds: 0.25,
    pulseSeconds: 0.25,
}

/** Frames of history the detector needs before it reports beats. */
const minHistory = 8

/** Intervals between beats the tempo is estimated from. */
const tempoIntervals = 8

/** Fewest intervals for an estimate of the tempo. */
const minTempoIntervals = 4

/** Longest interval between beats that counts for the tempo (30 beats per minute). */
const maxTempoInterval = 2

export class BeatDetector {
    private readonly options: Required<BeatDetectorOptions>
    private readonly times: number[] = []
    private readonly energies: number[] = []
    private readonly intervals: number[] = []
    private previous: number | null = null
    private lastTime: number | null = null
    private lastBeat: number | null = null
    private lastStrength = 0

    constructor(options: BeatDetectorOptions = {}) {
        this.options = { ...defaults, ...options }
    }

    /**
     * Takes the energy of one frame (0 to 1) at a time in seconds and tells whether a beat starts.
     * Times must rise; an earlier time (e.g. a new audio context) starts the detection again.
     */
    update(energy: number, time: number): BeatState {
        const value = Number.isFinite(energy) ? Math.max(0, energy) : 0
        if (this.lastTime !== null && time < this.lastTime) {
            this.reset()
        }

        this.lastTime = time
        this.forget(time)

        const { mean, deviation } = this.statistics()
        const rise = value - mean
        const rising = this.previous !== null && value > this.previous
        const rested =
            this.lastBeat === null || time - this.lastBeat >= this.options.minIntervalSeconds
        const beat =
            this.energies.length >= minHistory &&
            rising &&
            rested &&
            rise >= this.options.minRise &&
            rise > this.options.sensitivity * deviation

        if (beat) {
            if (this.lastBeat !== null) {
                this.addInterval(time - this.lastBeat)
            }

            this.lastBeat = time
            this.lastStrength = Math.min(1, rise / Math.max(value, Number.EPSILON))
        }

        this.times.push(time)
        this.energies.push(value)
        this.previous = value

        const sinceBeat = this.lastBeat === null ? null : time - this.lastBeat
        return {
            beat,
            strength: this.lastStrength,
            pulse:
                sinceBeat === null
                    ? 0
                    : this.lastStrength * Math.exp(-sinceBeat / this.options.pulseSeconds),
            sinceBeat,
            bpm: this.bpm(),
        }
    }

    /** Forgets everything heard so far, e.g. after a pause. */
    reset(): void {
        this.times.length = 0
        this.energies.length = 0
        this.intervals.length = 0
        this.previous = null
        this.lastTime = null
        this.lastBeat = null
        this.lastStrength = 0
    }

    private forget(time: number): void {
        const oldest = time - this.options.windowSeconds
        let count = 0
        while (count < this.times.length && (this.times[count] ?? 0) < oldest) {
            count++
        }

        if (count > 0) {
            this.times.splice(0, count)
            this.energies.splice(0, count)
        }
    }

    private statistics(): { mean: number; deviation: number } {
        const count = this.energies.length
        if (count === 0) {
            return { mean: 0, deviation: 0 }
        }

        let sum = 0
        for (const energy of this.energies) {
            sum += energy
        }

        const mean = sum / count
        let squares = 0
        for (const energy of this.energies) {
            squares += (energy - mean) * (energy - mean)
        }

        return { mean, deviation: Math.sqrt(squares / count) }
    }

    private addInterval(interval: number): void {
        if (interval > maxTempoInterval) {
            // A long pause (a break in the music): the tempo starts again.
            this.intervals.length = 0
            return
        }

        this.intervals.push(interval)
        if (this.intervals.length > tempoIntervals) {
            this.intervals.shift()
        }
    }

    private bpm(): number | null {
        if (this.intervals.length < minTempoIntervals) {
            return null
        }

        const sorted = [...this.intervals].sort((a, b) => a - b)
        const middle = Math.floor(sorted.length / 2)
        const median =
            sorted.length % 2 === 0
                ? ((sorted[middle - 1] ?? 0) + (sorted[middle] ?? 0)) / 2
                : (sorted[middle] ?? 0)
        return median > 0 ? 60 / median : null
    }
}
