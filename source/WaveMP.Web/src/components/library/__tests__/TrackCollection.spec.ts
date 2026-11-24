import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import type { Track } from '@/api/generated/models'
import { createWaveMpI18n } from '@/i18n'
import { createRoutes } from '@/router/routes'

const api = vi.hoisted(() => ({
    listTracks: vi.fn(),
    fetchAlbumCover: vi.fn(),
    fetchTrackCover: vi.fn(),
}))

vi.mock('@/library/api', () => api)

const { default: TrackCollection } = await import('../TrackCollection.vue')
const { defaultLayoutOptions, layoutOptions } = await import('@/themes/layoutOptions')

const artist = { id: 'artist', name: 'Long Artist' }

/** Song n of a long list: ten songs per album. */
function song(index: number): Track {
    const album = Math.floor(index / 10)
    return {
        id: `song-${index}`,
        title: `Song ${index}`,
        artist,
        album: { id: `album-${album}`, title: `Album ${album}`, artist, isCompilation: false },
        trackNumber: (index % 10) + 1,
        hasAudio: true,
        hasVideo: false,
        hasCover: false,
    }
}

/** A list of 150 songs, answered page by page. */
function serveLongList(): void {
    api.listTracks.mockImplementation(({ offset, limit }: { offset: number; limit: number }) => {
        const items = []
        for (let index = offset; index < Math.min(offset + limit, 150); index++) {
            items.push(song(index))
        }
        return Promise.resolve({ items, offset, limit, total: 150 })
    })
}

function stubDesktop(desktop: boolean): void {
    vi.stubGlobal(
        'matchMedia',
        (query: string) =>
            ({
                matches: query === '(width >= 64rem)' && desktop,
                addEventListener: () => undefined,
                removeEventListener: () => undefined,
            }) as unknown as MediaQueryList,
    )
}

async function mountCollection(path: string) {
    const router = createRouter({ history: createMemoryHistory(), routes: createRoutes() })
    await router.push(path)
    const wrapper = mount(TrackCollection, {
        props: { label: 'Songs' },
        global: { plugins: [router, createWaveMpI18n('en')] },
        attachTo: document.body,
    })
    await flushPromises()
    return { wrapper, router }
}

type Wrapper = Awaited<ReturnType<typeof mountCollection>>['wrapper']

function listState(wrapper: Wrapper): string[] {
    return (wrapper.get('[data-part="list"]').attributes('data-state') ?? '').split(' ')
}

async function loadMore(wrapper: Wrapper): Promise<void> {
    await wrapper.get('[data-part="load-more"]').trigger('click')
    await flushPromises()
}

describe('TrackCollection', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
        api.listTracks.mockReset()
        serveLongList()
    })

    afterEach(() => {
        vi.unstubAllGlobals()
        document.documentElement.style.removeProperty('--wmp-layout-view-options')
        layoutOptions.value = defaultLayoutOptions
        document.body.innerHTML = ''
    })

    it('groups the songs by album across the pages it loads', async () => {
        stubDesktop(true)
        const { wrapper } = await mountCollection('/library/songs')

        expect(wrapper.findAll('[data-part="list-group"]')).toHaveLength(10)
        expect(wrapper.findAll('[data-part="list-group-info"]')).toHaveLength(10)
        expect(listState(wrapper)).not.toContain('ungrouped')

        await loadMore(wrapper)
        expect(api.listTracks).toHaveBeenLastCalledWith(expect.objectContaining({ offset: 100 }))
        expect(wrapper.findAll('[data-part="list-row"]')).toHaveLength(150)
        expect(wrapper.findAll('[data-part="list-group"]')).toHaveLength(15)
        expect(wrapper.find('[data-part="load-more"]').exists()).toBe(false)
        wrapper.unmount()
    })

    it('ignores the view of the route while the theme does not offer the view options', async () => {
        stubDesktop(true)
        const { wrapper } = await mountCollection('/library/songs?view=list')

        expect(wrapper.findAll('[data-part="list-group-header"]')).toHaveLength(10)
        expect(wrapper.findAll('[data-part="list-group-info"]')).toHaveLength(10)
        expect(listState(wrapper)).not.toContain('ungrouped')
        wrapper.unmount()
    })

    it('ignores the view of the route on phones', async () => {
        stubDesktop(false)
        document.documentElement.style.setProperty('--wmp-layout-view-options', 'on')
        const { wrapper } = await mountCollection('/library/songs?view=list')

        expect(wrapper.findAll('[data-part="list-group-header"]')).toHaveLength(10)
        wrapper.unmount()
    })

    it('shows a plain list with the view options and switches back without loading again', async () => {
        stubDesktop(true)
        document.documentElement.style.setProperty('--wmp-layout-view-options', 'on')
        const { wrapper, router } = await mountCollection('/library/songs?view=list')

        expect(wrapper.findAll('[data-part="list-group"]')).toHaveLength(1)
        expect(wrapper.find('[data-part="list-group-header"]').exists()).toBe(false)
        expect(wrapper.find('[data-part="list-group-info"]').exists()).toBe(false)
        expect(listState(wrapper)).toContain('ungrouped')
        expect(wrapper.findAll('[data-part="list-row"]')).toHaveLength(100)

        await loadMore(wrapper)
        expect(wrapper.findAll('[data-part="list-group"]')).toHaveLength(1)
        const rows = wrapper.findAll('[data-part="list-row"]')
        expect(rows).toHaveLength(150)
        expect(rows[149]!.text()).toContain('Song 149')

        await router.replace({ query: {} })
        await flushPromises()
        expect(wrapper.findAll('[data-part="list-group"]')).toHaveLength(15)
        expect(wrapper.findAll('[data-part="list-group-info"]')).toHaveLength(15)
        expect(listState(wrapper)).not.toContain('ungrouped')
        expect(wrapper.findAll('[data-part="list-row"]')).toHaveLength(150)
        expect(api.listTracks).toHaveBeenCalledTimes(2)
        wrapper.unmount()
    })
})
