import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter, RouterView } from 'vue-router'
import type { Track, TrackMetadata, TrackReview } from '@/api/generated/models'

const review = vi.hoisted(() => ({
    listTrackReviews: vi.fn(),
    getTrackReviewSummary: vi.fn(),
    getTrackReview: vi.fn(),
    setTrackReview: vi.fn(),
    setTrackReviews: vi.fn(),
}))
const library = vi.hoisted(() => ({
    getTrack: vi.fn(),
    getTrackMetadata: vi.fn(),
    updateTrackMetadata: vi.fn(),
}))

vi.mock('@/review/api', () => review)
vi.mock('@/library/api', () => library)

const { default: AdminReviewView } = await import('../AdminReviewView.vue')
const { createWaveMpI18n } = await import('@/i18n')
const { createRoutes } = await import('@/router/routes')
const { useReviewStore } = await import('@/stores/review')

function item(trackId: string, missingTags: TrackReview['missingTags'] = []): TrackReview {
    return { trackId, title: `Song ${trackId}`, status: 'Unreviewed', missingTags }
}

function track(id: string): Track {
    return { id, title: `Song ${id}`, hasAudio: true, hasCover: false }
}

function metadata(trackId: string): TrackMetadata {
    return {
        trackId,
        status: 'Editable',
        audioFilePath: `Band/${trackId}.mp3`,
        title: `Song ${trackId}`,
        artist: 'Band',
        album: null,
        albumArtist: null,
        year: null,
        trackNumber: null,
        discNumber: null,
        genre: null,
    }
}

async function mountTrackView(id: string) {
    const router = createRouter({ history: createMemoryHistory(), routes: createRoutes() })
    await router.push('/admin/review')
    await router.push(`/admin/review/${id}`)
    // The router view renders the page of the route, so a new song reuses the page like in the app.
    const wrapper = mount(RouterView, {
        global: { plugins: [router, createWaveMpI18n('en')] },
        attachTo: document.body,
    })
    await flushPromises()
    return { wrapper, router }
}

describe('review inbox pages', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
        for (const mock of [...Object.values(review), ...Object.values(library)]) {
            mock.mockReset()
        }
        review.getTrackReviewSummary.mockResolvedValue({ unreviewedCount: 3, reviewedCount: 0 })
        review.getTrackReview.mockImplementation(async (id: string) => ({
            trackId: id,
            status: 'Unreviewed',
        }))
        review.setTrackReview.mockImplementation(async (id: string) => ({
            trackId: id,
            status: 'Reviewed',
        }))
        library.getTrack.mockImplementation(async (id: string) => track(id))
        library.getTrackMetadata.mockImplementation(async (id: string) => metadata(id))
    })

    describe('AdminReviewView', () => {
        async function mountList() {
            const router = createRouter({ history: createMemoryHistory(), routes: createRoutes() })
            await router.push('/admin/review')
            const wrapper = mount(AdminReviewView, {
                global: { plugins: [router, createWaveMpI18n('en')] },
                attachTo: document.body,
            })
            await flushPromises()
            return { wrapper, router }
        }

        it('lists the unreviewed songs added last first and hands them to the song pages', async () => {
            review.listTrackReviews.mockResolvedValue({
                items: [item('a', ['Genre']), item('b')],
                total: 2,
            })

            const { wrapper } = await mountList()

            expect(review.listTrackReviews).toHaveBeenCalledWith({
                sort: 'FirstSeenAt',
                offset: 0,
                limit: 100,
            })
            expect(wrapper.findAll('[data-part="review-item"]')).toHaveLength(2)
            expect(useReviewStore().queueIds).toEqual(['a', 'b'])
            wrapper.unmount()
        })

        it('filters by missing tags and sorts', async () => {
            review.listTrackReviews.mockResolvedValue({ items: [item('a')], total: 1 })
            const { wrapper } = await mountList()

            await wrapper
                .get('[data-item="review-filter"] [data-item="missing-tags"]')
                .trigger('click')
            await flushPromises()
            await wrapper.get('[data-item="review-sort"] [data-item="album"]').trigger('click')
            await flushPromises()

            expect(review.listTrackReviews).toHaveBeenLastCalledWith({
                sort: 'Album',
                missingTags: true,
                offset: 0,
                limit: 100,
            })
            wrapper.unmount()
        })

        it('marks the selected songs as reviewed and keeps them in the list', async () => {
            review.listTrackReviews.mockResolvedValue({
                items: [item('a'), item('b'), item('c')],
                total: 3,
            })
            review.setTrackReviews.mockResolvedValue({ changedCount: 2 })
            const { wrapper } = await mountList()
            review.getTrackReviewSummary.mockResolvedValue({ unreviewedCount: 1 })

            await wrapper.get('input[aria-label="Select Song a"]').setValue(true)
            await wrapper.get('input[aria-label="Select Song c"]').setValue(true)
            await wrapper.get('[data-item="mark-selected-reviewed"]').trigger('click')
            await flushPromises()

            expect(review.setTrackReviews).toHaveBeenCalledWith(['a', 'c'], 'Reviewed')
            expect(
                wrapper
                    .findAll('[data-part="review-item"]')
                    .map((row) => row.attributes('data-state')),
            ).toEqual(['reviewed', 'unreviewed', 'reviewed'])
            expect(wrapper.get('[data-field="selected-count"]').text()).toBe('0 selected')
            expect(useReviewStore().unreviewedCount).toBe(1)
            wrapper.unmount()
        })

        it('says when every song is reviewed', async () => {
            review.listTrackReviews.mockResolvedValue({ items: [], total: 0 })

            const { wrapper } = await mountList()

            expect(wrapper.get('[data-part="empty-state"]').text()).toBe('All songs are reviewed.')
            wrapper.unmount()
        })
    })

    describe('AdminReviewTrackView', () => {
        beforeEach(() => {
            useReviewStore().setQueue([item('a', ['Year']), item('b'), item('c')], {}, 3)
        })

        it('shows the song in the metadata editor with its place in the list', async () => {
            const { wrapper } = await mountTrackView('a')

            expect(wrapper.get('[data-part="detail-title"]').text()).toBe('Song a')
            expect(wrapper.get('[data-field="review-status"]').text()).toContain('Not reviewed yet')
            expect(wrapper.get('[data-part="review-tags"]').text()).toContain('Year')
            expect(wrapper.get('[data-field="position"]').text()).toBe('Song 1 of 3')
            expect(wrapper.get('[data-field="title"] input').element).toHaveProperty(
                'value',
                'Song a',
            )
            wrapper.unmount()
        })

        it('saves, marks the song as reviewed and opens the next one in its place', async () => {
            library.updateTrackMetadata.mockImplementation(async (id: string) => ({
                ...metadata(id),
                genre: 'Rock',
            }))
            const { wrapper, router } = await mountTrackView('a')

            await wrapper.get('[data-field="genre"] input').setValue('Rock')
            await wrapper.get('form').trigger('submit')
            await flushPromises()

            expect(library.updateTrackMetadata).toHaveBeenCalledWith('a', { genre: 'Rock' })
            expect(review.setTrackReview).toHaveBeenCalledWith('a', 'Reviewed')
            expect(router.currentRoute.value.fullPath).toBe('/admin/review/b')
            // The song replaced the history entry, so back returns to the list.
            router.back()
            await flushPromises()
            expect(router.currentRoute.value.name).toBe('admin-review')
            wrapper.unmount()
        })

        it('marks the song as reviewed without a request for unchanged tags', async () => {
            const { wrapper, router } = await mountTrackView('a')

            await wrapper.get('form').trigger('submit')
            await flushPromises()

            expect(library.updateTrackMetadata).not.toHaveBeenCalled()
            expect(review.setTrackReview).toHaveBeenCalledWith('a', 'Reviewed')
            expect(router.currentRoute.value.fullPath).toBe('/admin/review/b')
            wrapper.unmount()
        })

        it('skips a song without marking it and never goes back to it', async () => {
            const { wrapper, router } = await mountTrackView('a')

            await wrapper.get('[data-item="skip"]').trigger('click')
            await flushPromises()
            expect(router.currentRoute.value.fullPath).toBe('/admin/review/b')

            await wrapper.get('[data-item="mark-reviewed"]').trigger('click')
            await flushPromises()
            expect(router.currentRoute.value.fullPath).toBe('/admin/review/c')
            expect(review.setTrackReview).toHaveBeenCalledOnce()
            expect(review.setTrackReview).toHaveBeenCalledWith('b', 'Reviewed')
            wrapper.unmount()
        })

        it('says it is done after the last unreviewed song', async () => {
            const { wrapper, router } = await mountTrackView('c')

            await wrapper.get('[data-item="mark-reviewed"]').trigger('click')
            await flushPromises()

            expect(router.currentRoute.value.fullPath).toBe('/admin/review/c')
            expect(wrapper.get('[data-part="empty-state"]').attributes('data-state')).toBe(
                'finished',
            )
            wrapper.unmount()
        })

        it('keeps the song when the review cannot be saved', async () => {
            review.setTrackReview.mockRejectedValue(new Error('failed'))
            const { wrapper, router } = await mountTrackView('a')

            await wrapper.get('[data-item="mark-reviewed"]').trigger('click')
            await flushPromises()

            expect(router.currentRoute.value.fullPath).toBe('/admin/review/a')
            expect(wrapper.get('[data-part="alert"]').text()).toContain(
                'The review could not be saved.',
            )
            wrapper.unmount()
        })

        it('goes to the previous and next song without changing anything', async () => {
            const { wrapper, router } = await mountTrackView('b')

            await wrapper.get('[data-item="previous"]').trigger('click')
            await flushPromises()
            expect(router.currentRoute.value.fullPath).toBe('/admin/review/a')

            await wrapper.get('[data-item="next"]').trigger('click')
            await flushPromises()
            await wrapper.get('[data-item="next"]').trigger('click')
            await flushPromises()
            expect(router.currentRoute.value.fullPath).toBe('/admin/review/c')
            expect(wrapper.get('[data-item="next"]').attributes('disabled')).toBeDefined()
            expect(review.setTrackReview).not.toHaveBeenCalled()
            wrapper.unmount()
        })
    })
})
