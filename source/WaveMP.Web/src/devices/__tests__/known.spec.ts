import { describe, expect, it } from 'vitest'
import { knownDeviceMeta, knownDeviceTokens, renameTarget } from '../known'
import { device } from './fakes'

describe('known devices', () => {
    it('say whether they are this device, open, or when they were last active', () => {
        expect(knownDeviceMeta(device('me'), 'me')).toEqual({ kind: 'current' })
        expect(knownDeviceMeta(device('tv'), 'me')).toEqual({ kind: 'online' })
        const lastActiveAt = new Date('2026-11-20T08:00:00Z')
        expect(knownDeviceMeta(device('old', { online: false, lastActiveAt }), 'me')).toEqual({
            kind: 'offline',
            lastActiveAt,
        })
        expect(knownDeviceMeta(device('me', { online: false, lastActiveAt: null }), 'me')).toEqual({
            kind: 'offline',
            lastActiveAt: null,
        })
    })

    it('describe themselves with state tokens', () => {
        expect(knownDeviceTokens(device('me'), 'me', false)).toBe('current online')
        expect(
            knownDeviceTokens(device('tv', { online: false, customName: 'TV' }), 'me', true),
        ).toBe('offline renamed busy')
    })
})

describe('renameTarget', () => {
    const renamed = device('pc', {
        name: 'Desk',
        customName: 'Desk',
        reportedName: 'Edge · Windows',
    })

    it('saves the trimmed name', () => {
        expect(renameTarget(renamed, '  Living room PC  ')).toBe('Living room PC')
    })

    it('brings back the reported name for an empty field', () => {
        expect(renameTarget(renamed, '   ')).toBe('Edge · Windows')
    })

    it('saves nothing without a change', () => {
        expect(renameTarget(renamed, 'Desk ')).toBeNull()
        expect(renameTarget(device('tv'), '')).toBeNull()
        expect(renameTarget(device('tv', { reportedName: null, name: '' }), '')).toBeNull()
    })
})
