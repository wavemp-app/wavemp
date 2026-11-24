import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createWaveMpI18n } from '@/i18n'
import type { OverviewGroup, OverviewUnit } from '@/library/groupOverview'
import type { ListGroupsStatus } from '@/composables/useListGroups'

const covers = vi.hoisted(() => ({
    acquire: vi.fn(() => Promise.resolve('blob:cover')),
    release: vi.fn(),
    versionOf: vi.fn((_kind: string, version?: string) => version),
}))

vi.mock('@/stores/covers', () => ({ useCoversStore: () => covers }))

const { default: GroupOverview } = await import('../GroupOverview.vue')

const songsGroups: OverviewGroup[] = [
    {
        id: 'g0',
        kind: 'album',
        position: 0,
        count: 12,
        album: { id: 'album-1', title: 'Blue', artist: { id: 'a1', name: 'Band' } },
        hasCover: true,
        letter: 'B',
    },
    {
        id: 'g1',
        kind: 'album',
        position: 12,
        count: 9,
        album: { id: 'album-2', title: 'Hits', artist: null, isCompilation: true },
        hasCover: false,
        letter: 'V',
    },
    {
        id: 'g2',
        kind: 'unknown',
        unknown: 'album',
        position: 21,
        count: 3,
        artist: { id: 'a2', name: 'Solo' },
    },
    { id: 'g3', kind: 'unknown', unknown: 'album', position: 24, count: 1 },
]

/** 40 album groups of the artists A, B, C, E, F ... (no D), two per letter. */
function manyGroups(): OverviewGroup[] {
    const letters = 'ABCEFGHKLMNPRSTVWXY#'
    return Array.from({ length: 40 }, (_, index) => ({
        id: `m${index}`,
        kind: 'album',
        position: index * 5,
        count: 5,
        album: { id: `album-${index}`, title: `Album ${index}` },
        hasCover: false,
        letter: letters[Math.floor(index / 2)],
    }))
}

const mounted: VueWrapper[] = []

function mountOverview(
    props: {
        groups?: readonly OverviewGroup[]
        status?: ListGroupsStatus
        current?: number
        unit?: OverviewUnit
        open?: boolean
    } = {},
) {
    const wrapper = mount(GroupOverview, {
        props: { groups: songsGroups, unit: 'songs', open: true, ...props },
        global: { plugins: [createWaveMpI18n('en')] },
        attachTo: document.body,
    })
    mounted.push(wrapper)
    return wrapper
}

function tiles(wrapper: VueWrapper) {
    return wrapper.findAll('[data-part="group-tile"]')
}

// jsdom has no modal dialogs: opening sets the attribute open, so the tiles can take the focus.
const dialogPrototype = HTMLDialogElement.prototype as Partial<HTMLDialogElement>

beforeEach(() => {
    setActivePinia(createPinia())
    dialogPrototype.showModal = function (this: HTMLDialogElement) {
        this.setAttribute('open', '')
    }
    dialogPrototype.close = function (this: HTMLDialogElement) {
        this.removeAttribute('open')
    }
})

afterEach(() => {
    mounted.splice(0).forEach((wrapper) => wrapper.unmount())
    vi.restoreAllMocks()
    delete dialogPrototype.showModal
    delete dialogPrototype.close
})

describe('GroupOverview', () => {
    it('shows every group with its name, details and number of entries', async () => {
        const wrapper = mountOverview()
        await flushPromises()

        const all = tiles(wrapper)
        expect(all.map((tile) => tile.attributes('data-kind'))).toEqual([
            'album',
            'album',
            'unknown',
            'unknown',
        ])
        expect(all.map((tile) => tile.find('[data-part="label"]').text())).toEqual([
            'Blue',
            'Hits',
            'Unknown album',
            'Unknown album',
        ])
        expect(
            all.map((tile) => {
                const detail = tile.find('[data-part="group-tile-detail"]')
                return detail.exists() ? detail.text() : null
            }),
        ).toEqual(['Band', 'Various artists', 'Solo', null])
        expect(all[0]?.find('[data-part="group-tile-count"]').text()).toBe('Songs: 12')
        // Albums show their cover, the songs without album the placeholder.
        expect(all.map((tile) => tile.find('[data-part="cover"]').exists())).toEqual([
            true,
            true,
            true,
            true,
        ])
        expect(all[1]?.find('[data-part="cover"]').attributes('data-state')).toBe('placeholder')
        expect(wrapper.find('[data-part="group-overview-title"]').text()).toBe('Jump to')
        expect(wrapper.find('[data-part="group-overview"]').attributes('data-state')).toBe('ready')
    })

    it('counts albums and artists', async () => {
        const letters: OverviewGroup[] = [
            { id: 'a', kind: 'letter', position: 0, count: 3, letter: 'A' },
            { id: 'b', kind: 'letter', position: 3, count: 2, letter: '#' },
        ]
        const wrapper = mountOverview({ groups: letters, unit: 'artists' })
        await flushPromises()

        const all = tiles(wrapper)
        expect(all[0]?.text()).toContain('Artists: 3')
        // Letters are the tile: no cover.
        expect(all[0]?.find('[data-part="cover"]').exists()).toBe(false)
        expect(all[1]?.find('[data-part="label"]').text()).toBe('#')
        expect(all[1]?.attributes('aria-label')).toBe('Numbers and symbols, Artists: 2')

        const albums = mountOverview({
            groups: [{ id: 'y', kind: 'year', position: 0, count: 4, year: 1999 }],
            unit: 'albums',
        })
        await flushPromises()
        expect(tiles(albums)[0]?.text()).toContain('Albums: 4')
        expect(tiles(albums)[0]?.find('[data-part="label"]').text()).toBe('1999')
    })

    it('marks the group of the current position', async () => {
        const wrapper = mountOverview({ current: 22 })
        await flushPromises()

        const current = tiles(wrapper).filter((tile) => tile.attributes('aria-current') === 'true')
        expect(current).toHaveLength(1)
        expect(current[0]?.attributes('data-state')).toBe('current')
        expect(current[0]?.text()).toContain('Solo')

        const grid = wrapper.find('[data-part="group-grid"]')
        expect(grid.attributes('style')).toContain('--wmp-item-count: 4')
        expect(grid.attributes('style')).toContain('--wmp-active-index: 2')
        expect(grid.element.firstElementChild?.getAttribute('data-part')).toBe('indicator')
    })

    it('moves the focus to the current group when it opens', async () => {
        const wrapper = mountOverview({ current: 13 })
        await flushPromises()

        expect(document.activeElement).toBe(tiles(wrapper)[1]?.element)
        expect(tiles(wrapper).map((tile) => tile.attributes('tabindex'))).toEqual([
            '-1',
            '0',
            '-1',
            '-1',
        ])
    })

    it('shows the letter bar only for many groups with different letters', async () => {
        expect(mountOverview().find('[data-part="letter-bar"]').exists()).toBe(false)

        const wrapper = mountOverview({ groups: manyGroups(), current: 12 })
        await flushPromises()
        const letters = wrapper.findAll('[data-part="letter"]')
        expect(letters.map((letter) => letter.attributes('data-item')).join('')).toBe(
            'ABCDEFGHIJKLMNOPQRSTUVWXYZ#',
        )
        const d = letters.find((letter) => letter.attributes('data-item') === 'D')
        expect(d?.attributes('aria-disabled')).toBe('true')
        expect(
            letters.find((letter) => letter.attributes('data-item') === 'A')?.attributes(),
        ).not.toHaveProperty('aria-disabled')
        // Position 12 is in the third group, of the letter B.
        expect(
            letters
                .filter((letter) => letter.attributes('data-state') === 'current')
                .map((letter) => letter.attributes('data-item')),
        ).toEqual(['B'])
        expect(
            letters
                .find((letter) => letter.attributes('data-item') === '#')
                ?.attributes('aria-label'),
        ).toBe('Numbers and symbols')
    })

    it('moves to the first group of a letter without closing', async () => {
        const wrapper = mountOverview({ groups: manyGroups() })
        await flushPromises()

        await wrapper.find('[data-part="letter"][data-item="E"]').trigger('click')
        await flushPromises()
        // E is the fourth letter with groups: its first group is the seventh.
        expect(document.activeElement).toBe(tiles(wrapper)[6]?.element)
        expect(wrapper.emitted('choose')).toBeUndefined()
        expect(wrapper.emitted('update:open')).toBeUndefined()

        // A letter without groups does nothing.
        await wrapper.find('[data-part="letter"][data-item="D"]').trigger('click')
        await flushPromises()
        expect(document.activeElement).toBe(tiles(wrapper)[6]?.element)
    })

    it('moves the focus with the arrow keys by the columns of the grid', async () => {
        const wrapper = mountOverview({ groups: manyGroups() })
        await flushPromises()
        const all = tiles(wrapper).map((tile) => tile.element as HTMLElement)
        // Three tiles per row of 40 pixels; four rows fit into the grid.
        vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(function (
            this: HTMLElement,
        ) {
            const index = all.indexOf(this)
            const top = Math.floor(Math.max(index, 0) / 3) * 40
            return { top, bottom: top + 40, height: 40, left: 0, width: 100 } as DOMRect
        })
        const grid = wrapper.find('[data-part="group-grid"]')
        Object.defineProperty(grid.element, 'clientHeight', { value: 160 })

        const press = async (key: string) => {
            await wrapper.find('[data-part="group-tile"][tabindex="0"]').trigger('keydown', { key })
            await flushPromises()
            return all.indexOf(document.activeElement as HTMLElement)
        }

        expect(await press('ArrowRight')).toBe(1)
        expect(await press('ArrowDown')).toBe(4)
        expect(await press('ArrowLeft')).toBe(3)
        expect(await press('ArrowUp')).toBe(0)
        expect(await press('PageDown')).toBe(12)
        expect(await press('PageUp')).toBe(0)
        expect(await press('End')).toBe(39)
        expect(await press('Home')).toBe(0)
        expect(all.filter((tile) => tile.tabIndex === 0)).toHaveLength(1)
    })

    it('moves to the next group whose name starts with the typed letters', async () => {
        const wrapper = mountOverview()
        await flushPromises()

        await tiles(wrapper)[0]?.trigger('keydown', { key: 'u' })
        await flushPromises()
        expect(document.activeElement).toBe(tiles(wrapper)[2]?.element)

        await tiles(wrapper)[2]?.trigger('keydown', { key: 'u' })
        await flushPromises()
        expect(document.activeElement).toBe(tiles(wrapper)[3]?.element)
    })

    it('chooses a group by click', async () => {
        const wrapper = mountOverview()
        await flushPromises()

        await tiles(wrapper)[2]?.trigger('click')
        expect(wrapper.emitted('choose')?.[0]?.[0]).toMatchObject({ id: 'g2', position: 21 })
    })

    it('closes with its close button', async () => {
        const wrapper = mountOverview()
        await wrapper.find('[data-part="btn-close"]').trigger('click')

        expect(wrapper.emitted('update:open')?.[0]).toEqual([false])
    })

    it('shows that the groups load, and offers to try again after an error', async () => {
        const loading = mountOverview({ status: 'loading' })
        await flushPromises()
        const dialog = loading.find('[data-part="group-overview"]')
        expect(dialog.attributes('data-state')).toBe('loading')
        expect(dialog.attributes('aria-busy')).toBe('true')
        expect(tiles(loading)).toHaveLength(0)

        const failed = mountOverview({ status: 'error' })
        await flushPromises()
        expect(failed.find('[data-part="group-overview"]').attributes('data-state')).toBe('error')
        expect(failed.find('[data-part="empty-state-title"]').text()).toBe(
            'The groups could not be loaded.',
        )
        await failed.find('[data-part="empty-state"] button').trigger('click')
        expect(failed.emitted('retry')).toHaveLength(1)
    })

    it('shows the discs of an album', async () => {
        const wrapper = mountOverview({
            groups: [
                { id: 'd1', kind: 'disc', position: 0, count: 10, disc: 1 },
                { id: 'd2', kind: 'disc', position: 10, count: 8, disc: 2 },
            ],
            current: 10,
        })
        await flushPromises()

        expect(tiles(wrapper).map((tile) => tile.find('[data-part="label"]').text())).toEqual([
            'Disc 1',
            'Disc 2',
        ])
        expect(tiles(wrapper)[1]?.attributes('aria-current')).toBe('true')
    })
})
