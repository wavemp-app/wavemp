// The back button of the detail pages: back to the previous page of the app, where back and
// forward restore its position and its loaded list; opened directly (a link from outside, a new
// tab), there is no previous page, so it leads to the overview the page belongs to.

import type { RouteLocationRaw, Router } from 'vue-router'
import { historyAvailability } from './history'

/** What the back button does: back in the history, to the overview, or close (e.g. an editor). */
export type BackAction =
    | { readonly kind: 'history' }
    | { readonly kind: 'route'; readonly to: RouteLocationRaw }
    | { readonly kind: 'close' }

export interface BackOptions {
    /** The overview the page belongs to, for pages opened directly. */
    readonly fallback?: RouteLocationRaw
    /** Whether the previous page of the history may be used; false closes or opens the fallback. */
    readonly history?: boolean
}

/** history.state of Vue Router: back holds the previous entry of the app, if there is one. */
export function backAction(state: unknown, options: BackOptions): BackAction {
    if ((options.history ?? true) && historyAvailability(state).back) {
        return { kind: 'history' }
    }

    return options.fallback ? { kind: 'route', to: options.fallback } : { kind: 'close' }
}

/** Goes back; returns false if the caller has to close itself instead. */
export function goBack(
    router: Pick<Router, 'back' | 'push'>,
    options: BackOptions,
    state: unknown = window.history.state,
): boolean {
    const action = backAction(state, options)
    switch (action.kind) {
        case 'history':
            router.back()
            return true
        case 'route':
            void router.push(action.to)
            return true
        case 'close':
            return false
    }
}
