import { describe, expect, it } from 'vitest'
import {
    aWeighting,
    bandEnergies,
    bandEnergy,
    bandLevel,
    bandLevels,
    bandSpectrum,
    bassBand,
    followLevels,
    loudness,
    midBand,
    silentDecibels,
    spectrumEnergies,
    spectrumHigh,
    spectrumLow,
    totalEnergy,
    trebleBand,
} from '../bands'

/** A spectrum of 1024 bins at 48 kHz (23.4 Hz per bin) with the given byte in a range of Hz. */
function spectrum(from: number, to: number, value = 255): Uint8Array {
    const bins = new Uint8Array(1024)
    const width = 48000 / 2 / bins.length
    for (let index = 0; index < bins.length; index++) {
        const frequency = index * width
        if (frequency >= from && frequency < to) {
            bins[index] = value
        }
    }

    return bins
}

describe('bandLevel', () => {
    it('averages the bins of the band', () => {
        const bins = spectrum(0, 24000, 102)

        expect(bandLevel(bins, 48000, midBand)).toBeCloseTo(0.4, 5)
    })

    it('sees only the frequencies of the band', () => {
        const bass = spectrum(30, 200)

        expect(bandLevel(bass, 48000, bassBand)).toBeGreaterThan(0.6)
        expect(bandLevel(bass, 48000, midBand)).toBe(0)
        expect(bandLevel(bass, 48000, trebleBand)).toBe(0)
    })

    it('is 0 for an empty spectrum, an invalid rate or a band beyond the spectrum', () => {
        expect(bandLevel(new Uint8Array(0), 48000, bassBand)).toBe(0)
        expect(bandLevel(spectrum(0, 24000), 0, bassBand)).toBe(0)
        expect(bandLevel(spectrum(0, 24000), 8000, { low: 5000, high: 6000 })).toBe(0)
    })

    it('takes at least one bin for a narrow band', () => {
        const bins = spectrum(0, 24000, 51)

        expect(bandLevel(bins, 48000, { low: 100, high: 101 })).toBeCloseTo(0.2, 5)
    })
})

describe('bandLevels', () => {
    it('separates bass, mids and highs', () => {
        const levels = bandLevels(spectrum(5000, 12000), 44100)

        expect(levels.bass).toBe(0)
        expect(levels.mid).toBe(0)
        expect(levels.treble).toBeGreaterThan(0.5)
    })
})

describe('bandSpectrum', () => {
    it('lays the bands from the deep bass to the highs', () => {
        const bass = bandSpectrum(spectrum(0, 120), 48000, new Float32Array(16))
        const highs = bandSpectrum(spectrum(8000, spectrumHigh), 48000, new Float32Array(16))

        expect(bass[0]).toBeGreaterThan(0.9)
        expect(bass[15]).toBe(0)
        expect(highs[0]).toBe(0)
        expect(highs[15]).toBeGreaterThan(0.9)
    })

    it('fills every band from 0 to 1 and gives silence as 0', () => {
        const full = bandSpectrum(spectrum(0, 24000), 48000, new Float32Array(16))
        const silent = bandSpectrum(new Uint8Array(1024), 48000, new Float32Array(16))

        expect([...full].every((level) => level > 0.99 && level <= 1)).toBe(true)
        expect([...silent].every((level) => level === 0)).toBe(true)
    })

    it('spaces the bands evenly on a logarithmic scale', () => {
        // The fifth of eight bands: from the middle of the range one eighth of it upwards.
        const ratio = spectrumHigh / spectrumLow
        const from = spectrumLow * Math.sqrt(ratio)
        const levels = bandSpectrum(
            spectrum(from, from * Math.pow(ratio, 1 / 8)),
            48000,
            new Float32Array(8),
        )

        expect(levels[4]).toBeGreaterThan(0.9)
        expect(levels[2]).toBe(0)
        expect(levels[6]).toBe(0)
    })
})

/** A spectrum in decibels of 1024 bins at 48 kHz: decibels in a range of Hz, silence elsewhere. */
function decibels(from: number, to: number, value: number): Float32Array {
    const bins = new Float32Array(1024).fill(-Infinity)
    const width = 48000 / 2 / bins.length
    for (let index = 0; index < bins.length; index++) {
        const frequency = index * width
        if (frequency >= from && frequency < to) {
            bins[index] = value
        }
    }

    return bins
}

describe('bandEnergy', () => {
    it('sums the power of the bins of the band', () => {
        // Ten bins at -30 dB: ten times the power of one, 10 dB more.
        const width = 48000 / 2 / 1024
        const bins = decibels(1000, 1000 + 10 * width - 1, -30)

        expect(bandEnergy(bins, 48000, midBand)).toBeCloseTo(-20, 1)
    })

    it('does not saturate however loud the band is', () => {
        const loud = bandEnergy(decibels(30, 200, 0), 48000, bassBand)
        const louder = bandEnergy(decibels(30, 200, 20), 48000, bassBand)

        expect(louder - loud).toBeCloseTo(20, 5)
    })

    it('gives silentDecibels for silence, an empty spectrum or an invalid rate', () => {
        expect(bandEnergy(decibels(0, 0, 0), 48000, bassBand)).toBe(silentDecibels)
        expect(bandEnergy(new Float32Array(0), 48000, bassBand)).toBe(silentDecibels)
        expect(bandEnergy(decibels(0, 24000, -20), 0, bassBand)).toBe(silentDecibels)
        expect(bandEnergy(new Float32Array(1024).fill(Number.NaN), 48000, bassBand)).toBe(
            silentDecibels,
        )
    })

    it('separates bass, mids and highs and sums the whole spectrum', () => {
        const energies = bandEnergies(decibels(5000, 12000, -40), 44100)
        const total = totalEnergy(decibels(5000, 12000, -40), 44100)

        expect(energies.bass).toBe(silentDecibels)
        expect(energies.mid).toBe(silentDecibels)
        expect(energies.treble).toBeGreaterThan(-40)
        expect(total).toBeCloseTo(energies.treble, 5)
    })
})

describe('loudness', () => {
    it('weighs the frequencies like the ear (A-weighting)', () => {
        const decibels = (frequency: number) => 10 * Math.log10(aWeighting(frequency))

        expect(decibels(1000)).toBeCloseTo(0, 1)
        expect(decibels(100)).toBeCloseTo(-19.1, 1)
        expect(decibels(50)).toBeCloseTo(-30.3, 1)
        expect(decibels(4000)).toBeCloseTo(1, 1)
    })

    it('hears a deep bass as quieter than mids of the same power', () => {
        const bass = loudness(decibels(40, 80, -20), 48000)
        const mids = loudness(decibels(1000, 1040, -20), 48000)

        expect(mids - bass).toBeGreaterThan(20)
    })

    it('rises with the power and gives silentDecibels for silence', () => {
        const quiet = loudness(decibels(500, 3000, -50), 48000)
        const loud = loudness(decibels(500, 3000, -30), 48000)

        expect(loud - quiet).toBeCloseTo(20, 5)
        expect(loudness(decibels(0, 0, 0), 48000)).toBe(silentDecibels)
        expect(loudness(new Float32Array(0), 48000)).toBe(silentDecibels)
    })
})

describe('spectrumEnergies', () => {
    it('gives pink noise the same energy in every band', () => {
        // Pink noise: the power of a bin falls with its frequency (3 dB per octave).
        const width = 48000 / 2 / 1024
        const pink = new Float32Array(1024).map(
            (_, bin) => -30 - 10 * Math.log10(Math.max(1, bin) * width),
        )
        const energies = spectrumEnergies(pink, 48000, new Float32Array(8))

        // The lowest band holds a single bin, so it is only close.
        const upper = [...energies].slice(2)
        expect(Math.max(...upper) - Math.min(...upper)).toBeLessThan(1.5)
    })

    it('finds the bands of a sound and keeps silent bands silent', () => {
        const bass = spectrumEnergies(decibels(0, 120, -20), 48000, new Float32Array(16))

        // -20 dB, tilted down by 3 dB per octave below 1 kHz (about 13 dB at 50 Hz).
        expect(bass[0]).toBeCloseTo(-33, 0)
        expect(bass[15]).toBe(silentDecibels)
    })
})

describe('followLevels', () => {
    it('rises faster than it falls', () => {
        const rising = followLevels(new Float32Array([0]), [1], 0.1, 0.1, 0.5)
        const falling = followLevels(new Float32Array([1]), [0], 0.1, 0.1, 0.5)

        expect(rising[0]).toBeCloseTo(1 - Math.exp(-1))
        expect(falling[0]).toBeCloseTo(Math.exp(-0.2))
    })

    it('stays put without time and ignores values that are not finite', () => {
        const levels = followLevels(new Float32Array([0.4, 0.4]), [1, Number.NaN], 0, 0.1, 0.5)

        expect([...levels]).toEqual([expect.closeTo(0.4), expect.closeTo(0.4)])
        followLevels(levels, [1, Number.NaN], 10, 0.1, 0.5)
        expect(levels[0]).toBeCloseTo(1)
        expect(levels[1]).toBeCloseTo(0)
    })
})
