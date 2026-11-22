import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import type { Playlist, PlaylistEntry } from '@/api/generated/models'
import { createWaveMpI18n } from '@/i18n'
import { createRoutes } from '@/router/routes'
import { usePlayerStore } from '@/stores/player'

const api = vi.hoisted(() => ({
    getPlaylist: vi.fn(),
    listPlaylists: vi.fn(),
    listPlaylistEntries: vi.fn(),
    movePlaylistEntry: vi.fn(),
    removePlaylistEntry: vi.fn(),
    fetchPlaylistCover: vi.fn(),
    uploadPlaylistCover: vi.fn(),
    deletePlaylistCover: vi.fn(),
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

const PlaylistView = (await import('../playlists/PlaylistView.vue')).default
const ContextMenuHost = (await import('@/components/menus/ContextMenuHost.vue')).default

/** The page of a playlist together with the context menu of the app, as in the shell. */
const PlaylistViewWithMenu = defineComponent({
    setup() {
        return () => [h(PlaylistView, { id: 'road' }), h(ContextMenuHost)]
    },
})

const playlist: Playlist = {
    id: 'road',
    name: 'Road Trip',
    trackCount: 3,
    cover: { kind: 'None', trackIds: [] },
}
const entries: PlaylistEntry[] = ['first', 'second', 'third'].map((id, position) => ({
    id: `entry-${id}`,
    position,
    track: { id, title: id, hasAudio: true },
}))

/** A promise the test settles: the answer of the API that is held back. */
function deferred<Value>() {
    let resolve!: (value: Value) => void
    let reject!: (reason: unknown) => void
    const promise = new Promise<Value>((onResolve, onReject) => {
        resolve = onResolve
        reject = onReject
    })
    return { promise, resolve, reject }
}

async function mountView() {
    const router = createRouter({ history: createMemoryHistory(), routes: createRoutes() })
    await router.push('/library/songs')
    const wrapper = mount(PlaylistViewWithMenu, {
        global: { plugins: [router, createWaveMpI18n('en')] },
        attachTo: document.body,
    })
    await flushPromises()
    return wrapper
}

async function openMenu(wrapper: VueWrapper, title: string) {
    const row = wrapper
        .findAll('[data-part="list-row"]')
        .find((candidate) => candidate.get('[data-column="title"]').text() === title)!
    await row.trigger('contextmenu')
    await flushPromises()
}

function item(id: string): HTMLButtonElement {
    return document.querySelector<HTMLButtonElement>(
        `[data-part="context-menu"] > [data-item="${id}"]`,
    )!
}

function disabled(): boolean[] {
    return ['move-up', 'move-down', 'remove-from-playlist'].map((id) => item(id).disabled)
}

async function choose(id: string) {
    item(id).click()
    await flushPromises()
}

function menuState(wrapper: VueWrapper) {
    return wrapper.get('[data-part="context-menu"]').attributes('data-state')
}

beforeEach(() => {
    setActivePinia(createPinia())
    for (const mock of [...Object.values(api), ...Object.values(library)]) {
        mock.mockReset()
    }
    api.getPlaylist.mockResolvedValue(playlist)
    api.listPlaylistEntries.mockResolvedValue({ items: entries, total: entries.length })
    api.movePlaylistEntry.mockResolvedValue(undefined)
})

afterEach(() => {
    vi.restoreAllMocks()
    document.body.innerHTML = ''
})

describe('PlaylistView', () => {
    it('enables the open menu of an entry when the running change is done', async () => {
        const wrapper = await mountView()
        const reload = deferred<Playlist>()
        api.getPlaylist.mockReturnValueOnce(reload.promise)
        await openMenu(wrapper, 'third')
        await choose('move-up')

        // Moved at once; the page is busy until it has the playlist again.
        await openMenu(wrapper, 'third')
        expect(wrapper.get('[data-part="view"]').attributes('aria-busy')).toBe('true')
        expect(disabled()).toEqual([true, true, true])

        reload.resolve(playlist)
        await flushPromises()

        expect(menuState(wrapper)).toBe('open')
        expect(disabled()).toEqual([false, false, false])
        await choose('move-up')
        expect(api.movePlaylistEntry).toHaveBeenLastCalledWith('road', 'entry-third', 0)
        wrapper.unmount()
    })

    it.each([
        { id: 'move-down', call: ['road', 'entry-second', 2] },
        { id: 'remove-from-playlist', call: ['road', 'entry-second'] },
    ])('$id acts on the entry of the menu after a failed change', async ({ id, call }) => {
        const wrapper = await mountView()
        const move = deferred<undefined>()
        api.movePlaylistEntry.mockReturnValueOnce(move.promise)
        await openMenu(wrapper, 'second')
        await choose('move-up')
        // The menu opens for the first row, which is the second again after the failure.
        await openMenu(wrapper, 'second')
        expect(disabled()).toEqual([true, true, true])

        move.reject(new Error('offline'))
        await flushPromises()

        expect(menuState(wrapper)).toBe('open')
        expect(disabled()).toEqual([false, false, false])
        await choose(id)
        const mock = id === 'move-down' ? api.movePlaylistEntry : api.removePlaylistEntry
        expect(mock).toHaveBeenLastCalledWith(...call)
        wrapper.unmount()
    })

    it('plays from the entry of the menu after a failed change', async () => {
        const playTracks = vi.spyOn(usePlayerStore(), 'playTracks').mockImplementation(vi.fn())
        const wrapper = await mountView()
        const move = deferred<undefined>()
        api.movePlaylistEntry.mockReturnValueOnce(move.promise)
        await openMenu(wrapper, 'second')
        await choose('move-up')
        await openMenu(wrapper, 'second')

        move.reject(new Error('offline'))
        await flushPromises()
        await choose('play')

        expect(playTracks.mock.calls[0]![1]).toBe(1)
        wrapper.unmount()
    })

    it.each([
        { gone: 'the playlist cannot be loaded again', loaded: null },
        { gone: 'the entry is no longer in it', loaded: [entries[0]!, entries[2]!] },
    ])('keeps the entries of the open menu disabled when $gone', async ({ loaded }) => {
        const wrapper = await mountView()
        const move = deferred<undefined>()
        api.movePlaylistEntry.mockReturnValueOnce(move.promise)
        await openMenu(wrapper, 'second')
        await choose('move-up')
        await openMenu(wrapper, 'second')

        if (loaded) {
            api.listPlaylistEntries.mockResolvedValue({ items: loaded, total: loaded.length })
        } else {
            api.getPlaylist.mockRejectedValue(new Error('offline'))
        }
        move.reject(new Error('offline'))
        await flushPromises()

        expect(menuState(wrapper)).toBe('open')
        expect(disabled()).toEqual([true, true, true])
        wrapper.unmount()
    })
})
