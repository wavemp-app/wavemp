// An automatic range for energies in decibels, one per value (e.g. per band): each value is
// measured between a floor and a peak it learns from its own recent past, so a kick drum reaches
// about 1 and the gap after it about 0, whatever the volume and the mastering of the music. The
// peak follows a rise at once and falls slowly; the floor follows a fall at once and rises slowly,
// so it settles on the quiet moments between the hits. The range is at least minRangeDb wide, so a
// steady sound reads loud and noise is not blown up. Pure: the caller hands in the energies and the
// seconds since the last frame.

import { silentDecibels } from './bands'

export interface AdaptiveRangeOptions {
    /** Narrowest range between floor and peak in decibels. */
    readonly minRangeDb?: number
    /**
     * Widest range in decibels: a floor far below the peak (a sound that dies away completely
     * between its hits) would squeeze the hits together near the top.
     */
    readonly maxRangeDb?: number
    /** Decibels per second the peak falls after a loud moment. */
    readonly peakFallDbPerSecond?: number
    /** Seconds the floor needs to follow a rise (to about two thirds). */
    readonly floorRiseSeconds?: number
    /** Seconds the floor needs to follow a fall (to about two thirds). */
    readonly floorFallSeconds?: number
    /**
     * Energy in decibels below which a value counts as silent (level 0); the level fades in over
     * the first minRangeDb above it.
     */
    readonly silenceDb?: number
}

export const adaptiveRangeDefaults: Required<AdaptiveRangeOptions> = {
    minRangeDb: 12,
    maxRangeDb: 36,
    peakFallDbPerSecond: 1.5,
    floorRiseSeconds: 6,
    floorFallSeconds: 0.1,
    silenceDb: -100,
}

export class AdaptiveRange {
    /** Number of values (bands) the range measures. */
    readonly count: number
    private readonly options: Required<AdaptiveRangeOptions>
    private readonly peaks: Float64Array
    private readonly floors: Float64Array
    private started = false

    constructor(count: number, options: AdaptiveRangeOptions = {}) {
        this.count = count
        this.options = { ...adaptiveRangeDefaults, ...options }
        this.peaks = new Float64Array(count)
        this.floors = new Float64Array(count)
    }

    /**
     * Measures the energies (decibels) of a frame that lasted seconds and writes their levels, 0 to
     * 1, into out. The first frame after the start or a reset takes the energies as floor and peak.
     */
    update(energies: ArrayLike<number>, seconds: number, out: Float32Array): Float32Array {
        const {
            minRangeDb,
            maxRangeDb,
            peakFallDbPerSecond,
            floorRiseSeconds,
            floorFallSeconds,
            silenceDb,
        } = this.options
        const time = Math.max(0, seconds)
        const fall = peakFallDbPerSecond * time
        const rise = 1 - Math.exp(-time / Math.max(floorRiseSeconds, 1e-6))
        const drop = 1 - Math.exp(-time / Math.max(floorFallSeconds, 1e-6))
        const count = Math.min(this.count, energies.length, out.length)
        for (let index = 0; index < count; index++) {
            const value = energies[index]
            const energy =
                value !== undefined && Number.isFinite(value)
                    ? Math.max(silentDecibels, value)
                    : silentDecibels
            if (!this.started) {
                this.peaks[index] = energy
                this.floors[index] = energy
            }

            // Silence teaches nothing: the range of the music is kept for when it plays again.
            if (energy > silenceDb) {
                const floor = this.floors[index] ?? energy
                this.peaks[index] = Math.max(energy, (this.peaks[index] ?? energy) - fall)
                // After silence the floor starts again where the music is.
                this.floors[index] =
                    floor <= silenceDb
                        ? energy
                        : floor + (energy - floor) * (energy > floor ? rise : drop)
            }

            const peak = Math.max(this.peaks[index] ?? energy, silenceDb + minRangeDb)
            const floor = this.floors[index] ?? energy
            const low = Math.max(Math.min(floor, peak - minRangeDb), peak - maxRangeDb)
            const level = (energy - low) / Math.max(peak - low, 1e-6)
            const audible = (energy - silenceDb) / minRangeDb
            out[index] = clamp01(level) * clamp01(audible)
        }

        this.started = true
        return out
    }

    /** Forgets floors and peaks, e.g. when the time goes backwards. */
    reset(): void {
        this.started = false
    }
}

function clamp01(value: number): number {
    return Math.min(1, Math.max(0, value))
}
