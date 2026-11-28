// The video of the Now Playing stage. The audio engine always plays the track; the video element
// only shows the picture: muted, at the position of the video that plays the moment of the song the
// audio plays (see VideoMapping; the same position while the engine plays the sound of the video). While
// the stage shows the cover or the visualization, the video waits paused with its source loaded, so
// switching back is quick. A video file plays as it is, a transcoded video as HLS.
//
// In the native full screen of iPhone and iPad the video shows its own controls. Play, pause and
// the timeline there control the whole player (userPlay, userPause, userSeek): the audio engine
// plays, pauses or seeks and the video follows it as always. The video stays muted there as well,
// so it never takes the audio over from the engine; the start and the end of the full screen are
// reported, so the player can check its state when the page shows again.
//
// The follower moves the video only when it can play: never while it waits for data or for its
// last seek, and with longer pauses after seeks that missed (rules in videoSync.ts). With the
// developer setting of the video diagnostics, it reports what it does with every check.

import { finiteOrZero } from './engine'
import { browserHlsSupport, MediaSourceSlot, type HlsSupport } from './hls'
import {
    VideoDiagnosticsRecorder,
    type VideoDiagnosticsEventKind,
    type VideoDiagnosticsSnapshot,
} from './videoDiagnostics'
import {
    missedSeeksAfter,
    nextSeekLatency,
    softSyncThreshold,
    syncAction,
    videoTarget,
    type VideoState,
} from './videoSync'

/** The audio the video follows. */
export interface FollowerClock {
    /** Position of the audio in seconds. */
    readonly position: number
    /** Whether the audio plays. */
    readonly playing: boolean
}

export interface VideoFollowerEvents {
    /** The video is loading or positioning, ready at the right position, failed or unloaded. */
    stateChange?(state: VideoState): void
    /** The video was started by its own controls (e.g. the native full screen on the iPhone). */
    userPlay?(): void
    /** The video was paused by its own controls. */
    userPause?(): void
    /** The timeline of its own controls moved the video; position of the audio in seconds. */
    userSeek?(position: number): void
    /** The video entered (true) or left (false) the native full screen with its own controls. */
    ownControlsChange?(shown: boolean): void
    /** The diagnostics after each check, while they are switched on (setDiagnostics). */
    diagnostics?(snapshot: VideoDiagnosticsSnapshot): void
}

/** The source of a video. */
/**
 * Where the video is for a position of the audio the engine plays, and back: the same position
 * while the engine plays the sound of the video itself, otherwise the position of the video that
 * plays the same moment of the song (see src/player/songSegments.ts).
 */
export interface VideoMapping {
    /** The position of the video for a position of the audio, in seconds. */
    toVideo(position: number): number
    /** The position of the audio for a position of the video, in seconds. */
    toClock(videoPosition: number): number
}

/** A mapping by a fixed offset in seconds (position of the video = audio + offset). */
export function offsetMapping(offset: number): VideoMapping {
    return {
        toVideo: (position) => position + offset,
        toClock: (videoPosition) => videoPosition - offset,
    }
}

export interface VideoFollowerSource {
    readonly url: string
    /** Whether the URL is an HLS master playlist instead of a file. */
    readonly hls?: boolean
}

/** What the follower needs from the browser; tests replace it. */
export interface FollowerEnvironment {
    setInterval(callback: () => void, delay: number): number
    clearInterval(handle: number): void
    /** Milliseconds of a monotonic clock. */
    now(): number
    hls: HlsSupport
    /** Whether the video shows its own controls, e.g. in the native full screen on the iPhone. */
    hasOwnControls(element: HTMLVideoElement): boolean
}

export const browserFollowerEnvironment: FollowerEnvironment = {
    setInterval: (callback, delay) => window.setInterval(callback, delay),
    clearInterval: (handle) => window.clearInterval(handle),
    now: () => performance.now(),
    hls: browserHlsSupport,
    hasOwnControls: (element) =>
        (element as unknown as { webkitDisplayingFullscreen?: boolean })
            .webkitDisplayingFullscreen === true,
}

/** Milliseconds between two checks of the position while the video is shown. */
export const syncInterval = 250

/** readyState from which the element shows the picture of its position (HAVE_CURRENT_DATA). */
const haveCurrentData = 2

/** readyState from which the element can play on from its position (HAVE_FUTURE_DATA). */
const haveFutureData = 3

/** Events of the element that the diagnostics log. */
const diagnosticEvents = ['waiting', 'stalled'] as const

/** Seconds within which a seek of the element counts as the one the follower started. */
const ownSeekTolerance = 0.5

/** Seconds a seek of the own controls has to move the video away from the audio to move the audio. */
const userSeekThreshold = 1

/** The events of the native full screen of a video in Safari on the iPhone and iPad. */
const fullscreenEvents = ['webkitbeginfullscreen', 'webkitendfullscreen'] as const

export class VideoFollower {
    private readonly slot: MediaSourceSlot
    private readonly listeners = new AbortController()
    private state: VideoState = 'idle'
    private loaded = false
    private hasMetadata = false
    private active = false
    /** The video was hidden and is about to be shown: a seek keeps it hidden until it is done. */
    private revealing = false
    /** Whether the follower itself wants the video to play; other plays and pauses are the user's. */
    private wantsPlaying = false
    private mapping: VideoMapping = offsetMapping(0)
    private hls = false
    private lastSeek = Number.NEGATIVE_INFINITY
    /** The last seek of the follower is not done: the video did not play (or show) again since. */
    private settling = false
    /** Whether the audio played when the follower last seeked; only such seeks are measured. */
    private seekWhilePlaying = false
    /** Seeks in a row after which the video was still out of sync. */
    private missedSeeks = 0
    /** Seconds the seeks of this video took so far; null before the first one. */
    private seekLatency: number | null = null
    private readonly diagnostics = new VideoDiagnosticsRecorder()
    private diagnosticsEnabled = false
    /** Where the follower last sent the video; seeks elsewhere come from the own controls. */
    private ownSeek: number | null = null
    private timer: number | null = null

    constructor(
        readonly element: HTMLVideoElement,
        private readonly clock: () => FollowerClock,
        private readonly events: VideoFollowerEvents = {},
        private readonly environment: FollowerEnvironment = browserFollowerEnvironment,
    ) {
        element.muted = true
        element.defaultMuted = true
        element.playsInline = true
        this.slot = new MediaSourceSlot(element, environment.hls)
        const signal = this.listeners.signal
        element.addEventListener(
            'loadedmetadata',
            () => {
                if (this.loaded) {
                    this.hasMetadata = true
                    this.revealing = true
                    this.sync()
                    this.checkReady()
                }
            },
            { signal },
        )
        for (const name of ['loadeddata', 'canplay', 'seeked', 'playing']) {
            element.addEventListener(
                name,
                () => {
                    this.checkSettled()
                    this.checkReady()
                },
                { signal },
            )
        }

        for (const name of diagnosticEvents) {
            element.addEventListener(name, () => this.record(name), { signal })
        }
        element.addEventListener(
            'play',
            () => {
                if (this.loaded && !this.wantsPlaying && environment.hasOwnControls(element)) {
                    this.events.userPlay?.()
                }
            },
            { signal },
        )
        element.addEventListener(
            'pause',
            () => {
                if (
                    this.loaded &&
                    this.wantsPlaying &&
                    !element.ended &&
                    environment.hasOwnControls(element)
                ) {
                    this.wantsPlaying = false
                    this.events.userPause?.()
                }
            },
            { signal },
        )
        element.addEventListener('seeking', () => this.onSeeking(), { signal })
        // The native full screen offers a button for the sound of the video: the sound always comes
        // from the audio engine, and an audible video would take the audio over from it.
        element.addEventListener(
            'volumechange',
            () => {
                if (!element.muted) {
                    element.muted = true
                }
            },
            { signal },
        )
        for (const name of fullscreenEvents) {
            element.addEventListener(
                name,
                () => {
                    const shown = name === 'webkitbeginfullscreen'
                    element.muted = true
                    this.events.ownControlsChange?.(shown)
                    // Back on the page, the video catches up with the audio at once.
                    if (!shown) {
                        this.sync()
                    }
                },
                { signal },
            )
        }

        element.addEventListener(
            'error',
            () => {
                this.record('error')
                this.fail()
            },
            { signal },
        )
    }

    /** State of the video. */
    get videoState(): VideoState {
        return this.state
    }

    /**
     * Loads a video with the mapping of its positions to those of the audio, or a fixed offset in
     * milliseconds (position of the video = audio + offset).
     */
    load(source: VideoFollowerSource, mapping: VideoMapping | number = 0): void {
        this.unload()
        this.loaded = true
        this.mapping = typeof mapping === 'number' ? offsetMapping(mapping / 1000) : mapping
        this.hls = source.hls === true
        this.diagnostics.start(this.hls ? 'hls' : 'file', this.environment.now())
        this.setState('loading')
        this.slot.set(source.url, source.hls === true, () => this.fail())
    }

    /** Stops and removes the video. */
    unload(): void {
        this.stopTimer()
        this.wantsPlaying = false
        this.loaded = false
        this.hasMetadata = false
        this.revealing = false
        this.lastSeek = Number.NEGATIVE_INFINITY
        this.settling = false
        this.seekWhilePlaying = false
        this.missedSeeks = 0
        this.seekLatency = null
        this.ownSeek = null
        this.slot.clear()
        this.setState('idle')
    }

    /** Switches the report of the diagnostics after each check on or off. */
    setDiagnostics(enabled: boolean): void {
        this.diagnosticsEnabled = enabled
        if (enabled && this.loaded) {
            this.events.diagnostics?.(this.diagnostics.snapshot(this.environment.now()))
        }
    }

    /** A new fixed offset in milliseconds; it applies with the next check. */
    setOffset(offsetMilliseconds: number): void {
        this.mapping = offsetMapping(offsetMilliseconds / 1000)
    }

    /** The offset of the video at a position of the audio (constant within a piece of the song). */
    private offsetAt(position: number): number {
        return this.mapping.toVideo(position) - position
    }

    /**
     * Shows (true) or hides (false) the video. A hidden video waits paused with its source loaded; a
     * shown one is positioned at once and checked regularly.
     */
    setActive(active: boolean): void {
        if (!active) {
            this.active = false
            this.stopTimer()
            this.wantsPlaying = false
            this.element.pause()
            this.setRate(1)
            return
        }

        if (!this.active) {
            this.active = true
            this.revealing = true
        }

        this.sync()
        this.timer ??= this.environment.setInterval(() => this.sync(), syncInterval)
    }

    /**
     * Lets the video play from now on in browsers that allow it only after a gesture of the user
     * (Safari on the iPhone and iPad, for example in Low Power Mode); called within a tap or click.
     * The video starts and stops at once unless the follower wants it to play; the next check plays
     * or pauses it like the audio.
     */
    allowPlayback(): void {
        const element = this.element
        if (!element.paused) {
            return
        }

        element.play().catch(() => {
            // Stopped at once or refused: the next check starts the video when it should play.
        })
        if (!this.wantsPlaying) {
            element.pause()
        }
    }

    /** Brings the video to the position of the audio and plays or pauses it like the audio. */
    sync(): void {
        if (!this.active || !this.loaded || this.state === 'failed') {
            return
        }

        if (!this.hasMetadata) {
            // Safari on the iPhone and iPad loads the data of a media element only once it plays,
            // so a video that waits for its metadata before it plays would never get it. The shown
            // video starts at once (muted, and invisible until it is ready); as soon as its
            // metadata is there, it moves to the position of the audio and plays or pauses like it.
            this.play()
            return
        }

        const element = this.element
        if (element.seeking) {
            this.report(null)
            return
        }

        this.checkSettled()
        const clock = this.clock()
        const revealing = this.revealing
        const action = syncAction({
            audioPosition: clock.position,
            playing: clock.playing,
            videoPosition: finiteOrZero(element.currentTime),
            offset: this.offsetAt(clock.position),
            videoDuration: finiteOrZero(element.duration),
            sinceLastSeek: (this.environment.now() - this.lastSeek) / 1000,
            hls: this.hls,
            rate: element.playbackRate,
            // Only a video that showed and then ran out of data waits; a new one is positioned.
            buffering: this.state === 'ready' && !revealing && element.readyState < haveFutureData,
            settling: this.settling,
            missedSeeks: this.missedSeeks,
            seekLatency: this.seekLatency ?? 0,
        })
        this.revealing = false
        if (action.kind === 'hold') {
            this.setRate(1)
            this.pause()
            if (Math.abs(finiteOrZero(element.currentTime) - action.at) > softSyncThreshold) {
                this.seek(action.at, revealing, false)
            }
        } else if (action.kind === 'seek') {
            this.setRate(1)
            this.seek(action.to, revealing, clock.playing)
            this.follow(clock.playing)
        } else if (action.kind === 'wait') {
            this.setRate(1)
            this.follow(clock.playing)
        } else {
            this.setRate(clock.playing ? action.rate : 1)
            this.follow(clock.playing)
        }

        this.checkReady()
        this.report(action.kind)
    }

    /** Stops the video and lets go of the element, e.g. when the stage closes. */
    dispose(): void {
        this.unload()
        this.active = false
        this.listeners.abort()
    }

    private seek(to: number, hidden: boolean, playing: boolean): void {
        this.lastSeek = this.environment.now()
        this.settling = true
        this.seekWhilePlaying = playing
        this.ownSeek = to
        this.element.currentTime = to
        this.record('seek', to)
        // A video that is about to be shown stays hidden until it shows the right picture.
        if (hidden && this.state === 'ready') {
            this.setState('loading')
        }
    }

    /**
     * A seek of the element: the follower's own, or one of the timeline of the own controls of the
     * video, which moves the audio to the same place.
     */
    private onSeeking(): void {
        const element = this.element
        const time = finiteOrZero(element.currentTime)
        const own = this.ownSeek !== null && Math.abs(time - this.ownSeek) <= ownSeekTolerance
        if (own || !this.loaded || !this.hasMetadata || !this.environment.hasOwnControls(element)) {
            return
        }

        // Small jumps are the browser's (e.g. over a gap of the stream), not a move of the user.
        const expected = this.mapping.toVideo(this.clock().position)
        if (Math.abs(time - expected) <= userSeekThreshold) {
            return
        }

        // The video is where the user wants it: no correction of the follower moves it back first.
        this.lastSeek = this.environment.now()
        this.events.userSeek?.(Math.max(0, this.mapping.toClock(time)))
    }

    /**
     * Ends the wait for the last seek once the video plays again at its new position (or shows it,
     * while the audio is paused). A seek while the audio played is measured: its time lets the next
     * seek aim ahead, and a seek that still left the video out of sync makes the next one wait
     * longer.
     */
    private checkSettled(): void {
        const element = this.element
        if (!this.settling || !this.loaded || element.seeking) {
            return
        }

        const clock = this.clock()
        if (clock.playing && element.readyState < haveFutureData) {
            return
        }

        this.settling = false
        if (!this.seekWhilePlaying || !clock.playing) {
            return
        }

        const measured = (this.environment.now() - this.lastSeek) / 1000
        this.seekLatency = nextSeekLatency(this.seekLatency, measured)
        const target = videoTarget(clock.position, this.offsetAt(clock.position))
        const distance = Math.abs(finiteOrZero(element.currentTime) - target)
        this.missedSeeks = missedSeeksAfter(this.missedSeeks, distance, this.hls)
    }

    /** Sets the speed of the video only when it changes: each change may cost pictures. */
    private setRate(rate: number): void {
        if (Math.abs(this.element.playbackRate - rate) > 1e-6) {
            this.element.playbackRate = rate
            if (this.state === 'ready') {
                this.record('rate', rate)
            }
        }
    }

    /** The values of a check for the diagnostics, and their report while they are switched on. */
    private report(action: string | null): void {
        if (!this.loaded) {
            return
        }

        const element = this.element
        const clock = this.clock()
        const quality = playbackQualityOf(element)
        const videoPosition = finiteOrZero(element.currentTime)
        this.diagnostics.sample({
            time: this.environment.now(),
            audioPosition: clock.position,
            drift: this.hasMetadata
                ? videoPosition - videoTarget(clock.position, this.offsetAt(clock.position))
                : null,
            rate: element.playbackRate,
            readyState: element.readyState,
            videoPosition,
            buffered: bufferedRangesOf(element),
            droppedFrames: quality?.droppedVideoFrames ?? null,
            totalFrames: quality?.totalVideoFrames ?? null,
            action,
            missedSeeks: this.missedSeeks,
            seekLatency: this.seekLatency,
        })
        if (this.diagnosticsEnabled) {
            this.events.diagnostics?.(this.diagnostics.snapshot(this.environment.now()))
        }
    }

    private record(kind: VideoDiagnosticsEventKind, value: number | null = null): void {
        if (this.loaded) {
            this.diagnostics.record(kind, this.environment.now(), value, this.wantsPlaying)
        }
    }

    private follow(playing: boolean): void {
        if (playing) {
            this.play()
        } else {
            this.pause()
        }
    }

    private play(): void {
        this.wantsPlaying = true
        if (this.element.paused) {
            this.element.play().catch(() => {
                // An interrupted start is no error; the next check starts the video again.
            })
        }
    }

    private pause(): void {
        this.wantsPlaying = false
        if (!this.element.paused) {
            this.element.pause()
        }
    }

    private checkReady(): void {
        const element = this.element
        if (
            this.state === 'loading' &&
            this.hasMetadata &&
            !element.seeking &&
            element.readyState >= haveCurrentData
        ) {
            this.setState('ready')
        }
    }

    private fail(): void {
        if (this.loaded && this.state !== 'failed') {
            this.stopTimer()
            this.wantsPlaying = false
            this.setState('failed')
        }
    }

    private stopTimer(): void {
        if (this.timer !== null) {
            this.environment.clearInterval(this.timer)
            this.timer = null
        }
    }

    private setState(state: VideoState): void {
        if (this.state !== state) {
            this.state = state
            this.events.stateChange?.(state)
        }
    }
}

/** The loaded ranges of the element in seconds. */
function bufferedRangesOf(element: HTMLVideoElement): [number, number][] {
    const ranges = element.buffered as TimeRanges | undefined
    const result: [number, number][] = []
    for (let index = 0; index < (ranges?.length ?? 0); index++) {
        result.push([ranges!.start(index), ranges!.end(index)])
    }

    return result
}

/** The pictures the element dropped and showed, if the browser tells. */
function playbackQualityOf(element: HTMLVideoElement): VideoPlaybackQuality | null {
    const read = (element as Partial<HTMLVideoElement>).getVideoPlaybackQuality
    return typeof read === 'function' ? read.call(element) : null
}
