import { describe, expect, it } from 'vitest'
import {
    activeIndexOf,
    commandStatusKeys,
    deviceEntries,
    deviceIconSlot,
    deviceSelectable,
    deviceStateTokens,
    deviceStatusKeys,
    deviceStatusOf,
    devicesControlTokens,
    pickerDevices,
} from '../presentation'
import { device, live } from './fakes'

describe('deviceIconSlot', () => {
    it('has an icon slot per kind', () => {
        expect(deviceIconSlot('Desktop')).toBe('device-desktop')
        expect(deviceIconSlot('Phone')).toBe('device-phone')
        expect(deviceIconSlot('Tablet')).toBe('device-tablet')
        expect(deviceIconSlot('Tv')).toBe('device-tv')
        expect(deviceIconSlot('Watch')).toBe('device-watch')
        expect(deviceIconSlot('Other')).toBe('device-other')
        expect(deviceIconSlot(null)).toBe('device-other')
    })
})

describe('the device list', () => {
    const devices = [
        device('tv', { online: true, remoteControl: true }),
        device('me', { online: true }),
        device('phone', { online: true }),
        device('old', { online: false }),
    ]

    it('offers the open devices and this one', () => {
        expect(pickerDevices(devices, 'me').map((entry) => entry.id)).toEqual(['tv', 'me', 'phone'])
        expect(pickerDevices([device('me', { online: false })], 'me')).toHaveLength(1)
    })

    it('can move playback to this device and to open ones that allow remote control', () => {
        expect(devices.map((entry) => deviceSelectable(entry, 'me'))).toEqual([
            true,
            true,
            false,
            false,
        ])
    })

    it('finds the active device', () => {
        expect(activeIndexOf(devices, live({ activeDeviceId: 'phone' }))).toBe(2)
        expect(activeIndexOf(devices, live())).toBe(-1)
        expect(activeIndexOf(devices, null)).toBe(-1)
    })

    it('describes each device with state tokens', () => {
        const playback = live({ activeDeviceId: 'tv', status: 'Playing' })
        expect(deviceStateTokens(devices[0]!, 'me', playback)).toBe('active allowed online playing')
        expect(deviceStateTokens(devices[1]!, 'me', playback)).toBe('current allowed online')
        expect(deviceStateTokens(devices[2]!, 'me', playback)).toBe('blocked online')
        expect(deviceStateTokens(devices[3]!, 'me', live({ activeDeviceId: 'old' }))).toBe(
            'active blocked offline idle',
        )
    })

    it('says what each device does', () => {
        const paused = live({ activeDeviceId: 'tv', status: 'Paused' })
        expect(deviceStatusOf(devices[0]!, 'me', paused)).toBe('paused')
        expect(
            deviceStatusOf(devices[0]!, 'me', live({ activeDeviceId: 'tv', status: 'Loading' })),
        ).toBe('playing')
        expect(deviceStatusOf(devices[0]!, 'me', live())).toBe('available')
        expect(deviceStatusOf(devices[1]!, 'me', paused)).toBe('this-device')
        expect(deviceStatusOf(devices[2]!, 'me', paused)).toBe('remote-control-off')
        expect(deviceStatusOf(devices[3]!, 'me', paused)).toBe('offline')
    })
})

describe('the entries of the device picker', () => {
    const self = { id: 'me', name: 'Firefox · Windows', type: 'Desktop' as const }

    it('lists the open devices with this one and marks the one that plays', () => {
        const entries = deviceEntries(
            [
                device('tv', { type: 'Tv', online: true, remoteControl: true }),
                device('me', { name: 'My PC' }),
                device('tablet', { type: 'Tablet', online: true }),
                device('old', { online: false, remoteControl: true }),
            ],
            self,
            live({ activeDeviceId: 'tv', status: 'Playing' }),
        )

        expect(entries).toEqual([
            {
                id: 'tv',
                name: 'tv',
                icon: 'device-tv',
                tokens: 'active allowed online playing',
                status: 'playing',
                selectable: true,
                active: true,
                current: false,
            },
            {
                id: 'me',
                name: 'My PC',
                icon: 'device-desktop',
                tokens: 'current allowed online',
                status: 'this-device',
                selectable: true,
                active: false,
                current: true,
            },
            {
                id: 'tablet',
                name: 'tablet',
                icon: 'device-tablet',
                tokens: 'blocked online',
                status: 'remote-control-off',
                selectable: false,
                active: false,
                current: false,
            },
        ])
    })

    it('shows this device before the server lists it', () => {
        expect(deviceEntries([], self, null)).toEqual([
            {
                id: 'me',
                name: 'Firefox · Windows',
                icon: 'device-desktop',
                tokens: 'current allowed online',
                status: 'this-device',
                selectable: true,
                active: false,
                current: true,
            },
        ])
    })

    it('has a message for every line and every status of a command', () => {
        expect(Object.keys(deviceStatusKeys).sort()).toEqual([
            'available',
            'offline',
            'paused',
            'playing',
            'remote-control-off',
            'this-device',
        ])
        expect(commandStatusKeys.RemoteControlNotAllowed).toBe(
            'devices.errors.remoteControlNotAllowed',
        )
        expect(commandStatusKeys.Conflict).toBe('devices.errors.conflict')
    })

    it('describes the devices control with state tokens', () => {
        expect(devicesControlTokens('local', 'connected', false)).toBe('local connected')
        expect(devicesControlTokens('remote', 'connecting', true)).toBe('remote connecting open')
        expect(devicesControlTokens('local', 'offline', false)).toBe('local offline')
    })
})
