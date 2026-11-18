import { describe, expect, it } from 'vitest'
import type { TransportCast } from '@/components/shell/transport'
import { castButtonText, castStatusText } from '../presentation'

function cast(extra: Partial<TransportCast> = {}): TransportCast {
    return {
        availability: 'available',
        connection: 'disconnected',
        technology: 'chromecast',
        device: null,
        devicesFound: true,
        ...extra,
    }
}

describe('casting texts', () => {
    it('labels the button by availability, devices and connection', () => {
        expect(castButtonText(cast())).toEqual({ key: 'cast.button' })
        expect(castButtonText(cast({ devicesFound: false }))).toEqual({ key: 'cast.noDevices' })
        expect(castButtonText(cast({ availability: 'insecure' }))).toEqual({
            key: 'cast.buttonInsecure',
        })
        expect(castButtonText(cast({ connection: 'connected', device: 'Kitchen' }))).toEqual({
            key: 'cast.buttonConnected',
            params: { device: 'Kitchen' },
        })
        expect(castButtonText(cast({ connection: 'connected' }))).toEqual({
            key: 'cast.buttonConnectedUnnamed',
        })
        expect(castButtonText(undefined)).toEqual({ key: 'cast.button' })
    })

    it('names the device that connects or plays', () => {
        expect(castStatusText(cast())).toBeNull()
        expect(castStatusText(cast({ connection: 'connecting', device: 'Kitchen' }))).toEqual({
            key: 'cast.connecting',
            params: { device: 'Kitchen' },
        })
        expect(castStatusText(cast({ connection: 'connecting' }))).toEqual({
            key: 'cast.connectingUnnamed',
        })
        expect(castStatusText(cast({ connection: 'connected', device: 'Kitchen' }))).toEqual({
            key: 'cast.playingOn',
            params: { device: 'Kitchen' },
        })
        expect(castStatusText(cast({ connection: 'connected' }))).toEqual({
            key: 'cast.playingOnUnnamed',
        })
        expect(castStatusText(cast({ connection: 'connected', technology: 'airplay' }))).toEqual({
            key: 'cast.playingOnAirPlay',
        })
        expect(castStatusText(cast({ availability: 'insecure' }))).toBeNull()
        expect(castStatusText(undefined)).toBeNull()
    })
})
