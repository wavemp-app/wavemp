import { describe, expect, it } from 'vitest'
import { contrastColor, contrastRatio, cssColor, dominantColor, type Rgb } from '../coverColor'

/** RGBA pixels: a number of pixels of each color, opaque unless an alpha is given. */
function pixels(...groups: Array<[Rgb, number, number?]>): Uint8ClampedArray {
    const values: number[] = []
    for (const [{ r, g, b }, count, alpha = 255] of groups) {
        for (let index = 0; index < count; index++) {
            values.push(r, g, b, alpha)
        }
    }
    return new Uint8ClampedArray(values)
}

const red: Rgb = { r: 200, g: 30, b: 40 }
const blue: Rgb = { r: 20, g: 90, b: 210 }
const grey: Rgb = { r: 128, g: 128, b: 128 }
const white: Rgb = { r: 255, g: 255, b: 255 }
const black: Rgb = { r: 0, g: 0, b: 0 }

describe('dominantColor', () => {
    it('returns the color of a plain picture', () => {
        expect(dominantColor(pixels([blue, 16]))).toEqual(blue)
    })

    it('returns the most frequent color', () => {
        expect(dominantColor(pixels([red, 10], [blue, 30]))).toEqual(blue)
    })

    it('prefers a colorful motif to a larger grey, white or black background', () => {
        expect(dominantColor(pixels([grey, 100], [red, 40]))).toEqual(red)
        expect(dominantColor(pixels([white, 300], [blue, 40]))).toEqual(blue)
        expect(dominantColor(pixels([black, 300], [red, 40]))).toEqual(red)
    })

    it('returns a grey for a grey picture', () => {
        expect(dominantColor(pixels([grey, 20]))).toEqual(grey)
    })

    it('averages similar colors', () => {
        const color = dominantColor(
            pixels([{ r: 200, g: 30, b: 40 }, 1], [{ r: 202, g: 30, b: 40 }, 1]),
        )
        expect(color).toEqual({ r: 201, g: 30, b: 40 })
    })

    it('ignores transparent pixels and returns null without opaque ones', () => {
        expect(dominantColor(pixels([red, 50, 0], [blue, 5]))).toEqual(blue)
        expect(dominantColor(pixels([red, 5, 0]))).toBeNull()
        expect(dominantColor(new Uint8ClampedArray())).toBeNull()
    })
})

describe('contrast', () => {
    it('computes the contrast ratio of WCAG 2', () => {
        expect(contrastRatio(white, black)).toBeCloseTo(21, 5)
        expect(contrastRatio(grey, grey)).toBe(1)
        expect(contrastRatio({ r: 118, g: 118, b: 118 }, white)).toBeCloseTo(4.54, 2)
    })

    it('chooses black or white for text on the color', () => {
        expect(contrastColor({ r: 250, g: 220, b: 90 })).toBe('#000000')
        expect(contrastColor({ r: 20, g: 40, b: 120 })).toBe('#ffffff')
    })

    it('formats the color for CSS', () => {
        expect(cssColor(blue)).toBe('rgb(20 90 210)')
    })
})
