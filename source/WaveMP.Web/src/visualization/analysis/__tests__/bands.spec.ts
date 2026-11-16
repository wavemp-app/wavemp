import { describe, expect, it } from 'vitest'
import { bandLevel, bandLevels, bassBand, midBand, trebleBand } from '../bands'

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
