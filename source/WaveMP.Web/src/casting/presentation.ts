// Texts of casting as message keys with their parameters, so the rules are tested without i18n.

import type { TransportCast } from '@/components/shell/transport'

export interface CastText {
    readonly key: string
    readonly params?: Readonly<Record<string, string>>
}

/** The label of the cast button. */
export function castButtonText(cast: TransportCast | undefined): CastText {
    if (!cast || cast.availability === 'unsupported') {
        return { key: 'cast.button' }
    }

    if (cast.availability === 'insecure') {
        return { key: 'cast.buttonInsecure' }
    }

    if (cast.connection === 'connected') {
        return cast.device
            ? { key: 'cast.buttonConnected', params: { device: cast.device } }
            : { key: 'cast.buttonConnectedUnnamed' }
    }

    return cast.devicesFound || cast.connection === 'connecting'
        ? { key: 'cast.button' }
        : { key: 'cast.noDevices' }
}

/** The notice while a device connects or plays ("Playing on ..."); null otherwise. */
export function castStatusText(cast: TransportCast | undefined): CastText | null {
    if (!cast || cast.availability !== 'available') {
        return null
    }

    switch (cast.connection) {
        case 'connecting':
            return cast.device
                ? { key: 'cast.connecting', params: { device: cast.device } }
                : { key: 'cast.connectingUnnamed' }
        case 'connected':
            if (cast.device) {
                return { key: 'cast.playingOn', params: { device: cast.device } }
            }

            return cast.technology === 'airplay'
                ? { key: 'cast.playingOnAirPlay' }
                : { key: 'cast.playingOnUnnamed' }
        default:
            return null
    }
}
