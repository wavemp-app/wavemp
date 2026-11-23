// Counts flashes in a sequence of frames as WCAG 2.3.1 defines them, for the tests of the flash
// guard only:
// - a general flash is a pair of opposing changes of the relative luminance by 0.1 or more, where
//   the darker state is below 0.8;
// - a red flash is a pair of opposing changes of the redness (linear red minus green minus blue) by
//   20 / 320 or more, where the redder state is a saturated red (red is at least 80 % of the sum).
// A change counts from the last turning point of the value, as soon as it reaches the threshold.
// The frames come as the mean colors of the cells of the grid of the guard; the counter looks at
// every cell, at every block of two by two cells, at the four quarters and at the whole picture,
// and reports the most flashes of any of these areas in any one second (a pair started counts:
// three changes are two flashes).

import {
    flashCellCount,
    flashGridColumns,
    flashGridRows,
    flashLuminanceChange,
    flashRednessChange,
    redness,
    relativeLuminance,
} from '../flashLimit'

export interface FlashFrame {
    /** Time of the frame in seconds. */
    readonly time: number
    /** Mean color (linear light, red, green, blue) of every cell, see measureCells. */
    readonly cells: ArrayLike<number>
}

export interface FlashCount {
    /** Most general flashes in any one second of any area. */
    readonly general: number
    /** Most red flashes in any one second of any area. */
    readonly red: number
}

/** The areas the counter looks at, as lists of cells. */
function areas(): number[][] {
    const result: number[][] = []
    const block = (left: number, top: number, columns: number, rows: number) => {
        const cells: number[] = []
        for (let row = top; row < top + rows; row++) {
            for (let column = left; column < left + columns; column++) {
                cells.push(row * flashGridColumns + column)
            }
        }

        result.push(cells)
    }

    for (let cell = 0; cell < flashCellCount; cell++) {
        result.push([cell])
    }

    for (let top = 0; top + 2 <= flashGridRows; top++) {
        for (let left = 0; left + 2 <= flashGridColumns; left++) {
            block(left, top, 2, 2)
        }
    }

    const halfColumns = flashGridColumns / 2
    const halfRows = flashGridRows / 2
    for (const top of [0, halfRows]) {
        for (const left of [0, halfColumns]) {
            block(left, top, halfColumns, halfRows)
        }
    }

    block(0, 0, flashGridColumns, flashGridRows)
    return result
}

interface State {
    readonly value: number
    /** Whether the change may count (general: dark enough; red: saturated red). */
    readonly flag: boolean
}

/** The times of the changes of a sequence of states that reach the threshold. */
function changes(
    times: readonly number[],
    states: readonly State[],
    threshold: number,
    counts: (low: State, high: State) => boolean,
): number[] {
    const result: number[] = []
    let direction = 0
    let extreme = states[0]!
    let lowest = states[0]!
    let highest = states[0]!
    states.forEach((state, index) => {
        const time = times[index]!
        if (direction === 0) {
            lowest = state.value < lowest.value ? state : lowest
            highest = state.value > highest.value ? state : highest
            if (state.value - lowest.value >= threshold && counts(lowest, state)) {
                result.push(time)
                direction = 1
                extreme = state
            } else if (highest.value - state.value >= threshold && counts(state, highest)) {
                result.push(time)
                direction = -1
                extreme = state
            }
        } else if (direction > 0) {
            if (state.value >= extreme.value) {
                extreme = state
            } else if (extreme.value - state.value >= threshold && counts(state, extreme)) {
                result.push(time)
                direction = -1
                extreme = state
            }
        } else if (state.value <= extreme.value) {
            extreme = state
        } else if (state.value - extreme.value >= threshold && counts(extreme, state)) {
            result.push(time)
            direction = 1
            extreme = state
        }
    })

    return result
}

/** Most flashes (changes / 2, rounded up) in any one second. */
function mostPerSecond(times: readonly number[]): number {
    let most = 0
    let first = 0
    for (let last = 0; last < times.length; last++) {
        // Within one second, with room for the rounding of the times of the frames.
        while (times[last]! - times[first]! > 1 - 1e-9) {
            first++
        }

        most = Math.max(most, Math.ceil((last - first + 1) / 2))
    }

    return most
}

export function countFlashes(frames: readonly FlashFrame[]): FlashCount {
    const times = frames.map((frame) => frame.time)
    let general = 0
    let red = 0
    for (const area of areas()) {
        const colors = frames.map((frame) => {
            let r = 0
            let g = 0
            let b = 0
            for (const cell of area) {
                r += frame.cells[cell * 3] ?? 0
                g += frame.cells[cell * 3 + 1] ?? 0
                b += frame.cells[cell * 3 + 2] ?? 0
            }

            return [r / area.length, g / area.length, b / area.length] as const
        })
        const luminance = colors.map((color) => {
            const value = relativeLuminance(...color)
            return { value, flag: value < 0.8 }
        })
        const reds = colors.map((color) => {
            const sum = color[0] + color[1] + color[2]
            return { value: redness(...color), flag: sum > 0 && color[0] / sum >= 0.8 }
        })
        general = Math.max(
            general,
            mostPerSecond(changes(times, luminance, flashLuminanceChange, (low) => low.flag)),
        )
        red = Math.max(
            red,
            mostPerSecond(changes(times, reds, flashRednessChange, (_, high) => high.flag)),
        )
    }

    return { general, red }
}
