// The audio engine of the player: two decks (audio elements) that take turns, so the next track is
// loaded while the current one plays and starts the moment it ends (gapless as far as the browser
// allows), or fades into it with a crossfade. With Web Audio, each deck runs through its own gain
// (ReplayGain) and fade (crossfade) into the equalizer, the output fade (sleep timer), the master gain
// (volume) and an analyser, which the visualizations read, so they see the sound as it is heard:
//
//   deck -> track gain -> fade -> equalizer (preamp, 10 bands) -> output fade -> master -> analyser
//
// Without Web Audio, the volume of the elements does gain, volume and fading of the sleep timer; the
// equalizer is unavailable and tracks change gapless instead of fading. Transcoded tracks arrive as
// HLS and are attached through hls.js. The engine knows nothing about the queue: the player store
// decides what plays.

import { crossfadeCurveSteps, equalPowerCurve } from './crossfade'
import { fallbackEqualizerBands, type EqualizerBandLayout, type EqualizerState } from './equalizer'
import { EqualizerChain } from './equalizerChain'
import { browserHlsSupport, MediaSourceSlot, type HlsSupport } from './hls'
import type { CastMedia } from '@/casting/types'

export interface DeckSource {
    /** URL of the stream. */
    readonly url: string
    /** Gain factor of the track (ReplayGain), 1 for none. */
    readonly gain: number
    /** Whether the URL is an HLS master playlist instead of a file. */
    readonly hls?: boolean
    /** Seconds the previous track fades into this one when it ends on its own; gapless without. */
    readonly crossfade?: number
    /** What a casting device plays instead (see src/casting/engine.ts); the audio engine ignores it. */
    readonly cast?: CastMedia
    /**
     * Seconds of the file at which the track starts for the player (the intro of a music video is
     * skipped): a prepared track starts there, and a load without a position as well.
     */
    readonly start?: number
    /**
     * Seconds of the file at which the track ends for the player (the outro of a music video is
     * skipped): the next track follows from there as if the file ended. To the end of the file
     * without.
     */
    readonly end?: number
}

export interface EngineEvents {
    /** Position and duration in seconds; the duration is 0 while unknown. */
    timeupdate?(position: number, duration: number): void
    playing?(): void
    paused?(): void
    /** The playback waits for data. */
    waiting?(): void
    /** The current track ended and no next one was prepared. */
    ended?(): void
    /** The prepared track took over from the current one, which ended or fades out. */
    advanced?(): void
    /** The current track failed to load or play. */
    error?(error: unknown): void
    /**
     * The browser interrupted or suspended the audio graph while a track should play (e.g. WebKit
     * when the page goes to the background, or in the foreground when the native full screen of a
     * video, a call or another app takes over the audio): the elements may run on but are not heard.
     */
    interrupted?(): void
}

/** What the engine needs from the browser; tests replace it. */
export interface AudioEnvironment {
    createAudio(): HTMLAudioElement
    /** A new audio context, or null without Web Audio. */
    createContext(): AudioContext | null
    setTimeout(callback: () => void, delay: number): number
    clearTimeout(handle: number): void
    /** HLS playback; the browser's by default. */
    hls?: HlsSupport
    /** Whether the browser has Web Audio at all; assumed when not given. */
    webAudio?: boolean
}

function audioContextClass(): typeof AudioContext | undefined {
    return typeof window === 'undefined'
        ? undefined
        : (window.AudioContext ??
              (window as unknown as { webkitAudioContext?: typeof AudioContext })
                  .webkitAudioContext)
}

export const browserAudioEnvironment: AudioEnvironment = {
    createAudio: () => {
        const audio = new Audio()
        audio.preload = 'auto'
        return audio
    },
    createContext: () => {
        const Context = audioContextClass()
        try {
            return Context ? new Context({ latencyHint: 'playback' }) : null
        } catch {
            return null
        }
    },
    setTimeout: (callback, delay) => window.setTimeout(callback, delay),
    clearTimeout: (handle) => window.clearTimeout(handle),
    hls: browserHlsSupport,
    get webAudio() {
        return audioContextClass() !== undefined
    },
}

/**
 * The next track starts this many seconds before the current one ends, which roughly makes up for
 * the time a starting element needs until it sounds.
 */
export const handoverLead = 0.05

/** Seconds before the end of a track (or of its crossfade) from which the start of the next one is timed. */
export const handoverWindow = 0.6

/** Shorter crossfades than this are played as a gapless change. */
export const minimumCrossfade = 0.1

/** The fading deck is unloaded this long after its crossfade. */
export const crossfadeUnloadDelay = 0.25

/** Seconds the sources of a track fade into each other when the player switches between them. */
export const sourceSwitchCrossfade = 0.25

/** Milliseconds a switch of the source waits for the new one (a transcoded stream may take a while). */
export const sourceSwitchTimeout = 10000

/** Steps per second of the fade of the sleep timer without Web Audio. */
export const elementFadeStepsPerSecond = 10

/** Time constant in seconds with which the output returns after the sleep timer. */
export const outputRestoreSmoothing = 0.05

/**
 * Milliseconds play waits for a suspended or interrupted audio graph to run again. WebKit keeps the
 * promise of resume pending as long as it interrupts the graph (e.g. after the native full screen of a
 * video, a call or another app), which can last until the page is reloaded; play never waits longer.
 */
export const playResumeTimeout = 1000

interface Deck {
    readonly audio: HTMLAudioElement
    gain: GainNode | null
    /** Gain of the crossfade, 1 outside of one. */
    fade: GainNode | null
    source: DeckSource | null
    /** The file or HLS stream of the element. */
    readonly slot: MediaSourceSlot
    /**
     * Where the deck was last sent (start position of the track or seek) until the element is
     * there; null without one. Cleared by seeked and with every new source.
     */
    target: SeekTarget | null
    /** Whether the deck reached the end of its source (DeckSource.end) and reported it. */
    endReached: boolean
}

interface Crossfade {
    /** The deck that fades out. */
    readonly deck: Deck
    readonly timer: number
}

/** A position a deck was sent to. */
export interface SeekTarget {
    readonly position: number
    /**
     * Whether the element was sent there: false while it waits for its metadata, before which it
     * cannot seek; the deck moves it there once the metadata is known.
     */
    readonly sent: boolean
}

/** A time of a media element in seconds, 0 while unknown. */
export function finiteOrZero(value: number): number {
    return Number.isFinite(value) && value > 0 ? value : 0
}

/** The ready state from which a media element knows its duration and can seek (HAVE_METADATA). */
const haveMetadata = 1

/** The ready state from which a media element can play on for a while (HAVE_FUTURE_DATA). */
const haveFutureData = 3

/** What deckPosition needs of a media element. */
export interface SeekableElement {
    readonly currentTime: number
    readonly readyState: number
    readonly seeking: boolean
}

/**
 * The position of a deck: the position it was sent to until the element is there (it waits for the
 * metadata to go there, has no metadata or still seeks), otherwise the time of the element. So no
 * event of the element from before the seek or the metadata moves the player back.
 */
export function deckPosition(target: SeekTarget | null, element: SeekableElement): number {
    if (target !== null && (!target.sent || element.readyState < haveMetadata || element.seeking)) {
        return target.position
    }

    return finiteOrZero(element.currentTime)
}

/** A position within the duration of a track; without a known duration only not below 0. */
function clampPosition(position: number, duration: number): number {
    return Math.max(0, duration > 0 ? Math.min(position, duration) : position)
}

export class AudioEngine {
    private readonly decks: [Deck, Deck]
    private current = 0
    private prepared: DeckSource | null = null
    private context: AudioContext | null = null
    private master: GainNode | null = null
    private outputFade: GainNode | null = null
    private equalizer: EqualizerChain | null = null
    private analyserNode: AnalyserNode | null = null
    private graphReady = false
    private graphFailed = false
    /** Whether the last request was play; a pause while the context resumes cancels it. */
    private wantsPlay = false
    private volume = 1
    private handover: number | null = null
    private crossfade: Crossfade | null = null
    /** A next track prepared while a crossfade still uses the other deck; set once it ended. */
    private deferredPrepare: DeckSource | null = null
    /** Increases with every switch of the source and every command that ends one. */
    private switchGeneration = 0
    /** Whether a switch of the source loads the other deck. */
    private switching = false
    private equalizerState: EqualizerState = { enabled: false, preampDb: 0, gainsDb: [] }
    private equalizerLayout: readonly EqualizerBandLayout[] = fallbackEqualizerBands
    /** Factor of the output without Web Audio while the sleep timer fades it out. */
    private elementFade = 1
    private elementFadeTimer: number | null = null
    /** Waits of whenReady; a new track or a stop ends them as not ready. */
    private readonly readyWaits = new Set<() => void>()

    constructor(
        private readonly events: EngineEvents = {},
        private readonly environment: AudioEnvironment = browserAudioEnvironment,
    ) {
        this.decks = [this.createDeck(0), this.createDeck(1)]
    }

    /** The analyser of the output; null without Web Audio or before the first playback. */
    get analyser(): AnalyserNode | null {
        return this.analyserNode
    }

    /** Whether the equalizer (and the crossfade) can work: Web Audio exists and its graph was built. */
    get equalizerAvailable(): boolean {
        return this.environment.webAudio !== false && !this.graphFailed
    }

    /** Whether the audio graph exists but does not run (suspended or interrupted by the browser). */
    get outputSuspended(): boolean {
        return this.context !== null && !outputRunning(this.context)
    }

    /** The source of the current deck. */
    get source(): DeckSource | null {
        return this.deck.source
    }

    get position(): number {
        return this.positionOf(this.deck)
    }

    get duration(): number {
        return this.durationOf(this.deck)
    }

    get paused(): boolean {
        return this.deck.audio.paused
    }

    /** Whether a next track is loaded (or waits for the end of a crossfade to be loaded). */
    get hasPrepared(): boolean {
        return this.prepared !== null || this.deferredPrepare !== null
    }

    /** The next track that is loaded (or waits for the end of a crossfade to be loaded). */
    get preparedSource(): DeckSource | null {
        return this.deferredPrepare ?? this.prepared
    }

    /** Whether the previous track still fades out. */
    get crossfading(): boolean {
        return this.crossfade !== null
    }

    /**
     * Loads a track on the current deck, optionally at a position, and starts it with play. The
     * element moves to the position once it knows the metadata; a seek before that replaces it.
     */
    load(source: DeckSource, position = 0): void {
        this.cancelSwitch()
        this.endReadyWaits()
        this.cancelHandover()
        this.finishCrossfade()
        this.prepare(null)
        const deck = this.deck
        deck.source = source
        this.applyGain(deck)
        this.attach(deck, source)
        const start = position > 0 ? position : (source.start ?? 0)
        deck.target = start > 0 ? { position: start, sent: false } : null
    }

    /** Loads the next track on the other deck, or forgets it with null. */
    prepare(source: DeckSource | null): void {
        if (this.crossfade || this.switching) {
            // The other deck still fades out; the track is loaded there once it is done.
            this.deferredPrepare = source
            return
        }

        const other = this.other
        if (source && sameSource(source, this.prepared)) {
            return
        }

        this.prepared = source
        if (source) {
            other.source = source
            this.applyGain(other)
            this.attach(other, source)
            const start = source.start ?? 0
            other.target = start > 0 ? { position: start, sent: false } : null
        } else {
            this.unload(other)
        }
    }

    async play(): Promise<void> {
        this.wantsPlay = true
        this.ensureGraph()
        if (this.context && !outputRunning(this.context)) {
            // At most for a while: a graph that stays interrupted must not block play for good.
            await this.resumeOutput(playResumeTimeout)
            // Paused or stopped while the context resumed: stay paused.
            if (!this.wantsPlay) {
                return
            }
        }

        // An HLS stream that still attaches would drop the play once it sets the src of the element.
        const deck = this.deck
        if (deck.slot.pending && !(await this.sourceAttached(deck))) {
            return
        }

        try {
            await deck.audio.play()
        } catch (error) {
            // An interrupted play (pause or new source meanwhile) is no error.
            if (!(error instanceof DOMException && error.name === 'AbortError')) {
                this.events.error?.(error)
            }
        }
    }

    /**
     * Unlocks playback within a gesture of the user without playing anything, so a later play()
     * without a gesture (a command of another device) is allowed: builds and resumes the Web Audio
     * graph and loads the empty decks, which Safari counts as the start of the elements.
     */
    unlock(): void {
        this.ensureGraph()
        if (this.context && !outputRunning(this.context)) {
            void this.context.resume().catch(() => undefined)
        }

        for (const deck of this.decks) {
            if (!deck.source && deck.audio.paused) {
                deck.audio.load()
            }
        }
    }

    pause(): void {
        this.wantsPlay = false
        this.cancelSwitch()
        this.cancelHandover()
        this.finishCrossfade()
        this.deck.audio.pause()
    }

    /**
     * Waits until the current track can play on (its element has data beyond the position): true
     * then, false when it fails, another track is loaded, the engine stops or the time runs out.
     */
    whenReady(timeoutMs: number): Promise<boolean> {
        return this.deckReady(this.deck, timeoutMs)
    }

    /**
     * Switches the current track to another source of the same song (the audio file or the sound
     * of its video) without a gap: the source loads on the other deck at the position mapPosition
     * gives for the current position, catches up once it is ready and fades in shortly while the
     * old one fades out (without Web Audio it takes over directly). A paused track just moves to
     * the new source. True once the new source plays (or waits paused); false when it does not
     * start in time, another command ended the switch, or a newer switch replaced it. A prepared
     * next track is loaded again after the switch.
     */
    async switchTo(
        source: DeckSource,
        mapPosition: (position: number) => number,
        crossfadeSeconds = sourceSwitchCrossfade,
    ): Promise<boolean> {
        const prepared = this.preparedSource
        if (!this.wantsPlay || this.deck.audio.paused) {
            this.load(source, mapPosition(this.position))
            this.prepare(prepared)
            return true
        }

        this.cancelSwitch()
        this.cancelHandover()
        this.finishCrossfade()
        const generation = ++this.switchGeneration
        this.prepared = null
        this.deferredPrepare = prepared
        this.switching = true
        const target = this.other
        target.source = source
        this.applyGain(target)
        this.attach(target, source)
        target.target = { position: mapPosition(this.position), sent: false }
        const ready = await this.deckReady(target, sourceSwitchTimeout)
        if (generation !== this.switchGeneration) {
            return false
        }

        this.switching = false
        if (!ready || !this.wantsPlay) {
            this.unload(target)
            this.restoreDeferredPrepare()
            return false
        }

        // Catch up the time the new source needed to load.
        const position = mapPosition(this.position)
        target.target = { position, sent: true }
        target.audio.currentTime = position
        const previous = this.deck
        this.current = 1 - this.current
        const fade = this.context !== null && target.fade !== null
        if (fade && crossfadeSeconds >= minimumCrossfade) {
            this.startCrossfade(previous, crossfadeSeconds)
        } else {
            this.setFade(target, 1)
            previous.audio.pause()
            this.unload(previous)
            this.restoreDeferredPrepare()
        }

        try {
            await target.audio.play()
        } catch (error) {
            if (!(error instanceof DOMException && error.name === 'AbortError')) {
                this.events.error?.(error)
            }
        }

        this.events.timeupdate?.(this.positionOf(target), this.durationOf(target))
        return true
    }

    /** Waits until a deck can play on (see whenReady). */
    private deckReady(deck: Deck, timeoutMs: number): Promise<boolean> {
        const audio = deck.audio
        const ready = (): boolean => audio.readyState >= haveFutureData && !audio.seeking
        if (!deck.source) {
            return Promise.resolve(false)
        }

        if (ready()) {
            return Promise.resolve(true)
        }

        return new Promise((resolve) => {
            const readyEvents = ['canplay', 'canplaythrough', 'seeked', 'playing']
            const check = (): void => {
                if (ready()) {
                    finish(true)
                }
            }
            const fail = (): void => finish(false)
            const finish = (result: boolean): void => {
                this.readyWaits.delete(fail)
                this.environment.clearTimeout(timer)
                readyEvents.forEach((name) => audio.removeEventListener(name, check))
                audio.removeEventListener('error', fail)
                resolve(result)
            }
            const timer = this.environment.setTimeout(fail, timeoutMs)
            this.readyWaits.add(fail)
            readyEvents.forEach((name) => audio.addEventListener(name, check))
            audio.addEventListener('error', fail)
        })
    }

    /**
     * Builds and resumes the audio graph (some browsers allow that only within a gesture of the
     * user): true once it runs, or without Web Audio; false when the browser keeps it suspended or
     * interrupted, or it does not run within the time.
     */
    async resumeOutput(timeoutMs = 3000): Promise<boolean> {
        this.ensureGraph()
        const context = this.context
        if (!context || outputRunning(context)) {
            return true
        }

        let timer: number | null = null
        await Promise.race([
            context.resume().catch(() => undefined),
            new Promise<void>((resolve) => {
                timer = this.environment.setTimeout(resolve, timeoutMs)
            }),
        ])
        if (timer !== null) {
            this.environment.clearTimeout(timer)
        }

        return outputRunning(context)
    }

    /** Stops and unloads both decks. */
    stop(): void {
        this.cancelSwitch()
        this.endReadyWaits()
        this.wantsPlay = false
        this.cancelHandover()
        this.finishCrossfade()
        this.prepare(null)
        this.unload(this.deck)
    }

    /**
     * Moves the current track to the position. Without metadata the element cannot seek yet: it
     * moves there once the metadata is known, and the deck reports the position until then.
     */
    seek(position: number): void {
        this.cancelSwitch()
        this.cancelHandover()
        this.finishCrossfade()
        const deck = this.deck
        const audio = deck.audio
        const target = clampPosition(position, finiteOrZero(audio.duration))
        deck.endReached = false
        deck.target = { position: target, sent: audio.readyState >= haveMetadata }
        // Without metadata the browser keeps it as its start position; the deck makes sure of it.
        audio.currentTime = target
    }

    /** Output gain between 0 and 1 (already shaped by the volume curve). */
    setVolume(gain: number): void {
        this.volume = gain
        if (this.master) {
            this.master.gain.value = gain
        }

        this.decks.forEach((deck) => this.applyGain(deck))
    }

    /** Changes the gain of the current track, e.g. when the ReplayGain mode changes. */
    setTrackGain(gain: number): void {
        const deck = this.deck
        if (deck.source) {
            deck.source = { ...deck.source, gain }
            this.applyGain(deck)
        }
    }

    /**
     * Applies the equalizer at once, also during playback. Before the graph exists the settings are
     * kept and applied when it is built.
     */
    setEqualizer(state: EqualizerState, layout?: readonly EqualizerBandLayout[]): void {
        this.equalizerState = state
        if (layout) {
            this.equalizerLayout = layout
        }

        this.equalizer?.apply(state, layout)
    }

    /** Fades the output out to silence within the seconds from now on (sleep timer). */
    fadeOut(seconds: number): void {
        const duration = Math.max(0, seconds)
        if (this.outputFade && this.context) {
            const gain = this.outputFade.gain
            const now = this.context.currentTime
            gain.cancelScheduledValues(now)
            gain.setValueAtTime(gain.value, now)
            gain.linearRampToValueAtTime(0, now + duration)
            return
        }

        this.cancelElementFade()
        const steps = Math.max(1, Math.round(duration * elementFadeStepsPerSecond))
        const start = this.elementFade
        let step = 0
        const next = (): void => {
            step++
            this.elementFade = Math.max(0, start * (1 - step / steps))
            this.decks.forEach((deck) => this.applyGain(deck))
            this.elementFadeTimer =
                step < steps
                    ? this.environment.setTimeout(next, 1000 / elementFadeStepsPerSecond)
                    : null
        }
        this.elementFadeTimer = this.environment.setTimeout(next, 1000 / elementFadeStepsPerSecond)
    }

    /** Ends a fade of the sleep timer and brings the output back smoothly. */
    restoreOutput(): void {
        if (this.outputFade && this.context) {
            const gain = this.outputFade.gain
            const now = this.context.currentTime
            gain.cancelScheduledValues(now)
            gain.setValueAtTime(gain.value, now)
            gain.setTargetAtTime(1, now, outputRestoreSmoothing)
        }

        this.cancelElementFade()
        this.elementFade = 1
        this.decks.forEach((deck) => this.applyGain(deck))
    }

    private get deck(): Deck {
        return this.decks[this.current]!
    }

    /** The position of a deck: its target while the element is not there yet (see deckPosition). */
    private positionOf(deck: Deck): number {
        return deckPosition(deck.target, deck.audio)
    }

    private get other(): Deck {
        return this.decks[1 - this.current]!
    }

    /** Where the track of a deck ends: the end of its source or of the file, 0 while unknown. */
    private durationOf(deck: Deck): number {
        const file = finiteOrZero(deck.audio.duration)
        const end = deck.source?.end
        return typeof end === 'number' && end > 0 && (file === 0 || end < file) ? end : file
    }

    /**
     * Waits until the source of a deck is on its element (an HLS stream attaches asynchronously):
     * true when the deck should play it now, false when the stream failed, the engine should no
     * longer play or the deck is no longer the current one (a takeover plays the new one itself).
     */
    private async sourceAttached(deck: Deck): Promise<boolean> {
        const attached = await deck.slot.ready()
        return attached && this.wantsPlay && deck === this.deck && deck.source !== null
    }

    /**
     * Plays the deck that just became the current one: at once when its source is on the element
     * (gapless), otherwise once its stream is attached.
     */
    private playCurrentDeck(deck: Deck): void {
        const play = (): void => {
            void deck.audio.play().catch((error: unknown) => this.events.error?.(error))
        }
        if (!deck.slot.pending) {
            play()
            return
        }

        void this.sourceAttached(deck).then((attached) => attached && play())
    }

    /** Ends a running switch of the source; the deck it loaded is freed. */
    private cancelSwitch(): void {
        if (!this.switching) {
            return
        }

        this.switchGeneration++
        this.switching = false
        this.unload(this.other)
        this.restoreDeferredPrepare()
    }

    /** Loads the next track that waited for a switch or crossfade to end. */
    private restoreDeferredPrepare(): void {
        const deferred = this.deferredPrepare
        this.deferredPrepare = null
        if (deferred && !this.crossfade) {
            this.prepare(deferred)
        }
    }

    /**
     * The track reached the end of its source before the end of the file (a skipped outro): it ends
     * there as if the file ended.
     */
    private checkSourceEnd(deck: Deck): void {
        const end = deck.source?.end
        if (
            deck.endReached ||
            typeof end !== 'number' ||
            end <= 0 ||
            deck.audio.paused ||
            this.positionOf(deck) < end
        ) {
            return
        }

        deck.endReached = true
        this.cancelHandover()
        if (this.prepared) {
            this.takeOver(true)
        } else {
            deck.audio.pause()
            this.events.ended?.()
        }
    }

    private createDeck(index: number): Deck {
        const audio = this.environment.createAudio()
        const deck: Deck = {
            audio,
            gain: null,
            fade: null,
            source: null,
            slot: new MediaSourceSlot(audio, this.environment.hls ?? browserHlsSupport),
            target: null,
            endReached: false,
        }
        const active = (): boolean => this.decks[this.current] === deck
        audio.addEventListener('loadedmetadata', () => {
            // The start position of the track, or a seek from before the metadata.
            const target = deck.target
            if (target === null) {
                return
            }

            const position = clampPosition(target.position, finiteOrZero(audio.duration))
            deck.target = { position, sent: true }
            if (finiteOrZero(audio.currentTime) !== position) {
                audio.currentTime = position
            }

            // Already there: the browser applied it as its start position, and no seeked follows.
            if (!audio.seeking && finiteOrZero(audio.currentTime) === position) {
                deck.target = null
            }
        })
        audio.addEventListener('seeked', () => {
            // A newer seek may still run (its own seeked follows), or the metadata is still missing.
            if (!audio.seeking && deck.target?.sent !== false) {
                deck.target = null
            }
        })
        audio.addEventListener('timeupdate', () => {
            if (active()) {
                this.events.timeupdate?.(this.positionOf(deck), this.durationOf(deck))
                this.scheduleHandover()
                this.checkSourceEnd(deck)
            }
        })
        audio.addEventListener('durationchange', () => {
            if (active()) {
                this.events.timeupdate?.(this.positionOf(deck), this.durationOf(deck))
            }
        })
        audio.addEventListener('playing', () => active() && this.events.playing?.())
        audio.addEventListener('pause', () => {
            if (active() && !audio.ended) {
                this.events.paused?.()
            }
        })
        audio.addEventListener('waiting', () => active() && this.events.waiting?.())
        audio.addEventListener('ended', () => {
            if (active()) {
                this.onEnded()
            } else if (this.crossfade?.deck === deck) {
                // The fading track ended before its timer: the crossfade is over.
                this.finishCrossfade()
            }
        })
        audio.addEventListener('error', () => {
            if (active() && deck.source) {
                this.events.error?.(audio.error ?? new Error(`Deck ${index} failed to play.`))
            }
        })
        return deck
    }

    /** Builds the Web Audio graph once, within the first play (a user gesture). */
    private ensureGraph(): void {
        if (this.graphReady) {
            return
        }

        this.graphReady = true
        const context = this.environment.createContext()
        if (!context) {
            this.graphFailed = true
            return
        }

        try {
            const master = context.createGain()
            const outputFade = context.createGain()
            const analyser = context.createAnalyser()
            const equalizer = new EqualizerChain(context, this.equalizerLayout)
            equalizer.output.connect(outputFade)
            outputFade.connect(master)
            master.connect(analyser)
            analyser.connect(context.destination)
            for (const deck of this.decks) {
                const gain = context.createGain()
                const fade = context.createGain()
                context.createMediaElementSource(deck.audio).connect(gain)
                gain.connect(fade)
                fade.connect(equalizer.input)
                deck.gain = gain
                deck.fade = fade
            }

            master.gain.value = this.volume
            outputFade.gain.value = this.elementFade
            context.addEventListener('statechange', () => this.onContextState(context))
            this.cancelElementFade()
            this.elementFade = 1
            this.context = context
            this.master = master
            this.outputFade = outputFade
            this.equalizer = equalizer
            this.analyserNode = analyser
            equalizer.apply(this.equalizerState, this.equalizerLayout)
        } catch {
            // Without the graph the elements play on their own.
            this.graphFailed = true
            this.decks.forEach((deck) => {
                deck.gain = null
                deck.fade = null
            })
            void context.close().catch(() => undefined)
        }

        this.decks.forEach((deck) => this.applyGain(deck))
    }

    /** The graph stopped running while a track should play, not through a pause of the engine. */
    private onContextState(context: AudioContext): void {
        if (this.context === context && this.wantsPlay && !outputRunning(context)) {
            this.events.interrupted?.()
        }
    }

    /** Ends all waits of whenReady as not ready. */
    private endReadyWaits(): void {
        // Each wait removes itself; a Set iterates on safely.
        for (const end of this.readyWaits) {
            end()
        }
    }

    private applyGain(deck: Deck): void {
        const trackGain = deck.source?.gain ?? 1
        if (deck.gain && this.master) {
            deck.gain.gain.value = trackGain
            deck.audio.volume = 1
            deck.audio.muted = false
        } else {
            const volume = Math.min(1, Math.max(0, this.volume * trackGain * this.elementFade))
            deck.audio.volume = volume
            // WebKit on iOS ignores the volume of an element, but silences it when muted.
            deck.audio.muted = volume === 0
        }
    }

    private cancelElementFade(): void {
        if (this.elementFadeTimer !== null) {
            this.environment.clearTimeout(this.elementFadeTimer)
            this.elementFadeTimer = null
        }
    }

    /** Seconds of the crossfade into the prepared track; 0 without Web Audio. */
    private preparedCrossfade(): number {
        const seconds = this.prepared?.crossfade ?? 0
        return this.context && this.deck.fade && seconds >= minimumCrossfade ? seconds : 0
    }

    /** Starts the prepared deck shortly before the current one ends, or before its crossfade. */
    private scheduleHandover(): void {
        if (!this.prepared || this.handover !== null || this.crossfade || this.deck.audio.paused) {
            return
        }

        const audio = this.deck.audio
        const crossfade = this.preparedCrossfade()
        const length = this.durationOf(this.deck)
        const remaining = length - audio.currentTime
        if (length === 0 || remaining > crossfade + handoverWindow) {
            return
        }

        const delay = Math.max(0, (remaining - crossfade - handoverLead) * 1000)
        this.handover = this.environment.setTimeout(() => {
            this.handover = null
            this.takeOver(false)
        }, delay)
    }

    private cancelHandover(): void {
        if (this.handover !== null) {
            this.environment.clearTimeout(this.handover)
            this.handover = null
        }
    }

    private onEnded(): void {
        this.cancelHandover()
        if (this.prepared) {
            this.takeOver(true)
        } else {
            this.events.ended?.()
        }
    }

    /**
     * The prepared deck becomes the current one and plays. With a crossfade, the old one fades out
     * while the new one fades in, and is unloaded afterwards. Otherwise the old one is unloaded at
     * once if it ended, or once its last moments have played, unless it was prepared again meanwhile.
     */
    private takeOver(ended: boolean): void {
        if (!this.prepared) {
            return
        }

        const previous = this.deck
        const remaining = this.durationOf(previous) - previous.audio.currentTime
        const crossfade = ended ? 0 : Math.min(this.preparedCrossfade(), Math.max(0, remaining))
        this.current = 1 - this.current
        this.prepared = null
        if (crossfade >= minimumCrossfade) {
            this.startCrossfade(previous, crossfade)
        } else {
            this.setFade(this.deck, 1)
        }

        this.playCurrentDeck(this.deck)
        previous.source = null
        if (ended) {
            this.unload(previous)
        } else if (crossfade < minimumCrossfade) {
            this.environment.setTimeout(() => {
                if (
                    previous !== this.deck &&
                    previous.source === null &&
                    this.crossfade?.deck !== previous
                ) {
                    this.unload(previous)
                }
            }, 1000)
        }

        this.events.advanced?.()
        this.events.timeupdate?.(this.positionOf(this.deck), this.durationOf(this.deck))
    }

    private startCrossfade(previous: Deck, seconds: number): void {
        const now = this.context?.currentTime ?? 0
        this.rampFade(this.deck, equalPowerCurve('in', crossfadeCurveSteps), now, seconds)
        this.rampFade(previous, equalPowerCurve('out', crossfadeCurveSteps), now, seconds)
        const timer = this.environment.setTimeout(
            () => this.finishCrossfade(),
            (seconds + crossfadeUnloadDelay) * 1000,
        )
        this.crossfade = { deck: previous, timer }
    }

    /**
     * Ends a crossfade at once: the old track stops and is unloaded, the current one plays at full
     * level. A track prepared meanwhile is loaded on the freed deck.
     */
    private finishCrossfade(): void {
        const crossfade = this.crossfade
        if (!crossfade) {
            return
        }

        this.crossfade = null
        this.environment.clearTimeout(crossfade.timer)
        if (crossfade.deck !== this.deck) {
            crossfade.deck.audio.pause()
            this.unload(crossfade.deck)
            this.setFade(crossfade.deck, 1)
        }

        this.setFade(this.deck, 1)
        this.restoreDeferredPrepare()
    }

    private rampFade(deck: Deck, curve: Float32Array, start: number, seconds: number): void {
        const fade = deck.fade?.gain
        if (!fade) {
            return
        }

        fade.cancelScheduledValues(start)
        fade.setValueCurveAtTime(curve, start, seconds)
    }

    private setFade(deck: Deck, value: number): void {
        const fade = deck.fade?.gain
        if (!fade) {
            return
        }

        const now = this.context?.currentTime ?? 0
        fade.cancelScheduledValues(now)
        fade.setValueAtTime(value, now)
    }

    /** Sets the source of a deck: a file directly, an HLS stream through the HLS support. */
    private attach(deck: Deck, source: DeckSource): void {
        deck.target = null
        deck.endReached = false
        deck.slot.set(source.url, source.hls === true, (error) => {
            if (this.decks[this.current] === deck) {
                this.events.error?.(error)
            }
        })
    }

    private unload(deck: Deck): void {
        deck.source = null
        deck.target = null
        deck.endReached = false
        deck.slot.clear()
    }
}

/** Whether the audio graph runs: not suspended, closed or interrupted (WebKit). */
function outputRunning(context: AudioContext): boolean {
    return (context.state as string) === 'running'
}

function sameSource(source: DeckSource, other: DeckSource | null): boolean {
    return (
        !!other &&
        source.url === other.url &&
        source.gain === other.gain &&
        (source.crossfade ?? 0) === (other.crossfade ?? 0) &&
        source.start === other.start &&
        source.end === other.end
    )
}
