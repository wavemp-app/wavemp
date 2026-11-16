// Scroll positions of the pages. The page itself never scrolls, the content area of the app shell
// does: the scroll behavior of Vue Router only reaches the window, so the router alone would keep
// the offset of the previous page in the content area.

import type { RouteLocationNormalized, RouterScrollBehavior } from 'vue-router'

/** Id of the content area of the app shell, the area in which the pages scroll. */
export const contentAreaId = 'wavemp-content'

/** Where the content area goes after a navigation. */
export type ScrollTarget =
    | { readonly kind: 'top' }
    | { readonly kind: 'restore'; readonly top: number }
    | { readonly kind: 'keep' }

type ScrollRoute = Pick<RouteLocationNormalized, 'path' | 'fullPath'>

/**
 * Back and forward return to the position the page had when it was left; another page starts at
 * its top. Changes of the query or hash alone (sorting, the scope of the library, the search term,
 * the editor of a song) stay on the same page and keep the position.
 */
export function scrollTarget(
    to: ScrollRoute,
    from: ScrollRoute,
    traversed: boolean,
    saved: number | undefined,
): ScrollTarget {
    if (traversed && saved !== undefined) {
        return { kind: 'restore', top: saved }
    }

    return to.path === from.path ? { kind: 'keep' } : { kind: 'top' }
}

export interface ScrollMemoryOptions {
    /** The content area, or null while a page without the app shell is shown. */
    readonly container: () => HTMLElement | null
    /** Identity of the current history entry (the position Vue Router keeps in history.state). */
    readonly entry?: () => unknown
    /** How long a restored position waits for the page to become tall enough, in milliseconds. */
    readonly restoreTimeout?: number
    /** Schedules the next try of a restore. */
    readonly schedule?: (callback: () => void) => void
    readonly now?: () => number
}

export interface ScrollMemory {
    /** Remembers the position of the page that is left: before every navigation. */
    remember(): void
    /** Makes the page of a finished navigation the current one. */
    enter(to: ScrollRoute): void
    /** Scroll behavior of the router for the content area and, for themes that scroll it, the window. */
    readonly scrollBehavior: RouterScrollBehavior
}

/** Number of remembered history entries; older ones are dropped first. */
const maxEntries = 200
/** Interactions with which the user takes over the scrolling while a position is restored. */
const userScrollEvents = ['wheel', 'touchstart', 'pointerdown', 'keydown'] as const

function currentHistoryPosition(): unknown {
    const state: unknown = window.history.state
    return state && typeof state === 'object' && 'position' in state ? state.position : undefined
}

export function createScrollMemory(options: ScrollMemoryOptions): ScrollMemory {
    const entry = options.entry ?? currentHistoryPosition
    const restoreTimeout = options.restoreTimeout ?? 1500
    const schedule = options.schedule ?? ((callback) => requestAnimationFrame(callback))
    const now = options.now ?? (() => performance.now())

    const positions = new Map<string, number>()
    let current: string | null = null
    let stopRestore: (() => void) | null = null

    const keyOf = (route: ScrollRoute) => `${String(entry())}|${route.fullPath}`

    function cancelRestore(): void {
        stopRestore?.()
    }

    /**
     * Sets the position and, while the page is not tall enough yet (a list still loading), tries
     * again on the next frames, until the time is up or the user scrolls himself.
     */
    function restore(container: HTMLElement, top: number): void {
        cancelRestore()
        container.scrollTop = top
        if (Math.abs(container.scrollTop - top) < 1) {
            return
        }

        const deadline = now() + restoreTimeout
        let active = true
        const stop = () => {
            active = false
            stopRestore = null
            for (const type of userScrollEvents) {
                container.removeEventListener(type, stop)
            }
        }
        for (const type of userScrollEvents) {
            container.addEventListener(type, stop, { passive: true })
        }
        stopRestore = stop

        const retry = () => {
            if (!active) {
                return
            }
            container.scrollTop = top
            if (Math.abs(container.scrollTop - top) < 1 || now() >= deadline) {
                stop()
            } else {
                schedule(retry)
            }
        }
        schedule(retry)
    }

    return {
        remember() {
            cancelRestore()
            const container = options.container()
            if (current === null || !container) {
                return
            }

            positions.delete(current)
            positions.set(current, container.scrollTop)
            if (positions.size > maxEntries) {
                positions.delete(positions.keys().next().value!)
            }
        },

        enter(to) {
            current = keyOf(to)
        },

        scrollBehavior(to, from, savedPosition) {
            // Vue Router passes a saved position only for back and forward (and a reload).
            const traversed = savedPosition !== null
            const target = scrollTarget(to, from, traversed, positions.get(keyOf(to)))
            const container = options.container()

            if (target.kind === 'keep') {
                return false
            }

            if (container) {
                if (target.kind === 'restore') {
                    restore(container, target.top)
                } else {
                    container.scrollTop = 0
                }
            }

            return savedPosition ?? { left: 0, top: 0 }
        },
    }
}
