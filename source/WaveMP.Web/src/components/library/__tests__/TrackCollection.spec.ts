import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import type { Track } from '@/api/generated/models'
import { createWaveMpI18n } from '@/i18n'
import { createRoutes } from '@/router/routes'

const api = vi.hoisted(() => ({
    listTracks: vi.fn(),
    listTrackGroups: vi.fn(),
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
        api.listTrackGroups.mockReset()
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

    it('marks every group with the position of its first song', async () => {
        stubDesktop(true)
        const { wrapper } = await mountCollection('/library/songs')

        const positions = wrapper
            .findAll('[data-part="list-group"]')
            .map((group) => group.attributes('data-position'))
        expect(positions).toEqual(['0', '10', '20', '30', '40', '50', '60', '70', '80', '90'])
        wrapper.unmount()
    })

    it('shows the list from a jump on and loads the songs before it on demand', async () => {
        stubDesktop(true)
        const { wrapper } = await mountCollection('/library/songs')

        await wrapper.vm.list.jumpTo(125)
        await flushPromises()

        expect(api.listTracks).toHaveBeenLastCalledWith(
            expect.objectContaining({ offset: 125, limit: 100 }),
        )
        const groups = wrapper.findAll('[data-part="list-group"]')
        expect(groups[0]!.attributes('data-position')).toBe('125')
        expect(groups[1]!.attributes('data-position')).toBe('130')
        expect(wrapper.findAll('[data-part="list-row"]')).toHaveLength(25)
        expect(wrapper.find('[data-part="load-more"]').exists()).toBe(false)

        await wrapper.get('[data-part="load-previous"]').trigger('click')
        await flushPromises()

        expect(api.listTracks).toHaveBeenLastCalledWith(
            expect.objectContaining({ offset: 25, limit: 100 }),
        )
        expect(wrapper.findAll('[data-part="list-row"]')).toHaveLength(125)
        expect(wrapper.findAll('[data-part="list-group"]')[0]!.attributes('data-position')).toBe(
            '25',
        )
        expect(wrapper.get('[data-part="load-previous"]').attributes('data-state')).toBeUndefined()

        await wrapper.get('[data-part="load-previous"]').trigger('click')
        await flushPromises()

        expect(api.listTracks).toHaveBeenLastCalledWith(
            expect.objectContaining({ offset: 0, limit: 25 }),
        )
        expect(wrapper.find('[data-part="load-previous"]').exists()).toBe(false)
        expect(wrapper.findAll('[data-part="list-row"]')).toHaveLength(150)
        wrapper.unmount()
    })

    it('loads the groups of the whole list with the same query and forgets them on a reload', async () => {
        stubDesktop(true)
        api.listTrackGroups.mockResolvedValue({
            items: [{ kind: 'Album', position: 0, count: 10 }],
            total: 1,
        })
        const router = createRouter({ history: createMemoryHistory(), routes: createRoutes() })
        await router.push('/library/songs')
        const wrapper = mount(TrackCollection, {
            props: { label: 'Songs', sort: 'Album', direction: 'Desc', filter: { myMusic: true } },
            global: { plugins: [router, createWaveMpI18n('en')] },
        })
        await flushPromises()

        expect(api.listTrackGroups).not.toHaveBeenCalled()
        await wrapper.vm.groups.load()

        expect(api.listTrackGroups).toHaveBeenCalledWith(
            expect.objectContaining({
                offset: 0,
                limit: 1000,
                sort: 'Album',
                direction: 'Desc',
                myMusic: true,
            }),
        )
        expect(wrapper.vm.groups.status.value).toBe('ready')

        await wrapper.setProps({ search: 'love' })
        await flushPromises()

        expect(wrapper.vm.groups.status.value).toBe('idle')
        expect(wrapper.vm.groups.groups.value).toEqual([])
        wrapper.unmount()
    })

    it('offers the quick jump in the headings of a list with two groups or more', async () => {
        stubDesktop(true)
        const { wrapper } = await mountCollection('/library/songs')

        const buttons = wrapper.findAll(
            '[data-part="list-group-title"] [data-part="btn-group-jump"]',
        )
        expect(buttons).toHaveLength(10)
        expect(buttons[0]!.attributes('aria-haspopup')).toBe('dialog')
        expect(buttons[0]!.attributes('aria-expanded')).toBe('false')
        expect(buttons[0]!.get('[data-part="label"]').text()).toBe('Album 0')
        expect(buttons[0]!.find('[data-icon="group-jump"]').exists()).toBe(true)
        wrapper.unmount()
    })

    it('keeps plain headings for one group and in a plain list', async () => {
        stubDesktop(true)
        api.listTracks.mockResolvedValue({ items: [song(0), song(1)], offset: 0, total: 2 })
        const { wrapper } = await mountCollection('/library/songs')

        expect(wrapper.findAll('[data-part="list-group-header"]')).toHaveLength(1)
        expect(wrapper.find('[data-part="btn-group-jump"]').exists()).toBe(false)
        expect(wrapper.get('[data-part="list-group-title"]').text()).toBe('Album 0')
        wrapper.unmount()

        serveLongList()
        document.documentElement.style.setProperty('--wmp-layout-view-options', 'on')
        const plain = await mountCollection('/library/songs?view=list')
        expect(plain.wrapper.find('[data-part="btn-group-jump"]').exists()).toBe(false)
        plain.wrapper.unmount()
    })

    it('jumps from the overview to a group far down without loading the list up to it', async () => {
        stubDesktop(true)
        // 6000 songs in 600 albums; the groups come from the group endpoint.
        api.listTracks.mockImplementation(({ offset, limit }: { offset: number; limit: number }) =>
            Promise.resolve({
                items: Array.from({ length: Math.min(limit, 6000 - offset) }, (_, index) =>
                    song(offset + index),
                ),
                offset,
                limit,
                total: 6000,
            }),
        )
        api.listTrackGroups.mockImplementation(
            ({ offset, limit }: { offset: number; limit: number }) =>
                Promise.resolve({
                    items: Array.from({ length: Math.min(limit, 600 - offset) }, (_, index) => ({
                        kind: 'Album',
                        position: (offset + index) * 10,
                        count: 10,
                        album: { id: `album-${offset + index}`, title: `Album ${offset + index}` },
                    })),
                    offset,
                    limit,
                    total: 600,
                }),
        )
        const dialogPrototype = HTMLDialogElement.prototype as Partial<HTMLDialogElement>
        dialogPrototype.showModal = function (this: HTMLDialogElement) {
            this.setAttribute('open', '')
        }
        dialogPrototype.close = function (this: HTMLDialogElement) {
            this.removeAttribute('open')
        }
        try {
            const { wrapper } = await mountCollection('/library/songs')
            await wrapper.findAll('[data-part="btn-group-jump"]')[2]!.trigger('click')
            await flushPromises()

            const overview = wrapper.get('[data-part="group-overview"]')
            expect(overview.attributes('open')).toBeDefined()
            expect(listState(wrapper)).toContain('zoomed-out')
            expect(api.listTrackGroups).toHaveBeenCalledTimes(1)
            const tiles = overview.findAll('[data-part="group-tile"]')
            expect(tiles).toHaveLength(600)
            expect(tiles[2]!.attributes('aria-current')).toBe('true')

            await tiles[450]!.trigger('click')
            await flushPromises()

            expect(overview.attributes('open')).toBeUndefined()
            expect(listState(wrapper)).not.toContain('zoomed-out')
            const offsets = api.listTracks.mock.calls.map(([query]) => query.offset)
            // The first page, the page of the group and at most the page before it.
            expect(offsets).toContain(4500)
            expect(offsets.every((offset) => [0, 4400, 4500].includes(offset))).toBe(true)
            const group = wrapper.get('[data-part="list-group"][data-position="4500"]')
            expect(document.activeElement).toBe(group.get('[data-part="btn-group-jump"]').element)
            wrapper.unmount()
        } finally {
            delete dialogPrototype.showModal
            delete dialogPrototype.close
        }
    })
})
