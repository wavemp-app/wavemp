// Identity of the entries of the browser history. Back and forward (also the back button of a
// detail page) return to an entry and find the page it showed, kept alive in the background with
// its loaded data; every new navigation gets a new entry and so a freshly loaded page.

import { readonly, ref, type InjectionKey, type Ref } from 'vue'
import type { Router } from 'vue-router'

export interface HistoryEntries {
    /**
     * Id of the current history entry: the same when back and forward return to the entry or when
     * it is only replaced (sorting, the scope of the library), a new one for every other navigation.
     */
    readonly current: Readonly<Ref<number>>
}

/** Provides the history entries of the app to its components (the pages of the app shell). */
export const historyEntriesKey: InjectionKey<HistoryEntries> = Symbol('historyEntries')

interface KnownEntry {
    readonly id: number
    readonly fullPath: string
}

/** Position of the current entry in the browser history, kept by Vue Router in history.state. */
function routerPosition(router: Router): unknown {
    const state: unknown = router.options.history.state
    return state && typeof state === 'object' && 'position' in state ? state.position : undefined
}

/**
 * Follows the navigations of the router: back and forward (and history.go) come from the history,
 * everything else pushes or replaces an entry.
 */
export function trackHistoryEntries(
    router: Router,
    position: () => unknown = () => routerPosition(router),
): HistoryEntries {
    const current = ref(0)
    const known = new Map<unknown, KnownEntry>()
    let nextId = 0
    let traversing = false
    let lastPosition: unknown = undefined

    // Called for back, forward and history.go before the router navigates.
    router.options.history.listen(() => {
        traversing = true
    })

    router.afterEach((to, _from, failure) => {
        const traversed = traversing
        traversing = false
        if (failure) {
            return
        }

        const at = position()
        const entry = known.get(at)
        if (traversed && entry?.fullPath === to.fullPath) {
            current.value = entry.id
        } else if (!traversed && at !== undefined && at === lastPosition) {
            // Replaced: still the same entry.
        } else {
            current.value = ++nextId
            if (!traversed && typeof at === 'number') {
                // A new entry drops the entries that were forward of it.
                for (const key of known.keys()) {
                    if (typeof key === 'number' && key > at) {
                        known.delete(key)
                    }
                }
            }
        }

        known.set(at, { id: current.value, fullPath: to.fullPath })
        lastPosition = at
    })

    return { current: readonly(current) }
}
