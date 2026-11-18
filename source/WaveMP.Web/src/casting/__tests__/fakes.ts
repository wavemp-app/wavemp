// Simulated casting devices for the tests: a Chromecast behind a fake Google Cast SDK and an AirPlay
// device behind a fake video element of WebKit. They behave like the real ones as far as the
// casting layer uses them and let the tests act as the device (connect, play to the end, fail).

import type { AirPlayElement } from '../airplay'
import type {
    CastContextApi,
    CastSdk,
    CastSessionApi,
    LoadRequestApi,
    MediaInfoApi,
    MediaMetadataApi,
    RemotePlayerApi,
} from '../chromecast'
import type { CastBackend, CastFormats, CastMedia, CastPlaybackEvents, CastStatus } from '../types'

const castState = {
    NO_DEVICES_AVAILABLE: 'NO_DEVICES_AVAILABLE',
    NOT_CONNECTED: 'NOT_CONNECTED',
    CONNECTING: 'CONNECTING',
    CONNECTED: 'CONNECTED',
}

const playerState = { IDLE: 'IDLE', PLAYING: 'PLAYING', PAUSED: 'PAUSED', BUFFERING: 'BUFFERING' }

/** A media load the simulated Chromecast received. */
export interface ReceivedLoad {
    readonly info: MediaInfoApi & { readonly contentId: string; readonly contentType: string }
    readonly request: LoadRequestApi
}

/** A Chromecast with the Default Media Receiver, behind a fake Google Cast SDK. */
export class SimulatedChromecast {
    state = castState.NOT_CONNECTED
    deviceName = 'Living Room'
    /** What the dialog of the browser does when it opens. */
    dialogChoice: 'connect' | 'cancel' = 'connect'
    /** An error code the next load fails with. */
    failNextLoad: string | null = null
    idleReason: string | null = null
    readonly loads: ReceivedLoad[] = []
    readonly options: { receiverApplicationId: string; autoJoinPolicy: string }[] = []
    requestedSessions = 0
    endedSessions = 0
    controls: string[] = []
    readonly player: RemotePlayerApi = {
        currentTime: 0,
        duration: 0,
        isPaused: true,
        playerState: null,
        volumeLevel: 0.5,
        isMuted: false,
        isMediaLoaded: false,
    }
    private readonly contextListeners: (() => void)[] = []
    private readonly playerListeners: ((event: { field: string }) => void)[] = []

    /** The SDK as the page sees it. */
    sdk(): CastSdk {
        const device = this.deviceName
        const player = this.player
        const controller = this.controller()
        const session: CastSessionApi = {
            getCastDevice: () => ({ friendlyName: device }),
            loadMedia: (request) =>
                this.receive(request as LoadRequestApi & { media: ReceivedLoad['info'] }),
            getMediaSession: () =>
                this.player.isMediaLoaded || this.idleReason
                    ? {
                          playerState: this.player.playerState ?? 'IDLE',
                          idleReason: this.idleReason,
                      }
                    : null,
        }
        const context: CastContextApi = {
            setOptions: (options) => this.options.push(options),
            getCastState: () => this.state,
            addEventListener: (_type, listener) => this.contextListeners.push(listener),
            requestSession: async () => {
                this.requestedSessions++
                if (this.dialogChoice === 'cancel') {
                    throw 'cancel'
                }

                this.connect()
                return undefined
            },
            getCurrentSession: () => (this.state === castState.CONNECTED ? session : null),
            endCurrentSession: () => {
                this.endedSessions++
                this.disconnect()
            },
        }

        class MediaInfo implements MediaInfoApi {
            contentUrl?: string
            streamType?: string
            duration?: number | null
            metadata?: unknown
            hlsSegmentFormat?: string
            hlsVideoSegmentFormat?: string
            constructor(
                readonly contentId: string,
                readonly contentType: string,
            ) {}
        }
        class Metadata implements MediaMetadataApi {
            title?: string
            artist?: string
            albumName?: string
            subtitle?: string
            images?: unknown[]
            constructor(readonly type: string) {}
        }
        class LoadRequest implements LoadRequestApi {
            autoplay?: boolean | null = true
            currentTime?: number | null = null
            constructor(readonly media: MediaInfoApi) {}
        }
        class Image {
            constructor(readonly url: string) {}
        }

        return {
            framework: {
                CastContext: { getInstance: () => context },
                CastContextEventType: { CAST_STATE_CHANGED: 'caststatechanged' },
                CastState: castState,
                RemotePlayer: function RemotePlayer() {
                    return player
                } as unknown as new () => RemotePlayerApi,
                RemotePlayerController: function RemotePlayerController() {
                    return controller
                } as unknown as CastSdk['framework']['RemotePlayerController'],
                RemotePlayerEventType: { ANY_CHANGE: 'anyChanged' },
            },
            chromeCast: {
                AutoJoinPolicy: { ORIGIN_SCOPED: 'origin_scoped' },
                Image,
                media: {
                    DEFAULT_MEDIA_RECEIVER_APP_ID: 'CC1AD845',
                    MediaInfo,
                    MusicTrackMediaMetadata: class extends Metadata {
                        constructor() {
                            super('music')
                        }
                    },
                    GenericMediaMetadata: class extends Metadata {
                        constructor() {
                            super('generic')
                        }
                    },
                    LoadRequest,
                    StreamType: { BUFFERED: 'BUFFERED' },
                    HlsSegmentFormat: { TS: 'ts', TS_AAC: 'ts_aac' },
                    HlsVideoSegmentFormat: { MPEG2_TS: 'mpeg2_ts' },
                    PlayerState: playerState,
                    IdleReason: { FINISHED: 'FINISHED', ERROR: 'ERROR' },
                },
            },
        }
    }

    /** The browser finds devices or none. */
    setDevices(found: boolean): void {
        this.state = found ? castState.NOT_CONNECTED : castState.NO_DEVICES_AVAILABLE
        this.notifyContext()
    }

    connect(): void {
        this.state = castState.CONNECTING
        this.notifyContext()
        this.state = castState.CONNECTED
        this.notifyContext()
    }

    disconnect(): void {
        this.state = castState.NOT_CONNECTED
        this.player.isMediaLoaded = false
        this.player.playerState = null
        this.notifyContext()
    }

    /** The device plays to a position. */
    progress(position: number): void {
        this.player.currentTime = position
        this.change('currentTime')
    }

    /** The device reaches the end of the media. */
    finish(): void {
        this.player.currentTime = this.player.duration
        this.idleReason = 'FINISHED'
        this.setState(playerState.IDLE)
        this.player.isMediaLoaded = false
        this.change('isMediaLoaded')
    }

    /** The device cannot play the media. */
    fail(): void {
        this.idleReason = 'ERROR'
        this.setState(playerState.IDLE)
    }

    /** Someone changes the volume on the device. */
    changeVolume(level: number): void {
        this.player.volumeLevel = level
        this.change('volumeLevel')
    }

    setState(state: string): void {
        this.player.playerState = state
        this.player.isPaused = state === playerState.PAUSED
        this.change('playerState')
    }

    private async receive(
        request: LoadRequestApi & { media: ReceivedLoad['info'] },
    ): Promise<undefined> {
        await Promise.resolve()
        if (this.failNextLoad) {
            const code = this.failNextLoad
            this.failNextLoad = null
            throw code
        }

        this.loads.push({ info: request.media, request })
        this.idleReason = null
        this.player.isMediaLoaded = true
        this.player.duration = request.media.duration ?? 0
        this.player.currentTime = request.currentTime ?? 0
        this.setState(request.autoplay === false ? playerState.PAUSED : playerState.PLAYING)
        return undefined
    }

    private controller() {
        return {
            addEventListener: (_type: string, listener: (event: { field: string }) => void) =>
                this.playerListeners.push(listener),
            playOrPause: () => {
                this.controls.push('playOrPause')
                this.setState(this.player.isPaused ? playerState.PLAYING : playerState.PAUSED)
            },
            stop: () => {
                this.controls.push('stop')
                this.idleReason = 'CANCELLED'
                this.setState(playerState.IDLE)
                this.player.isMediaLoaded = false
            },
            seek: () => {
                this.controls.push(`seek ${this.player.currentTime}`)
                this.change('currentTime')
            },
            setVolumeLevel: () => {
                this.controls.push(`volume ${this.player.volumeLevel}`)
                this.change('volumeLevel')
            },
            muteOrUnmute: () => {
                this.controls.push('muteOrUnmute')
                this.player.isMuted = !this.player.isMuted
                this.change('isMuted')
            },
        }
    }

    private change(field: string): void {
        this.playerListeners.forEach((listener) => listener({ field }))
    }

    private notifyContext(): void {
        this.contextListeners.forEach((listener) => listener())
    }
}

/** An AirPlay device behind the video element of WebKit. */
export class SimulatedAirPlayElement implements AirPlayElement {
    src = ''
    currentTime = 0
    duration = NaN
    paused = true
    volume = 1
    muted = false
    preload = 'auto'
    webkitCurrentPlaybackTargetIsWireless = false
    pickerOpened = 0
    /** What the AirPlay menu does when it opens. */
    menuChoice: 'device' | 'cancel' = 'device'
    readonly attributes = new Map<string, string>()
    private readonly listeners = new Map<
        string,
        { listener: (event: { availability?: string }) => void; once: boolean }[]
    >()

    play(): Promise<void> {
        this.paused = false
        this.emit('playing')
        return Promise.resolve()
    }

    pause(): void {
        this.paused = true
        this.emit('pause')
    }

    setAttribute(name: string, value: string): void {
        this.attributes.set(name, value)
    }

    webkitShowPlaybackTargetPicker(): void {
        this.pickerOpened++
        if (this.menuChoice === 'device') {
            this.setWireless(!this.webkitCurrentPlaybackTargetIsWireless)
        }
    }

    addEventListener(
        type: string,
        listener: (event: { availability?: string }) => void,
        options?: { once?: boolean },
    ): void {
        const list = this.listeners.get(type) ?? []
        list.push({ listener, once: options?.once === true })
        this.listeners.set(type, list)
    }

    /** The route of the element changes to an AirPlay device or back to this device. */
    setWireless(wireless: boolean): void {
        this.webkitCurrentPlaybackTargetIsWireless = wireless
        this.emit('webkitcurrentplaybacktargetiswirelesschanged')
    }

    /** The media has loaded its metadata. */
    loadMetadata(duration: number): void {
        this.duration = duration
        this.emit('loadedmetadata')
        this.emit('durationchange')
    }

    emit(type: string, event: { availability?: string } = {}): void {
        const list = this.listeners.get(type) ?? []
        this.listeners.set(
            type,
            list.filter((entry) => !entry.once),
        )
        list.forEach((entry) => entry.listener(event))
    }
}

/** A casting layer that records what it is asked, for the player store and the engine. */
export class FakeCastLayer implements CastBackend {
    readonly technology = 'chromecast' as const
    status: CastStatus = {
        technology: 'chromecast',
        availability: 'available',
        devicesFound: true,
        connection: 'disconnected',
        deviceName: null,
        volume: 0.5,
        muted: false,
    }
    position = 0
    events: CastPlaybackEvents = {}
    readonly loads: { media: CastMedia; position: number; autoplay: boolean }[] = []
    readonly calls: string[] = []
    readonly formatsOfDevice: CastFormats = {
        audio: ['Mp3', 'Flac'],
        video: { containers: ['Mp4'], videoCodecs: ['H264'], audioCodecs: ['Aac'] },
    }
    failLoad = false
    private readonly listeners: (() => void)[] = []

    onChange(listener: () => void): void {
        this.listeners.push(listener)
    }

    setPlaybackEvents(events: CastPlaybackEvents): void {
        this.events = events
    }

    async start(): Promise<void> {}

    async requestSession(preview?: string | null): Promise<void> {
        this.calls.push(`request ${preview ?? ''}`.trim())
    }

    endSession(): void {
        this.calls.push('end')
    }

    formats(): CastFormats {
        return this.formatsOfDevice
    }

    async load(media: CastMedia, position: number, autoplay: boolean): Promise<void> {
        await Promise.resolve()
        if (this.failLoad) {
            throw new Error('load failed')
        }

        this.loads.push({ media, position, autoplay })
        if (autoplay) {
            this.events.playing?.()
        }
    }

    async play(): Promise<void> {
        this.calls.push('play')
        this.events.playing?.()
    }

    pause(): void {
        this.calls.push('pause')
        this.events.paused?.()
    }

    stop(): void {
        this.calls.push('stop')
    }

    seek(position: number): void {
        this.calls.push(`seek ${position}`)
        this.position = position
    }

    setVolume(volume: number): void {
        this.calls.push(`volume ${volume}`)
        this.update({ volume })
    }

    setMuted(muted: boolean): void {
        this.calls.push(`muted ${muted}`)
        this.update({ muted })
    }

    /** A device connects, with its name. */
    connect(deviceName = 'Living Room'): void {
        this.update({ connection: 'connected', deviceName })
    }

    disconnect(): void {
        this.update({ connection: 'disconnected', deviceName: null })
    }

    update(change: Partial<CastStatus>): void {
        this.status = { ...this.status, ...change }
        this.listeners.forEach((listener) => listener())
    }
}
