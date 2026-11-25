import { describe, expect, it } from 'vitest'
import { activeDeviceOf, roleOf, type RoleInput } from '../roles'
import { device, live } from './fakes'

function input(extra: Partial<RoleInput> = {}): RoleInput {
    return {
        deviceId: 'me',
        registered: true,
        playback: live({ activeDeviceId: 'tv' }),
        devices: [device('me'), device('tv', { online: true, remoteControl: true })],
        playing: false,
        ...extra,
    }
}

describe('roleOf', () => {
    it('is a remote of an open active device that allows remote control', () => {
        expect(roleOf(input())).toBe('remote')
    })

    it('plays on its own when the active device does not allow remote control or is closed', () => {
        expect(
            roleOf(input({ devices: [device('me'), device('tv', { remoteControl: false })] })),
        ).toBe('local')
        expect(
            roleOf(
                input({
                    devices: [device('me'), device('tv', { online: false, remoteControl: true })],
                }),
            ),
        ).toBe('local')
    })

    it('plays on its own as the active device, without one and while unknown', () => {
        expect(roleOf(input({ playback: live({ activeDeviceId: 'me' }) }))).toBe('local')
        expect(roleOf(input({ playback: live() }))).toBe('local')
        expect(roleOf(input({ playback: null }))).toBe('local')
        expect(roleOf(input({ devices: [device('me')] }))).toBe('local')
    })

    it('plays on its own while it plays or is not registered', () => {
        expect(roleOf(input({ playing: true }))).toBe('local')
        expect(roleOf(input({ registered: false }))).toBe('local')
    })
})

describe('activeDeviceOf', () => {
    it('finds the active device in the list', () => {
        expect(activeDeviceOf(live({ activeDeviceId: 'tv' }), [device('tv')])?.id).toBe('tv')
        expect(activeDeviceOf(live(), [device('tv')])).toBeUndefined()
        expect(activeDeviceOf(null, [device('tv')])).toBeUndefined()
    })
})
