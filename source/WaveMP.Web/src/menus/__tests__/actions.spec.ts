import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { PlayerViewModeObject, type Track } from '@/api/generated/models'
import { registerDefaultMenuActions, resetDefaultMenuActions } from '../actions'
import {
    clearMenuActions,
    registerMenuActions,
    resolveMenu,
    runMenuAction,
    type ResolvedMenuEntry,
} from '../registry'
import type { MenuServices, MenuTarget, NowPlayingTarget, SongTarget } from '../targets'

const album = { id: 'album-1', title: 'First Album' }
const artist = { id: 'artist-1', name: 'Alpha Band' }
const song: Track = {
    id: 'song',
    title: 'Song',
    album,
    artist,
    hasAudio: true,
    hasVideo: true,
    rating: 3,
}
const plain: Track = { id: 'plain', title: 'Plain', album, artist, hasAudio: true, rating: 3 }
const clip: Track = { id: 'clip', title: 'Clip', hasAudio: false, hasVideo: true }

function fakeServices(overrides: Partial<MenuServices> = {}): MenuServices {
    const favorites = new Set<string>()
    const ratings = new Map<string, number>()
    return {
        isAdmin: false,
        playTracks: vi.fn(),
        playNext: vi.fn(),
        addToQueue: vi.fn(),
        isFavorite: (type, id, fallback) => favorites.has(`${type}:${id}`) || !!fallback,
        setFavorite: vi.fn((type, id, value) => {
            if (value) {
                favorites.add(`${type}:${id}`)
            } else {
                favorites.delete(`${type}:${id}`)
            }
        }),
        ratingOf: (id, fallback) => ratings.get(id) ?? fallback ?? 0,
        rate: vi.fn((id, rating) => ratings.set(id, rating)),
        navigate: vi.fn(),
        addToPlaylist: vi.fn(),
        addToOwnAlbum: vi.fn(),
        watchVideo: vi.fn(),
        tracksOf: vi.fn(() => Promise.resolve([plain, clip, song])),
        renamePlaylist: vi.fn(),
        changePlaylistCover: vi.fn(),
        deletePlaylist: vi.fn(),
        convertSmartPlaylist: vi.fn(),
        deleteSmartPlaylist: vi.fn(),
        queue: { playNext: vi.fn(), move: vi.fn(), remove: vi.fn() },
        ...overrides,
    }
}

/** Ids of the entries, disabled ones with a minus, checked ones with a star, separators as |. */
function summary(entries: readonly ResolvedMenuEntry[]): string[] {
    return entries.map((entry) =>
        entry.separator
            ? '|'
            : `${entry.disabled ? '-' : ''}${entry.id}${entry.checked ? '*' : ''}`,
    )
}

beforeEach(() => {
    clearMenuActions()
    resetDefaultMenuActions()
    registerDefaultMenuActions()
})

afterEach(() => {
    clearMenuActions()
    resetDefaultMenuActions()
})

describe('registry', () => {
    it('adds actions per type after the registered ones, divided by group', () => {
        registerMenuActions('artist', [
            { id: 'extra', group: 'more', label: 'contextMenu.details', run: vi.fn() },
        ])

        const entries = resolveMenu(
            { type: 'artist', artist: { ...artist, trackCount: 2 } },
            fakeServices(),
        )
        expect(summary(entries)).toEqual(['play', 'shuffle', '|', 'favorite', '|', 'extra'])
    })

    it('runs no action that is hidden or disabled', async () => {
        const services = fakeServices()
        const target: SongTarget = { type: 'song', tracks: [clip] }

        expect(await runMenuAction(target, 'play', services)).toBe(false)
        expect(await runMenuAction(target, 'edit-metadata', services)).toBe(false)
        expect(await runMenuAction(target, 'unknown', services)).toBe(false)
        expect(services.playTracks).not.toHaveBeenCalled()
        expect(services.navigate).not.toHaveBeenCalled()
    })
})

describe('menu of songs', () => {
    it('lists all entries of one song and leaves those for admins out', () => {
        const entries = resolveMenu({ type: 'song', tracks: [song] }, fakeServices())

        expect(summary(entries)).toEqual([
            'play',
            'play-next',
            'add-to-queue',
            '|',
            'add-to-playlist',
            'favorite',
            'rate',
            '|',
            'details',
            'album',
            'artist',
            'watch-video',
        ])
        expect(entries.find((entry) => entry.id === 'favorite')?.labelKey).toBe(
            'contextMenu.addFavorite',
        )
        expect(summary(entries.find((entry) => entry.id === 'rate')?.children ?? [])).toEqual([
            'rate-0',
            'rate-1',
            'rate-2',
            'rate-3*',
            'rate-4',
            'rate-5',
        ])
    })

    it('shows the entries for admins and greys out what a song without audio cannot do', () => {
        const entries = resolveMenu(
            { type: 'song', tracks: [clip] },
            fakeServices({ isAdmin: true }),
        )

        expect(summary(entries)).toEqual([
            '-play',
            '-play-next',
            '-add-to-queue',
            '|',
            'add-to-playlist',
            'favorite',
            'rate',
            '|',
            'details',
            '-album',
            '-artist',
            '-watch-video',
            '|',
            'edit-metadata',
            'show-files',
            '-add-to-own-album',
        ])
    })

    it('applies to all selected songs where it makes sense', async () => {
        const services = fakeServices({ isAdmin: true })
        const target: SongTarget = { type: 'song', tracks: [song, clip, plain] }

        expect(summary(resolveMenu(target, services))).toEqual([
            'play',
            'play-next',
            'add-to-queue',
            '|',
            'add-to-playlist',
            'favorite',
            'rate',
            '|',
            '-details',
            '-album',
            '-artist',
            '-watch-video',
            '|',
            '-edit-metadata',
            '-show-files',
            'add-to-own-album',
        ])

        await runMenuAction(target, 'play', services)
        expect(services.playTracks).toHaveBeenCalledWith([song, plain], 0)
        await runMenuAction(target, 'play-next', services)
        expect(services.playNext).toHaveBeenCalledWith([song, plain])
        await runMenuAction(target, 'add-to-playlist', services)
        expect(services.addToPlaylist).toHaveBeenCalledWith(['song', 'clip', 'plain'])
        await runMenuAction(target, 'add-to-own-album', services)
        expect(services.addToOwnAlbum).toHaveBeenCalledWith(['song', 'plain'])
        await runMenuAction(target, 'rate-5', services)
        expect(vi.mocked(services.rate).mock.calls).toEqual([
            ['song', 5],
            ['clip', 5],
            ['plain', 5],
        ])
        const rate = resolveMenu(target, services).find((entry) => entry.id === 'rate')
        expect(rate?.children?.find((entry) => entry.checked)?.id).toBe('rate-5')
    })

    it('marks all songs as favorites, and removes them once all of them are', async () => {
        const services = fakeServices()
        const target: SongTarget = { type: 'song', tracks: [song, { ...plain, isFavorite: true }] }

        await runMenuAction(target, 'favorite', services)
        expect(vi.mocked(services.setFavorite).mock.calls).toEqual([
            ['Track', 'song', true],
            ['Track', 'plain', true],
        ])
        expect(
            resolveMenu(target, services).find((entry) => entry.id === 'favorite'),
        ).toMatchObject({ labelKey: 'contextMenu.removeFavorite', icon: 'remove-favorite' })

        vi.mocked(services.setFavorite).mockClear()
        await runMenuAction({ type: 'song', tracks: [song] }, 'favorite', services)
        expect(services.setFavorite).toHaveBeenCalledWith('Track', 'song', false)
    })

    it('plays the list from the song where the list offers it', async () => {
        const services = fakeServices()
        const playFrom = vi.fn()

        await runMenuAction({ type: 'song', tracks: [song], playFrom }, 'play', services)

        expect(playFrom).toHaveBeenCalledOnce()
        expect(services.playTracks).not.toHaveBeenCalled()
    })

    it('leads to album, artist, video, metadata and files of one song', async () => {
        const services = fakeServices({ isAdmin: true })
        const target: SongTarget = { type: 'song', tracks: [song] }

        for (const id of ['details', 'album', 'artist', 'edit-metadata', 'show-files']) {
            await runMenuAction(target, id, services)
        }
        await runMenuAction(target, 'watch-video', services)

        expect(vi.mocked(services.navigate).mock.calls.map(([to]) => to)).toEqual([
            { name: 'library-track', params: { id: 'song' } },
            { name: 'library-album', params: { id: 'album-1' } },
            { name: 'library-artist', params: { id: 'artist-1' } },
            { name: 'library-track', params: { id: 'song' }, query: { edit: 'metadata' } },
            { name: 'admin-track-files', params: { id: 'song' } },
        ])
        expect(services.watchVideo).toHaveBeenCalledWith(song)
    })

    it('moves and removes entries on the page of a playlist', async () => {
        const services = fakeServices()
        const entry = {
            canMoveUp: false,
            canMoveDown: true,
            canRemove: true,
            busy: false,
            moveUp: vi.fn(),
            moveDown: vi.fn(),
            remove: vi.fn(),
        }
        const one: SongTarget = { type: 'song', tracks: [song], playlistEntry: entry }

        expect(summary(resolveMenu(one, services)).slice(-4)).toEqual([
            '|',
            '-move-up',
            'move-down',
            'remove-from-playlist',
        ])
        await runMenuAction(one, 'move-down', services)
        expect(entry.moveDown).toHaveBeenCalledOnce()

        const several: SongTarget = { type: 'song', tracks: [song, plain], playlistEntry: entry }
        expect(summary(resolveMenu(several, services)).slice(-3)).toEqual([
            '-move-up',
            '-move-down',
            'remove-from-playlist',
        ])
        await runMenuAction(several, 'remove-from-playlist', services)
        expect(entry.remove).toHaveBeenCalledOnce()

        const busy: SongTarget = { ...one, playlistEntry: { ...entry, busy: true } }
        expect(summary(resolveMenu(busy, services)).slice(-3)).toEqual([
            '-move-up',
            '-move-down',
            '-remove-from-playlist',
        ])

        const gone: SongTarget = {
            ...one,
            playlistEntry: { ...entry, canMoveDown: false, canRemove: false },
        }
        expect(summary(resolveMenu(gone, services)).slice(-3)).toEqual([
            '-move-up',
            '-move-down',
            '-remove-from-playlist',
        ])
        entry.remove.mockClear()
        expect(await runMenuAction(gone, 'remove-from-playlist', services)).toBe(false)
        expect(entry.remove).not.toHaveBeenCalled()
    })
})

describe('menus of albums, artists, playlists and smart playlists', () => {
    it('plays, shuffles and queues the songs with audio of an album', async () => {
        const services = fakeServices()
        const target: MenuTarget = {
            type: 'album',
            album: { ...album, artist, trackCount: 3 },
        }

        expect(summary(resolveMenu(target, services))).toEqual([
            'play',
            'shuffle',
            'add-to-queue',
            '|',
            'add-to-playlist',
            'favorite',
            '|',
            'artist',
        ])

        await runMenuAction(target, 'play', services)
        await runMenuAction(target, 'shuffle', services)
        await runMenuAction(target, 'add-to-queue', services)
        await runMenuAction(target, 'add-to-playlist', services)
        await runMenuAction(target, 'favorite', services)
        await runMenuAction(target, 'artist', services)

        expect(services.tracksOf).toHaveBeenCalledWith({ kind: 'album', id: 'album-1' })
        expect(vi.mocked(services.playTracks).mock.calls).toEqual([
            [[plain, song], 0, { shuffle: false, source: { kind: 'album', id: 'album-1' } }],
            [[plain, song], -1, { shuffle: true, source: { kind: 'album', id: 'album-1' } }],
        ])
        expect(services.addToQueue).toHaveBeenCalledWith([plain, song])
        expect(services.addToPlaylist).toHaveBeenCalledWith(['plain', 'clip', 'song'])
        expect(services.setFavorite).toHaveBeenCalledWith('Album', 'album-1', true)
        expect(services.navigate).toHaveBeenCalledWith({
            name: 'library-artist',
            params: { id: 'artist-1' },
        })
    })

    it('greys out playing an empty album and the artist of an album without one', () => {
        const entries = resolveMenu(
            { type: 'album', album: { id: 'empty', trackCount: 0 } },
            fakeServices(),
        )

        expect(summary(entries)).toEqual([
            '-play',
            '-shuffle',
            '-add-to-queue',
            '|',
            '-add-to-playlist',
            'favorite',
            '|',
            '-artist',
        ])
    })

    it('plays and marks an artist', async () => {
        const services = fakeServices()
        const target: MenuTarget = { type: 'artist', artist: { ...artist, trackCount: 3 } }

        await runMenuAction(target, 'shuffle', services)
        await runMenuAction(target, 'favorite', services)

        expect(services.tracksOf).toHaveBeenCalledWith({ kind: 'artist', id: 'artist-1' })
        expect(services.playTracks).toHaveBeenCalledWith([plain, song], -1, {
            shuffle: true,
            source: { kind: 'artist', id: 'artist-1' },
        })
        expect(services.setFavorite).toHaveBeenCalledWith('Artist', 'artist-1', true)
    })

    it('plays, renames, changes the cover of and deletes a playlist', async () => {
        const services = fakeServices()
        const playlist = { id: 'list', name: 'Mix', trackCount: 2 }
        const target: MenuTarget = { type: 'playlist', playlist }

        expect(summary(resolveMenu(target, services))).toEqual([
            'play',
            'shuffle',
            '|',
            'rename',
            'change-cover',
            '|',
            'delete',
        ])

        await runMenuAction(target, 'play', services)
        await runMenuAction(target, 'rename', services)
        await runMenuAction(target, 'change-cover', services)
        await runMenuAction(target, 'delete', services)

        expect(services.tracksOf).toHaveBeenCalledWith({ kind: 'playlist', id: 'list' })
        expect(services.renamePlaylist).toHaveBeenCalledWith(playlist)
        expect(services.changePlaylistCover).toHaveBeenCalledWith(playlist)
        expect(services.deletePlaylist).toHaveBeenCalledWith(playlist)
    })

    it('greys out playing an empty playlist', () => {
        const entries = resolveMenu(
            { type: 'playlist', playlist: { id: 'list', trackCount: 0 } },
            fakeServices(),
        )

        expect(summary(entries).slice(0, 2)).toEqual(['-play', '-shuffle'])
    })

    it('plays, edits, converts and deletes a smart playlist', async () => {
        const services = fakeServices()
        const smartPlaylist = { id: 'smart', name: 'Good', trackCount: 3 }
        const target: MenuTarget = { type: 'smart-playlist', smartPlaylist }

        expect(summary(resolveMenu(target, services))).toEqual([
            'play',
            'shuffle',
            '|',
            'edit-rules',
            'convert',
            '|',
            'delete',
        ])

        await runMenuAction(target, 'shuffle', services)
        await runMenuAction(target, 'edit-rules', services)
        await runMenuAction(target, 'convert', services)
        await runMenuAction(target, 'delete', services)

        expect(services.tracksOf).toHaveBeenCalledWith({ kind: 'smart-playlist', id: 'smart' })
        expect(services.playTracks).toHaveBeenCalledWith([plain, song], -1, {
            shuffle: true,
            source: { kind: 'smart-playlist', id: 'smart' },
        })
        expect(services.navigate).toHaveBeenCalledWith({
            name: 'smart-playlist-edit',
            params: { id: 'smart' },
        })
        expect(services.convertSmartPlaylist).toHaveBeenCalledWith(smartPlaylist)
        expect(services.deleteSmartPlaylist).toHaveBeenCalledWith(smartPlaylist)
    })

    it('greys out playing a smart playlist without songs', () => {
        const entries = resolveMenu(
            { type: 'smart-playlist', smartPlaylist: { id: 'smart', trackCount: 0 } },
            fakeServices(),
        )

        expect(summary(entries).slice(0, 2)).toEqual(['-play', '-shuffle'])
    })
})

describe('menu of a queue entry', () => {
    const entry = (index: number) =>
        ({
            type: 'queue-entry',
            key: `q${index}`,
            track: song,
            index,
            count: 4,
            currentIndex: 1,
        }) as const

    it('greys out what does not apply to the position of the entry', () => {
        const services = fakeServices()

        expect(summary(resolveMenu(entry(0), services))).toEqual([
            'play-next',
            '-move-up',
            'move-down',
            '|',
            'remove',
        ])
        expect(summary(resolveMenu(entry(1), services))).toEqual([
            '-play-next',
            'move-up',
            'move-down',
            '|',
            '-remove',
        ])
        expect(summary(resolveMenu(entry(2), services))[0]).toBe('-play-next')
        expect(summary(resolveMenu(entry(3), services))).toEqual([
            'play-next',
            'move-up',
            '-move-down',
            '|',
            'remove',
        ])
    })

    it('plays the entry next, moves and removes it', async () => {
        const services = fakeServices()

        await runMenuAction(entry(3), 'play-next', services)
        await runMenuAction(entry(3), 'move-up', services)
        await runMenuAction(entry(0), 'move-down', services)
        await runMenuAction(entry(3), 'remove', services)

        expect(services.queue.playNext).toHaveBeenCalledWith('q3')
        expect(services.queue.move).toHaveBeenNthCalledWith(1, 'q3', -1)
        expect(services.queue.move).toHaveBeenNthCalledWith(2, 'q0', 1)
        expect(services.queue.remove).toHaveBeenCalledWith('q3')
    })
})

describe('menu of Now Playing', () => {
    function stage(overrides: Partial<NowPlayingTarget> = {}): NowPlayingTarget {
        return {
            type: 'now-playing',
            views: [
                { mode: PlayerViewModeObject.Cover, available: true },
                { mode: PlayerViewModeObject.Video, available: false },
                { mode: PlayerViewModeObject.Visualization, available: true },
                { mode: PlayerViewModeObject.Lyrics, available: false },
            ],
            selected: PlayerViewModeObject.Visualization,
            selectView: vi.fn(),
            fullscreen: { available: true, active: false, toggle: vi.fn() },
            ...overrides,
        }
    }

    it('chooses the view and greys out views the song lacks', async () => {
        const target = stage()
        const services = fakeServices()

        expect(summary(resolveMenu(target, services))).toEqual([
            'view-cover',
            '-view-video',
            'view-visualization*',
            '-view-lyrics',
            '|',
            'fullscreen',
        ])
        await runMenuAction(target, 'view-cover', services)
        expect(target.selectView).toHaveBeenCalledWith('Cover')
        expect(await runMenuAction(target, 'view-video', services)).toBe(false)
    })

    it('enters and leaves the full screen of the view shown', async () => {
        const services = fakeServices()
        const toggle = vi.fn()
        const active = stage({ fullscreen: { available: true, active: true, toggle } })

        expect(resolveMenu(active, services).at(-1)).toMatchObject({
            id: 'fullscreen',
            labelKey: 'contextMenu.exitFullscreen',
            icon: 'exit-fullscreen',
        })
        await runMenuAction(active, 'fullscreen', services)
        expect(toggle).toHaveBeenCalledOnce()

        const cover = stage({ fullscreen: { available: false, active: false, toggle } })
        expect(summary(resolveMenu(cover, services)).at(-1)).toBe('-fullscreen')
    })
})
