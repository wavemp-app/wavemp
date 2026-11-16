import { describe, expect, it } from 'vitest'
import {
    bandLevel,
    bandLevels,
    bandSpectrum,
    bassBand,
    followLevels,
    midBand,
    spectrumHigh,
    spectrumLow,
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
