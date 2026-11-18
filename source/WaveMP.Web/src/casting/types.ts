// Types of the casting layer. The web app casts with one technique per browser: Chromecast through
// the Google Cast Web Sender SDK (Chrome, Edge) or AirPlay through the playback target of WebKit
// (Safari). The rest of the app only knows the layer (layer.ts) and these types.

import type { AudioFormat } from '@/api/generated/models'
import type { VideoFormats } from '@/player/videoFormats'

/** The technique of the browser. */
export type CastTechnology = 'chromecast' | 'airplay'

/**
 * Whether the browser can cast: not at all (the cast button is hidden), only over HTTPS, which the
 * page does not have (Chromecast; the button is greyed out with a hint), or yes.
 */
export type CastAvailability = 'unsupported' | 'insecure' | 'available'

/** The connection to a device: none, being set up or established. */
export type CastConnection = 'disconnected' | 'connecting' | 'connected'

/** What the layer reports about casting. */
export interface CastStatus {
    readonly technology: CastTechnology | null
    readonly availability: CastAvailability
    /** Whether the browser found devices; unknown counts as found (AirPlay never tells). */
    readonly devicesFound: boolean
    readonly connection: CastConnection
    /** Name of the device; null while unknown (AirPlay never tells it). */
    readonly deviceName: string | null
    /** Volume of the device between 0 and 1, and whether it is muted. */
    readonly volume: number
    readonly muted: boolean
}

/** What a device plays: one stream with the metadata it shows. */
export interface CastMedia {
    /** Absolute URL of the stream, which the device loads itself. */
    readonly url: string
    readonly contentType: string
    /** The video of the track or only its sound. */
    readonly kind: 'audio' | 'video'
    /** Whether the URL is an HLS master playlist (MPEG-TS segments). */
    readonly hls: boolean
    readonly title?: string
    readonly artist?: string
    readonly album?: string
    /** Absolute URL of the cover. */
    readonly artworkUrl?: string
    /** Duration in seconds; 0 while unknown. */
    readonly duration: number
}

/** What the device reports while it plays. */
export interface CastPlaybackEvents {
    /** Position and duration in seconds; the duration is 0 while unknown. */
    timeupdate?(position: number, duration: number): void
    playing?(): void
    paused?(): void
    /** The device waits for data. */
    waiting?(): void
    /** The media played to its end. */
    ended?(): void
    /** The media failed to load or play. */
    error?(error: unknown): void
}

/** The formats a device plays; the server transcodes everything else. */
export interface CastFormats {
    readonly audio: readonly AudioFormat[]
    readonly video: VideoFormats
}

/** One technique of casting; the layer uses one per browser. */
export interface CastBackend {
    readonly technology: CastTechnology
    readonly status: CastStatus
    /** Calls the listener whenever the status changes. */
    onChange(listener: () => void): void
    /** The events of the media that plays on the device. */
    setPlaybackEvents(events: CastPlaybackEvents): void
    /** Prepares the technique, e.g. loads the SDK; the status tells the result. */
    start(): Promise<void>
    /**
     * Opens the device selection of the browser or the operating system. Must run within a user
     * gesture. AirPlay needs a source on its element for that; preview is the stream of the
     * current track, if any.
     */
    requestSession(preview?: string | null): Promise<void>
    /** Ends the connection to the device. */
    endSession(): void
    /** The formats of the connected device. */
    formats(): CastFormats
    /** Loads media on the device at a position (seconds) and starts it or keeps it paused. */
    load(media: CastMedia, position: number, autoplay: boolean): Promise<void>
    play(): Promise<void>
    pause(): void
    /** Stops the media on the device; the connection stays. */
    stop(): void
    seek(position: number): void
    /** Position of the media on the device in seconds. */
    readonly position: number
    setVolume(volume: number): void
    setMuted(muted: boolean): void
}

/** The status of a browser that cannot cast. */
export const noCastStatus: CastStatus = {
    technology: null,
    availability: 'unsupported',
    devicesFound: false,
    connection: 'disconnected',
    deviceName: null,
    volume: 1,
    muted: false,
}
