// Simulated hub connection, timers and messages for the tests of the devices.

import { HubConnectionState } from '@microsoft/signalr'
import type { Device, Track } from '@/api/generated/models'
import type { HubConnectionLike } from '../connection'
import type { DeviceQueue, LivePlayback, ReceivedCommand } from '../protocol'

type Handler = (...args: unknown[]) => unknown

/** A SignalR connection without a server: tests answer the calls and send the messages. */
export class FakeHubConnection implements HubConnectionLike {
    state: HubConnectionState = HubConnectionState.Disconnected
    readonly handlers = new Map<string, Handler>()
    readonly closeHandlers: ((error?: Error) => void)[] = []
    readonly reconnectingHandlers: ((error?: Error) => void)[] = []
    readonly reconnectedHandlers: ((connectionId?: string) => void)[] = []
    readonly invocations: { method: string; args: unknown[] }[] = []
    /** Answers of the methods of the hub, by name; without one a call answers undefined. */
    readonly answers = new Map<string, (argument: unknown) => unknown>()
    /** What start does; fails to simulate an unreachable server. */
    startWith: () => Promise<void> = () => Promise.resolve()
    starts = 0
    stops = 0

    constructor(readonly accessToken: () => Promise<string>) {}

    on(methodName: string, newMethod: Handler): void {
        this.handlers.set(methodName, newMethod)
    }

    onclose(callback: (error?: Error) => void): void {
        this.closeHandlers.push(callback)
    }

    onreconnecting(callback: (error?: Error) => void): void {
        this.reconnectingHandlers.push(callback)
    }

    onreconnected(callback: (connectionId?: string) => void): void {
        this.reconnectedHandlers.push(callback)
    }

    async start(): Promise<void> {
        this.starts++
        this.state = HubConnectionState.Connecting
        try {
            await this.startWith()
        } catch (error) {
            this.state = HubConnectionState.Disconnected
            throw error
        }

        this.state = HubConnectionState.Connected
    }

    stop(): Promise<void> {
        this.stops++
        this.state = HubConnectionState.Disconnected
        return Promise.resolve()
    }

    invoke<T>(methodName: string, ...args: unknown[]): Promise<T> {
        this.invocations.push({ method: methodName, args })
        const answer = this.answers.get(methodName)
        return Promise.resolve((answer ? answer(args[0]) : undefined) as T)
    }

    /** The server sends a message. */
    emit(methodName: string, payload: unknown): void {
        this.handlers.get(methodName)?.(payload)
    }

    /** The automatic reconnect starts. */
    reconnecting(): void {
        this.state = HubConnectionState.Reconnecting
        this.reconnectingHandlers.forEach((handler) => handler())
    }

    /** The automatic reconnect succeeded. */
    reconnected(): void {
        this.state = HubConnectionState.Connected
        this.reconnectedHandlers.forEach((handler) => handler('next'))
    }

    /** The connection closed for good. */
    close(): void {
        this.state = HubConnectionState.Disconnected
        this.closeHandlers.forEach((handler) => handler(new Error('closed')))
    }

    /** The arguments of the calls of a method. */
    callsOf(methodName: string): unknown[] {
        return this.invocations
            .filter((call) => call.method === methodName)
            .map((call) => call.args[0])
    }
}

/** Timers that only run when the test says so. */
export class FakeTimers {
    private next = 1
    readonly timeouts = new Map<number, { handler: () => void; milliseconds: number }>()
    readonly intervals = new Map<number, { handler: () => void; milliseconds: number }>()

    setTimeout = (handler: () => void, milliseconds: number): number => {
        const id = this.next++
        this.timeouts.set(id, { handler, milliseconds })
        return id
    }

    clearTimeout = (id: number): void => {
        this.timeouts.delete(id)
    }

    setInterval = (handler: () => void, milliseconds: number): number => {
        const id = this.next++
        this.intervals.set(id, { handler, milliseconds })
        return id
    }

    clearInterval = (id: number): void => {
        this.intervals.delete(id)
    }

    /** Runs the waiting timeouts (also those they start) and once every interval. */
    runTimeouts(): void {
        for (let round = 0; round < 10 && this.timeouts.size > 0; round++) {
            const due = [...this.timeouts.entries()]
            this.timeouts.clear()
            due.forEach(([, timeout]) => timeout.handler())
        }
    }

    tickIntervals(): void {
        ;[...this.intervals.values()].forEach((interval) => interval.handler())
    }
}

export function track(id: string, extra: Partial<Track> = {}): Track {
    return {
        id,
        title: id.toUpperCase(),
        hasAudio: true,
        hasCover: false,
        durationMilliseconds: 200_000,
        artist: { id: 'ar', name: 'Band' },
        ...extra,
    }
}

export function device(id: string, extra: Partial<Device> = {}): Device {
    return {
        id,
        name: id,
        reportedName: id,
        customName: null,
        type: 'Desktop',
        online: true,
        remoteControl: false,
        active: false,
        lastActiveAt: new Date('2026-11-25T10:00:00Z'),
        ...extra,
    }
}

export function live(extra: Partial<LivePlayback> = {}): LivePlayback {
    return {
        version: 100,
        queueVersion: 90,
        activeDeviceId: null,
        status: 'Idle',
        currentIndex: -1,
        currentTrackId: null,
        queueLength: 0,
        positionMilliseconds: 0,
        durationMilliseconds: 0,
        shuffle: false,
        repeat: 'Off',
        source: null,
        volume: 1,
        muted: false,
        crossfade: false,
        autoplay: false,
        handover: null,
        ...extra,
    }
}

export function deviceQueue(
    tracks: readonly (Track | null)[],
    extra: Partial<DeviceQueue> = {},
): DeviceQueue {
    return {
        version: 100,
        queueVersion: 90,
        currentIndex: tracks.length > 0 ? 0 : -1,
        shuffle: false,
        source: null,
        items: tracks.map((value, position) => ({
            position,
            trackId: value?.id ?? `missing-${position}`,
            autoplay: false,
            track: value,
        })),
        ...extra,
    }
}

export function received(
    extra: Partial<ReceivedCommand> & Pick<ReceivedCommand, 'type'>,
): ReceivedCommand {
    return {
        fromDeviceId: 'phone',
        version: 100,
        expectedQueueVersion: null,
        positionMilliseconds: null,
        volume: null,
        muted: null,
        enabled: null,
        repeat: null,
        tracks: null,
        startIndex: null,
        source: null,
        shuffle: null,
        index: null,
        toIndex: null,
        ...extra,
    }
}
