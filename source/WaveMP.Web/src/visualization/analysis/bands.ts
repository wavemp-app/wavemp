// Levels and energies of frequency bands in a spectrum of an AnalyserNode. Pure functions; the
// visualizations (A-Force, BlackCap) share them. The energies work on the spectrum in decibels as
// getFloatFrequencyData writes it: unlike the bytes of getByteFrequencyData it has no upper limit,
// so loud music does not saturate, and a band sums the power of its bins instead of averaging
// decibels, so a kick drum rises as far as it really does.

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

    const { first, end } = binRange(count, sampleRate, band)
    if (first >= count) {
        return 0
    }

    let sum = 0
    for (let index = first; index < end; index++) {
        sum += spectrum[index] ?? 0
    }

    return Math.min(1, Math.max(0, sum / (end - first) / 255))
}

/** The first bin of a band and the bin after its last one, in a spectrum of count bins. */
function binRange(
    count: number,
    sampleRate: number,
    band: FrequencyBand,
): { first: number; end: number } {
    const binWidth = sampleRate / 2 / count
    const first = Math.max(0, Math.floor(band.low / binWidth))
    return { first, end: Math.min(count, Math.max(first + 1, Math.ceil(band.high / binWidth))) }
}

/** The levels of bass, mids and highs of a spectrum, see bandLevel. */
export function bandLevels(spectrum: ArrayLike<number>, sampleRate: number): BandLevels {
    return {
        bass: bandLevel(spectrum, sampleRate, bassBand),
        mid: bandLevel(spectrum, sampleRate, midBand),
        treble: bandLevel(spectrum, sampleRate, trebleBand),
    }
}

/** Lowest and highest frequency of the bands of bandSpectrum, in Hz. */
export const spectrumLow = 40
export const spectrumHigh = 14000

/**
 * The levels (0 to 1) of out.length bands from spectrumLow to spectrumHigh into out, spaced evenly
 * on a logarithmic scale as the ear hears pitch: the first band is the deep bass, the last the air
 * of the mix. A coarse spectrum a visualization can lay over a surface.
 */
export function bandSpectrum(
    spectrum: ArrayLike<number>,
    sampleRate: number,
    out: Float32Array,
): Float32Array {
    const count = out.length
    const ratio = spectrumHigh / spectrumLow
    for (let index = 0; index < count; index++) {
        out[index] = bandLevel(spectrum, sampleRate, {
            low: spectrumLow * Math.pow(ratio, index / count),
            high: spectrumLow * Math.pow(ratio, (index + 1) / count),
        })
    }

    return out
}

/**
 * Lowest energy in decibels: silence, and the bins getFloatFrequencyData reports as -Infinity,
 * count as this, so the energies stay finite.
 */
export const silentDecibels = -160

/** The energies of bass, mids and highs in decibels. */
export interface BandEnergies {
    readonly bass: number
    readonly mid: number
    readonly treble: number
}

/**
 * The energy of the bins of a spectrum between the frequencies of a band, in decibels: the power of
 * the bins (10^(dB/10)) summed and turned back into decibels. The spectrum holds decibels as
 * getFloatFrequencyData writes them, one bin per sampleRate / (2 * spectrum.length) Hz. A band
 * beyond the spectrum, silence and invalid values give silentDecibels.
 */
export function bandEnergy(
    spectrum: ArrayLike<number>,
    sampleRate: number,
    band: FrequencyBand,
): number {
    const count = spectrum.length
    if (count === 0 || !(sampleRate > 0)) {
        return silentDecibels
    }

    const { first, end } = binRange(count, sampleRate, band)
    let power = 0
    for (let index = first; index < end; index++) {
        const decibels = spectrum[index] ?? -Infinity
        if (decibels > silentDecibels && Number.isFinite(decibels)) {
            power += Math.pow(10, decibels / 10)
        }
    }

    return power > 0 ? Math.max(silentDecibels, 10 * Math.log10(power)) : silentDecibels
}

/** The energies of bass, mids and highs of a spectrum in decibels, see bandEnergy. */
export function bandEnergies(spectrum: ArrayLike<number>, sampleRate: number): BandEnergies {
    return {
        bass: bandEnergy(spectrum, sampleRate, bassBand),
        mid: bandEnergy(spectrum, sampleRate, midBand),
        treble: bandEnergy(spectrum, sampleRate, trebleBand),
    }
}

/** The energy of the whole spectrum from the lowest bass to the highs in decibels. */
export function totalEnergy(spectrum: ArrayLike<number>, sampleRate: number): number {
    return bandEnergy(spectrum, sampleRate, { low: bassBand.low, high: trebleBand.high })
}

/** The A-weighting (IEC 61672) of a frequency in Hz as a factor of power. */
export function aWeighting(frequency: number): number {
    const f2 = frequency * frequency
    const response =
        (12194 * 12194 * f2 * f2) /
        ((f2 + 20.6 * 20.6) *
            Math.sqrt((f2 + 107.7 * 107.7) * (f2 + 737.9 * 737.9)) *
            (f2 + 12194 * 12194))
    // +2 dB puts 1 kHz at 0 dB.
    return response * response * Math.pow(10, 0.2)
}

/** The A-weighting of every bin, for the last size of spectrum and sample rate asked for. */
let weights: { count: number; sampleRate: number; values: Float64Array } | null = null

function binWeights(count: number, sampleRate: number): Float64Array {
    if (weights?.count !== count || weights.sampleRate !== sampleRate) {
        const width = sampleRate / 2 / count
        weights = {
            count,
            sampleRate,
            values: Float64Array.from({ length: count }, (_, bin) => aWeighting(bin * width)),
        }
    }

    return weights.values
}

/**
 * The loudness of a spectrum in decibels as the ear weighs it: the power of all bins up to the
 * highs, A-weighted, so a deep bass counts less than voices and instruments of the same power and
 * a passage without its mids sounds quiet even if its bass goes on. silentDecibels for silence.
 */
export function loudness(spectrum: ArrayLike<number>, sampleRate: number): number {
    const count = spectrum.length
    if (count === 0 || !(sampleRate > 0)) {
        return silentDecibels
    }

    const values = binWeights(count, sampleRate)
    const { first, end } = binRange(count, sampleRate, { low: bassBand.low, high: trebleBand.high })
    let power = 0
    for (let index = first; index < end; index++) {
        const decibels = spectrum[index] ?? -Infinity
        if (decibels > silentDecibels && Number.isFinite(decibels)) {
            power += Math.pow(10, decibels / 10) * (values[index] ?? 0)
        }
    }

    return power > 0 ? Math.max(silentDecibels, 10 * Math.log10(power)) : silentDecibels
}

/** Frequency at which spectrumEnergies leaves the energy as it is (the tilt turns around it). */
const tiltCenter = 1000

/** Decibels per octave spectrumEnergies adds towards the highs: pink noise comes out flat. */
const tiltPerOctave = 3

/**
 * The energies of out.length bands from spectrumLow to spectrumHigh into out, spaced like those of
 * bandSpectrum, from a spectrum in decibels (see bandEnergy), comparable between the bands: the
 * mean power of the bins of a band (not their sum, so a wide band is not louder for its width),
 * tilted by tiltPerOctave, so pink noise, the rough balance of most music, gives the same energy
 * in every band. In decibels; silentDecibels for silent bands.
 */
export function spectrumEnergies(
    spectrum: ArrayLike<number>,
    sampleRate: number,
    out: Float32Array,
): Float32Array {
    const count = out.length
    const bins = spectrum.length
    const ratio = spectrumHigh / spectrumLow
    for (let index = 0; index < count; index++) {
        const band = {
            low: spectrumLow * Math.pow(ratio, index / count),
            high: spectrumLow * Math.pow(ratio, (index + 1) / count),
        }
        const energy = bandEnergy(spectrum, sampleRate, band)
        if (energy <= silentDecibels || bins === 0) {
            out[index] = silentDecibels
            continue
        }

        const { first, end } = binRange(bins, sampleRate, band)
        const center = Math.sqrt(band.low * band.high)
        out[index] =
            energy -
            10 * Math.log10(Math.max(1, end - first)) +
            tiltPerOctave * Math.log2(center / tiltCenter)
    }

    return out
}

/**
 * Moves every level of current towards target over the seconds of a frame: rises within about
 * attackSeconds, falls within about releaseSeconds (to two thirds each). Slow times make the levels
 * calm.
 */
export function followLevels(
    current: Float32Array,
    target: ArrayLike<number>,
    seconds: number,
    attackSeconds: number,
    releaseSeconds: number,
): Float32Array {
    const rise = seconds > 0 ? 1 - Math.exp(-seconds / Math.max(attackSeconds, 1e-6)) : 0
    const fall = seconds > 0 ? 1 - Math.exp(-seconds / Math.max(releaseSeconds, 1e-6)) : 0
    const count = Math.min(current.length, target.length)
    for (let index = 0; index < count; index++) {
        const value = current[index] ?? 0
        const goal = Number.isFinite(target[index]) ? (target[index] ?? 0) : 0
        current[index] = value + (goal - value) * (goal > value ? rise : fall)
    }

    return current
}
