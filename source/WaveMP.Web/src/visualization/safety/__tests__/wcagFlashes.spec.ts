import { describe, expect, it } from 'vitest'
import { flashCellCount } from '../flashLimit'
import { countFlashes, type FlashFrame } from './wcagFlashes'

// The counter of the tests itself: it has to count what WCAG 2.3.1 counts, or the tests of the
// guard would prove nothing.

/** Frames at 60 per second whose cells all have one color, given per time. */
function frames(
    seconds: number,
    color: (time: number) => readonly [number, number, number],
    cells: (cell: number) => boolean = () => true,
): FlashFrame[] {
    return Array.from({ length: seconds * 60 }, (_, frame) => {
        const time = frame / 60
        const values = new Float32Array(flashCellCount * 3)
        for (let cell = 0; cell < flashCellCount; cell++) {
            values.set(cells(cell) ? color(time) : [0, 0, 0], cell * 3)
        }

        return { time, cells: values }
    })
}

function strobe(rate: number, on: readonly [number, number, number]) {
    return (time: number) => (Math.floor(time * rate * 2) % 2 === 1 ? on : ([0, 0, 0] as const))
}

describe('countFlashes', () => {
    it('counts the flashes of a strobe per second', () => {
        expect(countFlashes(frames(3, strobe(3, [1, 1, 1]))).general).toBe(3)
        expect(countFlashes(frames(3, strobe(5, [1, 1, 1]))).general).toBe(5)
        expect(countFlashes(frames(3, strobe(5, [1, 1, 1]))).red).toBe(0)
    })

    it('counts a flash of a single cell', () => {
        expect(countFlashes(frames(3, strobe(5, [1, 1, 1]), (cell) => cell === 7)).general).toBe(5)
    })

    it('ignores changes below 0.1 and changes between light states', () => {
        expect(countFlashes(frames(3, strobe(5, [0.09, 0.09, 0.09]))).general).toBe(0)
        const light = (time: number) =>
            Math.floor(time * 10) % 2 === 1 ? ([1, 1, 1] as const) : ([0.85, 0.85, 0.85] as const)
        expect(countFlashes(frames(3, light)).general).toBe(0)
    })

    it('counts red flashes of a saturated red only', () => {
        expect(countFlashes(frames(3, strobe(4, [1, 0, 0]))).red).toBe(4)
        // An orange is not a saturated red.
        expect(countFlashes(frames(3, strobe(4, [1, 0.4, 0]))).red).toBe(0)
    })

    it('counts a started pair as a flash', () => {
        // Three changes within one second: white, black, white, black.
        const steps = (time: number) =>
            time < 1 || (time >= 1.3 && time < 1.6) ? ([1, 1, 1] as const) : ([0, 0, 0] as const)
        expect(countFlashes(frames(3, steps)).general).toBe(2)
    })
})
