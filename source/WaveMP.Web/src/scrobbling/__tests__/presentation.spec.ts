import { describe, expect, it } from 'vitest'
import {
    connectErrorKey,
    lastFmAuthorizationUrl,
    scrobbleServicePresentation,
    scrobbleState,
    scrobbleStateKey,
} from '../presentation'

describe('scrobbleState', () => {
    it('follows the status of the connection', () => {
        expect(scrobbleState({ service: 'LastFm', isAvailable: true, status: 'Connected' })).toBe(
            'connected',
        )
        expect(
            scrobbleState({ service: 'LastFm', isAvailable: true, status: 'AuthorizationFailed' }),
        ).toBe('authorization-failed')
        expect(
            scrobbleState({ service: 'ListenBrainz', isAvailable: true, status: 'NotConnected' }),
        ).toBe('not-connected')
    })

    it('marks a service without API account as unavailable', () => {
        expect(
            scrobbleState({ service: 'LastFm', isAvailable: false, status: 'NotConnected' }),
        ).toBe('unavailable')
    })

    it('has a message for every state', () => {
        expect(scrobbleStateKey('connected')).toBe('scrobbling.states.connected')
        expect(scrobbleStateKey('not-connected')).toBe('scrobbling.states.notConnected')
        expect(scrobbleStateKey('authorization-failed')).toBe(
            'scrobbling.states.authorizationFailed',
        )
        expect(scrobbleStateKey('unavailable')).toBe('scrobbling.states.unavailable')
    })
})

describe('scrobbleServicePresentation', () => {
    it('names the services', () => {
        expect(scrobbleServicePresentation('LastFm')).toEqual({
            token: 'last-fm',
            nameKey: 'scrobbling.services.lastFm',
        })
        expect(scrobbleServicePresentation('ListenBrainz').token).toBe('listen-brainz')
    })
})

describe('lastFmAuthorizationUrl', () => {
    it('adds the address Last.fm returns to', () => {
        expect(
            lastFmAuthorizationUrl(
                'https://www.last.fm/api/auth/?api_key=key',
                'https://music.example/settings/scrobbling/last-fm',
            ),
        ).toBe(
            'https://www.last.fm/api/auth/?api_key=key&cb=https%3A%2F%2Fmusic.example%2Fsettings%2Fscrobbling%2Flast-fm',
        )
    })
})

describe('connectErrorKey', () => {
    it('translates the codes of the server', () => {
        expect(connectErrorKey({ code: 'scrobbling.token_invalid' })).toBe(
            'scrobbling.errors.tokenInvalid',
        )
        expect(connectErrorKey({ code: 'scrobbling.service_unavailable' })).toBe(
            'scrobbling.errors.serviceUnavailable',
        )
        expect(connectErrorKey({ code: 'scrobbling.not_configured' })).toBe(
            'scrobbling.errors.notConfigured',
        )
        expect(connectErrorKey(new Error('offline'))).toBe('scrobbling.errors.connectFailed')
    })
})
