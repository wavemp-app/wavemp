// The color of the cover of the current song for themes (theme API: --wmp-cover-color and
// --wmp-cover-color-contrast on the root element). The dominant color is the most weighty color of
// a small copy of the cover: colors are grouped coarsely, colorful pixels count more than grey ones
// and nearly black or white pixels (borders, backgrounds) count little, so a colorful motif wins
// over a large plain background. Pure functions only.

export interface Rgb {
    readonly r: number
    readonly g: number
    readonly b: number
}

/** Variables of the theme API the app sets on the root element. */
export const coverColorVariable = '--wmp-cover-color'
export const coverColorContrastVariable = '--wmp-cover-color-contrast'

/** Edge length of the copy of the cover the color is taken from. */
export const coverSampleSize = 24

/** Bits per channel of the groups of similar colors. */
const groupBits = 4

function weightOf(r: number, g: number, b: number): number {
    const max = Math.max(r, g, b)
    const min = Math.min(r, g, b)
    const lightness = (max + min) / 510
    const saturation = max === min ? 0 : (max - min) / (255 - Math.abs(max + min - 255))
    if (lightness < 0.08 || lightness > 0.94) {
        return 0.1
    }

    return 1 + saturation * 3
}

/**
 * The dominant color of RGBA pixels (as from CanvasRenderingContext2D.getImageData), or null
 * without opaque pixels.
 */
export function dominantColor(pixels: ArrayLike<number>): Rgb | null {
    const shift = 8 - groupBits
    const groups = new Map<number, { weight: number; r: number; g: number; b: number }>()
    let best: { weight: number; r: number; g: number; b: number } | null = null

    for (let index = 0; index + 3 < pixels.length; index += 4) {
        const r = pixels[index] ?? 0
        const g = pixels[index + 1] ?? 0
        const b = pixels[index + 2] ?? 0
        const alpha = pixels[index + 3] ?? 0
        if (alpha < 128) {
            continue
        }

        const key = ((r >> shift) << (2 * groupBits)) | ((g >> shift) << groupBits) | (b >> shift)
        const weight = weightOf(r, g, b)
        const group = groups.get(key) ?? { weight: 0, r: 0, g: 0, b: 0 }
        group.weight += weight
        group.r += r * weight
        group.g += g * weight
        group.b += b * weight
        groups.set(key, group)
        if (!best || group.weight > best.weight) {
            best = group
        }
    }

    if (!best || best.weight === 0) {
        return null
    }

    return {
        r: Math.round(best.r / best.weight),
        g: Math.round(best.g / best.weight),
        b: Math.round(best.b / best.weight),
    }
}

function channel(value: number): number {
    const c = value / 255
    return c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4
}

/** Relative luminance of a color (WCAG 2). */
export function relativeLuminance({ r, g, b }: Rgb): number {
    return 0.2126 * channel(r) + 0.7152 * channel(g) + 0.0722 * channel(b)
}

/** Contrast ratio of two colors (WCAG 2), from 1 to 21. */
export function contrastRatio(first: Rgb, second: Rgb): number {
    const a = relativeLuminance(first)
    const b = relativeLuminance(second)
    return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05)
}

const white: Rgb = { r: 255, g: 255, b: 255 }
const black: Rgb = { r: 0, g: 0, b: 0 }

/** Black or white, whichever contrasts more with the color: for text and icons on it. */
export function contrastColor(color: Rgb): '#000000' | '#ffffff' {
    return contrastRatio(color, black) > contrastRatio(color, white) ? '#000000' : '#ffffff'
}

/** The color as CSS value, e.g. "rgb(31 111 214)". */
export function cssColor({ r, g, b }: Rgb): string {
    return `rgb(${r} ${g} ${b})`
}
