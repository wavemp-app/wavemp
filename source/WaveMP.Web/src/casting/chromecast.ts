// Chromecast through the Google Cast Web Sender SDK (CAF), with the Default Media Receiver of Google:
// the web app needs no receiver of its own. The SDK is loaded from Google at runtime (sdk.ts) and is
// only reached through the small part of its API typed here, so tests replace it with a simulated
// device. The SDK only works in a secure context; without HTTPS the backend reports "insecure".

import { chromecastFormats } from './formats'
import {
    noCastStatus,
    type CastBackend,
    type CastFormats,
    type CastMedia,
    type CastPlaybackEvents,
    type CastStatus,
} from './types'

/** The session of the SDK with a device. */
export interface CastSessionApi {
    getCastDevice(): { readonly friendlyName: string }
    /** Resolves when the device loaded the media; rejects with an error code of the SDK. */
    loadMedia(request: unknown): Promise<unknown>
    /** The media that plays on the device. */
    getMediaSession(): CastMediaSessionApi | null
}

/** The media that plays on the device (chrome.cast.media.Media). */
export interface CastMediaSessionApi {
    readonly playerState: string
    readonly idleReason: string | null
    /** Sets the level of the stream, below the volume of the device. */
    setVolume(request: unknown, success: () => void, error: (error: unknown) => void): void
}

export interface CastContextApi {
    setOptions(options: { receiverApplicationId: string; autoJoinPolicy: string }): void
    getCastState(): string
    addEventListener(type: string, listener: () => void): void
    /** Opens the device selection of the browser; rejects when it is closed without a choice. */
    requestSession(): Promise<unknown>
    getCurrentSession(): CastSessionApi | null
    endCurrentSession(stopCasting: boolean): void
}

/** State of the media on the device, which the SDK keeps up to date. */
export interface RemotePlayerApi {
    currentTime: number
    duration: number
    isPaused: boolean
    playerState: string | null
    volumeLevel: number
    isMuted: boolean
    isMediaLoaded: boolean
}

/** Controls of the media; they apply the values set on the remote player. */
export interface RemotePlayerControllerApi {
    addEventListener(type: string, listener: (event: { field: string }) => void): void
    playOrPause(): void
    stop(): void
    seek(): void
    setVolumeLevel(): void
    muteOrUnmute(): void
}

export interface MediaInfoApi {
    contentUrl?: string
    streamType?: string
    duration?: number | null
    metadata?: unknown
    hlsSegmentFormat?: string
    hlsVideoSegmentFormat?: string
}

export interface MediaMetadataApi {
    title?: string
    artist?: string
    albumName?: string
    subtitle?: string
    images?: unknown[]
}

export interface LoadRequestApi {
    autoplay?: boolean | null
    currentTime?: number | null
}

/** The namespace cast.framework of the SDK. */
export interface CastFrameworkApi {
    readonly CastContext: { getInstance(): CastContextApi }
    readonly CastContextEventType: { readonly CAST_STATE_CHANGED: string }
    readonly CastState: {
        readonly NO_DEVICES_AVAILABLE: string
        readonly NOT_CONNECTED: string
        readonly CONNECTING: string
        readonly CONNECTED: string
    }
    readonly RemotePlayer: new () => RemotePlayerApi
    readonly RemotePlayerController: new (player: RemotePlayerApi) => RemotePlayerControllerApi
    readonly RemotePlayerEventType: { readonly ANY_CHANGE: string }
}

/** The namespace chrome.cast of the SDK. */
export interface ChromeCastApi {
    readonly AutoJoinPolicy: { readonly ORIGIN_SCOPED: string }
    readonly Image: new (url: string) => unknown
    /** A level between 0 and 1 and whether it is muted; null leaves a value as it is. */
    readonly Volume: new (level: number | null, muted: boolean | null) => unknown
    readonly media: {
        readonly DEFAULT_MEDIA_RECEIVER_APP_ID: string
        readonly MediaInfo: new (contentId: string, contentType: string) => MediaInfoApi
        readonly MusicTrackMediaMetadata: new () => MediaMetadataApi
        readonly GenericMediaMetadata: new () => MediaMetadataApi
        readonly LoadRequest: new (mediaInfo: MediaInfoApi) => LoadRequestApi
        /** A request to set the level of the stream (Media.setVolume). */
        readonly VolumeRequest: new (volume: unknown) => unknown
        readonly StreamType: { readonly BUFFERED: string }
        readonly HlsSegmentFormat: { readonly TS: string; readonly TS_AAC: string }
        readonly HlsVideoSegmentFormat: { readonly MPEG2_TS: string }
        readonly PlayerState: {
            readonly IDLE: string
            readonly PLAYING: string
            readonly PAUSED: string
            readonly BUFFERING: string
        }
        readonly IdleReason: { readonly FINISHED: string; readonly ERROR: string }
    }
}

/** The part of the Google Cast SDK the web app uses. */
export interface CastSdk {
    readonly framework: CastFrameworkApi
    readonly chromeCast: ChromeCastApi
}

export interface ChromecastOptions {
    /** Whether the page runs in a secure context; the SDK needs HTTPS. */
    readonly secure: boolean
    /** Loads the SDK; null if the browser cannot cast with it. */
    readonly loadSdk: () => Promise<CastSdk | null>
}

interface Connected {
    readonly sdk: CastSdk
    readonly context: CastContextApi
    readonly player: RemotePlayerApi
    readonly controller: RemotePlayerControllerApi
}

/** Error of the SDK, e.g. when a device cannot load a stream. */
export class CastError extends Error {
    constructor(readonly code: string) {
        super(`Casting failed: ${code}`)
        this.name = 'CastError'
    }
}

export class ChromecastBackend implements CastBackend {
    readonly technology = 'chromecast' as const
    private current: CastStatus
    private readonly listeners: (() => void)[] = []
    private events: CastPlaybackEvents = {}
    private api: Connected | null = null
    /** Whether the media of the web app is loaded on the device. */
    private mediaLoaded = false
    /** Whether a load request runs; the device reports the end of the previous media meanwhile. */
    private loading = false

    constructor(private readonly options: ChromecastOptions) {
        // Without HTTPS the button is greyed out; with it, it shows once the SDK is ready.
        this.current = {
            ...noCastStatus,
            technology: 'chromecast',
            availability: options.secure ? 'unsupported' : 'insecure',
        }
    }

    get status(): CastStatus {
        return this.current
    }

    get position(): number {
        return this.api?.player.currentTime ?? 0
    }

    onChange(listener: () => void): void {
        this.listeners.push(listener)
    }

    setPlaybackEvents(events: CastPlaybackEvents): void {
        this.events = events
    }

    async start(): Promise<void> {
        if (!this.options.secure || this.api) {
            return
        }

        const sdk = await this.options.loadSdk().catch(() => null)
        if (!sdk) {
            return
        }

        const context = sdk.framework.CastContext.getInstance()
        context.setOptions({
            receiverApplicationId: sdk.chromeCast.media.DEFAULT_MEDIA_RECEIVER_APP_ID,
            autoJoinPolicy: sdk.chromeCast.AutoJoinPolicy.ORIGIN_SCOPED,
        })
        const player = new sdk.framework.RemotePlayer()
        const controller = new sdk.framework.RemotePlayerController(player)
        this.api = { sdk, context, player, controller }
        context.addEventListener(sdk.framework.CastContextEventType.CAST_STATE_CHANGED, () =>
            this.update(),
        )
        controller.addEventListener(sdk.framework.RemotePlayerEventType.ANY_CHANGE, (event) =>
            this.onPlayerChange(event.field),
        )
        this.update()
    }

    async requestSession(): Promise<void> {
        // Closing the dialog without a choice is no error.
        await this.api?.context.requestSession().catch(() => undefined)
    }

    endSession(): void {
        this.api?.context.endCurrentSession(true)
    }

    formats(): CastFormats {
        return chromecastFormats
    }

    async load(media: CastMedia, position: number, autoplay: boolean): Promise<void> {
        const api = this.api
        const session = api?.context.getCurrentSession()
        if (!api || !session) {
            throw new CastError('session_missing')
        }

        const request = new api.sdk.chromeCast.media.LoadRequest(this.mediaInfo(api.sdk, media))
        request.autoplay = autoplay
        request.currentTime = position > 0 ? position : 0
        this.loading = true
        this.mediaLoaded = false
        try {
            await session.loadMedia(request)
        } catch (error) {
            throw error instanceof Error ? error : new CastError(String(error))
        } finally {
            this.loading = false
        }

        this.setStreamVolume(api.sdk, session, media.volume ?? 1)
        // The device may report its state before the load resolves, while it was still ignored.
        this.mediaLoaded = true
        const states = api.sdk.chromeCast.media.PlayerState
        if (api.player.playerState === states.PLAYING) {
            this.events.playing?.()
        } else if (api.player.playerState === states.PAUSED) {
            this.events.paused?.()
        }
    }

    async play(): Promise<void> {
        const api = this.api
        if (api && this.mediaLoaded && api.player.isPaused) {
            api.controller.playOrPause()
        }
    }

    pause(): void {
        const api = this.api
        if (api && this.mediaLoaded && !api.player.isPaused) {
            api.controller.playOrPause()
        }
    }

    stop(): void {
        const api = this.api
        if (api && this.mediaLoaded) {
            this.mediaLoaded = false
            api.controller.stop()
        }
    }

    seek(position: number): void {
        const api = this.api
        if (api && this.mediaLoaded) {
            api.player.currentTime = Math.max(0, position)
            api.controller.seek()
        }
    }

    setVolume(volume: number): void {
        const api = this.api
        if (api) {
            api.player.volumeLevel = Math.min(1, Math.max(0, volume))
            api.controller.setVolumeLevel()
        }
    }

    setMuted(muted: boolean): void {
        const api = this.api
        if (api && api.player.isMuted !== muted) {
            api.controller.muteOrUnmute()
        }
    }

    /**
     * Sets the level of the stream that just loaded (the loudness match of the sound of a video):
     * the stream volume of the media, which the volume of the device keeps on top. Every load sets
     * it, so the next stream does not keep the level of a video. A device that refuses it plays the
     * stream at the volume of the device.
     */
    private setStreamVolume(sdk: CastSdk, session: CastSessionApi, level: number): void {
        const mediaSession = session.getMediaSession()
        if (!mediaSession) {
            return
        }

        const { chromeCast } = sdk
        const volume = new chromeCast.Volume(Math.min(1, Math.max(0, level)), null)
        mediaSession.setVolume(
            new chromeCast.media.VolumeRequest(volume),
            () => undefined,
            () => undefined,
        )
    }

    /** The media for the Default Media Receiver: the stream, its HLS segments and the metadata. */
    private mediaInfo(sdk: CastSdk, media: CastMedia): MediaInfoApi {
        const { media: castMedia } = sdk.chromeCast
        const info = new castMedia.MediaInfo(media.url, media.contentType)
        info.contentUrl = media.url
        info.streamType = castMedia.StreamType.BUFFERED
        info.duration = media.duration > 0 ? media.duration : null
        if (media.hls) {
            // The server transcodes to MPEG-TS: AAC alone for the sound, H.264 with AAC for videos.
            if (media.kind === 'video') {
                info.hlsSegmentFormat = castMedia.HlsSegmentFormat.TS
                info.hlsVideoSegmentFormat = castMedia.HlsVideoSegmentFormat.MPEG2_TS
            } else {
                info.hlsSegmentFormat = castMedia.HlsSegmentFormat.TS_AAC
            }
        }

        const metadata =
            media.kind === 'video'
                ? new castMedia.GenericMediaMetadata()
                : new castMedia.MusicTrackMediaMetadata()
        metadata.title = media.title
        if (media.kind === 'video') {
            metadata.subtitle = media.artist
        } else {
            metadata.artist = media.artist
            metadata.albumName = media.album
        }

        metadata.images = media.artworkUrl ? [new sdk.chromeCast.Image(media.artworkUrl)] : []
        info.metadata = metadata
        return info
    }

    private update(): void {
        const api = this.api
        if (!api) {
            return
        }

        const states = api.sdk.framework.CastState
        const state = api.context.getCastState()
        const connection =
            state === states.CONNECTED
                ? 'connected'
                : state === states.CONNECTING
                  ? 'connecting'
                  : 'disconnected'
        if (connection !== 'connected') {
            this.mediaLoaded = false
        }

        const device =
            connection === 'disconnected'
                ? null
                : (api.context.getCurrentSession()?.getCastDevice().friendlyName ?? null)
        this.setStatus({
            technology: 'chromecast',
            availability: 'available',
            devicesFound: state !== states.NO_DEVICES_AVAILABLE,
            connection,
            deviceName: device,
            volume: api.player.volumeLevel,
            muted: api.player.isMuted,
        })
    }

    private onPlayerChange(field: string): void {
        const api = this.api
        if (!api) {
            return
        }

        switch (field) {
            case 'currentTime':
            case 'duration':
                if (this.mediaLoaded) {
                    this.events.timeupdate?.(
                        finite(api.player.currentTime),
                        finite(api.player.duration),
                    )
                }
                break
            case 'playerState':
            case 'isPaused':
                this.onPlayerState(api)
                break
            case 'isMediaLoaded':
                if (!api.player.isMediaLoaded && this.mediaLoaded && !this.loading) {
                    this.onIdle(api)
                }
                break
            case 'volumeLevel':
            case 'isMuted':
            case 'isConnected':
                this.update()
                break
            default:
                break
        }
    }

    private onPlayerState(api: Connected): void {
        if (!this.mediaLoaded || this.loading) {
            return
        }

        const states = api.sdk.chromeCast.media.PlayerState
        switch (api.player.playerState) {
            case states.PLAYING:
                this.events.playing?.()
                break
            case states.PAUSED:
                this.events.paused?.()
                break
            case states.BUFFERING:
                this.events.waiting?.()
                break
            case states.IDLE:
                this.onIdle(api)
                break
            default:
                break
        }
    }

    /**
     * The device stopped: the media ended or failed, or someone stopped it on the device. Without a
     * reason, a position at the end counts as the end.
     */
    private onIdle(api: Connected): void {
        const reasons = api.sdk.chromeCast.media.IdleReason
        const reason = api.context.getCurrentSession()?.getMediaSession()?.idleReason ?? null
        const duration = finite(api.player.duration)
        const atEnd = duration > 0 && api.player.currentTime >= duration - endTolerance
        this.mediaLoaded = false
        if (reason === reasons.FINISHED || (reason === null && atEnd)) {
            this.events.ended?.()
        } else if (reason === reasons.ERROR) {
            this.events.error?.(new CastError('media_error'))
        } else {
            this.events.paused?.()
        }
    }

    private setStatus(status: CastStatus): void {
        const previous = this.current
        this.current = status
        if (
            (Object.keys(status) as (keyof CastStatus)[]).some(
                (key) => status[key] !== previous[key],
            )
        ) {
            this.listeners.forEach((listener) => listener())
        }
    }
}

/** Seconds before the end at which a stop without a reason counts as the end of the media. */
export const endTolerance = 2

function finite(value: number): number {
    return Number.isFinite(value) && value > 0 ? value : 0
}
