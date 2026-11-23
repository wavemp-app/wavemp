import { describe, expect, it } from 'vitest'
import {
    applyFlashGuard,
    flashCellCount,
    flashGridColumns,
    flashLimits,
    FlashLimiter,
    measureCells,
    mixAt,
    redness,
    relativeLuminance,
    spreadMix,
    srgbToLinear,
    type FlashIntensity,
} from '../flashLimit'
import { countFlashes, type FlashCount, type FlashFrame } from './wcagFlashes'

// Strobes and other pictures run through the flash guard frame by frame, as the renderers will do
// it on the GPU; the counter of WCAG 2.3.1 then counts the flashes of what comes out.

/** A small picture: 6 x 4 cells of 16 x 16 pixels, red, green and blue in linear light. */
const width = 96
const height = 64
const channels = 3

type Picture = (x: number, y: number, time: number) => readonly [number, number, number]

interface Run {
    /** The flashes of the pictures as they are. */
    readonly input: FlashCount
    /** The flashes of the output of the guard. */
    readonly output: FlashCount
    /** Largest difference of a channel between a picture and its output, after the first second. */
    readonly largestChange: number
}

function guard(
    picture: Picture,
    seconds: number,
    intensity: FlashIntensity = 'normal',
    framesPerSecond = 60,
): Run {
    const limiter = new FlashLimiter()
    const limits = flashLimits[intensity]
    const current = new Float32Array(width * height * channels)
    let previous = new Float32Array(width * height * channels)
    let output = new Float32Array(width * height * channels)
    const currentCells = new Float32Array(flashCellCount * 3)
    const previousCells = new Float32Array(flashCellCount * 3)
    const mix = new Float32Array(flashCellCount)
    const spread = new Float32Array(flashCellCount)
    const inputFrames: FlashFrame[] = []
    const outputFrames: FlashFrame[] = []
    let largestChange = 0
    for (let frame = 0; frame < seconds * framesPerSecond; frame++) {
        const time = frame / framesPerSecond
        for (let y = 0; y < height; y++) {
            for (let x = 0; x < width; x++) {
                current.set(picture(x, y, time), (y * width + x) * channels)
            }
        }

        measureCells(current, width, height, channels, currentCells)
        measureCells(previous, width, height, channels, previousCells)
        limiter.update(
            currentCells,
            previousCells,
            frame === 0 ? 0 : 1 / framesPerSecond,
            limits,
            mix,
        )
        applyFlashGuard(current, previous, width, height, channels, spreadMix(mix, spread), output)
        inputFrames.push({ time, cells: Float32Array.from(currentCells) })
        outputFrames.push({
            time,
            cells: Float32Array.from(measureCells(output, width, height, channels, previousCells)),
        })
        if (time >= 1) {
            for (let index = 0; index < current.length; index++) {
                largestChange = Math.max(largestChange, Math.abs(current[index]! - output[index]!))
            }
        }

        ;[previous, output] = [output, previous]
    }

    return {
        input: countFlashes(inputFrames),
        output: countFlashes(outputFrames),
        largestChange,
    }
}

const black = [0, 0, 0] as const
const white = [1, 1, 1] as const
const red = [1, 0, 0] as const

/** The whole picture switching between black and a color at a rate in Hz, starting black. */
function strobe(rate: number, on: readonly [number, number, number] = white): Picture {
    return (_x, _y, time) => (Math.floor(time * rate * 2) % 2 === 1 ? on : black)
}

/** A pseudo random generator with a fixed seed, values from 0 to 1. */
function random(seed: number): () => number {
    let state = seed >>> 0
    return () => {
        state = (Math.imul(state, 1664525) + 1013904223) >>> 0
        return state / 4294967296
    }
}

describe('the flash guard', () => {
    it.each([30, 10, 4])(
        'turns a full white strobe at %i Hz into at most three flashes a second',
        (rate) => {
            const run = guard(strobe(rate), 3)

            expect(run.input.general).toBeGreaterThan(3)
            expect(run.output.general).toBeLessThanOrEqual(3)
        },
    )

    it('limits a strobe of a quarter of the picture', () => {
        const quarter: Picture = (x, y, time) =>
            x < width / 2 && y < height / 2 ? strobe(10)(x, y, time) : [0.05, 0.05, 0.05]
        const run = guard(quarter, 3)

        expect(run.input.general).toBeGreaterThan(3)
        expect(run.output.general).toBeLessThanOrEqual(3)
    })

    it('limits a saturated red strobe', () => {
        const run = guard(strobe(10, red), 3)

        expect(run.input.red).toBeGreaterThan(3)
        expect(run.output.red).toBeLessThanOrEqual(3)
        expect(run.output.general).toBeLessThanOrEqual(3)
    })

    it('limits cuts between random pictures several times a second', () => {
        // A new random color in every cell eight times a second, like fast changes of presets.
        const next = random(89)
        const colors: [number, number, number][][] = []
        const picture: Picture = (x, y, time) => {
            const cut = Math.floor(time * 8)
            while (colors.length <= cut) {
                colors.push(
                    Array.from(
                        { length: flashCellCount },
                        () => [next(), next(), next()] as [number, number, number],
                    ),
                )
            }

            const cell =
                Math.floor((y * 4) / height) * flashGridColumns + Math.floor((x * 6) / width)
            return colors[cut]![cell]!
        }

        const run = guard(picture, 4)

        expect(run.input.general).toBeGreaterThan(3)
        expect(run.output.general).toBeLessThanOrEqual(3)
        expect(run.output.red).toBeLessThanOrEqual(3)
    })

    it('limits flashes at a high display rate as well', () => {
        const run = guard(strobe(10), 2, 'normal', 144)

        expect(run.output.general).toBeLessThanOrEqual(3)
    })

    it('limits flashes further at the reduced intensity', () => {
        // A slow strobe (1.5 Hz) still flashes through the normal guard, slowly, but not through
        // the reduced one; a fast one flashes through neither.
        const normal = guard(strobe(1.5), 3, 'normal')
        const reduced = guard(strobe(1.5), 3, 'reduced')
        const red = guard(strobe(10, [1, 0, 0]), 3, 'reduced')

        expect(normal.output.general).toBeGreaterThan(0)
        expect(reduced.output.general).toBeLessThan(normal.output.general)
        expect(reduced.output.general).toBeLessThanOrEqual(2)
        expect(red.output.red).toBeLessThanOrEqual(2)
    })

    it('lets a slow fade pass unchanged', () => {
        // From black to white within five seconds.
        const fade: Picture = (_x, _y, time) => {
            const level = Math.min(1, time / 5)
            return [level, level, level]
        }

        expect(guard(fade, 5).largestChange).toBeLessThan(1e-6)
    })

    it('lets a small bright dot move freely', () => {
        // A dot of 2 x 2 pixels crossing the picture on a dark background, a pixel a frame.
        const dot: Picture = (x, y, time) => {
            const left = Math.round(time * 60) % width
            return x >= left && x < left + 2 && y >= 30 && y < 32 ? white : [0.02, 0.02, 0.02]
        }

        expect(guard(dot, 3).largestChange).toBeLessThan(1e-6)
    })

    it('starts with the first picture as it is', () => {
        const limiter = new FlashLimiter()
        const cells = new Float32Array(flashCellCount * 3).fill(1)
        const mix = limiter.update(
            cells,
            new Float32Array(flashCellCount * 3),
            1 / 60,
            flashLimits.normal,
            new Float32Array(flashCellCount),
        )

        expect([...mix].every((value) => value === 0)).toBe(true)
    })
})

describe('the measures of the flash guard', () => {
    it('decodes sRGB and weighs the luminance and the redness like WCAG', () => {
        expect(srgbToLinear(0)).toBe(0)
        expect(srgbToLinear(1)).toBeCloseTo(1, 6)
        expect(srgbToLinear(0.5)).toBeCloseTo(0.214, 3)
        expect(relativeLuminance(1, 1, 1)).toBeCloseTo(1, 6)
        expect(relativeLuminance(0, 1, 0)).toBeCloseTo(0.7152, 6)
        expect(redness(1, 0.2, 0.1)).toBeCloseTo(0.7, 6)
        expect(redness(0.2, 0.5, 0)).toBe(0)
    })

    it('spreads the mix of a cell to its neighbours and interpolates between the centers', () => {
        const mix = new Float32Array(flashCellCount)
        mix[0] = 1
        const spread = spreadMix(mix, new Float32Array(flashCellCount))

        expect(spread[0]).toBe(1)
        expect(spread[1]).toBe(1)
        expect(spread[flashGridColumns + 1]).toBe(1)
        expect(spread[2]).toBe(0)
        // The middle of a cell gets its own factor, between two centers the mean.
        expect(mixAt(spread, 1 / 12, 1 / 8)).toBe(1)
        expect(mixAt(spread, 5 / 12, 1 / 8)).toBeCloseTo(0, 6)
        expect(mixAt(spread, 4 / 12, 1 / 8)).toBeCloseTo(0.5, 6)
    })
})
