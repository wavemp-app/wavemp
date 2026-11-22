import type { Page } from '@playwright/test'

// Headless Chromium draws WebGL in software (SwiftShader). That takes every core it gets: it draws
// as many frames per second as it can, and the cost of a frame grows with the size of the canvas
// and differs more than tenfold between the presets of Milkdrop. The clicks, the server and the
// database of a test then wait for the CPU, so tests of the visualization draw less.

/** The pause before each frame of {@link limitDrawingRate}. */
const pauseBetweenFrames = 1000 / 15

/** The width of the stage of {@link drawSmall}. */
const smallStage = '12rem'

/**
 * Lets the page draw at most about 15 frames per second: each frame it requests waits for a pause
 * and then for the next frame of the browser. A canceled request does not draw, whether it still
 * waits for the pause or already for the frame. Playwright checks the elements in its own world,
 * which keeps all frames of the browser. Call it before the page loads.
 */
export async function limitDrawingRate(page: Page): Promise<void> {
    await page.addInitScript((pause) => {
        const requestFrame = window.requestAnimationFrame.bind(window)
        const cancelFrame = window.cancelAnimationFrame.bind(window)
        const pauses = new Map<number, number>()
        const frames = new Map<number, number>()
        let lastRequest = 0

        window.requestAnimationFrame = (callback) => {
            const request = ++lastRequest
            const timer = window.setTimeout(() => {
                pauses.delete(request)
                const frame = requestFrame((time) => {
                    frames.delete(request)
                    callback(time)
                })
                frames.set(request, frame)
            }, pause)
            pauses.set(request, timer)
            return request
        }
        window.cancelAnimationFrame = (request) => {
            const timer = pauses.get(request)
            if (timer !== undefined) {
                window.clearTimeout(timer)
                pauses.delete(request)
            }
            const frame = frames.get(request)
            if (frame !== undefined) {
                cancelFrame(frame)
                frames.delete(request)
            }
        }
    }, pauseBetweenFrames)
}

/**
 * Shows the views of the stage small, so the visualization has few pixels to draw. It holds for
 * every page the tab loads afterwards. Tests that measure the layout do not use it.
 */
export async function drawSmall(page: Page): Promise<void> {
    await page.addInitScript((width) => {
        document.addEventListener('DOMContentLoaded', () => {
            const style = document.createElement('style')
            style.textContent = `[data-part='stage-views'] { max-inline-size: ${width} !important; }`
            document.head.append(style)
        })
    }, smallStage)
}
