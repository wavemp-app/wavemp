// AirPlay through the playback target of WebKit (Safari on Mac, iPhone and iPad). The device plays a
// media element of its own, which stays outside the audio graph of the player: WebKit routes the
// element to the device as it is, so the equalizer, crossfade and ReplayGain do not apply. The device
// selection is the AirPlay menu of the operating system; WebKit does not tell the name of the device.
// The volume of the element is the volume of the player; the level of a stream (the loudness match of
// the sound of a video) lowers it on top, and the status reports the volume without it. Tests replace
// the element with a simulated device.

import {
    noCastStatus,
    type CastBackend,
    type CastFormats,
    type CastMedia,
    type CastPlaybackEvents,
    type CastStatus,
} from './types'

/** The part of a video element of WebKit the backend uses. */
export interface AirPlayElement {
    src: string
    currentTime: number
    readonly duration: number
    readonly paused: boolean
    volume: number
    muted: boolean
    preload: string
    /** Whether the element plays on an AirPlay device. */
    readonly webkitCurrentPlaybackTargetIsWireless: boolean
    play(): Promise<void>
    pause(): void
    setAttribute(name: string, value: string): void
    /** Opens the AirPlay menu; must run within a user gesture. */
    webkitShowPlaybackTargetPicker(): void
    addEventListener(
        type: string,
        listener: (event: { readonly availability?: string }) => void,
        options?: { once?: boolean },
    ): void
}

export interface AirPlayOptions {
    /** Creates the element that plays on the device. */
    readonly createElement: () => AirPlayElement
    /** What the device plays: the formats of Safari, which hands the stream on. */
    readonly formats: () => CastFormats
}

export class AirPlayBackend implements CastBackend {
    readonly technology = 'airplay' as const
    private current: CastStatus = {
        ...noCastStatus,
        technology: 'airplay',
        availability: 'available',
        // Until WebKit tells otherwise; the menu of the operating system shows when there is none.
        devicesFound: true,
    }
    private readonly listeners: (() => void)[] = []
    private events: CastPlaybackEvents = {}
    private element: AirPlayElement | null = null
    /** Whether the media of the web app is loaded (not only the preview for the menu). */
    private mediaLoaded = false
    /** The level of the loaded stream (CastMedia.volume), applied on top of the volume. */
    private level = 1

    constructor(private readonly options: AirPlayOptions) {}

    get status(): CastStatus {
        return this.current
    }

    get position(): number {
        return finite(this.element?.currentTime ?? 0)
    }

    onChange(listener: () => void): void {
        this.listeners.push(listener)
    }

    setPlaybackEvents(events: CastPlaybackEvents): void {
        this.events = events
    }

    async start(): Promise<void> {
        if (this.element) {
            return
        }

        const element = this.options.createElement()
        element.setAttribute('x-webkit-airplay', 'allow')
        element.preload = 'none'
        this.element = element
        element.addEventListener('webkitplaybacktargetavailabilitychanged', (event) =>
            this.setStatus({ devicesFound: event.availability !== 'not-available' }),
        )
        element.addEventListener('webkitcurrentplaybacktargetiswirelesschanged', () =>
            this.onTargetChanged(element),
        )
        element.addEventListener('volumechange', () =>
            this.setStatus({ volume: this.volumeOf(element), muted: element.muted }),
        )
        const time = (): void => {
            if (this.active) {
                this.events.timeupdate?.(finite(element.currentTime), finite(element.duration))
            }
        }
        element.addEventListener('timeupdate', time)
        element.addEventListener('durationchange', time)
        element.addEventListener('playing', () => {
            if (this.active) {
                this.events.playing?.()
            }
        })
        element.addEventListener('pause', () => {
            if (this.active) {
                this.events.paused?.()
            }
        })
        element.addEventListener('waiting', () => {
            if (this.active) {
                this.events.waiting?.()
            }
        })
        element.addEventListener('ended', () => {
            if (this.active) {
                this.mediaLoaded = false
                this.events.ended?.()
            }
        })
        element.addEventListener('error', () => {
            if (this.active) {
                this.mediaLoaded = false
                this.events.error?.(new Error('AirPlay playback failed'))
            }
        })
    }

    async requestSession(preview?: string | null): Promise<void> {
        const element = this.element
        if (!element) {
            return
        }

        // The menu needs a source; the stream of the current track serves until the device plays.
        if (!element.src && preview) {
            element.src = preview
        }

        element.webkitShowPlaybackTargetPicker()
    }

    endSession(): void {
        // WebKit cannot end AirPlay by itself; its menu offers to play on this device again.
        this.element?.webkitShowPlaybackTargetPicker()
    }

    formats(): CastFormats {
        return this.options.formats()
    }

    async load(media: CastMedia, position: number, autoplay: boolean): Promise<void> {
        const element = this.element
        if (!element || this.current.connection !== 'connected') {
            throw new Error('No AirPlay device')
        }

        this.mediaLoaded = true
        const volume = this.current.volume
        this.level = Math.min(1, Math.max(minimumLevel, media.volume ?? 1))
        element.volume = volume * this.level
        element.src = media.url
        if (position > 0) {
            element.addEventListener(
                'loadedmetadata',
                () => {
                    element.currentTime = position
                },
                { once: true },
            )
        }

        if (autoplay) {
            await element.play()
        }
    }

    async play(): Promise<void> {
        if (this.active) {
            await this.element?.play()
        }
    }

    pause(): void {
        if (this.active) {
            this.element?.pause()
        }
    }

    stop(): void {
        // The source stays, so the element keeps its route to the device.
        if (this.active) {
            this.mediaLoaded = false
            this.element?.pause()
        }
    }

    seek(position: number): void {
        if (this.active && this.element) {
            this.element.currentTime = Math.max(0, position)
        }
    }

    setVolume(volume: number): void {
        if (this.element) {
            this.element.volume = Math.min(1, Math.max(0, volume)) * this.level
        }
    }

    setMuted(muted: boolean): void {
        if (this.element) {
            this.element.muted = muted
        }
    }

    /** Whether the media of the web app plays on a device. */
    private get active(): boolean {
        return this.mediaLoaded && this.current.connection === 'connected'
    }

    private onTargetChanged(element: AirPlayElement): void {
        const wireless = element.webkitCurrentPlaybackTargetIsWireless
        if (!wireless) {
            // Back on this device: the element stops, the player goes on here.
            this.mediaLoaded = false
            element.pause()
        }

        this.setStatus({
            connection: wireless ? 'connected' : 'disconnected',
            volume: this.volumeOf(element),
            muted: element.muted,
        })
    }

    /** The volume of the player: that of the element without the level of the stream. */
    private volumeOf(element: AirPlayElement): number {
        return Math.min(1, element.volume / this.level)
    }

    private setStatus(change: Partial<CastStatus>): void {
        const next: CastStatus = { ...this.current, ...change }
        const changed = (Object.keys(change) as (keyof CastStatus)[]).some(
            (key) => next[key] !== this.current[key],
        )
        this.current = next
        if (changed) {
            this.listeners.forEach((listener) => listener())
        }
    }
}

/**
 * The lowest level of a stream, so the volume of the player can always be read back from the
 * element; a loudness match lowers a stream by at most 12 dB (about a quarter).
 */
const minimumLevel = 0.01

function finite(value: number): number {
    return Number.isFinite(value) && value > 0 ? value : 0
}

/** A hidden video element in the page, which WebKit routes to the AirPlay device. */
export function browserAirPlayElement(): AirPlayElement {
    const element = document.createElement('video')
    element.playsInline = true
    element.hidden = true
    document.body.append(element)
    return element as unknown as AirPlayElement
}
