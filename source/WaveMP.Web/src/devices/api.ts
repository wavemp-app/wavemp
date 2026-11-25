// REST requests of the devices (the hub itself is in connection.ts). The only place of the web app
// that calls these endpoints, so the stores and their tests can replace it as a whole
// (vi.mock('@/devices/api')).

import { apiClient } from '@/api/client'
import type { Device, DevicePage, PlaybackState } from '@/api/generated/models'

/**
 * A new ticket for the connection to the hub (valid for a minute). Browsers cannot send headers
 * when they open a WebSocket or an event stream, so the ticket goes into the URL instead of the
 * session token.
 */
export async function createHubTicket(): Promise<string> {
    const ticket = (await apiClient.api.v1.me.deviceHubTickets.post())?.ticket
    if (!ticket) {
        throw new Error('The server answered without a ticket.')
    }

    return ticket
}

/** The playback state the server saved when playback was handed over to this device. */
export function getHandoverState(): Promise<PlaybackState | undefined> {
    return apiClient.api.v1.me.playbackState.get()
}

/** A page of the devices of the current profile, open ones first. */
export function listDevices(offset: number, limit: number): Promise<DevicePage | undefined> {
    return apiClient.api.v1.me.devices.get({ queryParameters: { offset, limit } })
}

/** Gives a device of the current profile a name. */
export function renameDevice(id: string, name: string): Promise<Device | undefined> {
    return apiClient.api.v1.me.devices.byDeviceId(id).patch({ name })
}

/** Forgets a closed device of the current profile. */
export function forgetDevice(id: string): Promise<void> {
    return apiClient.api.v1.me.devices.byDeviceId(id).delete()
}
