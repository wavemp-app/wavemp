import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import type { Component } from 'vue'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import type { Artist, ListGroupPage } from '@/api/generated/models'
import type { PageLike } from '@/api/paging'
import { createWaveMpI18n } from '@/i18n'
import { createRoutes } from '@/router/routes'
import { contentAreaId } from '@/router/scroll'
import { artistTileGrouping, type TileGrouping } from '@/library/tileGrouping'
import type { Tile } from '../TileList.vue'

vi.mock('@/library/api', () => ({ fetchAlbumCover: vi.fn(), fetchTrackCover: vi.fn() }))

const { default: TileCollection } = await import('../TileCollection.vue')
const { default: TileGroupList } = await import('../TileGroupList.vue')

/** 26 artists per letter A to Z, then 26 starting with a digit: 702 artists by name. */
function artistAt(index: number): Artist {
    const letter = index < 676 ? String.fromCharCode(65 + Math.floor(index / 26)) : '9'
    return { id: `artist-${index}`, name: `${letter}rtist ${index}` }
}

const total = 702

function fetchArtists(offset: number, limit: number): Promise<PageLike<Artist>> {
    const items = []
    for (let index = offset; index < Math.min(offset + limit, total); index++) {
        items.push(artistAt(index))
    }
    return Promise.resolve({ items, total })
}

function toTile(artist: Artist): Tile {
    return {
        id: artist.id ?? '',
        kind: 'artist',
        title: artist.name ?? '',
        to: { name: 'library-artist', params: { id: artist.id ?? '' } },
    }
}

async function mountCollection(
    props: {
        grouping?: TileGrouping<Artist>
        fetchGroups?: (offset: number, limit: number) => Promise<ListGroupPage | undefined>
        fetchPage?: (offset: number, limit: number) => Promise<PageLike<Artist>>
    } = {},
) {
    const router = createRouter({ history: createMemoryHistory(), routes: createRoutes() })
    await router.push('/library/artists')
    const content = document.createElement('main')
    content.id = contentAreaId
    document.body.append(content)
    const wrapper = mount(TileCollection as unknown as Component, {
        props: { label: 'Artists', fetchPage: fetchArtists, toTile, ...props },
        global: { plugins: [router, createWaveMpI18n('en')] },
        attachTo: content,
    })
    await flushPromises()
    return wrapper
}

type Wrapper = Awaited<ReturnType<typeof mountCollection>>

/** The exposed list and groups of the collection (a generic component). */
function exposed(wrapper: Wrapper) {
    return wrapper.vm as unknown as {
        list: { jumpTo(position: number): Promise<boolean> }
        groups: { load(): Promise<void>; status: { value: string } }
    }
}

describe('TileCollection', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
    })

    afterEach(() => {
        document.body.innerHTML = ''
    })

    it('shows the tiles ungrouped without a grouping', async () => {
        const wrapper = await mountCollection()

        expect(wrapper.find('[data-part="tile-groups"]').exists()).toBe(false)
        expect(wrapper.findAll('[data-part="tile-list"]')).toHaveLength(1)
        expect(wrapper.findAll('[data-part="tile-item"]')).toHaveLength(100)
        wrapper.unmount()
    })

    it('groups the tiles with a heading per group and the position of its first tile', async () => {
        const wrapper = await mountCollection({ grouping: artistTileGrouping({}, false) })

        const groups = wrapper.findAll('[data-part="tile-group"]')
        expect(groups.map((group) => group.attributes('data-position'))).toEqual([
            '0',
            '26',
            '52',
            '78',
        ])
        expect(groups.map((group) => group.get('[data-part="tile-group-title"]').text())).toEqual([
            'A',
            'B',
            'C',
            'D',
        ])
        expect(groups[3]!.findAll('[data-part="tile-item"]')).toHaveLength(22)
        expect(groups[0]!.attributes('data-kind')).toBe('letter')
        wrapper.unmount()
    })

    it('starts at a jump target and prepends the tiles before it', async () => {
        const wrapper = await mountCollection({ grouping: artistTileGrouping({}, false) })

        await exposed(wrapper).list.jumpTo(650)
        await flushPromises()

        const titles = () =>
            wrapper.findAll('[data-part="tile-group-title"]').map((title) => title.text())
        expect(titles()).toEqual(['Z', '#'])
        expect(wrapper.get('[data-part="tile-group"]').attributes('data-position')).toBe('650')
        expect(wrapper.findAll('[data-part="tile-item"]')).toHaveLength(52)

        await wrapper.get('[data-part="load-previous"]').trigger('click')
        await flushPromises()

        expect(wrapper.get('[data-part="tile-group"]').attributes('data-position')).toBe('550')
        expect(titles()).toEqual(['V', 'W', 'X', 'Y', 'Z', '#'])
        expect(wrapper.findAll('[data-part="tile-item"]')).toHaveLength(152)
        wrapper.unmount()
    })

    it('keeps the tile at the top of the content area in place while tiles are prepended', async () => {
        const wrapper = await mountCollection({ grouping: artistTileGrouping({}, false) })
        await exposed(wrapper).list.jumpTo(650)
        await flushPromises()

        // Every tile is 100 pixels high; its top follows the tiles before it and the scrolling.
        const content = document.getElementById(contentAreaId)!
        let scrollTop = 0
        Object.defineProperty(content, 'scrollTop', {
            get: () => scrollTop,
            set: (value: number) => (scrollTop = value),
        })
        content.getBoundingClientRect = () => ({ top: 0, bottom: 800 }) as DOMRect
        const items = () =>
            Array.from(content.querySelectorAll<HTMLElement>('[data-part="tile-item"]'))
        for (const item of items()) {
            item.getBoundingClientRect = () => {
                const top = items().indexOf(item) * 100 - scrollTop
                return { top, bottom: top + 100 } as DOMRect
            }
        }
        scrollTop = 250

        const anchor = items()[2]!
        const before = anchor.getBoundingClientRect().top
        await wrapper.get('[data-part="load-previous"]').trigger('click')
        await flushPromises()

        expect(items()[102]).toBe(anchor)
        expect(anchor.getBoundingClientRect().top).toBe(before)
        expect(scrollTop).toBe(10250)
        wrapper.unmount()
    })

    it('loads the groups of the whole list only when asked and again after a reload', async () => {
        const fetchGroups = vi.fn().mockResolvedValue({
            items: [{ kind: 'Letter', letter: 'A', position: 0, count: 26 }],
            total: 1,
        })
        const wrapper = await mountCollection({
            grouping: artistTileGrouping({}, false),
            fetchGroups,
        })

        expect(fetchGroups).not.toHaveBeenCalled()
        await exposed(wrapper).groups.load()
        expect(fetchGroups).toHaveBeenCalledWith(0, 1000)
        expect(exposed(wrapper).groups.status.value).toBe('ready')

        await wrapper.setProps({ reloadKey: 'other' })
        await flushPromises()
        expect(exposed(wrapper).groups.status.value).toBe('idle')
        wrapper.unmount()
    })
})

describe('TileGroupList', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
    })

    it('names every group by its heading and reads # out', async () => {
        const router = createRouter({ history: createMemoryHistory(), routes: createRoutes() })
        await router.push('/library/albums')
        const tile = (id: string): Tile => ({
            id,
            kind: 'album',
            title: id,
            to: { name: 'library-album', params: { id } },
        })
        const wrapper = mount(TileGroupList, {
            props: {
                label: 'Albums',
                groups: [
                    {
                        id: 'artist:a',
                        kind: 'artist',
                        artist: { id: 'a', name: 'Alpha Band' },
                        position: 0,
                        items: [tile('one')],
                    },
                    { id: 'artist:unknown', kind: 'artist', position: 1, items: [tile('two')] },
                    { id: 'year:2001', kind: 'year', year: 2001, position: 2, items: [tile('x')] },
                    { id: 'year:unknown', kind: 'year', position: 3, items: [tile('y')] },
                    {
                        id: 'letter:#',
                        kind: 'letter',
                        letter: '#',
                        position: 4,
                        items: [tile('z')],
                    },
                ],
            },
            global: { plugins: [router, createWaveMpI18n('en')] },
        })

        const groups = wrapper.findAll('[data-part="tile-group"]')
        expect(wrapper.get('[data-part="tile-groups"]').attributes('aria-label')).toBe('Albums')
        expect(groups.map((group) => group.get('h2').text())).toEqual([
            'Alpha Band',
            'Unknown artist',
            '2001',
            'Unknown year',
            '#',
        ])
        for (const group of groups) {
            const title = group.get('[data-part="tile-group-title"]')
            expect(group.element.tagName).toBe('SECTION')
            expect(group.attributes('aria-labelledby')).toBe(title.attributes('id'))
        }
        expect(groups[4]!.get('h2').attributes('aria-label')).toBe('Numbers and symbols')
        expect(groups[4]!.get('[data-part="tile-list"]').attributes('aria-label')).toBe(
            'Numbers and symbols',
        )
        expect(groups[0]!.get('h2').attributes('aria-label')).toBeUndefined()
        wrapper.unmount()
    })
})
