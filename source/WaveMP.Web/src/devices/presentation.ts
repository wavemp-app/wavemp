// How the list of devices shows a device: its icon, its state tokens and what its line says. Pure
// functions for the components of the device picker and the settings.

import { DeviceTypeObject, type Device, type DeviceType } from '@/api/generated/models'
import { LivePlaybackStatusObject, type LivePlayback } from './protocol'

/** Icon slots of the kinds of devices (see THEMING.md). */
export type DeviceIconSlot =
    | 'device-desktop'
    | 'device-phone'
    | 'device-tablet'
    | 'device-tv'
    | 'device-watch'
    | 'device-other'

const iconSlots: Record<DeviceType, DeviceIconSlot> = {
    [DeviceTypeObject.Desktop]: 'device-desktop',
    [DeviceTypeObject.Phone]: 'device-phone',
    [DeviceTypeObject.Tablet]: 'device-tablet',
    [DeviceTypeObject.Tv]: 'device-tv',
    [DeviceTypeObject.Watch]: 'device-watch',
    [DeviceTypeObject.Other]: 'device-other',
}

/** The icon slot of the kind of a device. */
export function deviceIconSlot(type: DeviceType | null | undefined): DeviceIconSlot {
    return (type ? iconSlots[type] : undefined) ?? 'device-other'
}

/** What the line of a device says below its name. */
export type DeviceStatus =
    'this-device' | 'playing' | 'paused' | 'remote-control-off' | 'available' | 'offline'

/**
 * Whether playback can move to the device: this device, or an open one that allows remote
 * control.
 */
export function deviceSelectable(device: Device, deviceId: string): boolean {
    return device.id === deviceId || (device.online === true && device.remoteControl === true)
}

/** The open devices for the picker, in the order of the server; this device is always among them. */
export function pickerDevices(devices: readonly Device[], deviceId: string): Device[] {
    return devices.filter((device) => device.online === true || device.id === deviceId)
}

/** Index of the active device in a list; -1 without one. */
export function activeIndexOf(devices: readonly Device[], playback: LivePlayback | null): number {
    const id = playback?.activeDeviceId
    return id ? devices.findIndex((device) => device.id === id) : -1
}

function playbackToken(playback: LivePlayback | null): 'playing' | 'paused' | 'idle' {
    switch (playback?.status) {
        case LivePlaybackStatusObject.Playing:
        case LivePlaybackStatusObject.Loading:
            return 'playing'
        case LivePlaybackStatusObject.Paused:
            return 'paused'
        default:
            return 'idle'
    }
}

function isActive(device: Device, playback: LivePlayback | null): boolean {
    return !!playback?.activeDeviceId && playback.activeDeviceId === device.id
}

/**
 * data-state tokens of a device in a list, e.g. "active current allowed online playing": active
 * (the device that plays), current (this device), allowed or blocked (whether playback can move
 * there), online or offline and, for the active device, playing, paused or idle.
 */
export function deviceStateTokens(
    device: Device,
    deviceId: string,
    playback: LivePlayback | null,
): string {
    const active = isActive(device, playback)
    return [
        ...(active ? ['active'] : []),
        ...(device.id === deviceId ? ['current'] : []),
        deviceSelectable(device, deviceId) ? 'allowed' : 'blocked',
        device.online === true ? 'online' : 'offline',
        ...(active ? [playbackToken(playback)] : []),
    ].join(' ')
}

/** What the line of a device says: playback of the active device first, then this device. */
export function deviceStatusOf(
    device: Device,
    deviceId: string,
    playback: LivePlayback | null,
): DeviceStatus {
    const token = isActive(device, playback) ? playbackToken(playback) : 'idle'
    if (token !== 'idle') {
        return token
    }

    if (device.id === deviceId) {
        return 'this-device'
    }

    if (device.online !== true) {
        return 'offline'
    }

    return device.remoteControl === true ? 'available' : 'remote-control-off'
}
