import { describe, expect, it } from 'vitest'
import { AdaptiveRange } from '../range'

const frame = 1 / 60

/** Feeds one value per frame and returns the levels. */
function levels(range: AdaptiveRange, energies: readonly number[]): number[] {
    const out = new Float32Array(1)
    return energies.map((energy) => range.update([energy], frame, out)[0]!)
}

/** Hits at the top decibels every half second, the bottom decibels in between. */
function hits(top: number, bottom: number, seconds: number): number[] {
    return Array.from({ length: Math.round(seconds / frame) }, (_, index) =>
        (index * frame) % 0.5 < 0.1 ? top : bottom,
    )
}

describe('AdaptiveRange', () => {
    it('measures hits at the top and the gaps between them at the bottom', () => {
        const result = levels(new AdaptiveRange(1), hits(-20, -40, 4)).slice(120)

        expect(Math.max(...result)).toBeCloseTo(1, 5)
        expect(Math.min(...result)).toBeCloseTo(0, 5)
    })

    it('gives the same levels at any volume', () => {
        const loud = levels(new AdaptiveRange(1), hits(-20, -40, 4))
        const quiet = levels(new AdaptiveRange(1), hits(-50, -70, 4))

        loud.forEach((level, index) => expect(quiet[index]).toBeCloseTo(level, 5))
    })

    it('keeps a range of at least minRangeDb, so small wobbles stay small', () => {
        const result = levels(new AdaptiveRange(1, { minRangeDb: 12 }), hits(-20, -23, 4)).slice(
            120,
        )

        expect(Math.min(...result)).toBeCloseTo(0.75, 1)
        expect(Math.max(...result)).toBeCloseTo(1, 5)
    })

    it('keeps a range of at most maxRangeDb, so a gap of silence does not squeeze the hits', () => {
        const result = levels(
            new AdaptiveRange(1, { maxRangeDb: 30 }),
            hits(-20, -90, 2).map((energy, index) => (index % 30 === 15 ? -35 : energy)),
        )

        // A softer hit 15 decibels below the loud ones sits in the middle, not near the top.
        const soft = result.filter((_, index) => index % 30 === 15 && index > 60)
        soft.forEach((level) => expect(level).toBeCloseTo(0.5, 1))
    })

    it('lets the peak fall slowly after a loud moment', () => {
        const range = new AdaptiveRange(1, { peakFallDbPerSecond: 2 })
        levels(range, hits(-10, -30, 2))
        // Five seconds of hits ten decibels softer: the peak has fallen to them by then.
        const softer = levels(range, hits(-20, -30, 5))

        expect(softer[5]).toBeLessThan(0.7)
        expect(Math.max(...softer.slice(-60))).toBeCloseTo(1, 5)
    })

    it('is silent below silenceDb and keeps the range of the music through a pause', () => {
        const range = new AdaptiveRange(1, { silenceDb: -100 })
        levels(range, hits(-20, -40, 2))
        const pause = levels(range, Array(120).fill(-Infinity) as number[])
        const again = levels(range, hits(-20, -40, 0.5))

        expect(pause.every((level) => level === 0)).toBe(true)
        expect(again[0]).toBeCloseTo(1, 5)
        expect(again[10]).toBeCloseTo(0, 5)
    })

    it('measures every value on its own', () => {
        const range = new AdaptiveRange(2)
        const out = new Float32Array(2)
        for (let index = 0; index < 240; index++) {
            const hit = (index * frame) % 0.5 < 0.1
            range.update([hit ? -20 : -40, -60], frame, out)
        }

        // The second value never changed: a steady sound reads loud.
        expect(out[1]).toBeCloseTo(1, 5)
        expect(out[0]).toBeCloseTo(0, 5)
    })

    it('starts again after a reset', () => {
        const range = new AdaptiveRange(1)
        levels(range, hits(-10, -60, 2))
        range.reset()

        expect(levels(range, [-80, -80])).toEqual([1, 1])
    })
})
