import { ref, shallowRef } from 'vue'
import type { ListGroup, ListGroupPage } from '@/api/generated/models'
import { fetchAllPages } from '@/api/paging'

/** Groups loaded per request; the most the group endpoints return. */
export const groupPageSize = 1000

export type ListGroupsStatus = 'idle' | 'loading' | 'ready' | 'error'

/**
 * All groups of a long list (GET /api/v1/tracks/groups and the like) with the position of their
 * first entry, so a list that loads page by page can show and jump to every group without loading
 * the entries. load() fetches all pages of groups once, when they are first needed (e.g. when the
 * overview of the groups opens); invalidate() forgets them when the list loads again (new search,
 * filter or sort order). Answers for a forgotten list are dropped.
 */
export function useListGroups(
    fetchPage: (offset: number, limit: number) => Promise<ListGroupPage | undefined>,
    pageSize = groupPageSize,
) {
    const groups = shallowRef<ListGroup[]>([])
    const status = ref<ListGroupsStatus>('idle')
    let generation = 0
    let pending: Promise<void> | null = null

    async function fetchAll(current: number): Promise<void> {
        try {
            const all = await fetchAllPages<ListGroup>(fetchPage, pageSize)
            if (current === generation) {
                groups.value = all
                status.value = 'ready'
            }
        } catch {
            if (current === generation) {
                status.value = 'error'
            }
        } finally {
            if (current === generation) {
                pending = null
            }
        }
    }

    /** Loads the groups unless they are loaded or loading; after an error it tries again. */
    function load(): Promise<void> {
        if (status.value === 'ready') {
            return Promise.resolve()
        }

        if (!pending) {
            status.value = 'loading'
            pending = fetchAll(generation)
        }

        return pending
    }

    /** Forgets the groups of the list; the next load() fetches them again. */
    function invalidate(): void {
        generation++
        pending = null
        groups.value = []
        status.value = 'idle'
    }

    /** The group the entry at the position belongs to, if the groups are loaded. */
    function groupAt(position: number): ListGroup | undefined {
        const list = groups.value
        let low = 0
        let high = list.length - 1
        while (low <= high) {
            const middle = Math.floor((low + high) / 2)
            const group = list[middle]
            if (!group) {
                return undefined
            }

            const first = group.position ?? 0
            if (position < first) {
                high = middle - 1
            } else if (position >= first + (group.count ?? 0)) {
                low = middle + 1
            } else {
                return group
            }
        }

        return undefined
    }

    return { groups, status, load, invalidate, groupAt }
}
