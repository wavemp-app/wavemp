// The devices of the profile as the settings page lists them. Pure functions for
// DeviceSettings.vue.

import type { Device } from '@/api/generated/models'

/** Longest name of a device (the server checks it as well). */
export const nameMaxLength = 100

/** What the line of a known device says: this device, open now, or when it was last active. */
export type KnownDeviceMeta =
    | { readonly kind: 'current' }
    | { readonly kind: 'online' }
    | { readonly kind: 'offline'; readonly lastActiveAt: Date | null }

export function knownDeviceMeta(device: Device, deviceId: string): KnownDeviceMeta {
    if (device.online === true) {
        return device.id === deviceId ? { kind: 'current' } : { kind: 'online' }
    }

    return { kind: 'offline', lastActiveAt: device.lastActiveAt ?? null }
}

/**
 * data-state tokens of a known device, e.g. "current online renamed": current (this device),
 * online or offline, renamed (the user gave it a name) and busy while a change is saved.
 */
export function knownDeviceTokens(device: Device, deviceId: string, busy: boolean): string {
    return [
        ...(device.id === deviceId ? ['current'] : []),
        device.online === true ? 'online' : 'offline',
        ...(device.customName ? ['renamed'] : []),
        ...(busy ? ['busy'] : []),
    ].join(' ')
}

/**
 * The name to save for a device from the text of its field: the trimmed text, or the name the
 * device reports when the field is empty (which removes the given name). Null when nothing
 * changes.
 */
export function renameTarget(device: Device, text: string): string | null {
    const trimmed = text.trim()
    const target = trimmed === '' ? (device.reportedName ?? '') : trimmed
    if (target === '' || target === (device.name ?? '')) {
        return null
    }

    return target
}
