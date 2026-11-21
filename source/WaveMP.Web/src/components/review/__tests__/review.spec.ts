import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import type { Component } from 'vue'
import type { TrackReview } from '@/api/generated/models'
import { createWaveMpI18n } from '@/i18n'
import { createRoutes } from '@/router/routes'

const api = vi.hoisted(() => ({
    listTrackReviews: vi.fn(),
    getTrackReviewSummary: vi.fn(),
    getTrackReview: vi.fn(),
    setTrackReview: vi.fn(),
    setTrackReviews: vi.fn(),
}))

vi.mock('@/review/api', () => api)

const { default: ReviewInboxButton } = await import('../ReviewInboxButton.vue')
const { default: ReviewList } = await import('../ReviewList.vue')
const { default: ReviewBulkBar } = await import('../ReviewBulkBar.vue')
const { default: ReviewFilters } = await import('../ReviewFilters.vue')
const { default: ReviewActions } = await import('../ReviewActions.vue')
const { default: ReviewNav } = await import('../ReviewNav.vue')
const { useReviewStore } = await import('@/stores/review')
const { useSessionStore } = await import('@/stores/session')

async function mountWithRouter(
    component: Component,
    props: Record<string, unknown> = {},
    path = '/',
) {
    const router = createRouter({ history: createMemoryHistory(), routes: createRoutes() })
    await router.push(path)
    const wrapper = mount(component, {
        props,
        global: { plugins: [router, createWaveMpI18n('en')] },
    })
    await flushPromises()
    return { wrapper, router }
}

const items: TrackReview[] = [
    {
        trackId: 'a',
        title: 'First',
        artist: { id: 'artist', name: 'Artist' },
        album: { id: 'album', title: 'Album' },
        directoryPath: 'Artist/Album',
        baseName: '01 First',
        hasCover: false,
        firstSeenAt: new Date(Date.UTC(2026, 10, 16)),
        missingTags: ['Year', 'Genre'],
        status: 'Unreviewed',
    },
    {
        trackId: 'b',
        title: 'Second',
        artist: null,
        album: null,
        directoryPath: '',
        baseName: 'Second',
        hasCover: false,
        missingTags: [],
        status: 'Unreviewed',
    },
]

describe('ReviewInboxButton', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
        for (const mock of Object.values(api)) {
            mock.mockReset()
        }
    })

    it('shows the number of unreviewed songs of the session as badge', async () => {
        api.getTrackReviewSummary.mockResolvedValue({ unreviewedCount: 12, reviewedCount: 0 })
        useSessionStore().token = 'admin'

        const { wrapper } = await mountWithRouter(ReviewInboxButton)

        const button = wrapper.get('a[data-part="btn-review-inbox"]')
        expect(button.attributes('href')).toBe('/admin/review')
        expect(button.attributes('data-state')).toBe('pending')
        expect(button.attributes('aria-label')).toBe('Review, 12 unreviewed songs')
        expect(button.get('[data-part="icon"]').attributes('data-icon')).toBe('review-inbox')
        expect(button.get('[data-part="badge"]').text()).toBe('12')
        wrapper.unmount()
    })

    it('shows no badge while every song is reviewed and marks the pages of the inbox', async () => {
        api.getTrackReviewSummary.mockResolvedValue({ unreviewedCount: 0, reviewedCount: 4 })
        useSessionStore().token = 'admin'

        const { wrapper } = await mountWithRouter(ReviewInboxButton, {}, '/admin/review/a')

        const button = wrapper.get('[data-part="btn-review-inbox"]')
        expect(button.find('[data-part="badge"]').exists()).toBe(false)
        expect(button.attributes('data-state')).toBe('empty active')
        expect(button.attributes('aria-current')).toBe('page')
        wrapper.unmount()
    })

    it('does not load the number for previews', async () => {
        useReviewStore().unreviewedCount = 3

        const { wrapper } = await mountWithRouter(ReviewInboxButton, { poll: false })

        expect(api.getTrackReviewSummary).not.toHaveBeenCalled()
        expect(wrapper.get('[data-part="badge"]').text()).toBe('3')
        wrapper.unmount()
    })
})

describe('ReviewList', () => {
    beforeEach(() => setActivePinia(createPinia()))

    it('shows cover, title, artist, album and the missing tags of each song', async () => {
        const { wrapper } = await mountWithRouter(ReviewList, {
            items,
            reviewed: new Set(),
            selected: new Set(['a']),
            label: 'Songs',
        })

        const rows = wrapper.findAll('[data-part="review-item"]')
        expect(rows).toHaveLength(2)
        expect(rows[0]!.attributes('data-state')).toBe('unreviewed selected missing-tags')
        expect(rows[0]!.find('[data-part="cover"]').exists()).toBe(true)
        expect(rows[0]!.get('[data-part="review-title"]').attributes('href')).toBe(
            '/admin/review/a',
        )
        expect(rows[0]!.get('[data-field="context"]').text()).toBe('Artist · Album')
        expect(rows[0]!.get('[data-part="file-path"]').text()).toBe('Artist/Album/01 First')
        expect(
            rows[0]!
                .findAll('[data-state="missing-tag"]')
                .map((badge) => [badge.attributes('data-field'), badge.text()]),
        ).toEqual([
            ['year', 'Year'],
            ['genre', 'Genre'],
        ])
        expect(rows[1]!.attributes('data-state')).toBe('unreviewed')
        expect(rows[1]!.find('[data-part="review-tags"]').exists()).toBe(false)
        expect(rows[1]!.find('[data-field="context"]').exists()).toBe(false)
        wrapper.unmount()
    })

    it('marks reviewed songs and does not let them be selected', async () => {
        const { wrapper } = await mountWithRouter(ReviewList, {
            items,
            reviewed: new Set(['b']),
            selected: new Set(),
            label: 'Songs',
        })

        const row = wrapper.findAll('[data-part="review-item"]')[1]!
        expect(row.attributes('data-state')).toBe('reviewed')
        expect(row.get('[data-state="reviewed"]').text()).toBe('Reviewed')
        expect(row.get('input[data-part="checkbox"]').attributes('disabled')).toBeDefined()
        expect(row.get('[data-part="review-select"]').attributes('data-state')).toBe('disabled')
        wrapper.unmount()
    })

    it('puts the checkbox in an area of its own that takes the touch', async () => {
        const { wrapper } = await mountWithRouter(ReviewList, {
            items,
            reviewed: new Set(),
            selected: new Set(),
            label: 'Songs',
        })

        const area = wrapper.findAll('[data-part="review-select"]')[0]!
        expect(area.element.tagName).toBe('LABEL')
        expect(area.attributes('data-state')).toBeUndefined()
        expect(area.get('input[data-part="checkbox"]').attributes('aria-label')).toBe(
            'Select First',
        )
        wrapper.unmount()
    })

    it('tells which song was selected', async () => {
        const { wrapper } = await mountWithRouter(ReviewList, {
            items,
            reviewed: new Set(),
            selected: new Set(),
            label: 'Songs',
        })

        const checkbox = wrapper.get('input[aria-label="Select First"]')
        await checkbox.setValue(true)

        expect(wrapper.emitted('select')).toEqual([['a', true]])
        wrapper.unmount()
    })
})

describe('ReviewBulkBar', () => {
    it('marks the selected songs and selects all of them', async () => {
        const wrapper = mount(ReviewBulkBar, {
            props: { selectedCount: 2, selectableCount: 5 },
            global: { plugins: [createWaveMpI18n('en')] },
        })

        expect(wrapper.get('[data-part="review-bulk"]').attributes('data-state')).toBe('selected')
        expect(wrapper.get('[data-field="selected-count"]').text()).toBe('2 selected')
        await wrapper.get('[data-item="mark-selected-reviewed"]').trigger('click')
        await wrapper.get('input[type="checkbox"]').setValue(true)

        expect(wrapper.emitted('markReviewed')).toHaveLength(1)
        expect(wrapper.emitted('selectAll')).toEqual([[true]])
    })

    it('offers nothing without a selection', () => {
        const wrapper = mount(ReviewBulkBar, {
            props: { selectedCount: 0, selectableCount: 0 },
            global: { plugins: [createWaveMpI18n('en')] },
        })

        expect(wrapper.get('[data-part="review-bulk"]').attributes('data-state')).toBe('empty')
        expect(
            wrapper.get('[data-item="mark-selected-reviewed"]').attributes('disabled'),
        ).toBeDefined()
        expect(wrapper.get('input[type="checkbox"]').attributes('disabled')).toBeDefined()
    })
})

describe('ReviewFilters', () => {
    it('offers the filters and sort orders as segmented controls', async () => {
        const wrapper = mount(ReviewFilters, {
            props: { search: '', filter: 'all', sort: 'FirstSeenAt' },
            global: { plugins: [createWaveMpI18n('en')] },
        })

        const filter = wrapper.get('[data-item="review-filter"]')
        expect(filter.attributes('role')).toBe('radiogroup')
        expect(filter.findAll('[data-part="segment"]').map((segment) => segment.text())).toEqual([
            'All',
            'Missing tags',
            'Complete tags',
        ])
        const sort = wrapper.get('[data-item="review-sort"]')
        expect(sort.findAll('[data-part="segment"]').map((segment) => segment.text())).toEqual([
            'Date added',
            'Artist',
            'Album',
            'Title',
        ])

        await filter.get('[data-item="missing-tags"]').trigger('click')
        await sort.get('[data-item="artist"]').trigger('click')

        expect(wrapper.emitted('update:filter')).toEqual([['missing']])
        expect(wrapper.emitted('update:sort')).toEqual([['Artist']])
    })
})

describe('ReviewActions and ReviewNav', () => {
    it('submit the editor, mark as reviewed and skip', async () => {
        const wrapper = mount(ReviewActions, {
            props: { formId: 'editor', canSave: false },
            global: { plugins: [createWaveMpI18n('en')] },
        })

        const save = wrapper.get('[data-item="save-and-review"]')
        expect(save.attributes('type')).toBe('submit')
        expect(save.attributes('form')).toBe('editor')
        expect(save.attributes('disabled')).toBeDefined()
        await wrapper.get('[data-item="mark-reviewed"]').trigger('click')
        await wrapper.get('[data-item="skip"]').trigger('click')

        expect(wrapper.emitted('markReviewed')).toHaveLength(1)
        expect(wrapper.emitted('skip')).toHaveLength(1)
    })

    it('go to the previous and next song and tell the position', async () => {
        const wrapper = mount(ReviewNav, {
            props: { index: 0, count: 3, hasPrevious: false, hasNext: true },
            global: { plugins: [createWaveMpI18n('en')] },
        })

        expect(wrapper.get('[data-field="position"]').text()).toBe('Song 1 of 3')
        expect(wrapper.get('[data-item="previous"]').attributes('disabled')).toBeDefined()
        await wrapper.get('[data-item="next"]').trigger('click')

        expect(wrapper.emitted('next')).toHaveLength(1)
        await wrapper.setProps({ index: -1 })
        expect(wrapper.find('[data-field="position"]').exists()).toBe(false)
    })
})
