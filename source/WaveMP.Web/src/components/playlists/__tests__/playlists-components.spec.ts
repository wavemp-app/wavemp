import { afterAll, afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import { createPinia, setActivePinia } from 'pinia'
import Sortable from 'sortablejs'
import { createMemoryHistory, createRouter } from 'vue-router'
import type { Playlist, Track } from '@/api/generated/models'
import { createWaveMpI18n } from '@/i18n'
import type { SongTarget } from '@/menus/targets'
import {
    coverChangeMilliseconds,
    coverLoadTimeoutMilliseconds,
    firstCoverChangeMilliseconds,
    stillCoversFlag,
} from '@/playlists/coverGrid'
import { createRoutes } from '@/router/routes'
import { useContextMenuStore } from '@/stores/contextMenu'
import { usePlayerStore } from '@/stores/player'

const api = vi.hoisted(() => ({
    listPlaylists: vi.fn(),
    getPlaylist: vi.fn(),
    createPlaylist: vi.fn(),
    renamePlaylist: vi.fn(),
    deletePlaylist: vi.fn(),
    listPlaylistEntries: vi.fn(),
    addPlaylistEntries: vi.fn(),
    movePlaylistEntry: vi.fn(),
    removePlaylistEntry: vi.fn(),
    fetchPlaylistCover: vi.fn(),
    uploadPlaylistCover: vi.fn(),
    deletePlaylistCover: vi.fn(),
    listRecentlyPlayed: vi.fn(),
}))
const library = vi.hoisted(() => ({
    fetchAlbumCover: vi.fn(),
    fetchTrackCover: vi.fn(),
}))

vi.mock('@/playlists/api', () => api)
vi.mock('@/library/api', () => library)
vi.mock('@/player/api', () => ({
    getPlayback: vi.fn(),
    recordPlay: vi.fn(),
    reportNowPlaying: vi.fn(),
}))

const AddToPlaylistDialog = (await import('../AddToPlaylistDialog.vue')).default
const PlaylistCover = (await import('../PlaylistCover.vue')).default
const PlaylistEditDialog = (await import('../PlaylistEditDialog.vue')).default
const PlaylistTrackList = (await import('../PlaylistTrackList.vue')).default
const ContextMenuHost = (await import('@/components/menus/ContextMenuHost.vue')).default

/** The list together with the context menu of the app; shown: false takes the list off the page. */
const PlaylistTrackListWithMenu = defineComponent({
    inheritAttrs: false,
    props: { shown: { type: Boolean, default: true } },
    setup(props, { attrs }) {
        return () => [props.shown ? h(PlaylistTrackList as never, attrs) : null, h(ContextMenuHost)]
    },
})

/** The tracks whose covers a playlist cover shows, slide by slide. */
function shownTracks(wrapper: VueWrapper): string[] {
    return wrapper
        .findAllComponents({ name: 'LibraryCover' })
        .map((cover) => cover.props('id') as string)
}

/** The tracks whose covers were loaded, in order. */
function fetchedTracks(): string[] {
    return library.fetchTrackCover.mock.calls.map((call) => call[0] as string)
}

const roadTrip: Playlist = {
    id: 'road',
    name: 'Road Trip',
    trackCount: 2,
    cover: { kind: 'None', trackIds: [] },
}
const quiet: Playlist = {
    id: 'quiet',
    name: 'Quiet',
    trackCount: 0,
    cover: { kind: 'None', trackIds: [] },
}

const tracks: Track[] = [
    {
        id: 'opening',
        title: 'Opening',
        artist: { id: 'alpha', name: 'Alpha Band' },
        album: { id: 'first', title: 'First Album' },
        durationMilliseconds: 185_000,
        hasAudio: true,
        hasVideo: false,
        hasCover: false,
    },
    { id: 'clip', title: 'Clip', hasAudio: false, hasVideo: true, hasCover: false },
]

async function mountWith(component: unknown, props: Record<string, unknown>) {
    const router = createRouter({ history: createMemoryHistory(), routes: createRoutes() })
    await router.push('/library/songs')
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const wrapper = mount(component as any, {
        props,
        global: { plugins: [router, createWaveMpI18n('en')] },
        attachTo: document.body,
    })
    await flushPromises()
    return wrapper
}

function problem(code: string, status = 409) {
    return Object.assign(new Error(code), { code, responseStatusCode: status })
}

beforeEach(() => {
    setActivePinia(createPinia())
    for (const mock of [...Object.values(api), ...Object.values(library)]) {
        mock.mockReset()
    }
    library.fetchTrackCover.mockResolvedValue(undefined)
    api.fetchPlaylistCover.mockResolvedValue(undefined)
})

afterEach(() => {
    vi.useRealTimers()
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
    document.body.innerHTML = ''
})

describe('PlaylistCover', () => {
    it('shows the placeholder icon without any cover', async () => {
        const wrapper = await mountWith(PlaylistCover, { id: 'road', cover: roadTrip.cover })

        expect(wrapper.find('[data-part="playlist-cover"]').exists()).toBe(false)
        expect(wrapper.find('[data-part="cover"] [data-icon="playlist"]').exists()).toBe(true)
        wrapper.unmount()
    })

    it('loads its own picture', async () => {
        const wrapper = await mountWith(PlaylistCover, {
            id: 'road',
            cover: { kind: 'Custom', version: 'v7', trackIds: [] },
        })

        expect(api.fetchPlaylistCover).toHaveBeenCalledWith('road', expect.any(Number))
        expect(library.fetchTrackCover).not.toHaveBeenCalled()
        wrapper.unmount()
    })

    it('shows the covers of its tracks as a 2x2 grid, repeating fewer than four', async () => {
        vi.useFakeTimers()
        const wrapper = await mountWith(PlaylistCover, {
            id: 'road',
            cover: { kind: 'Tracks', trackIds: ['t1', 't2'] },
        })

        const cover = wrapper.get('[data-part="playlist-cover"]')
        expect(cover.attributes('data-count')).toBe('2')
        expect(cover.attributes('aria-hidden')).toBe('true')
        expect(cover.attributes('data-state')).toBeUndefined()
        expect((cover.element as HTMLElement).style.getPropertyValue('--wmp-item-count')).toBe('2')
        const tiles = wrapper.findAll('[data-part="playlist-cover-tile"]')
        expect(tiles.map((tile) => tile.attributes('data-index'))).toEqual(['0', '1', '2', '3'])
        expect(shownTracks(wrapper)).toEqual(['t1', 't2', 't2', 't1'])

        // Nothing waits for its turn, so nothing changes.
        await vi.advanceTimersByTimeAsync(60_000)
        expect(wrapper.findAll('[data-part="playlist-cover-slide"]')).toHaveLength(4)
        wrapper.unmount()
    })

    it('slides a waiting cover into one tile from its outer edge once it is loaded', async () => {
        vi.useFakeTimers({ now: new Date('2040-01-01T00:00:00Z') })
        vi.spyOn(Math, 'random').mockReturnValue(0)
        library.fetchTrackCover.mockResolvedValue(new Uint8Array([1]).buffer)
        vi.stubGlobal('URL', {
            ...URL,
            createObjectURL: () => 'blob:track',
            revokeObjectURL: vi.fn(),
        })
        const wrapper = await mountWith(PlaylistCover, {
            id: 'road',
            cover: { kind: 'Tracks', trackIds: ['t1', 't2', 't3', 't4', 't5', 't6'] },
        })
        const tileAt = (index: number) =>
            wrapper.findAll('[data-part="playlist-cover-tile"]')[index]!
        const statesOf = (index: number) =>
            tileAt(index)
                .findAll('[data-part="playlist-cover-slide"]')
                .map((slide) => slide.attributes('data-state'))
        expect(wrapper.get('[data-part="playlist-cover"]').attributes('data-state')).toBe(
            'rotating',
        )
        expect(fetchedTracks()).toEqual(['t1', 't2', 't3', 't4'])

        // The first change comes after a calm pause: the cover waiting longest is added to the top
        // start tile, invisible until its picture is loaded.
        await vi.advanceTimersByTimeAsync(firstCoverChangeMilliseconds.min - 1)
        expect(statesOf(0)).toEqual(['active'])
        library.fetchTrackCover.mockReturnValueOnce(new Promise(() => {}))
        await vi.advanceTimersByTimeAsync(1)
        expect(statesOf(0)).toEqual(['active', 'loading'])
        expect(tileAt(0).attributes('data-state')).toBe('changing')

        // Meanwhile the next cover goes to another tile, from its outer edge, and slides in over
        // the shown one once it is loaded.
        await vi.advanceTimersByTimeAsync(coverChangeMilliseconds.min)
        await flushPromises()
        expect(statesOf(1)).toEqual(['active', 'entering'])
        const entering = tileAt(1).findAll('[data-part="playlist-cover-slide"]')[1]!
        expect(entering.attributes('data-from')).toBe('end')
        expect(fetchedTracks()).toEqual(['t1', 't2', 't3', 't4', 't5', 't6'])

        // A picture that never loads is given up; its cover waits for its turn again.
        await vi.advanceTimersByTimeAsync(
            coverLoadTimeoutMilliseconds - coverChangeMilliseconds.min,
        )
        expect(statesOf(0)).toEqual(['active'])
        expect(tileAt(0).attributes('data-state')).toBeUndefined()

        // Once the new cover is in place, it is the only one of its tile.
        await entering.trigger('animationend')
        expect(statesOf(1)).toEqual(['active'])
        expect(tileAt(1).attributes('data-state')).toBeUndefined()
        wrapper.unmount()
    })

    it('stands still for the automated screenshots', async () => {
        vi.useFakeTimers()
        ;(globalThis as Record<string, unknown>)[stillCoversFlag] = true
        try {
            const wrapper = await mountWith(PlaylistCover, {
                id: 'road',
                cover: { kind: 'Tracks', trackIds: ['t1', 't2', 't3', 't4', 't5'] },
            })

            expect(
                wrapper.get('[data-part="playlist-cover"]').attributes('data-state'),
            ).toBeUndefined()
            await vi.advanceTimersByTimeAsync(60_000)
            expect(wrapper.findAll('[data-part="playlist-cover-slide"]')).toHaveLength(4)
            expect(library.fetchTrackCover).toHaveBeenCalledTimes(4)
            wrapper.unmount()
        } finally {
            delete (globalThis as Record<string, unknown>)[stillCoversFlag]
        }
    })
})

describe('AddToPlaylistDialog', () => {
    async function mountDialog() {
        const wrapper = await mountWith(AddToPlaylistDialog, { trackIds: ['opening'], open: false })
        await wrapper.setProps({ open: true })
        await flushPromises()
        return wrapper
    }

    function choices() {
        return [...document.querySelectorAll<HTMLButtonElement>('[data-part="playlist-choice"]')]
    }

    it('lists the playlists of the profile with their number of songs', async () => {
        api.listPlaylists.mockResolvedValue({ items: [roadTrip, quiet], total: 2 })
        const wrapper = await mountDialog()

        expect(
            choices().map((choice) => [
                choice.querySelector('[data-part="label"]')?.textContent,
                choice.querySelector('[data-part="playlist-choice-count"]')?.textContent?.trim(),
            ]),
        ).toEqual([
            ['Quiet', 'Songs: 0'],
            ['Road Trip', 'Songs: 2'],
        ])
        wrapper.unmount()
    })

    it('adds the tracks to the chosen playlist and closes', async () => {
        api.listPlaylists.mockResolvedValue({ items: [roadTrip], total: 1 })
        api.addPlaylistEntries.mockResolvedValue({
            entries: [],
            playlist: { ...roadTrip, trackCount: 3 },
        })
        const wrapper = await mountDialog()

        choices()[0]!.click()
        await flushPromises()

        expect(api.addPlaylistEntries).toHaveBeenCalledWith('road', ['opening'])
        expect(wrapper.emitted('added')?.[0]).toEqual([{ ...roadTrip, trackCount: 3 }])
        expect(wrapper.emitted('update:open')?.at(-1)).toEqual([false])
        wrapper.unmount()
    })

    it('explains why the tracks could not be added', async () => {
        api.listPlaylists.mockResolvedValue({ items: [roadTrip], total: 1 })
        api.addPlaylistEntries.mockRejectedValue(problem('playlist.full'))
        const wrapper = await mountDialog()

        choices()[0]!.click()
        await flushPromises()

        expect(document.body.textContent).toContain(
            'The playlist has reached its maximum number of songs.',
        )
        expect(wrapper.emitted('added')).toBeUndefined()
        wrapper.unmount()
    })

    it('says so when there are no playlists yet', async () => {
        api.listPlaylists.mockResolvedValue({ items: [], total: 0 })
        const wrapper = await mountDialog()

        expect(choices()).toEqual([])
        expect(document.body.textContent).toContain('You have no playlists yet.')
        wrapper.unmount()
    })
})

describe('PlaylistEditDialog', () => {
    async function mountDialog(props: Record<string, unknown>) {
        const wrapper = await mountWith(PlaylistEditDialog, { ...props, open: false })
        await wrapper.setProps({ open: true })
        await flushPromises()
        return wrapper
    }

    function nameInput() {
        return document.querySelector<HTMLInputElement>('[data-part="dialog"] input')!
    }

    async function submit(name: string) {
        const input = nameInput()
        input.value = name
        input.dispatchEvent(new Event('input'))
        document.querySelector('[data-part="dialog"] form')!.dispatchEvent(new Event('submit'))
        await flushPromises()
    }

    it('creates a playlist with the trimmed name and its first tracks', async () => {
        api.createPlaylist.mockResolvedValue(roadTrip)
        const wrapper = await mountDialog({ trackIds: ['opening'] })

        await submit('  Road Trip  ')

        expect(api.createPlaylist).toHaveBeenCalledWith('Road Trip', ['opening'])
        expect(wrapper.emitted('saved')?.[0]).toEqual([roadTrip])
        wrapper.unmount()
    })

    it('renames a playlist and asks for a name before saving', async () => {
        api.renamePlaylist.mockResolvedValue({ ...roadTrip, name: 'Long Drive' })
        const wrapper = await mountDialog({ playlist: roadTrip })
        expect(nameInput().value).toBe('Road Trip')

        await submit('   ')
        expect(api.renamePlaylist).not.toHaveBeenCalled()
        expect(document.body.textContent).toContain('Please enter a name.')

        await submit('Long Drive')
        expect(api.renamePlaylist).toHaveBeenCalledWith('road', 'Long Drive')
        wrapper.unmount()
    })
})

describe('PlaylistTrackList', () => {
    const rows = tracks.map((track, index) => ({ id: `entry-${index}`, track }))

    function columns(wrapper: Awaited<ReturnType<typeof mountWith>>) {
        return wrapper
            .findAll('[data-part="list-column"]')
            .map((column) => column.attributes('data-column'))
    }

    it('shows a handle in each row only when the entries can be moved', async () => {
        const sortable = await mountWith(PlaylistTrackList, {
            rows,
            label: 'Road Trip',
            sortable: true,
        })
        const fixed = await mountWith(PlaylistTrackList, {
            rows,
            label: 'Recently played',
            detailLabel: 'Last played',
        })

        expect(sortable.findAll('[data-part="drag-handle"]')).toHaveLength(2)
        expect(columns(sortable)).toEqual([
            'handle',
            'number',
            'title',
            'duration',
            'artist',
            'album',
            'rating',
            'favorite',
        ])
        expect(fixed.findAll('[data-part="drag-handle"]')).toHaveLength(0)
        expect(columns(fixed)).toEqual([
            'number',
            'title',
            'duration',
            'artist',
            'album',
            'detail',
            'rating',
            'favorite',
        ])
        sortable.unmount()
        fixed.unmount()
    })

    it('lets the rows be dragged only while no change of the playlist runs', async () => {
        const wrapper = await mountWith(PlaylistTrackList, {
            rows,
            label: 'Road Trip',
            sortable: true,
        })
        const sortable = Sortable.get(
            wrapper.get<HTMLElement>('[data-part="list-group-rows"]').element,
        )!
        expect(sortable.option('disabled')).toBe(false)

        await wrapper.setProps({ state: 'busy' })
        expect(sortable.option('disabled')).toBe(true)

        await wrapper.setProps({ state: undefined })
        expect(sortable.option('disabled')).toBe(false)
        wrapper.unmount()
    })

    it('shows the entries in their order with album and duplicates', async () => {
        const wrapper = await mountWith(PlaylistTrackList, {
            rows: [...rows, { id: 'entry-2', track: tracks[0]! }],
            label: 'Road Trip',
        })

        const titles = wrapper
            .findAll('[data-part="list-row"] [data-column="title"] > [data-part="label"]')
            .map((cell) => cell.text())
        expect(titles).toEqual(['Opening', 'Clip', 'Opening'])
        expect(wrapper.find('[data-part="list-row"] [data-column="album"]').text()).toBe(
            'First Album',
        )
        wrapper.unmount()
    })

    it('names the artist of every entry below its title', async () => {
        const wrapper = await mountWith(PlaylistTrackList, { rows, label: 'Road Trip' })

        const subtitles = wrapper.findAll('[data-column="title"] > [data-part="list-subtitle"]')
        expect(subtitles).toHaveLength(rows.length)
        expect(
            subtitles.every((subtitle) => subtitle.attributes('data-state') === 'other-artist'),
        ).toBe(true)
        expect(subtitles[0]!.text()).toBe(
            wrapper.get('[data-part="list-row"] [data-column="artist"]').text(),
        )
        wrapper.unmount()
    })

    it('shows the video badge before the title and keeps its column in the list', async () => {
        const wrapper = await mountWith(PlaylistTrackList, {
            rows,
            label: 'Road Trip',
            state: 'busy',
        })

        expect(wrapper.get('[data-part="list"]').attributes('data-state')).toBe('busy has-video')
        const clip = wrapper.findAll('[data-part="list-cell"][data-column="title"]')[1]!.element
        expect([...clip.children].map((child) => child.getAttribute('data-part'))).toEqual([
            'badge',
            'label',
            'list-subtitle',
        ])
        wrapper.unmount()
    })
})

describe('the menu of a playlist entry', () => {
    // jsdom has no Popover API and keeps the content of a closed popover from the focus, so a menu
    // that opens loses the attribute here.
    beforeAll(() => {
        HTMLElement.prototype.showPopover = function (this: HTMLElement) {
            this.removeAttribute('popover')
        }
    })
    afterAll(() => {
        // @ts-expect-error The stand-in is removed again.
        delete HTMLElement.prototype.showPopover
    })

    const entries = ['first', 'second', 'third'].map((id) => ({
        id: `entry-${id}`,
        track: { id, title: id, hasAudio: true } as Track,
    }))
    const [first, second, third] = [entries[0]!, entries[1]!, entries[2]!]

    async function mountList(props: Record<string, unknown> = {}) {
        const move = vi.fn()
        const remove = vi.fn()
        const wrapper = await mountWith(PlaylistTrackListWithMenu, {
            rows: entries,
            label: 'Road Trip',
            sortable: true,
            state: 'busy',
            onMove: move,
            onRemove: remove,
            ...props,
        })
        return { wrapper, move, remove }
    }

    async function openMenu(wrapper: VueWrapper, index: number) {
        await wrapper.findAll('[data-part="list-row"]')[index]!.trigger('contextmenu')
        await flushPromises()
    }

    function item(id: string) {
        return document.querySelector<HTMLButtonElement>(
            `[data-part="context-menu"] > [data-item="${id}"]`,
        )
    }

    /** Which of move up, move down and remove can be chosen. */
    function enabled(): string[] {
        return ['move-up', 'move-down', 'remove-from-playlist'].filter(
            (id) => item(id)?.disabled === false,
        )
    }

    async function choose(id: string) {
        item(id)!.click()
        await flushPromises()
    }

    it.each([
        { index: 0, expected: ['move-down', 'remove-from-playlist'] },
        { index: 1, expected: ['move-up', 'move-down', 'remove-from-playlist'] },
        { index: 2, expected: ['move-up', 'remove-from-playlist'] },
    ])('offers what applies to row $index once the change is done', async ({ index, expected }) => {
        const { wrapper } = await mountList()
        await openMenu(wrapper, index)
        expect(enabled()).toEqual([])
        const focused = document.activeElement
        expect(focused?.getAttribute('data-item')).toBe('play')

        await wrapper.setProps({ state: undefined })
        await flushPromises()

        expect(enabled()).toEqual(expected)
        expect(document.activeElement).toBe(focused)
        expect(wrapper.get('[data-part="context-menu"]').attributes('data-state')).toBe('open')
        wrapper.unmount()
    })

    it('only removes several chosen rows once the change is done', async () => {
        const { wrapper, remove } = await mountList()
        const shown = wrapper.findAll('[data-part="list-row"]')
        await shown[0]!.trigger('click')
        await shown[1]!.trigger('click', { ctrlKey: true })
        await openMenu(wrapper, 1)
        expect(enabled()).toEqual([])

        await wrapper.setProps({ state: undefined, rows: [third, second, first] })
        await flushPromises()

        expect(enabled()).toEqual(['remove-from-playlist'])
        await choose('remove-from-playlist')
        expect(remove).toHaveBeenCalledWith([1, 2])
        wrapper.unmount()
    })

    it.each([
        { id: 'move-up', event: 'move', call: [1, 0] },
        { id: 'move-down', event: 'move', call: [1, 2] },
        { id: 'remove-from-playlist', event: 'remove', call: [[1]] },
    ] as const)(
        '$id acts on its row in the order loaded meanwhile',
        async ({ id, event, call }) => {
            const list = await mountList()
            await openMenu(list.wrapper, 0)

            await list.wrapper.setProps({ state: undefined, rows: [second, first, third] })
            await flushPromises()
            await choose(id)

            expect(list[event]).toHaveBeenCalledExactlyOnceWith(...call)
            list.wrapper.unmount()
        },
    )

    it('plays from its row on in the order loaded meanwhile', async () => {
        const playTracks = vi.spyOn(usePlayerStore(), 'playTracks').mockImplementation(vi.fn())
        const { wrapper } = await mountList()
        await openMenu(wrapper, 0)

        await wrapper.setProps({ state: undefined, rows: [third, second, first] })
        await flushPromises()
        await choose('play')

        expect(playTracks).toHaveBeenCalledExactlyOnceWith(
            [third.track, second.track, first.track],
            2,
            { source: undefined },
        )
        wrapper.unmount()
    })

    it.each([
        { gone: 'its row', change: { rows: [second, third] } as Record<string, unknown> },
        { gone: 'the list', change: { shown: false } as Record<string, unknown> },
    ])('offers no change and plays only the song without $gone', async ({ change }) => {
        const playTracks = vi.spyOn(usePlayerStore(), 'playTracks').mockImplementation(vi.fn())
        const { wrapper, move, remove } = await mountList({ state: undefined })
        await openMenu(wrapper, 0)
        expect(enabled()).toEqual(['move-down', 'remove-from-playlist'])

        await wrapper.setProps(change)
        await flushPromises()

        expect(enabled()).toEqual([])
        const target = useContextMenuStore().request!.target as SongTarget
        target.playlistEntry!.moveUp()
        target.playlistEntry!.moveDown()
        target.playlistEntry!.remove()
        expect(move).not.toHaveBeenCalled()
        expect(remove).not.toHaveBeenCalled()
        await choose('play')
        expect(playTracks).toHaveBeenCalledOnce()
        expect(playTracks.mock.calls[0]!.slice(0, 2)).toEqual([[first.track], 0])
        wrapper.unmount()
    })

    it('has no entries of a playlist in lists that cannot be sorted', async () => {
        const { wrapper } = await mountList({ sortable: false, state: undefined })
        await openMenu(wrapper, 1)

        expect(item('play')!.disabled).toBe(false)
        expect(item('move-up')).toBeNull()
        expect(item('move-down')).toBeNull()
        expect(item('remove-from-playlist')).toBeNull()
        wrapper.unmount()
    })
})
