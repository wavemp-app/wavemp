import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import type { Track } from '@/api/generated/models'
import { createWaveMpI18n } from '@/i18n'
import { groupTracksByAlbum } from '@/library/grouping'
import { createRoutes } from '@/router/routes'
import { getPlayback } from '@/player/api'
import { playerDependencies, usePlayerStore } from '@/stores/player'
import { useContextMenuStore } from '@/stores/contextMenu'
import { useSessionStore } from '@/stores/session'

const covers = vi.hoisted(() => ({
    fetchAlbumCover: vi.fn(),
    fetchTrackCover: vi.fn(),
}))

vi.mock('@/library/api', () => covers)
vi.mock('@/player/api', () => ({
    getPlayback: vi.fn(),
    recordPlay: vi.fn(),
    reportNowPlaying: vi.fn(),
}))

const LibraryCover = (await import('../LibraryCover.vue')).default
const TileList = (await import('../TileList.vue')).default
const TrackFileList = (await import('../TrackFileList.vue')).default
const TrackGroupList = (await import('../TrackGroupList.vue')).default
const ContextMenuHost = (await import('@/components/menus/ContextMenuHost.vue')).default

/** A list together with the context menu of the app, as in the shell. */
const TrackGroupListWithMenu = defineComponent({
    inheritAttrs: false,
    setup(_, { attrs }) {
        return () => [h(TrackGroupList as never, attrs), h(ContextMenuHost)]
    },
})

/** Ids of the entries of the open menu, disabled ones marked with a minus. */
function menuEntries(): string[] {
    const menu = document.querySelector('[data-part="context-menu"]')!
    return [...menu.querySelectorAll(':scope > [data-part="menu-item"]')].map(
        (item) =>
            `${(item as HTMLButtonElement).disabled ? '-' : ''}${item.getAttribute('data-item')}`,
    )
}

const alpha = { id: 'alpha', name: 'Alpha Band' }
const album = { id: 'first', title: 'First Album', artist: alpha, isCompilation: false }
const tracks: Track[] = [
    {
        id: 'opening',
        title: 'Opening',
        artist: alpha,
        album,
        trackNumber: 1,
        year: 2001,
        genre: 'Rock',
        durationMilliseconds: 185_000,
        hasAudio: true,
        hasVideo: false,
        hasCover: false,
    },
    {
        id: 'closing',
        title: 'Closing',
        artist: alpha,
        album,
        trackNumber: 2,
        durationMilliseconds: 61_000,
        hasAudio: true,
        hasVideo: true,
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
    return { wrapper, router }
}

describe('TrackGroupList', () => {
    const original = { ...playerDependencies }
    const engine = {
        position: 0,
        hasPrepared: false,
        load: vi.fn(),
        prepare: vi.fn(),
        play: vi.fn(() => Promise.resolve()),
        pause: vi.fn(),
        stop: vi.fn(),
        seek: vi.fn(),
        setVolume: vi.fn(),
        setTrackGain: vi.fn(),
        setEqualizer: vi.fn(),
        fadeOut: vi.fn(),
        restoreOutput: vi.fn(),
        equalizerAvailable: true,
        analyser: null,
    }

    beforeEach(() => {
        setActivePinia(createPinia())
        vi.mocked(getPlayback).mockReset()
        vi.mocked(getPlayback).mockImplementation((id) =>
            Promise.resolve({
                trackId: id,
                method: 'DirectPlay',
                streamUrl: `/stream/${id}`,
                replayGain: {},
            }),
        )
        playerDependencies.createEngine = () => engine
        playerDependencies.audioFormats = () => ['Mp3']
        playerDependencies.mediaSession = false
    })

    afterEach(() => {
        Object.assign(playerDependencies, original)
    })

    it('plays the list from a row on double click and on Enter and marks the playing row', async () => {
        const { wrapper } = await mountWith(TrackGroupList, {
            groups: groupTracksByAlbum(tracks),
            label: 'Songs',
        })
        const player = usePlayerStore()

        await wrapper.findAll('[data-part="list-row"]')[1]!.trigger('dblclick')
        await flushPromises()

        expect(player.queue.items.map((item) => item.track.id)).toEqual(['opening', 'closing'])
        expect(player.state.trackId).toBe('closing')
        const rows = wrapper.findAll('[data-part="list-row"]')
        expect(rows[1]!.attributes('data-state')).toContain('playing')
        expect(rows[0]!.attributes('data-state') ?? '').not.toContain('playing')

        await rows[0]!.trigger('keydown', { key: 'Enter' })
        await flushPromises()
        expect(player.state.trackId).toBe('opening')
        wrapper.unmount()
    })

    it('puts a track next or at the end of the queue from its menu', async () => {
        const { wrapper } = await mountWith(TrackGroupListWithMenu, {
            groups: groupTracksByAlbum(tracks),
            label: 'Songs',
        })
        const player = usePlayerStore()
        player.playTracks([tracks[0]!], 0)
        await flushPromises()

        const rows = wrapper.findAll('[data-part="list-row"]')
        await rows[1]!.trigger('contextmenu', { clientX: 10, clientY: 20 })
        await flushPromises()
        ;(document.querySelector('[data-item="add-to-queue"]') as HTMLElement).click()
        await flushPromises()
        await rows[1]!.trigger('contextmenu', { clientX: 10, clientY: 20 })
        await flushPromises()
        ;(document.querySelector('[data-item="play-next"]') as HTMLElement).click()
        await flushPromises()

        expect(player.queue.items.map((item) => item.track.id)).toEqual([
            'opening',
            'closing',
            'closing',
        ])
        expect(player.state.trackId).toBe('opening')
        wrapper.unmount()
    })

    it('shows the tracks grouped by album with links to album, artist, genre and year', async () => {
        const { wrapper } = await mountWith(TrackGroupList, {
            groups: groupTracksByAlbum(tracks),
            label: 'Songs',
        })

        const groups = wrapper.findAll('[data-part="list-group"]')
        expect(groups.map((group) => group.get('[data-part="list-group-title"]').text())).toEqual([
            'First Album',
            'Unknown album',
        ])
        const links = groups[0]!
            .findAll('[data-part="meta-value"] > [data-part="link"]')
            .map((link) => link.attributes('href'))
        expect(links).toEqual([
            '/library/albums/first',
            '/library/artists/alpha',
            '/library/genres/Rock',
            '/library/years/2001',
        ])
        expect(groups[1]!.get('[data-field="artist"] [data-part="meta-value"]').text()).toBe(
            'Unknown artist',
        )
        wrapper.unmount()
    })

    it('shows number, title, length, artist and the video badges', async () => {
        const { wrapper } = await mountWith(TrackGroupList, {
            groups: groupTracksByAlbum(tracks),
            label: 'Songs',
        })

        const rows = wrapper.findAll('[data-part="list-row"]')
        const cells = rows[0]!.findAll('[data-part="list-cell"]')
        expect(cells.map((cell) => cell.attributes('data-column'))).toEqual([
            'number',
            'title',
            'duration',
            'artist',
            'rating',
            'favorite',
        ])
        expect(cells.slice(0, 4).map((cell) => cell.text())).toEqual([
            '1',
            'Opening',
            '3:05',
            'Alpha Band',
        ])
        expect(cells[4]!.findAll('[data-part="rating-star"]')).toHaveLength(5)
        expect(cells[5]!.get('[data-part="btn-favorite"]').attributes('aria-pressed')).toBe('false')
        expect(rows[1]!.attributes('data-state')).toBe('has-video')
        expect(rows[1]!.get('[data-part="badge"]').attributes('data-state')).toBe('has-video')
        expect(rows[2]!.attributes('data-state')).toBe('has-video video-only')
        expect(rows[2]!.get('[data-part="badge"]').attributes('data-state')).toBe(
            'has-video video-only',
        )
        expect(rows[2]!.get('[data-part="badge"] [data-part="label"]').text()).toBe('Video only')
        // The badge sits before the title, in a column the list keeps for all rows.
        expect(wrapper.get('[data-part="list"]').attributes('data-state')).toBe('has-video')
        const title = rows[1]!.get('[data-part="list-cell"][data-column="title"]').element
        expect([...title.children].map((child) => child.getAttribute('data-part'))).toEqual([
            'badge',
            'label',
        ])
        expect(title.lastElementChild!.textContent).toBe('Closing')
        wrapper.unmount()
    })

    it('keeps no column for the video badge in lists without a video', async () => {
        const { wrapper } = await mountWith(TrackGroupList, {
            groups: groupTracksByAlbum(tracks.slice(0, 1)),
            label: 'Songs',
            state: 'placeholder',
        })

        expect(wrapper.get('[data-part="list"]').attributes('data-state')).toBe('placeholder')
        expect(wrapper.find('[data-part="badge"]').exists()).toBe(false)
        wrapper.unmount()
    })

    it('opens the menu of a track with a right click and leads to its details', async () => {
        const { wrapper, router } = await mountWith(TrackGroupListWithMenu, {
            groups: groupTracksByAlbum(tracks),
            label: 'Songs',
        })

        await wrapper.findAll('[data-part="list-row"]')[0]!.trigger('contextmenu', {
            clientX: 10,
            clientY: 20,
        })
        await flushPromises()

        expect(menuEntries()).toEqual([
            'play',
            'play-next',
            'add-to-queue',
            'add-to-playlist',
            'favorite',
            'rate',
            'details',
            'album',
            'artist',
            '-watch-video',
        ])
        ;(document.querySelector('[data-item="details"]') as HTMLElement).click()
        await flushPromises()
        expect(router.currentRoute.value.fullPath).toBe('/library/tracks/opening')
        wrapper.unmount()
    })

    it('offers admins to edit the metadata and to show the files of the track', async () => {
        useSessionStore().$patch({
            token: 'token',
            expiresAt: new Date(Date.now() + 60_000).toISOString(),
            profile: {
                id: 'admin',
                name: 'Admin',
                initials: 'A',
                color: '#1f6fd6',
                role: 'Admin',
                isBuiltIn: true,
            },
        })
        const { wrapper, router } = await mountWith(TrackGroupListWithMenu, {
            groups: groupTracksByAlbum(tracks),
            label: 'Songs',
        })

        await wrapper.findAll('[data-part="list-row"]')[2]!.trigger('contextmenu', {
            clientX: 10,
            clientY: 20,
        })
        await flushPromises()

        expect(menuEntries()).toEqual([
            '-play',
            '-play-next',
            '-add-to-queue',
            'add-to-playlist',
            'favorite',
            'rate',
            'details',
            '-album',
            '-artist',
            '-watch-video',
            'edit-metadata',
            'show-files',
        ])
        ;(document.querySelector('[data-item="edit-metadata"]') as HTMLElement).click()
        await flushPromises()
        expect(router.currentRoute.value.fullPath).toBe('/library/tracks/clip?edit=metadata')
        wrapper.unmount()
    })

    it('opens the menu with the keyboard and greys out what the track lacks', async () => {
        const { wrapper } = await mountWith(TrackGroupListWithMenu, {
            groups: groupTracksByAlbum(tracks),
            label: 'Songs',
        })

        await wrapper.findAll('[data-part="list-row"]')[2]!.trigger('keydown', {
            key: 'F10',
            shiftKey: true,
        })
        await flushPromises()

        expect(menuEntries()).toEqual([
            '-play',
            '-play-next',
            '-add-to-queue',
            'add-to-playlist',
            'favorite',
            'rate',
            'details',
            '-album',
            '-artist',
            '-watch-video',
        ])
        wrapper.unmount()
    })

    it('applies the menu to all rows chosen with Ctrl and Shift', async () => {
        const { wrapper } = await mountWith(TrackGroupListWithMenu, {
            groups: groupTracksByAlbum(tracks),
            label: 'Songs',
        })
        const player = usePlayerStore()
        const rows = wrapper.findAll('[data-part="list-row"]')

        await rows[0]!.trigger('click')
        await rows[2]!.trigger('click', { shiftKey: true })
        await rows[1]!.trigger('click', { ctrlKey: true })
        expect(rows.map((row) => row.attributes('aria-selected'))).toEqual([
            'true',
            'false',
            'true',
        ])
        expect(wrapper.get('[data-part="list"]').attributes('aria-multiselectable')).toBe('true')

        await rows[1]!.trigger('click', { ctrlKey: true })
        await rows[0]!.trigger('contextmenu', { clientX: 10, clientY: 20 })
        await flushPromises()

        expect(menuEntries()).toEqual([
            'play',
            'play-next',
            'add-to-queue',
            'add-to-playlist',
            'favorite',
            'rate',
            '-details',
            '-album',
            '-artist',
            '-watch-video',
        ])
        expect(
            document.querySelector('[data-part="context-menu"]')!.getAttribute('aria-label'),
        ).toBe('Actions for 3 songs')
        ;(document.querySelector('[data-item="add-to-queue"]') as HTMLElement).click()
        await flushPromises()

        // The track without audio stays out of the queue.
        expect(player.queue.items.map((item) => item.track.id)).toEqual(['opening', 'closing'])
        wrapper.unmount()
    })

    it('leaves out the details of the groups on request', async () => {
        const { wrapper } = await mountWith(TrackGroupList, {
            groups: groupTracksByAlbum(tracks),
            label: 'Songs',
            showGroupInfo: false,
        })

        expect(wrapper.find('[data-part="list-group-info"]').exists()).toBe(false)
        wrapper.unmount()
    })
})

describe('TileList', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
    })

    it('links every tile to its page and shows the icon of its kind without cover', async () => {
        const { wrapper } = await mountWith(TileList, {
            label: 'Artists',
            tiles: [
                {
                    id: 'alpha',
                    kind: 'artist',
                    title: 'Alpha Band',
                    subtitle: 'Songs: 3',
                    to: { name: 'library-artist', params: { id: 'alpha' } },
                },
            ],
        })

        const tile = wrapper.get('[data-part="tile"]')
        expect(tile.element.tagName).toBe('A')
        expect(tile.attributes('href')).toBe('/library/artists/alpha')
        expect(tile.attributes('data-kind')).toBe('artist')
        expect(tile.get('[data-part="tile-title"]').text()).toBe('Alpha Band')
        expect(tile.get('[data-part="tile-subtitle"]').text()).toBe('Songs: 3')
        expect(tile.get('[data-part="cover"] [data-part="icon"]').attributes('data-icon')).toBe(
            'artist',
        )
        wrapper.unmount()
    })
})

describe('TileList menus', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
    })

    it('opens the menu of a tile by right click, keyboard and its button', async () => {
        const album = { id: 'first', title: 'First Album', trackCount: 2 }
        const { wrapper, router } = await mountWith(TileList, {
            label: 'Albums',
            tiles: [
                {
                    id: 'first',
                    kind: 'album',
                    title: 'First Album',
                    to: { name: 'library-album', params: { id: 'first' } },
                    menu: { type: 'album', album },
                },
                {
                    id: 'Rock',
                    kind: 'genre',
                    title: 'Rock',
                    to: { name: 'library-genre', params: { genre: 'Rock' } },
                },
            ],
        })
        const menu = useContextMenuStore()
        const [withMenu, without] = wrapper.findAll('[data-part="tile-item"]')

        expect(withMenu!.find('[data-part="btn-item-menu"]').exists()).toBe(true)
        expect(without!.find('[data-part="btn-item-menu"]').exists()).toBe(false)

        await withMenu!.trigger('contextmenu', { clientX: 3, clientY: 4 })
        expect(menu.request).toMatchObject({ target: { type: 'album', album }, x: 3, y: 4 })

        await without!.trigger('contextmenu')
        await without!.get('[data-part="tile"]').trigger('keydown', { key: 'ContextMenu' })
        expect(menu.request?.sequence).toBe(1)

        await withMenu!.get('[data-part="tile"]').trigger('keydown', { key: 'ContextMenu' })
        await withMenu!.get('[data-part="btn-item-menu"]').trigger('click')
        expect(menu.request?.sequence).toBe(3)
        await flushPromises()
        expect(router.currentRoute.value.fullPath).toBe('/library/songs')
        wrapper.unmount()
    })
})

describe('LibraryCover', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
        covers.fetchAlbumCover.mockReset().mockResolvedValue(new Uint8Array([1]).buffer)
        vi.stubGlobal('URL', {
            ...URL,
            createObjectURL: () => 'blob:album',
            revokeObjectURL: vi.fn(),
        })
    })

    afterEach(() => {
        vi.unstubAllGlobals()
    })

    it('shows the cover once it is loaded', async () => {
        const { wrapper } = await mountWith(LibraryCover, {
            kind: 'album',
            id: 'a',
            available: true,
        })

        expect(wrapper.get('[data-part="cover-image"]').attributes('src')).toBe('blob:album')
        expect(wrapper.get('[data-part="cover"]').attributes('data-state')).toBeUndefined()
        // jsdom has no layout: without a width the default size is loaded.
        expect(covers.fetchAlbumCover).toHaveBeenCalledWith('a', 300)
        wrapper.unmount()
    })

    it('loads the size the cover is shown with', async () => {
        const measure = vi.spyOn(HTMLElement.prototype, 'offsetWidth', 'get').mockReturnValue(48)
        vi.stubGlobal('devicePixelRatio', 2)

        const { wrapper } = await mountWith(LibraryCover, {
            kind: 'album',
            id: 'mini',
            available: true,
        })

        expect(covers.fetchAlbumCover).toHaveBeenCalledWith('mini', 96)
        wrapper.unmount()
        measure.mockRestore()
    })

    it('loads sharp covers on phones with a pixel density of 3', async () => {
        const measure = vi.spyOn(HTMLElement.prototype, 'offsetWidth', 'get').mockReturnValue(44)
        vi.stubGlobal('devicePixelRatio', 3)

        const { wrapper } = await mountWith(LibraryCover, {
            kind: 'album',
            id: 'mini',
            available: true,
        })

        expect(covers.fetchAlbumCover).toHaveBeenCalledWith('mini', 192)
        wrapper.unmount()
        measure.mockRestore()
    })

    it('loads a larger picture when the cover grows and keeps the smaller one meanwhile', async () => {
        let width = 44
        const measure = vi
            .spyOn(HTMLElement.prototype, 'offsetWidth', 'get')
            .mockImplementation(() => width)
        vi.stubGlobal('devicePixelRatio', 3)
        let resize: (entries: { target: Element }[]) => void = () => {}
        vi.stubGlobal(
            'ResizeObserver',
            class {
                constructor(callback: typeof resize) {
                    resize = callback
                }

                observe = vi.fn()
                unobserve = vi.fn()
                disconnect = vi.fn()
            },
        )
        let finishLarge: (buffer: ArrayBuffer) => void = () => {}
        covers.fetchAlbumCover.mockImplementation((_: string, size: number) =>
            size === 1000
                ? new Promise<ArrayBuffer>((resolve) => {
                      finishLarge = resolve
                  })
                : Promise.resolve(new Uint8Array([1]).buffer),
        )
        let created = 0
        vi.stubGlobal('URL', {
            ...URL,
            createObjectURL: () => `blob:album-${++created}`,
            revokeObjectURL: vi.fn(),
        })

        const { wrapper } = await mountWith(LibraryCover, {
            kind: 'album',
            id: 'playing',
            available: true,
        })
        const image = () => wrapper.get('[data-part="cover-image"]').attributes('src')
        expect(covers.fetchAlbumCover).toHaveBeenLastCalledWith('playing', 192)
        expect(image()).toBe('blob:album-1')

        // The mini player becomes the full-screen player.
        width = 300
        resize([{ target: wrapper.element }])
        await flushPromises()
        expect(covers.fetchAlbumCover).toHaveBeenLastCalledWith('playing', 1000)
        expect(image()).toBe('blob:album-1')

        finishLarge(new Uint8Array([2]).buffer)
        await flushPromises()
        expect(image()).toBe('blob:album-2')

        // Smaller again: the sharp picture stays.
        width = 44
        resize([{ target: wrapper.element }])
        await flushPromises()
        expect(covers.fetchAlbumCover).toHaveBeenCalledTimes(2)
        expect(image()).toBe('blob:album-2')
        wrapper.unmount()
        measure.mockRestore()
    })

    it('shows the placeholder and loads nothing without a cover', async () => {
        const { wrapper } = await mountWith(LibraryCover, {
            kind: 'album',
            id: 'a',
            available: false,
        })

        expect(wrapper.find('[data-part="cover-image"]').exists()).toBe(false)
        expect(wrapper.get('[data-part="cover"]').attributes('data-state')).toBe('placeholder')
        expect(covers.fetchAlbumCover).not.toHaveBeenCalled()
        wrapper.unmount()
    })
})

describe('TrackFileList', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
    })

    it('shows path, kind, use and technical details of every file', async () => {
        const { wrapper } = await mountWith(TrackFileList, {
            label: 'Files',
            files: [
                {
                    id: 'flac',
                    kind: 'Audio',
                    relativePath: 'Band/Album/02 - Song.flac',
                    isPrimary: true,
                    isLossless: true,
                    isWritable: true,
                    codec: 'flac',
                    bitrate: 912_400,
                    sampleRate: 44_100,
                    bitDepth: 16,
                    channels: 2,
                    durationMilliseconds: 61_000,
                    size: 4_200_000,
                },
                {
                    id: 'mkv',
                    kind: 'Video',
                    relativePath: 'Band/Album/02 - Song.mkv',
                    isPrimary: false,
                    width: 1920,
                    height: 1080,
                    size: 999,
                    issue: 'file.probe_failed',
                },
            ],
        })

        const items = wrapper.findAll('[data-part="file-item"]')
        expect(items.map((item) => item.attributes('data-state'))).toEqual([
            'audio primary',
            'video',
        ])
        expect(items[0]!.get('[data-part="file-path"]').text()).toBe('Band/Album/02 - Song.flac')
        const badges = items[0]!
            .findAll('[data-part="badge"]')
            .map((badge) => badge.attributes('data-state'))
        expect(badges).toEqual(['audio', 'primary', 'lossless', 'writable'])
        const details = items[0]!
            .findAll('[data-part="file-meta"] > [data-part="label"]')
            .map((label) => label.text())
        expect(details).toEqual([
            'FLAC',
            '912 kbit/s',
            '44.1 kHz',
            '16 bit',
            'Channels: 2',
            '1:01',
            '4.2 MB',
        ])
        expect(items[1]!.get('[data-part="badge"][data-state="issue"]').text()).toBe(
            'No media information',
        )
        expect(items[1]!.text()).toContain('1920 × 1080')
        wrapper.unmount()
    })
})
