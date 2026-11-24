// View option of the lists of songs: grouped by album (the default) or a plain list. The choice is
// part of the query of the route (?view=list), like the sort order, so it survives back, forward
// and a reload and can be linked. It is offered only where a theme switches on the layout option
// --wmp-layout-view-options (THEMING.md, section "Layout options"); otherwise the lists stay grouped.

import type { LocationQuery, LocationQueryRaw } from 'vue-router'
import type { SortRoute } from './sorting'

export type ListView = 'grouped' | 'list'

/** The views in the order of the menu; the first one is the default. */
export const listViews: readonly ListView[] = ['grouped', 'list']

export const defaultListView: ListView = 'grouped'

/** The kind of list the view options apply to on a page. */
export type ViewList = 'tracks'

/**
 * The list that the view options of the toolbar apply to on the current page, or null where there
 * is none: the pages that show the songs of the library page by page (songs, recently added, an
 * artist, a genre, a year). The album page and the search show short lists of their own.
 */
export function viewContext(route: SortRoute): ViewList | null {
    switch (route.name) {
        case 'library-section': {
            const section = String(route.params.section)
            return section === 'songs' || section === 'recently-added' ? 'tracks' : null
        }
        case 'library-artist':
        case 'library-genre':
        case 'library-year':
            return 'tracks'
        default:
            return null
    }
}

/** The view of the route; a missing or unknown value means the default. */
export function parseListView(query: LocationQuery): ListView {
    const value = Array.isArray(query.view) ? query.view[0] : query.view
    return value === 'list' ? 'list' : defaultListView
}

/** The query of the route with the view; the default view leaves the parameter out. */
export function withListView(query: LocationQuery, view: ListView): LocationQueryRaw {
    const next: LocationQueryRaw = { ...query }
    if (view === defaultListView) {
        delete next.view
    } else {
        next.view = view
    }

    return next
}

/** Id of the menu entry of a view, e.g. "view-list" (data-item). */
export function viewItemId(view: ListView): `view-${ListView}` {
    return `view-${view}`
}
