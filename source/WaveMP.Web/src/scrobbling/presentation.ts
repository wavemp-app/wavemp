// How the web app shows the scrobbling services of a profile: tokens for data-state and data-item,
// keys of the messages, and the pages of the services the user is sent to.

import {
    ProblemCodeObject,
    ScrobbleConnectionStatusObject,
    ScrobbleServiceObject,
    type ScrobbleService,
    type ScrobblingService,
} from '@/api/generated/models'
import { problemCodeOf } from '@/api/problems'

/** Page of ListenBrainz that shows the personal token of the user. */
export const listenBrainzTokenUrl = 'https://listenbrainz.org/settings/'

/** Name of the route Last.fm sends the user back to after the authorization. */
export const lastFmCallbackRoute = 'settings-scrobbling-lastfm'

/** The services in the order of the settings page. */
export const scrobbleServices: readonly ScrobbleService[] = [
    ScrobbleServiceObject.LastFm,
    ScrobbleServiceObject.ListenBrainz,
]

export type ScrobbleState = 'connected' | 'not-connected' | 'authorization-failed' | 'unavailable'

export interface ScrobbleServicePresentation {
    /** data-item of the service. */
    readonly token: string
    /** Key of the name of the service. */
    readonly nameKey: string
}

const services: Record<ScrobbleService, ScrobbleServicePresentation> = {
    LastFm: { token: 'last-fm', nameKey: 'scrobbling.services.lastFm' },
    ListenBrainz: { token: 'listen-brainz', nameKey: 'scrobbling.services.listenBrainz' },
}

export function scrobbleServicePresentation(service: ScrobbleService): ScrobbleServicePresentation {
    return services[service]
}

/** State of a service for data-state and the badge; a service without API account is unavailable. */
export function scrobbleState(service: ScrobblingService): ScrobbleState {
    switch (service.status) {
        case ScrobbleConnectionStatusObject.Connected:
            return 'connected'
        case ScrobbleConnectionStatusObject.AuthorizationFailed:
            return 'authorization-failed'
        default:
            return service.isAvailable === false ? 'unavailable' : 'not-connected'
    }
}

/** Key of the message of a state. */
export function scrobbleStateKey(state: ScrobbleState): string {
    switch (state) {
        case 'connected':
            return 'scrobbling.states.connected'
        case 'authorization-failed':
            return 'scrobbling.states.authorizationFailed'
        case 'unavailable':
            return 'scrobbling.states.unavailable'
        default:
            return 'scrobbling.states.notConnected'
    }
}

/**
 * The page of Last.fm that asks the user to allow WaveMP, with the address Last.fm sends the user
 * back to (cb). The server returns the page with the API key; the callback depends on the address
 * the web app runs under.
 */
export function lastFmAuthorizationUrl(authorizationUrl: string, callbackUrl: string): string {
    const url = new URL(authorizationUrl)
    url.searchParams.set('cb', callbackUrl)
    return url.toString()
}

/** Key of the message of a failed connection attempt. */
export function connectErrorKey(error: unknown): string {
    switch (problemCodeOf(error)) {
        case ProblemCodeObject.ScrobblingToken_invalid:
            return 'scrobbling.errors.tokenInvalid'
        case ProblemCodeObject.ScrobblingService_unavailable:
            return 'scrobbling.errors.serviceUnavailable'
        case ProblemCodeObject.ScrobblingNot_configured:
            return 'scrobbling.errors.notConfigured'
        default:
            return 'scrobbling.errors.connectFailed'
    }
}
