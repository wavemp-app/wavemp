// Timing of BaseMarquee: a text too long for its place rests cut off for a moment, scrolls slowly to
// its end, rests there and starts again from the beginning. The phases are data-state tokens of the
// part marquee; the theme moves the text with CSS (--wmp-marquee-distance, --wmp-marquee-duration).
// With prefers-reduced-motion nothing moves and the text ends with an ellipsis.

/** How long the text rests at its start before it scrolls, in milliseconds. */
export const marqueeStartDelay = 3000

/** How long the text rests at its end before it starts again, in milliseconds. */
export const marqueeEndPause = 2000

/** Speed of the scrolling in CSS pixels per second. */
export const marqueeSpeed = 30

/** The shortest scrolling, in milliseconds, so a text only a little too long does not jerk. */
export const marqueeMinimumDuration = 1000

/** Pixels a text may be too wide without moving (rounding of the layout). */
export const marqueeTolerance = 1

/** Phase of a moving text: resting at its start, scrolling, resting at its end. */
export type MarqueePhase = 'waiting' | 'scrolling' | 'end'

/** How far the text has to move to show its end: 0 when it fits, else whole pixels. */
export function marqueeDistance(textWidth: number, boxWidth: number): number {
    const overflow = textWidth - boxWidth
    return overflow > marqueeTolerance ? Math.ceil(overflow) : 0
}

/** How long the text scrolls over a distance, in milliseconds. */
export function marqueeScrollDuration(distance: number): number {
    return Math.max(marqueeMinimumDuration, Math.round((distance / marqueeSpeed) * 1000))
}

/**
 * data-state tokens of the part marquee: "fit" when the text fits; "overflow" when it is too long,
 * alone while it does not move (ellipsis), else with its phase, e.g. "overflow scrolling".
 */
export function marqueeStateTokens(distance: number, phase: MarqueePhase | null): string {
    if (distance <= 0) {
        return 'fit'
    }

    return phase ? `overflow ${phase}` : 'overflow'
}

/** Timers of the cycle; replaceable in tests. */
export interface MarqueeScheduler {
    set(callback: () => void, milliseconds: number): unknown
    clear(handle: unknown): void
}

const timerScheduler: MarqueeScheduler = {
    set: (callback, milliseconds) => setTimeout(callback, milliseconds),
    clear: (handle) => clearTimeout(handle as ReturnType<typeof setTimeout>),
}

/**
 * The endless cycle of a text too long for its place: waiting (marqueeStartDelay), scrolling (as
 * long as the distance takes at marqueeSpeed), end (marqueeEndPause), then waiting again. Every
 * change of the phase is handed to the callback; stop hands null.
 */
export class MarqueeCycle {
    private timer: unknown = null
    private distance = 0

    constructor(
        private readonly onPhase: (phase: MarqueePhase | null) => void,
        private readonly scheduler: MarqueeScheduler = timerScheduler,
    ) {}

    /** Whether the cycle runs. */
    get running(): boolean {
        return this.distance > 0
    }

    /** Starts the cycle from the beginning for a text that has to move by the distance. */
    start(distance: number): void {
        this.clearTimer()
        if (distance <= 0) {
            this.stop()
            return
        }

        this.distance = distance
        this.enter('waiting')
    }

    /** Stops the cycle; the text rests at its start. */
    stop(): void {
        this.clearTimer()
        this.distance = 0
        this.onPhase(null)
    }

    private enter(phase: MarqueePhase): void {
        this.onPhase(phase)
        switch (phase) {
            case 'waiting':
                this.schedule('scrolling', marqueeStartDelay)
                break
            case 'scrolling':
                this.schedule('end', marqueeScrollDuration(this.distance))
                break
            case 'end':
                this.schedule('waiting', marqueeEndPause)
                break
        }
    }

    private schedule(next: MarqueePhase, milliseconds: number): void {
        this.timer = this.scheduler.set(() => {
            this.timer = null
            this.enter(next)
        }, milliseconds)
    }

    private clearTimer(): void {
        if (this.timer !== null) {
            this.scheduler.clear(this.timer)
            this.timer = null
        }
    }
}
