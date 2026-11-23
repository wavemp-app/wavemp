import { describe, expect, it } from 'vitest'
import { SpectrumLevels } from '../spectrum'

const frame = 1 / 60

/** A spectrum in decibels of 1024 bins at 48 kHz with pink balance, the bins below `to` louder. */
function spectrum(louder: number, to = 0): Float32Array {
    const width = 48000 / 2 / 1024
    return new Float32Array(1024).map(
        (_, bin) =>
            -40 - 10 * Math.log10(Math.max(1, bin) * width) + (bin * width < to ? louder : 0),
    )
}

function run(levels: SpectrumLevels, frames: number, input: (frame: number) => Float32Array) {
    const out = new Float32Array(8)
    for (let index = 0; index < frames; index++) {
        levels.update(input(index), 48000, frame, out)
    }

    return out
}

describe('SpectrumLevels', () => {
    it('lays a steady, balanced sound high over every band', () => {
        const out = run(new SpectrumLevels(8), 120, () => spectrum(0))

        expect([...out].every((level) => level > 0.9)).toBe(true)
    })

    it('shows the balance: bands far below the loudest stay lower', () => {
        // The lows up to 500 Hz are 24 dB louder than the rest, steadily.
        const out = run(new SpectrumLevels(8), 240, () => spectrum(24, 500))

        expect(out[0]).toBeGreaterThan(0.9)
        expect(out[7]).toBeLessThan(0.7)
    })

    it('lets a band rise with each hit in it', () => {
        const levels = new SpectrumLevels(8)
        run(levels, 240, (index) => spectrum(index % 30 < 4 ? 15 : 0, 200))
        const gap = run(levels, 20, () => spectrum(0))[0]!
        const hit = run(levels, 2, () => spectrum(15, 200))[0]!

        expect(hit - gap).toBeGreaterThan(0.4)
    })

    it('keeps silence at 0', () => {
        const out = run(new SpectrumLevels(8), 60, () => new Float32Array(1024).fill(-Infinity))

        expect([...out]).toEqual(Array(8).fill(0))
    })
})
