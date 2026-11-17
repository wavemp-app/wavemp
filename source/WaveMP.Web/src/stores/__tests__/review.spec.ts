import { beforeEach, describe, expect, it, vi } from 'vitest'
import { nextTick } from 'vue'
import { createPinia, setActivePinia } from 'pinia'
import type { TrackReview } from '@/api/generated/models'

const api = vi.hoisted(() => ({
    listTrackReviews: vi.fn(),
    getTrackReviewSummary: vi.fn(),
    getTrackReview: vi.fn(),
    setTrackReview: vi.fn(),
    setTrackReviews: vi.fn(),
}))

vi.mock('@/review/api', () => api)

const { useReviewStore } = await import('../review')
const { useSessionStore } = await import('../session')

function item(trackId: string): TrackReview {
    return { trackId, title: trackId, status: 'Unreviewed' }
}

describe('review store', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
        for (const mock of Object.values(api)) {
            mock.mockReset()
        }
    })

    it('loads the number of unreviewed songs', async () => {
        api.getTrackReviewSummary.mockResolvedValue({ unreviewedCount: 7, reviewedCount: 3 })
        const store = useReviewStore()

        await store.refreshCount()

        expect(store.unreviewedCount).toBe(7)
    })

    it('keeps the last number when it cannot be loaded', async () => {
        api.getTrackReviewSummary
            .mockResolvedValueOnce({ unreviewedCount: 7 })
            .mockRejectedValueOnce(new Error('offline'))
        const store = useReviewStore()

        await store.refreshCount()
        await store.refreshCount()

        expect(store.unreviewedCount).toBe(7)
    })

    it('marks a song as reviewed, remembers it and loads the number again', async () => {
        api.setTrackReview.mockResolvedValue({ trackId: 'a', status: 'Reviewed' })
        api.getTrackReviewSummary.mockResolvedValue({ unreviewedCount: 4 })
        const store = useReviewStore()

        await store.markReviewed('a')

        expect(api.setTrackReview).toHaveBeenCalledWith('a', 'Reviewed')
        expect(store.reviewed.has('a')).toBe(true)
        expect(store.unreviewedCount).toBe(4)
    })

    it('does not remember a song whose review failed', async () => {
        api.setTrackReview.mockRejectedValue(new Error('failed'))
        const store = useReviewStore()

        await expect(store.markReviewed('a')).rejects.toThrow('failed')

        expect(store.reviewed.has('a')).toBe(false)
        expect(api.getTrackReviewSummary).not.toHaveBeenCalled()
    })

    it('marks several songs at once', async () => {
        api.setTrackReviews.mockResolvedValue({ changedCount: 2 })
        api.getTrackReviewSummary.mockResolvedValue({ unreviewedCount: 1 })
        const store = useReviewStore()

        await store.markManyReviewed(['a', 'b'])
        await store.markManyReviewed([])

        expect(api.setTrackReviews).toHaveBeenCalledOnce()
        expect(api.setTrackReviews).toHaveBeenCalledWith(['a', 'b'], 'Reviewed')
        expect([...store.reviewed]).toEqual(['a', 'b'])
        expect(store.unreviewedCount).toBe(1)
    })

    it('goes through the list the inbox shows', () => {
        const store = useReviewStore()

        store.setQueue([item('a'), item('b')], { sort: 'Artist' }, 5)

        expect(store.queueIds).toEqual(['a', 'b'])
        expect(store.queueHasMore).toBe(true)
    })

    it('loads the next page after the songs that are still unreviewed', async () => {
        api.setTrackReview.mockResolvedValue({})
        api.getTrackReviewSummary.mockResolvedValue({ unreviewedCount: 3 })
        const store = useReviewStore()
        store.setQueue([item('a'), item('b')], { sort: 'Artist', missingTags: true }, 4)
        await store.markReviewed('a')
        // The server lists b, c and d now; the list holds a (reviewed) and b.
        api.listTrackReviews.mockResolvedValue({ items: [item('c'), item('d')], total: 3 })

        expect(await store.loadMoreQueue()).toBe(true)

        expect(api.listTrackReviews).toHaveBeenCalledWith({
            sort: 'Artist',
            missingTags: true,
            offset: 1,
            limit: 100,
        })
        expect(store.queueIds).toEqual(['a', 'b', 'c', 'd'])
    })

    it('ends the list when the next page brings no new songs', async () => {
        const store = useReviewStore()
        store.setQueue([item('a')], {}, 3)
        api.listTrackReviews.mockResolvedValue({ items: [item('a')], total: 1 })

        expect(await store.loadMoreQueue()).toBe(false)

        expect(store.queueHasMore).toBe(false)
    })

    it('loads the first page for a song opened directly', async () => {
        api.listTrackReviews.mockResolvedValue({ items: [item('a'), item('b')], total: 2 })
        const store = useReviewStore()

        await store.ensureQueue('b')
        await store.ensureQueue('b')

        expect(api.listTrackReviews).toHaveBeenCalledOnce()
        expect(api.listTrackReviews).toHaveBeenCalledWith({ offset: 0, limit: 100 })
        expect(store.queueIds).toEqual(['a', 'b'])
    })

    it('keeps the list of the inbox for a song opened from it', async () => {
        const store = useReviewStore()
        store.setQueue([item('a')], { sort: 'Title' }, 1)

        await store.ensureQueue('a')

        expect(api.listTrackReviews).not.toHaveBeenCalled()
    })

    it('forgets everything when the profile changes', async () => {
        api.getTrackReviewSummary.mockResolvedValue({ unreviewedCount: 2 })
        api.setTrackReview.mockResolvedValue({})
        const store = useReviewStore()
        const session = useSessionStore()
        session.token = 'admin'
        await nextTick()
        store.setQueue([item('a')], {}, 1)
        await store.markReviewed('a')

        session.token = 'other'
        await nextTick()

        expect(store.unreviewedCount).toBeNull()
        expect(store.reviewed.size).toBe(0)
        expect(store.queueIds).toEqual([])
    })
})
