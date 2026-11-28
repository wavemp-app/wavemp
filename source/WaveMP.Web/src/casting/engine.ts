// The engine of the player while it casts: the same interface as the audio engine, but the device
// plays. The queue stays in the web app: the player store loads one track after the other, and the
// controls go to the device. Equalizer, crossfade, ReplayGain, the sleep timer fade and the
// visualization need the audio graph of the browser and do nothing here. A source that ends before
// the end of its file (a skipped outro of a music video) ends there as if the media ended.

import type { DeckSource, EngineEvents } from '@/player/engine'
import type { PlayerEngine } from '@/stores/player'
import type { CastLayer } from './layer'
import type { CastMedia } from './types'

interface PendingLoad {
    readonly media: CastMedia
    readonly position: number
    autoplay: boolean
    readonly done: Promise<void>
}

export class CastEngine implements PlayerEngine {
    readonly equalizerAvailable = false
    readonly hasPrepared = false
    readonly analyser = null
    /** A load that waits for the rest of the current task, so a play right after it starts the media with it. */
    private pending: PendingLoad | null = null
    /** Whether the last request was play; a pause while the device loads keeps it paused. */
    private wantsPlay = false
    /** Where the loaded source ends before the end of its file (DeckSource.end); undefined without. */
    private end: number | undefined
    /** Whether the media reached that end and reported it. */
    private endReached = false

    constructor(
        private readonly layer: CastLayer,
        private readonly events: EngineEvents = {},
    ) {
        layer.setPlaybackEvents({
            timeupdate: (position, duration) => this.onTimeUpdate(position, duration),
            playing: () => events.playing?.(),
            paused: () => events.paused?.(),
            waiting: () => events.waiting?.(),
            ended: () => events.ended?.(),
            error: (error) => events.error?.(error),
        })
    }

    get position(): number {
        return this.layer.position
    }

    /**
     * Loads the media of the source on the device. The request leaves at the end of the current task,
     * so a play that follows at once starts it with the load instead of a second request.
     */
    load(source: DeckSource, position = 0): void {
        const media = source.cast
        if (!media) {
            this.events.error?.(new Error('The source has no media for the device'))
            return
        }

        this.end = typeof source.end === 'number' && source.end > 0 ? source.end : undefined
        this.endReached = false

        let resolve: () => void = () => undefined
        const pending: PendingLoad = {
            media,
            position,
            autoplay: false,
            done: new Promise<void>((done) => {
                resolve = done
            }),
        }
        this.pending = pending
        queueMicrotask(() => {
            if (this.pending !== pending) {
                resolve()
                return
            }

            this.pending = null
            this.layer
                .load(pending.media, pending.position, pending.autoplay)
                .then(() => {
                    if (pending.autoplay && !this.wantsPlay) {
                        this.layer.pause()
                    }
                })
                .catch((error: unknown) => this.events.error?.(error))
                .finally(resolve)
        })
    }

    /** The device loads one track at a time; the store loads the next one when it is due. */
    prepare(): void {}

    async play(): Promise<void> {
        this.wantsPlay = true
        const pending = this.pending
        if (pending) {
            pending.autoplay = true
            await pending.done
            return
        }

        try {
            await this.layer.play()
        } catch (error) {
            this.events.error?.(error)
        }
    }

    pause(): void {
        this.wantsPlay = false
        if (this.pending) {
            this.pending.autoplay = false
            return
        }

        this.layer.pause()
    }

    stop(): void {
        this.wantsPlay = false
        this.pending = null
        this.layer.stop()
    }

    seek(position: number): void {
        if (this.end !== undefined && position < this.end) {
            this.endReached = false
        }

        this.layer.seek(position)
    }

    /** The volume of the device follows the volume slider of the player (see the player store). */
    setVolume(): void {}

    setTrackGain(): void {}

    setEqualizer(): void {}

    fadeOut(): void {}

    restoreOutput(): void {}

    /**
     * Passes the time of the device on, with the end of the source as the duration. A playing
     * source that reaches its end before the end of the file ends there: the device pauses, and the
     * player goes on as if the media ended.
     */
    private onTimeUpdate(position: number, duration: number): void {
        const end = this.end
        const length = end !== undefined && (duration <= 0 || end < duration) ? end : duration
        this.events.timeupdate?.(position, length)
        if (end === undefined || this.endReached || !this.wantsPlay || position < end) {
            return
        }

        this.endReached = true
        this.events.ended?.()
        this.layer.pause()
    }
}
