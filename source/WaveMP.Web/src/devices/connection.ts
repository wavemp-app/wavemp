// The real-time connection of this device to the device hub (asyncapi.yaml). It wraps the official
// SignalR client: the credentials are a short-lived ticket fetched for every connection attempt (a
// session token never goes into a URL), lost connections reconnect on their own, and the page
// reconnects at once when the network comes back or the page returns to the front (mobile systems
// close connections of pages in the background). After every connect the store registers the
// device again. Tests replace the SignalR client and the timers.

import {
    HubConnectionBuilder,
    HubConnectionState,
    LogLevel,
    type HubConnection,
} from '@microsoft/signalr'
import {
    deviceHubPath,
    hubEvents,
    type DeviceList,
    type LivePlayback,
    type ReceivedCommand,
} from './protocol'
import { decodeDeviceList, decodeReceivedCommand } from './wire'

/** State of the connection as the store shows it. */
export type ConnectionStatus = 'offline' | 'connecting' | 'connected'

/** What the connection needs of a SignalR connection; tests replace it. */
export type HubConnectionLike = Pick<
    HubConnection,
    'on' | 'onclose' | 'onreconnecting' | 'onreconnected' | 'start' | 'stop' | 'invoke' | 'state'
>

/** Delays of the automatic reconnect of a lost connection, in milliseconds. */
export const reconnectDelays: readonly number[] = [0, 2000, 5000, 10000, 30000]
/** After the automatic reconnect gave up or the first connect failed: the next attempt. */
export const retryDelayMilliseconds = 30_000

export interface ConnectionTimers {
    setTimeout(handler: () => void, milliseconds: number): number
    clearTimeout(id: number): void
}

/** Parts of the browser the connection uses; tests replace them. */
export interface ConnectionDependencies {
    /** A new SignalR connection that asks for its credentials before every attempt. */
    createConnection(accessToken: () => Promise<string>): HubConnectionLike
    /** A new ticket of the current session (POST /api/v1/me/device-hub-tickets). */
    ticket(): Promise<string>
    timers: ConnectionTimers
    /**
     * Calls wake when the network comes back or the page returns to the front; returns the
     * function that stops listening.
     */
    listen(wake: () => void): () => void
}

/** What the connection reports to the store. */
export interface ConnectionEvents {
    status(status: ConnectionStatus): void
    /** Connected or reconnected: the device registers (again). */
    connected(): void
    /** The page came back while the connection stayed open: the device registers again to sync. */
    resumed(): void
    devicesChanged(list: DeviceList): void
    playbackChanged(playback: LivePlayback): void
    commandReceived(command: ReceivedCommand): void
    deviceReplaced(): void
}

/** The SignalR client of the browser with the delays above and without logs (they name URLs). */
export function createBrowserConnection(accessToken: () => Promise<string>): HubConnectionLike {
    return new HubConnectionBuilder()
        .withUrl(deviceHubPath, { accessTokenFactory: accessToken })
        .withAutomaticReconnect([...reconnectDelays])
        .configureLogging(LogLevel.None)
        .build()
}

/** Wakes the connection on `online`, `pageshow` and when the page becomes visible. */
export function listenToBrowser(wake: () => void): () => void {
    if (typeof window === 'undefined' || typeof document === 'undefined') {
        return () => undefined
    }

    const onVisibility = (): void => {
        if (document.visibilityState === 'visible') {
            wake()
        }
    }
    window.addEventListener('online', wake)
    window.addEventListener('pageshow', wake)
    document.addEventListener('visibilitychange', onVisibility)
    return () => {
        window.removeEventListener('online', wake)
        window.removeEventListener('pageshow', wake)
        document.removeEventListener('visibilitychange', onVisibility)
    }
}

/**
 * Keeps this device connected to the hub between start and stop. Messages of the server reach the
 * events decoded; every connect (also a reconnect) reports connected, so the store registers.
 */
export class DeviceConnection {
    private connection: HubConnectionLike | null = null
    private wanted = false
    private connecting = false
    private retry: number | null = null
    private stopListening: (() => void) | null = null
    private current: ConnectionStatus = 'offline'

    constructor(
        private readonly dependencies: ConnectionDependencies,
        private readonly events: ConnectionEvents,
    ) {}

    get status(): ConnectionStatus {
        return this.current
    }

    /** Connects and stays connected until stop. */
    start(): void {
        if (this.wanted) {
            return
        }

        this.wanted = true
        this.stopListening = this.dependencies.listen(() => this.wake())
        void this.connect()
    }

    /** Closes the connection; nothing reconnects. */
    async stop(): Promise<void> {
        this.wanted = false
        this.cancelRetry()
        this.stopListening?.()
        this.stopListening = null
        const connection = this.connection
        this.connection = null
        this.setStatus('offline')
        await connection?.stop().catch(() => undefined)
    }

    /**
     * The network came back or the page returned to the front: a closed connection connects at
     * once, an open one reports resumed, so the device syncs what it may have missed.
     */
    wake(): void {
        if (!this.wanted) {
            return
        }

        if (this.connection?.state === HubConnectionState.Connected) {
            this.events.resumed()
            return
        }

        if (!this.connection || this.connection.state === HubConnectionState.Disconnected) {
            this.cancelRetry()
            void this.connect()
        }
    }

    /** Calls a method of the hub; fails while not connected. */
    invoke<T>(method: string, ...args: unknown[]): Promise<T> {
        const connection = this.connection
        if (!connection || connection.state !== HubConnectionState.Connected) {
            return Promise.reject(new Error('The device hub is not connected.'))
        }

        return connection.invoke<T>(method, ...args)
    }

    private async connect(): Promise<void> {
        if (!this.wanted || this.connecting) {
            return
        }

        const connection = (this.connection ??= this.create())
        if (connection.state !== HubConnectionState.Disconnected) {
            return
        }

        this.connecting = true
        this.setStatus('connecting')
        try {
            await connection.start()
        } catch {
            this.connecting = false
            if (this.wanted && this.connection === connection) {
                this.setStatus('offline')
                this.scheduleRetry()
            } else {
                // Stopped and started again (another profile) while this attempt ran.
                void this.connect()
            }

            return
        }

        this.connecting = false
        if (!this.wanted || this.connection !== connection) {
            await connection.stop().catch(() => undefined)
            // Stopped and started again (another profile) while this attempt ran: the new
            // connection waited for it.
            void this.connect()
            return
        }

        this.setStatus('connected')
        this.events.connected()
    }

    private create(): HubConnectionLike {
        const connection = this.dependencies.createConnection(() => this.dependencies.ticket())
        const own = (): boolean => this.connection === connection
        connection.on(hubEvents.devicesChanged, (list: DeviceList) => {
            if (own()) {
                this.events.devicesChanged(decodeDeviceList(list))
            }
        })
        connection.on(hubEvents.playbackChanged, (playback: LivePlayback) => {
            if (own()) {
                this.events.playbackChanged(playback)
            }
        })
        connection.on(hubEvents.commandReceived, (command: ReceivedCommand) => {
            if (own()) {
                this.events.commandReceived(decodeReceivedCommand(command))
            }
        })
        connection.on(hubEvents.deviceReplaced, () => {
            if (own()) {
                this.events.deviceReplaced()
            }
        })
        connection.onreconnecting(() => {
            if (own()) {
                this.setStatus('connecting')
            }
        })
        connection.onreconnected(() => {
            if (own()) {
                this.setStatus('connected')
                this.events.connected()
            }
        })
        connection.onclose(() => {
            if (own() && !this.connecting) {
                this.setStatus('offline')
                if (this.wanted) {
                    this.scheduleRetry()
                }
            }
        })
        return connection
    }

    private scheduleRetry(): void {
        this.cancelRetry()
        this.retry = this.dependencies.timers.setTimeout(() => {
            this.retry = null
            void this.connect()
        }, retryDelayMilliseconds)
    }

    private cancelRetry(): void {
        if (this.retry !== null) {
            this.dependencies.timers.clearTimeout(this.retry)
            this.retry = null
        }
    }

    private setStatus(status: ConnectionStatus): void {
        if (this.current !== status) {
            this.current = status
            this.events.status(status)
        }
    }
}
