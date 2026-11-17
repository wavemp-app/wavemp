import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { ref } from 'vue'
import { createMemoryHistory, createRouter } from 'vue-router'
import type { TrackFileSummary } from '@/api/generated/models'

const api = vi.hoisted(() => ({
    listFileSelections: vi.fn(),
    getTrackFileSelection: vi.fn(),
    setTrackFileSelection: vi.fn(),
    resetTrackFileSelection: vi.fn(),
}))

vi.mock('@/admin/fileSelectionApi', () => ({ ...api, fileSelectionRevision: ref(0) }))

const { default: AdminFilesView } = await import('../AdminFilesView.vue')
const { createWaveMpI18n } = await import('@/i18n')
const { createRoutes } = await import('@/router/routes')
const { searchDelay } = await import('@/composables/useSearchField')

const song: TrackFileSummary = {
    trackId: 'track-1',
    title: 'Song',
    artist: { id: 'band', name: 'Band' },
    album: { id: 'album', title: 'Album' },
    directoryPath: 'Band/Album',
    baseName: '01 Song',
    audioCount: 2,
    videoCount: 1,
    coverCount: 1,
    audio: 'Selected',
    video: 'Automatic',
    cover: 'Missing',
}

async function mountView() {
    const router = createRouter({ history: createMemoryHistory(), routes: createRoutes() })
    await router.push('/admin/files')
    const wrapper = mount(AdminFilesView, {
        global: { plugins: [router, createWaveMpI18n('en')] },
        attachTo: document.body,
    })
    await flushPromises()
    return { wrapper, router }
}

describe('AdminFilesView', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
        api.listFileSelections.mockReset().mockResolvedValue({ items: [song], total: 1 })
    })

    afterEach(() => {
        vi.useRealTimers()
        document.body.innerHTML = ''
    })

    it('lists the songs with their counts and selection states', async () => {
        const { wrapper } = await mountView()

        expect(api.listFileSelections).toHaveBeenCalledWith({
            offset: 0,
            limit: 100,
            filter: 'All',
            sort: 'Path',
        })
        expect(wrapper.get('[data-part="admin-nav-item"][aria-current="page"]').text()).toBe(
            'Files',
        )
        const item = wrapper.get('[data-part="file-selection-item"]')
        expect(item.attributes('data-state')).toBe('selected missing choices')
        expect(item.get('[data-part="file-selection-title"]').attributes('href')).toBe(
            '/admin/files/track-1',
        )
        expect(item.get('[data-part="file-path"]').text()).toBe('Band/Album/01 Song')
        expect(
            item.findAll('[data-part="badge"]').map((badge) => badge.attributes('data-state')),
        ).toEqual(['selected audio', 'missing cover'])
        expect(item.get('[data-field="audio"]').text()).toBe('Audio: 2')
        wrapper.unmount()
    })

    it('loads the list again for another filter, sort order and search', async () => {
        const { wrapper } = await mountView()

        await wrapper.get('[data-item="file-filter"] [data-item="several-audio"]').trigger('click')
        await flushPromises()
        expect(api.listFileSelections).toHaveBeenLastCalledWith(
            expect.objectContaining({ filter: 'SeveralAudio', sort: 'Path' }),
        )

        await wrapper.get('[data-item="file-sort"] [data-item="file-count"]').trigger('click')
        await flushPromises()
        expect(api.listFileSelections).toHaveBeenLastCalledWith(
            expect.objectContaining({ filter: 'SeveralAudio', sort: 'FileCount' }),
        )

        vi.useFakeTimers()
        await wrapper.get('[data-part="search"] input').setValue('  song ')
        vi.advanceTimersByTime(searchDelay)
        await flushPromises()
        expect(api.listFileSelections).toHaveBeenLastCalledWith(
            expect.objectContaining({ q: 'song' }),
        )
        wrapper.unmount()
    })

    it('tells when no song matches the filter', async () => {
        api.listFileSelections.mockResolvedValue({ items: [], total: 0 })
        const { wrapper } = await mountView()

        expect(wrapper.get('[data-part="empty-state"]').text()).toBe('There are no songs yet.')

        await wrapper.get('[data-item="file-filter"] [data-item="missing"]').trigger('click')
        await flushPromises()
        expect(wrapper.get('[data-part="empty-state"]').text()).toBe('No songs match the filter.')
        wrapper.unmount()
    })

    it('offers to try again after an error', async () => {
        api.listFileSelections.mockRejectedValueOnce(new Error('offline'))
        const { wrapper } = await mountView()

        expect(wrapper.get('[data-part="empty-state-title"]').text()).toBe(
            'The files could not be loaded.',
        )
        await wrapper.get('[data-part="empty-state"] button').trigger('click')
        await flushPromises()
        expect(wrapper.findAll('[data-part="file-selection-item"]')).toHaveLength(1)
        wrapper.unmount()
    })
})
