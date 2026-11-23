// The core of the flash guard: it keeps the picture of a visualization from flashing, for viewers
// who are sensitive to flashing light. WCAG 2.3.1 allows no more than three general flashes and
// three red flashes in any one second; a general flash is a pair of opposing changes of the
// relative luminance by 0.1 or more (the darker state below 0.8), a red flash a pair of opposing
// changes of the saturated red by 20 / 320 (the red minus green minus blue of linear light).
//
// The guard looks at the picture as a coarse grid of cells (6 x 4). Per cell it lets the luminance
// and the redness move freely within a narrow band, smaller than a flash, around a center that
// follows the picture only at a limited rate. A change of 0.1 thus takes at least
// (0.1 - band) / rate seconds, 0.2 s at the normal intensity: at most five changes, two and a half
// flashes per second, and fewer at the reduced intensity. Hard cuts and strobes of large areas turn
// into fades; small details move freely, because they hardly change the mean of a cell.
//
// Per cell the guard answers how far each pixel of the new frame has to be mixed back towards the
// previous output frame (0: the new frame as it is, 1: the previous output), and mixes in linear
// light, so the luminance of the mix is exactly the mix of the luminances. Pure: the renderers do
// the same on the GPU, and the tests run whole frames through it.

/** Columns and rows of the grid of cells the guard measures the picture in. */
export const flashGridColumns = 6
export const flashGridRows = 4
export const flashCellCount = flashGridColumns * flashGridRows

/** How far the relative luminance must change to count as half of a general flash. */
export const flashLuminanceChange = 0.1

/** How far the redness (linear red minus green minus blue) must change for half of a red flash. */
export const flashRednessChange = 20 / 320

export type FlashIntensity = 'normal' | 'reduced'

export interface FlashLimits {
    /** Width of the band in which the luminance of a cell may change at once. */
    readonly luminanceBand: number
    /** Luminance per second the center of the band follows the picture with. */
    readonly luminanceRate: number
    /** Width of the band in which the redness of a cell may change at once. */
    readonly rednessBand: number
    /** Redness per second the center of its band follows the picture with. */
    readonly rednessRate: number
}

/**
 * The limits per intensity. Normal: a change of a flash takes at least 0.2 s (at most 2.5 flashes
 * per second); reduced: at least 0.4 s (at most 1.25 flashes per second).
 */
export const flashLimits: Readonly<Record<FlashIntensity, FlashLimits>> = {
    normal: {
        luminanceBand: 0.04,
        luminanceRate: (flashLuminanceChange - 0.04) / 0.2,
        rednessBand: 0.025,
        rednessRate: (flashRednessChange - 0.025) / 0.2,
    },
    reduced: {
        luminanceBand: 0.02,
        luminanceRate: (flashLuminanceChange - 0.02) / 0.4,
        rednessBand: 0.0125,
        rednessRate: (flashRednessChange - 0.0125) / 0.4,
    },
}

/** A channel of sRGB (0 to 1) in linear light. */
export function srgbToLinear(value: number): number {
    return value <= 0.04045 ? value / 12.92 : Math.pow((value + 0.055) / 1.055, 2.4)
}

/** Relative luminance of a color in linear light (WCAG). */
export function relativeLuminance(red: number, green: number, blue: number): number {
    return 0.2126 * red + 0.7152 * green + 0.0722 * blue
}

/** Redness of a color in linear light: red minus green minus blue, at least 0 (WCAG). */
export function redness(red: number, green: number, blue: number): number {
    return Math.max(0, red - green - blue)
}

/**
 * The mean color (linear light) of every cell of a picture into out (red, green, blue per cell,
 * row by row from the top left). The picture holds channels values per pixel in linear light, row
 * by row; the first three are red, green and blue.
 */
export function measureCells(
    pixels: ArrayLike<number>,
    width: number,
    height: number,
    channels: number,
    out: Float32Array,
): Float32Array {
    for (let row = 0; row < flashGridRows; row++) {
        const top = Math.floor((row * height) / flashGridRows)
        const bottom = Math.max(top + 1, Math.floor(((row + 1) * height) / flashGridRows))
        for (let column = 0; column < flashGridColumns; column++) {
            const left = Math.floor((column * width) / flashGridColumns)
            const right = Math.max(left + 1, Math.floor(((column + 1) * width) / flashGridColumns))
            let red = 0
            let green = 0
            let blue = 0
            for (let y = top; y < Math.min(bottom, height); y++) {
                for (let x = left; x < Math.min(right, width); x++) {
                    const index = (y * width + x) * channels
                    red += pixels[index] ?? 0
                    green += pixels[index + 1] ?? 0
                    blue += pixels[index + 2] ?? 0
                }
            }

            const count = Math.max(
                1,
                (Math.min(bottom, height) - top) * (Math.min(right, width) - left),
            )
            const cell = (row * flashGridColumns + column) * 3
            out[cell] = red / count
            out[cell + 1] = green / count
            out[cell + 2] = blue / count
        }
    }

    return out
}

/**
 * Remembers the center of the band of every cell and tells, frame by frame, how far each cell of
 * a new frame has to be mixed back towards the previous output.
 */
export class FlashLimiter {
    private readonly luminanceCenters = new Float64Array(flashCellCount)
    private readonly rednessCenters = new Float64Array(flashCellCount)
    private started = false

    /**
     * The mix factor of every cell (0 to 1, towards the previous output) into out. current holds
     * the mean colors of the cells of the new frame, previous those of the previous output frame
     * (see measureCells), seconds the time since that frame.
     */
    update(
        current: ArrayLike<number>,
        previous: ArrayLike<number>,
        seconds: number,
        limits: FlashLimits,
        out: Float32Array,
    ): Float32Array {
        const time = Math.max(0, seconds)
        for (let cell = 0; cell < flashCellCount; cell++) {
            const index = cell * 3
            const now = [
                current[index] ?? 0,
                current[index + 1] ?? 0,
                current[index + 2] ?? 0,
            ] as const
            const before = [
                previous[index] ?? 0,
                previous[index + 1] ?? 0,
                previous[index + 2] ?? 0,
            ] as const
            const luminance = relativeLuminance(...now)
            const red = redness(...now)
            if (!this.started) {
                this.luminanceCenters[cell] = luminance
                this.rednessCenters[cell] = red
                out[cell] = 0
                continue
            }

            const luminanceMix = this.limit(
                this.luminanceCenters,
                cell,
                luminance,
                relativeLuminance(...before),
                limits.luminanceBand,
                limits.luminanceRate * time,
            )
            const rednessMix = this.limit(
                this.rednessCenters,
                cell,
                red,
                redness(...before),
                limits.rednessBand,
                limits.rednessRate * time,
            )
            out[cell] = Math.max(luminanceMix, rednessMix)
        }

        this.started = true
        return out
    }

    /** Starts again: the next frame passes as it is, e.g. after a pause or a new renderer. */
    reset(): void {
        this.started = false
    }

    /**
     * Moves the center of a cell towards the value of the new frame by at most step and returns
     * the mix towards the previous output that keeps the value within the band around the center.
     */
    private limit(
        centers: Float64Array,
        cell: number,
        value: number,
        previous: number,
        band: number,
        step: number,
    ): number {
        const center = centers[cell] ?? value
        const moved = center + Math.max(-step, Math.min(step, value - center))
        centers[cell] = moved
        const target = Math.max(moved - band / 2, Math.min(moved + band / 2, value))
        const change = value - previous
        if (target === value || Math.abs(change) < 1e-9) {
            return 0
        }

        return Math.max(0, Math.min(1, (value - target) / change))
    }
}

/**
 * The mix factor of every cell as the pixels use it: the largest factor of the cell and its eight
 * neighbours, so the soft interpolation between the centers of the cells never weakens the guard
 * of a cell (its pixels are mixed at least as far as the cell needs).
 */
export function spreadMix(mix: ArrayLike<number>, out: Float32Array): Float32Array {
    for (let row = 0; row < flashGridRows; row++) {
        for (let column = 0; column < flashGridColumns; column++) {
            let largest = 0
            for (let y = Math.max(0, row - 1); y <= Math.min(flashGridRows - 1, row + 1); y++) {
                for (
                    let x = Math.max(0, column - 1);
                    x <= Math.min(flashGridColumns - 1, column + 1);
                    x++
                ) {
                    largest = Math.max(largest, mix[y * flashGridColumns + x] ?? 0)
                }
            }

            out[row * flashGridColumns + column] = largest
        }
    }

    return out
}

/**
 * The mix factor at a point of the picture (u, v from 0 to 1, from the top left): interpolated
 * between the centers of the cells of the spread mix, as a texture of the grid with linear
 * filtering gives it on the GPU.
 */
export function mixAt(spread: ArrayLike<number>, u: number, v: number): number {
    const x = Math.max(0, Math.min(flashGridColumns - 1, u * flashGridColumns - 0.5))
    const y = Math.max(0, Math.min(flashGridRows - 1, v * flashGridRows - 0.5))
    const left = Math.floor(x)
    const top = Math.floor(y)
    const right = Math.min(flashGridColumns - 1, left + 1)
    const bottom = Math.min(flashGridRows - 1, top + 1)
    const across = x - left
    const down = y - top
    const at = (column: number, row: number) => spread[row * flashGridColumns + column] ?? 0
    const upper = at(left, top) + (at(right, top) - at(left, top)) * across
    const lower = at(left, bottom) + (at(right, bottom) - at(left, bottom)) * across
    return upper + (lower - upper) * down
}

/**
 * Mixes a new frame back towards the previous output by the spread mix factor of each pixel, in
 * linear light: the output frame of the guard, into out. Frames hold channels values per pixel in
 * linear light, row by row.
 */
export function applyFlashGuard(
    current: ArrayLike<number>,
    previous: ArrayLike<number>,
    width: number,
    height: number,
    channels: number,
    spread: ArrayLike<number>,
    out: Float32Array,
): Float32Array {
    for (let y = 0; y < height; y++) {
        for (let x = 0; x < width; x++) {
            const mix = mixAt(spread, (x + 0.5) / width, (y + 0.5) / height)
            const index = (y * width + x) * channels
            for (let channel = 0; channel < channels; channel++) {
                const now = current[index + channel] ?? 0
                out[index + channel] = now + ((previous[index + channel] ?? 0) - now) * mix
            }
        }
    }

    return out
}
