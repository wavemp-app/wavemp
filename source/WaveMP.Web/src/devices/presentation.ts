// How the list of devices shows a device: its icon, its state tokens and what its line says. Pure
// functions for the components of the device picker and the settings.

import { DeviceTypeObject, type Device, type DeviceType } from '@/api/generated/models'
import { LivePlaybackStatusObject, type CommandStatus, type LivePlayback } from './protocol'

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

/** i18n key of what the line of a device says. */
export const deviceStatusKeys: Readonly<Record<DeviceStatus, string>> = {
    'this-device': 'devices.status.thisDevice',
    playing: 'devices.status.playing',
    paused: 'devices.status.paused',
    'remote-control-off': 'devices.status.remoteControlOff',
    available: 'devices.status.available',
    offline: 'devices.status.offline',
}

/** A device as the device picker shows it. */
export interface DeviceEntry {
    readonly id: string
    readonly name: string
    readonly icon: DeviceIconSlot
    /** data-state tokens (deviceStateTokens). */
    readonly tokens: string
    readonly status: DeviceStatus
    /** Whether playback can move there (aria-disabled otherwise). */
    readonly selectable: boolean
    /** Whether it is the device that plays (aria-checked). */
    readonly active: boolean
    /** Whether it is this device. */
    readonly current: boolean
}

/** This device as the picker knows it before the server lists it (e.g. without a connection). */
export interface OwnDevice {
    readonly id: string
    readonly name: string
    readonly type: DeviceType
}

/**
 * The entries of the device picker: the open devices of the profile and always this device (also
 * before the server lists it), in the order of the server.
 */
export function deviceEntries(
    devices: readonly Device[],
    self: OwnDevice,
    playback: LivePlayback | null,
): DeviceEntry[] {
    const shown = pickerDevices(devices, self.id)
    const list: Device[] = shown.some((device) => device.id === self.id)
        ? shown
        : [
              ...shown,
              {
                  id: self.id,
                  name: self.name,
                  reportedName: self.name,
                  customName: null,
                  type: self.type,
                  online: true,
                  remoteControl: false,
                  active: false,
              },
          ]
    return list.map((device) => ({
        id: device.id ?? '',
        name: device.name ?? '',
        icon: deviceIconSlot(device.type),
        tokens: deviceStateTokens(device, self.id, playback),
        status: deviceStatusOf(device, self.id, playback),
        selectable: deviceSelectable(device, self.id),
        active: isActive(device, playback),
        current: device.id === self.id,
    }))
}

/**
 * data-state tokens of the devices control in the transport bar, e.g. "remote connected" or
 * "local connecting open": the role of this device, the connection to the server and whether the
 * picker is open.
 */
export function devicesControlTokens(
    role: 'local' | 'remote',
    connection: 'offline' | 'connecting' | 'connected',
    open: boolean,
): string {
    return [role, connection, ...(open ? ['open'] : [])].join(' ')
}

/** i18n key of the notice of a command or hand-over that was not accepted. */
export const commandStatusKeys: Readonly<Record<CommandStatus, string>> = {
    Accepted: 'devices.errors.invalid',
    NotRegistered: 'devices.errors.notRegistered',
    DeviceNotFound: 'devices.errors.deviceNotFound',
    RemoteControlNotAllowed: 'devices.errors.remoteControlNotAllowed',
    NotActive: 'devices.errors.notActive',
    Conflict: 'devices.errors.conflict',
    Invalid: 'devices.errors.invalid',
}
