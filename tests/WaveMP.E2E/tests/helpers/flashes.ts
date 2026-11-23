import type { Page } from '@playwright/test'

// Flashes of the rendered visualization as WCAG 2.3.1 counts them (no more than three general and
// three red flashes in any one second):
// - a general flash is a pair of opposing changes of the relative luminance by 0.1 or more, where
//   the darker state is below 0.8;
// - a red flash is a pair of opposing changes of the redness (linear red minus green minus blue) by
//   20 / 320 or more, where the redder state is a saturated red (red is at least 80 % of the sum).
// An init script reads the canvas of the visualization after every animation frame (in the same
// task, while its drawing buffer still holds the frame) and keeps the mean color in linear light of
// each cell of a grid of 6 x 4. The counter looks at every cell, every block of 2 x 2 cells, the
// four quarters and the whole picture. The web app has its own copy of the counter for its unit
// tests (src/visualization/safety/__tests__/wcagFlashes.ts); the packages share no code.

/** Columns and rows of the grid the frames are measured in. */
export const flashGridColumns = 6
export const flashGridRows = 4

const luminanceChange = 0.1
const rednessChange = 20 / 320

export interface FlashFrame {
    /** Time of the frame in seconds. */
    readonly time: number
    /** Mean color (linear red, green, blue) of every cell, row by row from the top left. */
    readonly cells: readonly number[]
}

export interface FlashCount {
    /** Most general flashes in any one second of any area. */
    readonly general: number
    /** Most red flashes in any one second of any area. */
    readonly red: number
}

/**
 * Records the frames of the visualization canvas of the app from the next page load on; read them
 * with {@link takeFlashFrames}. Call it before the page loads.
 */
export async function recordFlashFrames(page: Page): Promise<void> {
    await page.addInitScript(
        ({ columns, rows }) => {
            const frames: { time: number; cells: number[] }[] = []
            ;(window as unknown as { __wmpFlashFrames: typeof frames }).__wmpFlashFrames = frames
            const linear = new Float64Array(256)
            for (let value = 0; value < 256; value++) {
                const channel = value / 255
                linear[value] =
                    channel <= 0.04045 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4
            }

            const copy = document.createElement('canvas')
            const context = copy.getContext('2d', { willReadFrequently: true })
            let last = -1
            const record = () => {
                const now = performance.now()
                const canvas = document.querySelector<HTMLCanvasElement>(
                    "[data-part='app'] [data-part='visualization'] [data-part='visualization-canvas']",
                )
                if (!context || !canvas || canvas.width < columns || now - last < 1) {
                    return
                }

                last = now
                const width = Math.min(canvas.width, 640)
                const height = Math.max(rows, Math.round((canvas.height * width) / canvas.width))
                if (copy.width !== width || copy.height !== height) {
                    copy.width = width
                    copy.height = height
                }

                try {
                    context.drawImage(canvas, 0, 0, width, height)
                    const data = context.getImageData(0, 0, width, height).data
                    const cells: number[] = []
                    for (let row = 0; row < rows; row++) {
                        const top = Math.floor((row * height) / rows)
                        const bottom = Math.floor(((row + 1) * height) / rows)
                        for (let column = 0; column < columns; column++) {
                            const left = Math.floor((column * width) / columns)
                            const right = Math.floor(((column + 1) * width) / columns)
                            let red = 0
                            let green = 0
                            let blue = 0
                            for (let y = top; y < bottom; y++) {
                                for (let x = left; x < right; x++) {
                                    const index = (y * width + x) * 4
                                    red += linear[data[index]!]!
                                    green += linear[data[index + 1]!]!
                                    blue += linear[data[index + 2]!]!
                                }
                            }

                            const count = Math.max(1, (bottom - top) * (right - left))
                            cells.push(red / count, green / count, blue / count)
                        }
                    }

                    frames.push({ time: now / 1000, cells })
                } catch {
                    // A lost context has no picture.
                }
            }

            // Only frames the visualization drew count: in other frames its drawing buffer may
            // already be presented and cleared.
            let drawn = false
            const prototype = WebGL2RenderingContext.prototype as unknown as Record<
                string,
                (...args: unknown[]) => unknown
            >
            for (const name of ['drawArrays', 'drawArraysInstanced', 'drawElements']) {
                const draw = prototype[name]!
                prototype[name] = function (this: WebGL2RenderingContext, ...args: unknown[]) {
                    const canvas = this.canvas as HTMLCanvasElement
                    if (canvas.dataset?.part === 'visualization-canvas') {
                        drawn = true
                    }

                    return draw.apply(this, args)
                }
            }

            const requestFrame = window.requestAnimationFrame.bind(window)
            window.requestAnimationFrame = (callback) =>
                requestFrame((time) => {
                    callback(time)
                    if (drawn) {
                        drawn = false
                        record()
                    }
                })
        },
        { columns: flashGridColumns, rows: flashGridRows },
    )
}

/** The frames recorded since the last call. */
export async function takeFlashFrames(page: Page): Promise<FlashFrame[]> {
    return page.evaluate(() =>
        (window as unknown as { __wmpFlashFrames: FlashFrame[] }).__wmpFlashFrames.splice(0),
    )
}

function relativeLuminance(red: number, green: number, blue: number): number {
    return 0.2126 * red + 0.7152 * green + 0.0722 * blue
}

function redness(red: number, green: number, blue: number): number {
    return Math.max(0, red - green - blue)
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

    for (let row = 0; row < flashGridRows; row++) {
        for (let column = 0; column < flashGridColumns; column++) {
            block(column, row, 1, 1)
        }
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

/** The times of the changes that reach the threshold, each from the last turning point. */
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
        while (times[last]! - times[first]! > 1 - 1e-9) {
            first++
        }

        most = Math.max(most, Math.ceil((last - first + 1) / 2))
    }

    return most
}

/** Most general and red flashes in any one second of any area of the frames. */
export function countFlashes(frames: readonly FlashFrame[]): FlashCount {
    if (frames.length === 0) {
        return { general: 0, red: 0 }
    }

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
            return {
                value: redness(...color),
                flag: sum > 0 && color[0] / sum >= 0.8,
            }
        })
        general = Math.max(
            general,
            mostPerSecond(changes(times, luminance, luminanceChange, (low) => low.flag)),
        )
        red = Math.max(
            red,
            mostPerSecond(changes(times, reds, rednessChange, (_, high) => high.flag)),
        )
    }

    return { general, red }
}

/**
 * The mean change of the relative luminance of a cell from one frame to the next, over all cells
 * and frames: how much the picture moves.
 */
export function meanChange(frames: readonly FlashFrame[]): number {
    let sum = 0
    let count = 0
    for (let index = 1; index < frames.length; index++) {
        const before = frames[index - 1]!.cells
        const after = frames[index]!.cells
        for (let cell = 0; cell < before.length; cell += 3) {
            sum += Math.abs(
                relativeLuminance(after[cell]!, after[cell + 1]!, after[cell + 2]!) -
                    relativeLuminance(before[cell]!, before[cell + 1]!, before[cell + 2]!),
            )
            count++
        }
    }

    return count > 0 ? sum / count : 0
}

/** Largest change of the luminance of a cell between two frames: whether the picture moves. */
export function largestChange(frames: readonly FlashFrame[]): number {
    let largest = 0
    for (let index = 1; index < frames.length; index++) {
        const before = frames[index - 1]!.cells
        const after = frames[index]!.cells
        for (let cell = 0; cell < before.length; cell += 3) {
            const change = Math.abs(
                relativeLuminance(after[cell]!, after[cell + 1]!, after[cell + 2]!) -
                    relativeLuminance(before[cell]!, before[cell + 1]!, before[cell + 2]!),
            )
            largest = Math.max(largest, change)
        }
    }

    return largest
}
