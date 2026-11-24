import { computed, ref, shallowRef } from 'vue'
import type { PageLike } from '@/api/paging'

export type PagedListStatus = 'idle' | 'loading' | 'ready' | 'error'

/** Items loaded per page; the most the server returns. */
export const libraryPageSize = 100

/**
 * A list that loads page by page into a window of consecutive items: reload() loads the first page
 * again, loadMore() appends the next one, jumpTo() loads the page at any position of the list
 * without the pages before it, and loadPrevious() then prepends the page before the window. start
 * is the position of the first loaded item in the whole list. Answers that arrive after a newer
 * reload or jump are dropped, so changing filters quickly never shows an outdated list.
 */
export function usePagedList<T>(
    fetchPage: (offset: number, limit: number) => Promise<PageLike<T> | undefined>,
    pageSize = libraryPageSize,
) {
    const items = shallowRef<T[]>([])
    const total = ref(0)
    const start = ref(0)
    const status = ref<PagedListStatus>('idle')
    const loadingMore = ref(false)
    const loadingPrevious = ref(false)
    let generation = 0

    const hasMore = computed(
        () => status.value === 'ready' && start.value + items.value.length < total.value,
    )
    const hasPrevious = computed(() => status.value === 'ready' && start.value > 0)

    function begin(): number {
        loadingMore.value = false
        loadingPrevious.value = false
        return ++generation
    }

    async function reload(): Promise<void> {
        const current = begin()
        status.value = 'loading'
        try {
            const page = await fetchPage(0, pageSize)
            if (current !== generation) {
                return
            }

            items.value = page?.items ?? []
            start.value = 0
            total.value = page?.total ?? items.value.length
            status.value = 'ready'
        } catch {
            if (current === generation) {
                status.value = 'error'
            }
        }
    }

    /**
     * Shows the list from the position on: the window then holds one page from there. Resolves
     * true when the item at the position is loaded; false if the page could not be loaded (or
     * holds nothing, e.g. because the list got shorter), in which case the window stays as it was.
     */
    async function jumpTo(position: number): Promise<boolean> {
        const target = Math.max(0, Math.floor(position))
        if (
            status.value === 'ready' &&
            target >= start.value &&
            target < start.value + items.value.length
        ) {
            return true
        }

        const current = begin()
        const previous = status.value
        status.value = 'loading'
        try {
            const page = await fetchPage(target, pageSize)
            if (current !== generation) {
                return false
            }

            const loaded = page?.items ?? []
            if (loaded.length === 0 && target > 0) {
                status.value = previous
                return false
            }

            items.value = loaded
            start.value = target
            total.value = page?.total ?? target + loaded.length
            status.value = 'ready'
            return loaded.length > 0
        } catch {
            if (current === generation) {
                status.value = previous
            }
            return false
        }
    }

    async function loadMore(): Promise<void> {
        if (!hasMore.value || loadingMore.value) {
            return
        }

        const current = generation
        loadingMore.value = true
        try {
            const page = await fetchPage(start.value + items.value.length, pageSize)
            if (current !== generation) {
                return
            }

            const more = page?.items ?? []
            items.value = [...items.value, ...more]
            // An empty page ends the list, also if the total changed in the meantime.
            total.value =
                more.length === 0 ? start.value + items.value.length : (page?.total ?? total.value)
        } catch {
            // The list stays as it is; the next attempt loads the same page again.
        } finally {
            if (current === generation) {
                loadingMore.value = false
            }
        }
    }

    /** Prepends the page before the window (or what is left of it before the first page). */
    async function loadPrevious(): Promise<void> {
        if (!hasPrevious.value || loadingPrevious.value) {
            return
        }

        const current = generation
        const offset = Math.max(0, start.value - pageSize)
        loadingPrevious.value = true
        try {
            const page = await fetchPage(offset, start.value - offset)
            if (current !== generation) {
                return
            }

            const earlier = page?.items ?? []
            items.value = [...earlier, ...items.value]
            // An empty page means the list got shorter; the window then counts as its start.
            start.value = earlier.length === 0 ? 0 : offset
            total.value = page?.total ?? total.value
        } catch {
            // The window stays as it is; the next attempt loads the same page again.
        } finally {
            if (current === generation) {
                loadingPrevious.value = false
            }
        }
    }

    return {
        items,
        total,
        start,
        status,
        loadingMore,
        loadingPrevious,
        hasMore,
        hasPrevious,
        reload,
        loadMore,
        loadPrevious,
        jumpTo,
    }
}
