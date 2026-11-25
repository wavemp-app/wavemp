// When the active device publishes the state of its player: at once for play, pause, stop, a new
// song, a seek and the switches; shortly after changes of the queue (so quick changes publish
// once); at most every 250 ms while the volume moves; and every 10 seconds while it plays, so
// remotes keep the position.

/** Milliseconds a change of the queue, shuffle or repeat waits before it is published. */
export const publishDelayMilliseconds = 300
/** Shortest time between two publications while the volume moves. */
export const volumeIntervalMilliseconds = 250
/** Milliseconds between two publications while the player plays. */
export const heartbeatMilliseconds = 10_000

export interface PublisherTimers {
    setTimeout(handler: () => void, milliseconds: number): number
    clearTimeout(id: number): void
    setInterval(handler: () => void, milliseconds: number): number
    clearInterval(id: number): void
}

/** Decides when the state is published; publish sends it (with claim for a take-over). */
export class PlaybackPublisher {
    private delayed: number | null = null
    private throttle: number | null = null
    private throttledWaiting = false
    private beat: number | null = null

    constructor(
        private readonly publish: (claim: boolean) => void,
        private readonly timers: PublisherTimers,
    ) {}

    /** Publishes at once; a waiting delayed publication is part of it. */
    now(claim = false): void {
        this.cancelDelayed()
        this.publish(claim)
    }

    /** Publishes shortly; further changes in the meantime publish once. */
    soon(): void {
        this.cancelDelayed()
        this.delayed = this.timers.setTimeout(() => {
            this.delayed = null
            this.publish(false)
        }, publishDelayMilliseconds)
    }

    /** Publishes at once, then at most every 250 ms with the latest state. */
    throttled(): void {
        if (this.throttle !== null) {
            this.throttledWaiting = true
            return
        }

        this.publish(false)
        this.throttle = this.timers.setTimeout(() => {
            this.throttle = null
            if (this.throttledWaiting) {
                this.throttledWaiting = false
                this.throttled()
            }
        }, volumeIntervalMilliseconds)
    }

    /** Publishes every 10 seconds while on. */
    heartbeat(on: boolean): void {
        if (on && this.beat === null) {
            this.beat = this.timers.setInterval(() => this.publish(false), heartbeatMilliseconds)
        } else if (!on && this.beat !== null) {
            this.timers.clearInterval(this.beat)
            this.beat = null
        }
    }

    /** Stops every timer; nothing more is published until the next change. */
    dispose(): void {
        this.cancelDelayed()
        if (this.throttle !== null) {
            this.timers.clearTimeout(this.throttle)
            this.throttle = null
        }

        this.throttledWaiting = false
        this.heartbeat(false)
    }

    private cancelDelayed(): void {
        if (this.delayed !== null) {
            this.timers.clearTimeout(this.delayed)
            this.delayed = null
        }
    }
}
