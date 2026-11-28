// The engine of the browser with two playback paths (see backgroundAudio.ts): the effects path, an
// audio engine through Web Audio with equalizer, crossfade, ReplayGain and the analyser of the
// visualization, and the plain path, an audio engine whose two elements never touch Web Audio, so
// they keep playing where the browser stops the audio graph in the background (iPhone and iPad).
// There, ReplayGain, volume and the sleep timer fade work through the element volume; equalizer,
// crossfade and visualization pause. The player store talks to this engine as to a single one.
//
// When the wanted path changes, the engine hands the track over at its position: a paused track
// moves silently; a playing one starts on the new path (loaded, ready and caught up to the position)
// before the old path stops, so it plays on without a gap. Every command of the store during a
// handover cancels it; the newest wanted path wins.
//
// The browser may also interrupt the audio graph in the foreground (WebKit when the native full
// screen of a video, a call or another app takes over the audio) and keep it interrupted, sometimes
// until the page is reloaded. Then the effects path waits for a gesture of the user and the music
// plays on through the plain path meanwhile: play never waits for the graph longer than a moment,
// and the next tap or click tries the effects path again.

import { wantedPath, type PlaybackPath } from './backgroundAudio'
import {
    AudioEngine,
    browserAudioEnvironment,
    playResumeTimeout,
    type AudioEnvironment,
    type DeckSource,
    type EngineEvents,
} from './engine'
import type { EqualizerBandLayout, EqualizerState } from './equalizer'
import type { PlayerEngine } from '@/stores/player'

/** Milliseconds a handover waits for the new path to be ready or for the audio graph to run. */
export const handoverTimeout = 3000

export interface PathEngineOptions {
    /** Whether the browser needs the plain path in the background (backgroundPathSupported). */
    readonly supported: boolean
    /** The browser parts of both paths; the plain one gets no audio context. */
    readonly environment?: AudioEnvironment
    /** Milliseconds of a handover to wait; handoverTimeout by default. */
    readonly timeout?: number
    /** Milliseconds play waits for the audio graph of the effects path; playResumeTimeout by default. */
    readonly resumeTimeout?: number
    /** The active path changed. */
    readonly onPathChange?: (path: PlaybackPath) => void
    /**
     * The effects path waits for a gesture of the user (true) because the browser kept its audio
     * graph suspended, or no longer (false); see resumeEffects.
     */
    readonly onEffectsPending?: (pending: boolean) => void
}

interface Handover {
    readonly generation: number
    readonly target: PlaybackPath
}

export class PlaybackPathEngine implements PlayerEngine {
    private readonly effects: AudioEngine
    private readonly plain: AudioEngine
    private active: PlaybackPath = 'effects'
    private hidden = false
    /** The browser interrupted the audio graph while the page was in the background. */
    private interrupted = false
    /** The effects path waits for a gesture of the user to resume its audio graph. */
    private pendingEffects = false
    /** Whether the track should play: set by play, cleared by pause, stop and its end. */
    private wantsPlay = false
    private plainUnlocked = false
    private generation = 0
    private handover: Handover | null = null

    constructor(
        private readonly events: EngineEvents = {},
        private readonly options: PathEngineOptions = { supported: false },
    ) {
        const environment = options.environment ?? browserAudioEnvironment
        this.effects = new AudioEngine(this.eventsOf('effects'), environment)
        this.plain = new AudioEngine(this.eventsOf('plain'), {
            ...environment,
            createContext: () => null,
            webAudio: false,
        })
    }

    /** The path that plays now. */
    get path(): PlaybackPath {
        return this.active
    }

    /** Whether the effects path waits for a gesture of the user (see resumeEffects). */
    get effectsPending(): boolean {
        return this.pendingEffects
    }

    /** The analyser of the effects path; null on the plain path, so the visualization pauses. */
    get analyser(): AnalyserNode | null {
        return this.current.analyser
    }

    /** Whether the browser can run the equalizer, also while the plain path plays for a while. */
    get equalizerAvailable(): boolean {
        return this.effects.equalizerAvailable
    }

    get hasPrepared(): boolean {
        return this.current.hasPrepared
    }

    get position(): number {
        return this.current.position
    }

    get paused(): boolean {
        return this.current.paused
    }

    get source(): DeckSource | null {
        return this.current.source
    }

    /** A new track starts directly on the wanted path; a running handover ends. */
    load(source: DeckSource, position = 0): void {
        this.cancelHandover()
        const target = this.targetPath()
        if (target !== this.active) {
            const previous = this.current
            this.setPath(target)
            previous.stop()
        }

        this.current.load(source, position)
    }

    prepare(source: DeckSource | null): void {
        // A running handover takes the prepared track along when it completes.
        this.current.prepare(source)
    }

    /** Switches the source of the current track on the active path (see AudioEngine.switchTo). */
    switchTo(
        source: DeckSource,
        mapPosition: (position: number) => number,
        crossfadeSeconds?: number,
    ): Promise<boolean> {
        this.cancelHandover()
        return this.current.switchTo(source, mapPosition, crossfadeSeconds)
    }

    async play(): Promise<void> {
        this.wantsPlay = true
        // Within the gesture of this play: the plain path may start later without one.
        if (this.options.supported && !this.plainUnlocked) {
            this.plainUnlocked = true
            this.plain.unlock()
        }

        this.cancelHandover()
        if (this.targetPath() === 'effects') {
            // The audio graph runs again (also within this gesture), or the browser keeps it
            // interrupted: then the plain path plays until a later gesture resumes the graph.
            const running = await this.effects.resumeOutput(
                this.options.resumeTimeout ?? playResumeTimeout,
            )
            // Paused, stopped or another track meanwhile: that command decides.
            if (!this.wantsPlay) {
                return
            }

            if (!running) {
                this.setEffectsPending(true)
            }
        }

        const target = this.targetPath()
        if (target !== this.active) {
            if (!this.current.paused) {
                // It still plays on the other path (unheard through an interrupted graph):
                // handed over at its position.
                this.sync()
                return
            }

            this.switchSilently(target)
        }

        await this.current.play()
        this.sync()
    }

    pause(): void {
        this.wantsPlay = false
        this.cancelHandover()
        this.current.pause()
        this.sync()
    }

    stop(): void {
        this.wantsPlay = false
        this.cancelHandover()
        this.current.stop()
        const target = this.targetPath()
        if (target !== this.active) {
            this.setPath(target)
        }
    }

    seek(position: number): void {
        this.cancelHandover()
        this.current.seek(position)
        this.sync()
    }

    /** Volume, gain and fades go to both paths, so a handover finds them as they are. */
    setVolume(gain: number): void {
        this.effects.setVolume(gain)
        this.plain.setVolume(gain)
    }

    setTrackGain(gain: number): void {
        this.effects.setTrackGain(gain)
        this.plain.setTrackGain(gain)
    }

    setEqualizer(state: EqualizerState, layout?: readonly EqualizerBandLayout[]): void {
        this.effects.setEqualizer(state, layout)
        this.plain.setEqualizer(state, layout)
    }

    fadeOut(seconds: number): void {
        this.effects.fadeOut(seconds)
        this.plain.fadeOut(seconds)
    }

    restoreOutput(): void {
        this.effects.restoreOutput()
        this.plain.restoreOutput()
    }

    /** Unlocks playback within a gesture of the user (see AudioEngine.unlock), on both paths. */
    unlock(): void {
        this.effects.unlock()
        if (this.options.supported) {
            this.plainUnlocked = true
            this.plain.unlock()
        }
    }

    /** The page went to the background or came back: the playback moves to the wanted path. */
    setPageHidden(hidden: boolean): void {
        this.hidden = hidden
        if (hidden) {
            // The browser may have stopped the audio graph before it told about the background.
            if (this.active === 'effects' && this.wantsPlay && this.effects.outputSuspended) {
                this.interrupted = true
            }
        } else {
            this.interrupted = false
            this.setEffectsPending(false)
        }

        this.sync()
    }

    /**
     * Resumes the effects path within a gesture of the user after the browser kept its audio graph
     * suspended on the return to the foreground or interrupted it in the foreground.
     */
    resumeEffects(): void {
        if (!this.pendingEffects) {
            return
        }

        this.setEffectsPending(false)
        this.effects.unlock()
        this.sync()
    }

    private get current(): AudioEngine {
        return this.engineOf(this.active)
    }

    private engineOf(path: PlaybackPath): AudioEngine {
        return path === 'effects' ? this.effects : this.plain
    }

    /** The wanted path; the plain one while the effects path waits for a gesture. */
    private targetPath(): PlaybackPath {
        const wanted = wantedPath({
            supported: this.options.supported,
            hidden: this.hidden,
            interrupted: this.interrupted,
        })
        return wanted === 'effects' && this.pendingEffects ? 'plain' : wanted
    }

    /** The events of a path; only those of the active one reach the store. */
    private eventsOf(path: PlaybackPath): EngineEvents {
        const active = (): boolean => this.active === path
        return {
            timeupdate: (position, duration) => {
                if (active()) {
                    this.events.timeupdate?.(position, duration)
                }
            },
            playing: () => {
                if (active()) {
                    this.wantsPlay = true
                    this.events.playing?.()
                }
            },
            paused: () => {
                if (active()) {
                    this.wantsPlay = false
                    this.events.paused?.()
                }
            },
            waiting: () => {
                if (active()) {
                    this.events.waiting?.()
                }
            },
            ended: () => {
                if (active()) {
                    this.wantsPlay = false
                    this.cancelHandover()
                    this.events.ended?.()
                    // The track stays on the path where it ended only while that path is wanted,
                    // e.g. after a handover that failed (see handOver).
                    this.sync()
                }
            },
            advanced: () => {
                if (active()) {
                    // A handover of the previous track starts again with the new one, and one
                    // that failed is tried again: the new track goes to the wanted path.
                    this.cancelHandover()
                    this.events.advanced?.()
                    this.sync()
                }
            },
            error: (error) => {
                if (active()) {
                    this.cancelHandover()
                    this.events.error?.(error)
                }
            },
            interrupted: () => {
                if (path !== 'effects' || !active()) {
                    return
                }

                if (this.hidden) {
                    this.interrupted = true
                } else {
                    // In the foreground the graph may stay interrupted for good (WebKit): the plain
                    // path plays on, and the next gesture tries the effects path again. The graph
                    // running again by itself does not move the track back, so two players that
                    // take the audio from each other cannot make it jump to and fro.
                    this.setEffectsPending(true)
                }

                this.sync()
            },
        }
    }

    /** Moves the playback to the wanted path if it is not there or on its way. */
    private sync(): void {
        const target = this.targetPath()
        if (this.handover) {
            if (this.handover.target === target) {
                return
            }

            this.cancelHandover()
        }

        if (target === this.active) {
            return
        }

        if (!this.current.source) {
            this.setPath(target)
        } else if (this.wantsPlay) {
            void this.handOver(target)
        } else {
            this.switchSilently(target)
        }
    }

    /** Moves a track that does not play to the other path at its position. */
    private switchSilently(target: PlaybackPath): void {
        const from = this.current
        const to = this.engineOf(target)
        const source = from.source
        if (source) {
            to.load(source, from.position)
            to.prepare(from.preparedSource)
        }

        this.setPath(target)
        from.stop()
    }

    /**
     * Hands a playing track over to the other path: it is loaded there at the position, starts once
     * it is ready and caught up, and only then the old path stops. Fails quietly (the old path plays
     * on) when the new one does not start, e.g. when a short track reaches its end there while it
     * catches up; the next track or the end of this one tries the wanted path again. Ends when a
     * command or a newer handover cancels it.
     */
    private async handOver(target: PlaybackPath): Promise<void> {
        const handover: Handover = { generation: ++this.generation, target }
        this.handover = handover
        const running = (): boolean => this.handover?.generation === handover.generation
        const timeout = this.options.timeout ?? handoverTimeout
        const from = this.current
        const to = this.engineOf(target)
        if (target === 'effects') {
            // Without a gesture the browser may keep the audio graph suspended: wait for one.
            const resumed = await this.effects.resumeOutput(timeout)
            if (!running()) {
                return
            }

            if (!resumed) {
                this.handover = null
                this.setEffectsPending(true)
                return
            }
        }

        const source = from.source
        if (!source) {
            this.handover = null
            return
        }

        to.load(source, from.position)
        await to.whenReady(timeout)
        if (!running()) {
            return
        }

        // Catch up the time the new path needed to load.
        to.seek(from.position)
        await to.play()
        if (!running()) {
            return
        }

        this.handover = null
        if (to.paused) {
            to.stop()
            return
        }

        const prepared = from.preparedSource
        this.setPath(target)
        from.stop()
        if (prepared) {
            to.prepare(prepared)
        }

        // The store takes the analyser of the new path with it (none on the plain path).
        this.events.playing?.()
        this.events.timeupdate?.(to.position, to.duration)
    }

    /** Ends a running handover and stops its target path; true when one ran. */
    private cancelHandover(): boolean {
        const handover = this.handover
        if (!handover) {
            return false
        }

        this.handover = null
        this.engineOf(handover.target).stop()
        return true
    }

    private setPath(path: PlaybackPath): void {
        if (this.active !== path) {
            this.active = path
            this.options.onPathChange?.(path)
        }
    }

    private setEffectsPending(pending: boolean): void {
        if (this.pendingEffects !== pending) {
            this.pendingEffects = pending
            this.options.onEffectsPending?.(pending)
        }
    }
}
