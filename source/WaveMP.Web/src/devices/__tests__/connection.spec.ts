import { HubConnectionState } from '@microsoft/signalr'
import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import { DeviceConnection, retryDelayMilliseconds, type ConnectionEvents } from '../connection'
import { FakeHubConnection, FakeTimers } from './fakes'

function setup(prepare: (hub: FakeHubConnection) => void = () => undefined) {
    const timers = new FakeTimers()
    const connections: FakeHubConnection[] = []
    let wake: (() => void) | null = null
    const stopListening = vi.fn()
    const events = {
        status: vi.fn(),
        connected: vi.fn(),
        resumed: vi.fn(),
        devicesChanged: vi.fn(),
        playbackChanged: vi.fn(),
        commandReceived: vi.fn(),
        deviceReplaced: vi.fn(),
    } satisfies ConnectionEvents
    const ticket = vi.fn(() => Promise.resolve('ticket-1'))
    const connection = new DeviceConnection(
        {
            createConnection: (accessToken) => {
                const created = new FakeHubConnection(accessToken)
                prepare(created)
                connections.push(created)
                return created
            },
            ticket,
            timers,
            listen: (callback) => {
                wake = callback
                return stopListening
            },
        },
        events,
    )
    return {
        connection,
        connections,
        events,
        timers,
        ticket,
        stopListening,
        wake: () => wake?.(),
    }
}

describe('DeviceConnection', () => {
    it('connects with a ticket per attempt and reports every connect', async () => {
        const { connection, connections, events, ticket } = setup()
        connection.start()
        connection.start()
        await flushPromises()

        expect(connections).toHaveLength(1)
        expect(connections[0]!.starts).toBe(1)
        expect(connection.status).toBe('connected')
        expect(events.status.mock.calls).toEqual([['connecting'], ['connected']])
        expect(events.connected).toHaveBeenCalledOnce()
        // The SignalR client asks for the credentials before every attempt.
        await expect(connections[0]!.accessToken()).resolves.toBe('ticket-1')
        expect(ticket).toHaveBeenCalledOnce()
    })

    it('reports reconnects of the automatic reconnect as connects', async () => {
        const { connection, connections, events } = setup()
        connection.start()
        await flushPromises()
        connections[0]!.reconnecting()
        expect(connection.status).toBe('connecting')
        connections[0]!.reconnected()

        expect(connection.status).toBe('connected')
        expect(events.connected).toHaveBeenCalledTimes(2)
    })

    it('tries again later when the server cannot be reached or the reconnect gave up', async () => {
        const { connection, connections, timers } = setup((hub) => {
            hub.startWith = () => Promise.reject(new Error('down'))
        })
        connection.start()
        await flushPromises()

        expect(connection.status).toBe('offline')
        expect([...timers.timeouts.values()].map((timeout) => timeout.milliseconds)).toEqual([
            retryDelayMilliseconds,
        ])

        timers.runTimeouts()
        await flushPromises()
        expect(connections[0]!.starts).toBe(2)
        expect(connection.status).toBe('offline')
        expect(timers.timeouts.size).toBe(1)

        connections[0]!.startWith = () => Promise.resolve()
        timers.runTimeouts()
        await flushPromises()
        expect(connection.status).toBe('connected')

        // The automatic reconnect gave up: the next attempt follows later.
        connections[0]!.close()
        expect(connection.status).toBe('offline')
        expect(timers.timeouts.size).toBe(1)
    })

    it('connects at once when the page comes back and syncs an open connection', async () => {
        const { connection, connections, events, timers, wake } = setup()
        connection.start()
        await flushPromises()

        wake()
        expect(events.resumed).toHaveBeenCalledOnce()

        connections[0]!.close()
        expect(timers.timeouts.size).toBe(1)
        wake()
        await flushPromises()
        expect(timers.timeouts.size).toBe(0)
        expect(connections[0]!.starts).toBe(2)
        expect(connection.status).toBe('connected')
    })

    it('passes the messages of the server on, decoded', async () => {
        const { connection, connections, events } = setup()
        connection.start()
        await flushPromises()
        const hub = connections[0]!

        hub.emit('DevicesChanged', {
            devices: [{ id: 'tv', type: 'Tv', lastActiveAt: '2026-11-25T10:00:00Z' }],
        })
        hub.emit('PlaybackChanged', { version: 3 })
        hub.emit('CommandReceived', { type: 'Play', tracks: null })
        hub.emit('DeviceReplaced', {})

        expect(events.devicesChanged.mock.calls[0]?.[0].devices[0].lastActiveAt).toBeInstanceOf(
            Date,
        )
        expect(events.playbackChanged).toHaveBeenCalledWith({ version: 3 })
        expect(events.commandReceived).toHaveBeenCalledWith({ type: 'Play', tracks: null })
        expect(events.deviceReplaced).toHaveBeenCalledOnce()
    })

    it('calls methods of the hub only while connected', async () => {
        const { connection, connections } = setup()
        await expect(connection.invoke('GetQueue')).rejects.toThrow()

        connection.start()
        await flushPromises()
        connections[0]!.answers.set('GetQueue', () => ({ status: 'Accepted' }))

        await expect(connection.invoke('GetQueue')).resolves.toEqual({ status: 'Accepted' })
        expect(connections[0]!.invocations).toEqual([{ method: 'GetQueue', args: [] }])
    })

    it('stops for good: no retry, no wake, messages of the old connection are ignored', async () => {
        const { connection, connections, events, timers, stopListening, wake } = setup()
        connection.start()
        await flushPromises()
        const old = connections[0]!

        await connection.stop()
        expect(old.stops).toBe(1)
        expect(old.state).toBe(HubConnectionState.Disconnected)
        expect(connection.status).toBe('offline')
        expect(stopListening).toHaveBeenCalledOnce()

        old.close()
        old.emit('PlaybackChanged', { version: 9 })
        wake()
        expect(timers.timeouts.size).toBe(0)
        expect(events.playbackChanged).not.toHaveBeenCalled()

        connection.start()
        await flushPromises()
        expect(connections).toHaveLength(2)
        expect(connection.status).toBe('connected')
    })

    it('closes a connection that opened after stop', async () => {
        let open: () => void = () => undefined
        const { connection, connections, events } = setup((hub) => {
            hub.startWith = () => new Promise<void>((resolve) => (open = resolve))
        })
        connection.start()
        await connection.stop()
        open()
        await flushPromises()

        expect(events.connected).not.toHaveBeenCalled()
        expect(connections[0]!.state).toBe(HubConnectionState.Disconnected)
    })

    it('connects anew when it was started again while an older attempt ran', async () => {
        const waiting: (() => void)[] = []
        const { connection, connections, events } = setup((hub) => {
            hub.startWith = () => new Promise<void>((resolve) => waiting.push(resolve))
        })
        connection.start()
        await connection.stop()
        connection.start()
        await flushPromises()
        expect(connections).toHaveLength(1)

        waiting.splice(0).forEach((resolve) => resolve())
        await flushPromises()
        expect(connections).toHaveLength(2)
        expect(connections[0]!.state).toBe(HubConnectionState.Disconnected)
        waiting.splice(0).forEach((resolve) => resolve())
        await flushPromises()

        expect(connections[1]!.state).toBe(HubConnectionState.Connected)
        expect(connection.status).toBe('connected')
        expect(events.connected).toHaveBeenCalledOnce()
    })
})
