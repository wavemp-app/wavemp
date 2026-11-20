import { describe, expect, it } from 'vitest'
import { coverSizeFor, coverSizes, defaultCoverSize, maxCoverPixelRatio } from '../covers'

describe('coverSizeFor', () => {
    it('rounds the shown width up to a size of the API', () => {
        expect(coverSizes).toEqual([96, 192, 300, 600, 1000, 2000])
        expect(coverSizeFor(40)).toBe(96)
        expect(coverSizeFor(96)).toBe(96)
        expect(coverSizeFor(97)).toBe(192)
        expect(coverSizeFor(180)).toBe(192)
        expect(coverSizeFor(250)).toBe(300)
        expect(coverSizeFor(400)).toBe(600)
        expect(coverSizeFor(900)).toBe(1000)
        expect(coverSizeFor(1200)).toBe(2000)
        expect(coverSizeFor(2400)).toBe(2000)
    })

    it('loads sharper covers on screens with a high pixel density, up to three times the width', () => {
        expect(maxCoverPixelRatio).toBe(3)
        expect(coverSizeFor(48, 2)).toBe(96)
        expect(coverSizeFor(64, 2)).toBe(192)
        expect(coverSizeFor(160, 1)).toBe(192)
        expect(coverSizeFor(160, 2)).toBe(600)
        expect(coverSizeFor(140, 3)).toBe(600)
        expect(coverSizeFor(48, 0.5)).toBe(96)
        expect(coverSizeFor(100, 4)).toBe(300)
    })

    it('never loads fewer pixels than shown on a phone with a density of 3 or a 4K screen', () => {
        // Mini player, list row, tile, stage of Now Playing and full-screen player of a phone.
        for (const width of [40, 44, 64, 176, 288, 400]) {
            expect(coverSizeFor(width, 3)).toBeGreaterThanOrEqual(width * 3)
        }

        // The stage of Now Playing on a 4K screen with a density of 1.5 or 2.
        for (const [width, ratio] of [
            [288, 2],
            [640, 1.5],
            [900, 2],
        ] as const) {
            expect(coverSizeFor(width, ratio)).toBeGreaterThanOrEqual(width * ratio)
        }
    })

    it('uses the default size while the width is unknown', () => {
        expect(defaultCoverSize).toBe(300)
        expect(coverSizeFor(0)).toBe(300)
        expect(coverSizeFor(Number.NaN)).toBe(300)
        expect(coverSizeFor(-10, 2)).toBe(300)
        expect(coverSizeFor(50, Number.NaN)).toBe(96)
    })
})
