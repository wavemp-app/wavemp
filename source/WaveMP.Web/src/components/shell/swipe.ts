// Swiping the full-screen player down (mobile): the player follows the finger while it drags and,
// when the finger lets go, closes or snaps back depending on the distance and the speed of the
// finger. A fast flick closes it even after a short distance, and the player then leaves the screen
// at about the speed of the flick.

/** Pixels the finger has to move before a drag starts, so a tap stays a tap. */
export const swipeStartThreshold = 8

/** Share of the height of the player that closes it when the finger lets go slowly. */
export const swipeCloseFraction = 0.25

/** Speed (CSS pixels per millisecond) from which a flick closes the player, or snaps it back upwards. */
export const swipeFlickVelocity = 0.5

/** The last moments of the drag that make up the speed of the finger, in milliseconds. */
export const swipeVelocityWindow = 100

/** Shortest and longest closing after the finger lets go, in milliseconds. */
export const swipeMinimumCloseDuration = 80
export const swipeMaximumCloseDuration = 300

/** How long the player takes to snap back, in milliseconds. */
export const swipeSnapDuration = 250

/** What a drag does when the finger lets go: close the player or snap back. */
export type SwipeOutcome = 'close' | 'snap'

/** Phase of the gesture: none, the finger drags, the player snaps back or it closes. */
export type SwipePhase = 'idle' | 'dragging' | 'settling' | 'dismissing'

/** What the player does when the finger lets go, with the speed of the finger (pixels per ms). */
export function swipeOutcome(offset: number, velocity: number, height: number): SwipeOutcome {
    if (velocity >= swipeFlickVelocity) {
        return 'close'
    }

    if (velocity <= -swipeFlickVelocity) {
        return 'snap'
    }

    return height > 0 && offset >= height * swipeCloseFraction ? 'close' : 'snap'
}

/**
 * How long the player takes to leave the screen after the finger let go: the rest of the way at the
 * speed of the finger, at least swipeMinimumCloseDuration and at most swipeMaximumCloseDuration.
 */
export function swipeCloseDuration(offset: number, velocity: number, height: number): number {
    const remaining = Math.max(0, height - offset)
    const duration = velocity > 0 ? remaining / velocity : swipeMaximumCloseDuration
    return Math.round(
        Math.min(swipeMaximumCloseDuration, Math.max(swipeMinimumCloseDuration, duration)),
    )
}

/** Share of the way the player has moved down, from 0 to 1. */
export function swipeProgress(offset: number, height: number): number {
    return height > 0 ? Math.min(1, Math.max(0, offset / height)) : 0
}

interface SwipeSample {
    readonly y: number
    readonly time: number
}

/**
 * Follows one drag of the finger: where it started, how far the player moved down (never up) and
 * how fast the finger moved at the end.
 */
export class SwipeTracker {
    private startY = 0
    private samples: SwipeSample[] = []

    /** The drag starts at the point of the finger. */
    begin(y: number, time: number): void {
        this.startY = y
        this.samples = [{ y, time }]
    }

    /** The finger moved; returns how far the player follows it down, in pixels. */
    move(y: number, time: number): number {
        this.samples.push({ y, time })
        // Only the moments of the window and one before it count for the speed.
        const from = time - swipeVelocityWindow
        const first = this.samples.findIndex((sample) => sample.time >= from)
        if (first > 1) {
            this.samples = this.samples.slice(first - 1)
        }

        return this.offset(y)
    }

    /** How far the player follows the finger down for a point of the finger. */
    offset(y: number): number {
        return Math.max(0, y - this.startY)
    }

    /**
     * Speed of the finger at the end, in pixels per millisecond (positive downwards), over the last
     * swipeVelocityWindow milliseconds; 0 when the finger rested longer than that.
     */
    velocity(time: number): number {
        const last = this.samples.at(-1)
        if (!last || time - last.time > swipeVelocityWindow) {
            return 0
        }

        // The first moment of the window; with only the last one in it, the one before.
        const from = last.time - swipeVelocityWindow
        const index = this.samples.findIndex((sample) => sample.time >= from)
        const first =
            index > 0 && index === this.samples.length - 1
                ? this.samples[index - 1]!
                : (this.samples[index] ?? last)
        const elapsed = last.time - first.time
        return elapsed > 0 ? (last.y - first.y) / elapsed : 0
    }
}
