// Levels of frequency bands in a spectrum of an AnalyserNode. Pure functions; the visualizations
// (A-Force, BlackCap) share them.

/** A range of frequencies in Hz. */
export interface FrequencyBand {
    readonly low: number
    readonly high: number
}

/** Bass: kick drum and bass lines. */
export const bassBand: FrequencyBand = { low: 20, high: 250 }

/** Mids: voices, guitars, keys and snare. */
export const midBand: FrequencyBand = { low: 250, high: 4000 }

/** Highs: cymbals, hi-hats and the air of the mix. */
export const trebleBand: FrequencyBand = { low: 4000, high: 16000 }

/** The levels of bass, mids and highs, each from 0 to 1. */
export interface BandLevels {
    readonly bass: number
    readonly mid: number
    readonly treble: number
}

/**
 * The average level (0 to 1) of the bins of a spectrum between the frequencies of a band. The
 * spectrum holds bytes (0 to 255) as getByteFrequencyData writes them, one bin per sampleRate /
 * (2 * spectrum.length) Hz. A band beyond the spectrum has the level 0.
 */
export function bandLevel(
    spectrum: ArrayLike<number>,
    sampleRate: number,
    band: FrequencyBand,
): number {
    const count = spectrum.length
    if (count === 0 || !(sampleRate > 0)) {
        return 0
    }

    const binWidth = sampleRate / 2 / count
    const first = Math.max(0, Math.floor(band.low / binWidth))
    const end = Math.min(count, Math.max(first + 1, Math.ceil(band.high / binWidth)))
    if (first >= count) {
        return 0
    }

    let sum = 0
    for (let index = first; index < end; index++) {
        sum += spectrum[index] ?? 0
    }

    return Math.min(1, Math.max(0, sum / (end - first) / 255))
}

/** The levels of bass, mids and highs of a spectrum, see bandLevel. */
export function bandLevels(spectrum: ArrayLike<number>, sampleRate: number): BandLevels {
    return {
        bass: bandLevel(spectrum, sampleRate, bassBand),
        mid: bandLevel(spectrum, sampleRate, midBand),
        treble: bandLevel(spectrum, sampleRate, trebleBand),
    }
}
