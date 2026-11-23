// A coarse spectrum a visualization can lay over a surface, from the spectrum in decibels of an
// analyser: per band (spaced like the ear hears pitch, see spectrumEnergies) a level from 0 to 1
// that shows both
// - the balance of the music: bands far below the loudest one stay low, and
// - its motion: every band is also measured within the automatic range of its own recent past
//   (see range.ts), so a band rises with each hit in it, whatever the volume.
// Pure: the caller hands in the spectrum and the seconds since the last frame.

import { silentDecibels, spectrumEnergies } from './bands'
import { AdaptiveRange, adaptiveRangeDefaults, type AdaptiveRangeOptions } from './range'

/** Decibels below the loudest band at which the balance part of a level reaches 0. */
const balanceRangeDb = 30

/** Share of the balance in a level; the rest is the motion of the band itself. */
const balanceShare = 0.5

export class SpectrumLevels {
    private readonly energies: Float32Array
    private readonly motion: Float32Array
    private readonly range: AdaptiveRange
    private readonly silenceDb: number

    constructor(count: number, options: AdaptiveRangeOptions = {}) {
        this.silenceDb = options.silenceDb ?? adaptiveRangeDefaults.silenceDb
        this.energies = new Float32Array(count)
        this.motion = new Float32Array(count)
        this.range = new AdaptiveRange(count, options)
    }

    /**
     * The levels (0 to 1) of out.length bands of a spectrum in decibels (getFloatFrequencyData) of
     * a frame that lasted seconds, written into out.
     */
    update(
        spectrum: ArrayLike<number>,
        sampleRate: number,
        seconds: number,
        out: Float32Array,
    ): Float32Array {
        spectrumEnergies(spectrum, sampleRate, this.energies)
        this.range.update(this.energies, seconds, this.motion)
        let loudest = silentDecibels
        for (const energy of this.energies) {
            loudest = Math.max(loudest, energy)
        }

        const count = Math.min(out.length, this.energies.length)
        for (let index = 0; index < count; index++) {
            const motion = this.motion[index] ?? 0
            const energy = this.energies[index] ?? silentDecibels
            // A silent band stays down, however the balance looks.
            const balance =
                energy <= this.silenceDb
                    ? 0
                    : Math.min(1, Math.max(0, 1 - (loudest - energy) / balanceRangeDb))
            out[index] = balanceShare * balance + (1 - balanceShare) * motion
        }

        return out
    }

    /** Forgets the ranges, e.g. when the time goes backwards. */
    reset(): void {
        this.range.reset()
    }
}
