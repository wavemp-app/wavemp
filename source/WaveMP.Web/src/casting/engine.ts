// The engine of the player while it casts: the same interface as the audio engine, but the device
// plays. The queue stays in the web app: the player store loads one track after the other, and the
// controls go to the device. Equalizer, crossfade, ReplayGain, the sleep timer fade and the
// visualization need the audio graph of the browser and do nothing here.

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

    constructor(
        private readonly layer: CastLayer,
        private readonly events: EngineEvents = {},
    ) {
        layer.setPlaybackEvents({
            timeupdate: (position, duration) => events.timeupdate?.(position, duration),
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
        this.layer.seek(position)
    }

    /** The volume of the device follows the volume slider of the player (see the player store). */
    setVolume(): void {}

    setTrackGain(): void {}

    setEqualizer(): void {}

    fadeOut(): void {}

    restoreOutput(): void {}
}
