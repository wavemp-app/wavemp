// The role of this device: the device that plays (local) or a remote of the active device of the
// profile. Pure functions, so the store and its tests share the rules.

import type { Device } from '@/api/generated/models'
import type { LivePlayback } from './protocol'

/** local: the player of this device plays; remote: it shows and controls the active device. */
export type DeviceRole = 'local' | 'remote'

export interface RoleInput {
    /** The id of this device. */
    readonly deviceId: string
    /** Whether this device is registered with the hub right now. */
    readonly registered: boolean
    readonly playback: LivePlayback | null
    readonly devices: readonly Device[]
    /** Whether the player of this device plays right now. */
    readonly playing: boolean
}

/** The active device of the profile, if it is known. */
export function activeDeviceOf(
    playback: LivePlayback | null,
    devices: readonly Device[],
): Device | undefined {
    const id = playback?.activeDeviceId
    return id ? devices.find((device) => device.id === id) : undefined
}

/**
 * The role of this device. It is a remote while another device is the active one, open and
 * allows remote control, and this device does not play itself (a device that kept playing after
 * another one took over, because it does not allow remote control, stays on its own). Otherwise,
 * and without a connection, it plays on its own.
 */
export function roleOf(input: RoleInput): DeviceRole {
    if (!input.registered || input.playing) {
        return 'local'
    }

    const active = activeDeviceOf(input.playback, input.devices)
    return active !== undefined &&
        active.id !== input.deviceId &&
        active.online === true &&
        active.remoteControl === true
        ? 'remote'
        : 'local'
}
