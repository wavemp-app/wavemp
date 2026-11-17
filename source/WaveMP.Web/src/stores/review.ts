import { defineStore } from 'pinia'
import { computed, ref, shallowRef, watch } from 'vue'
import { TrackReviewStatusObject, type TrackReview } from '@/api/generated/models'
import { libraryPageSize } from '@/composables/usePagedList'
import {
    getTrackReviewSummary,
    listTrackReviews,
    setTrackReview,
    setTrackReviews,
    type TrackReviewQuery,
} from '@/review/api'
import { isReviewed } from '@/review/inbox'
import { useSessionStore } from './session'

/**
 * The review inbox of the admins: the number of unreviewed songs for the badge, the songs marked
 * as reviewed in this tab and the list the inbox showed last, through which the page of a song
 * goes forward and back. The list keeps the songs that were reviewed meanwhile (marked as
 * reviewed), so a list kept in the background keeps its position.
 */
export const useReviewStore = defineStore('review', () => {
    const session = useSessionStore()

    /** Number of unreviewed songs; null until it is loaded or after it failed. */
    const unreviewedCount = ref<number | null>(null)
    /** Songs marked as reviewed in this tab. */
    const reviewed = shallowRef<ReadonlySet<string>>(new Set())
    /** The songs of the list the inbox showed last, in its order. */
    const queue = shallowRef<TrackReview[]>([])
    const queueTotal = ref(0)
    let queueQuery: TrackReviewQuery | null = null
    let generation = 0

    const queueIds = computed(() => queue.value.map((item) => item.trackId ?? ''))
    const queueHasMore = computed(() => queue.value.length < queueTotal.value)

    /** Loads the number of unreviewed songs again. */
    async function refreshCount(): Promise<void> {
        const current = generation
        try {
            const summary = await getTrackReviewSummary()
            if (current === generation) {
                unreviewedCount.value = summary?.unreviewedCount ?? null
            }
        } catch {
            // The badge keeps the last number; the next refresh tries again.
        }
    }

    /** The list the inbox shows now, with the query it was loaded with. */
    function setQueue(items: readonly TrackReview[], query: TrackReviewQuery, total: number): void {
        queue.value = [...items]
        queueQuery = { ...query }
        queueTotal.value = total
    }

    /**
     * Makes sure the list contains the song, e.g. for a page opened directly: without a list, the
     * first page of the unreviewed songs in the default order is loaded.
     */
    async function ensureQueue(trackId: string): Promise<void> {
        if (queueIds.value.includes(trackId) || queueQuery) {
            return
        }

        const query: TrackReviewQuery = {}
        const page = await listTrackReviews({ ...query, offset: 0, limit: libraryPageSize })
        if (!queueQuery) {
            setQueue(page?.items ?? [], query, page?.total ?? 0)
        }
    }

    /**
     * Appends the next page of the list. Songs reviewed since the list was loaded have left the
     * unreviewed songs of the server, so the next page starts after the songs that are still
     * unreviewed. Returns whether new songs came.
     */
    async function loadMoreQueue(): Promise<boolean> {
        if (!queueQuery || !queueHasMore.value) {
            return false
        }

        const query = queueQuery
        const offset = queue.value.filter((item) => !isReviewed(item, reviewed.value)).length
        const page = await listTrackReviews({ ...query, offset, limit: libraryPageSize })
        if (query !== queueQuery) {
            return false
        }

        const known = new Set(queueIds.value)
        const more = (page?.items ?? []).filter((item) => !known.has(item.trackId ?? ''))
        queue.value = [...queue.value, ...more]
        queueTotal.value = more.length === 0 ? queue.value.length : queueTotal.value
        return more.length > 0
    }

    function remember(ids: readonly string[]): void {
        reviewed.value = new Set([...reviewed.value, ...ids])
    }

    /** Marks one song as reviewed and loads the badge again. */
    async function markReviewed(trackId: string): Promise<void> {
        await setTrackReview(trackId, TrackReviewStatusObject.Reviewed)
        remember([trackId])
        await refreshCount()
    }

    /** Marks several songs as reviewed at once and loads the badge again. */
    async function markManyReviewed(trackIds: readonly string[]): Promise<void> {
        if (trackIds.length === 0) {
            return
        }

        await setTrackReviews(trackIds, TrackReviewStatusObject.Reviewed)
        remember(trackIds)
        await refreshCount()
    }

    /** Forgets everything, e.g. when the profile changes. */
    function clear(): void {
        generation++
        unreviewedCount.value = null
        reviewed.value = new Set()
        queue.value = []
        queueTotal.value = 0
        queueQuery = null
    }

    // The inbox belongs to the admin of the session.
    watch(
        () => session.token,
        () => clear(),
    )

    return {
        unreviewedCount,
        reviewed,
        queue,
        queueIds,
        queueHasMore,
        refreshCount,
        setQueue,
        ensureQueue,
        loadMoreQueue,
        markReviewed,
        markManyReviewed,
        clear,
    }
})
