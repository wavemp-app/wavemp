import { expect, type Locator } from '@playwright/test'

// Menus, sheets and dialogs open with transitions (motion system, THEMING.md "Motion"). Chromium
// gives a transition its start time only once the compositor has produced a frame with it, so while
// the visualization draws in software the transition can wait for a second or more before it moves
// at all, and a fixed pause measures the element somewhere on its way.

export interface Box {
    readonly x: number
    readonly y: number
    readonly width: number
    readonly height: number
}

/**
 * Waits until an element has stopped moving and returns its box: its own transitions and
 * animations that have an end have finished (also those that have not started yet), and its box is
 * the same in two frames of the browser one after another. Use it instead of a fixed pause before
 * measuring an element that opens or grows with a transition.
 */
export async function settledBox(element: Locator, timeout = 15_000): Promise<Box> {
    let box: Box | undefined
    await expect
        .poll(
            async () => {
                const reading = await element.evaluate(async (node) => {
                    // Reading the animations updates the styles first, so a transition that still
                    // waits for its start is listed. Waiting may let new ones begin (a discrete
                    // property, opening again), hence the loop; endless ones would never finish.
                    const moving = () =>
                        node
                            .getAnimations()
                            .filter(
                                (animation) =>
                                    animation.playState === 'running' &&
                                    animation.effect?.getComputedTiming().endTime !== Infinity,
                            )
                    for (let running = moving(); running.length > 0; running = moving()) {
                        // A canceled animation rejects, for example when the element closes.
                        await Promise.all(
                            running.map((animation) => animation.finished.catch(() => undefined)),
                        )
                    }

                    const read = () => {
                        const rect = node.getBoundingClientRect()
                        return { x: rect.x, y: rect.y, width: rect.width, height: rect.height }
                    }
                    const nextFrame = () =>
                        new Promise<void>((resolve) => requestAnimationFrame(() => resolve()))
                    const first = read()
                    await nextFrame()
                    await nextFrame()
                    const second = read()
                    const stable =
                        moving().length === 0 &&
                        first.x === second.x &&
                        first.y === second.y &&
                        first.width === second.width &&
                        first.height === second.height
                    return { stable, box: second }
                })
                box = reading.box
                return reading.stable
            },
            { timeout, message: 'the element keeps moving' },
        )
        .toBe(true)
    return box!
}
