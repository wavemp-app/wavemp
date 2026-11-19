import { describe, expect, it } from 'vitest'
import { activeTreeId, buildBreadcrumb, pageTitleKey } from '../breadcrumb'

describe('buildBreadcrumb', () => {
    it('shows music, library and the section of the library', () => {
        const crumbs = buildBreadcrumb({ name: 'library-section', params: { section: 'songs' } })

        expect(crumbs).toEqual([
            { id: 'music', labelKey: 'breadcrumb.music', to: { name: 'library' } },
            { id: 'library', labelKey: 'nav.library', to: { name: 'library' } },
            { id: 'songs', labelKey: 'library.songs' },
        ])
    })

    it('shows the equalizer below Now Playing', () => {
        expect(buildBreadcrumb({ name: 'equalizer', params: {} })).toEqual([
            { id: 'music', labelKey: 'breadcrumb.music', to: { name: 'library' } },
            { id: 'now-playing', labelKey: 'nav.nowPlaying', to: { name: 'now-playing' } },
            { id: 'equalizer', labelKey: 'nav.equalizer' },
        ])
        expect(pageTitleKey({ name: 'equalizer', params: {} })).toBe('nav.equalizer')
    })

    it('shows the main pages below music', () => {
        expect(buildBreadcrumb({ name: 'now-playing', params: {} }).map((c) => c.id)).toEqual([
            'music',
            'now-playing',
        ])
        expect(buildBreadcrumb({ name: 'playlists', params: {} }).map((c) => c.id)).toEqual([
            'music',
            'playlists',
        ])
        expect(buildBreadcrumb({ name: 'settings', params: {} }).map((c) => c.id)).toEqual([
            'music',
            'settings',
        ])
        expect(buildBreadcrumb({ name: 'settings-pin', params: {} }).map((c) => c.id)).toEqual([
            'music',
            'settings',
            'settings-pin',
        ])
        expect(
            buildBreadcrumb({ name: 'settings-scrobbling-lastfm', params: {} }).map((c) => c.id),
        ).toEqual(['music', 'settings', 'settings-scrobbling-lastfm'])
        expect(buildBreadcrumb({ name: 'not-found', params: {} }).map((c) => c.id)).toEqual([
            'music',
            'not-found',
        ])
    })

    it('shows the pages of the administration below the administration', () => {
        for (const page of [
            'admin-library',
            'admin-jobs',
            'admin-cache',
            'admin-transcoding',
            'admin-backups',
            'admin-users',
        ]) {
            const crumbs = buildBreadcrumb({ name: page, params: {} })
            expect(crumbs.map((c) => c.id)).toEqual(['music', 'admin', page])
            expect(crumbs[1]).toEqual({
                id: 'admin',
                labelKey: 'admin.title',
                to: { name: 'admin' },
            })
        }

        expect(pageTitleKey({ name: 'admin-library', params: {} })).toBe('adminLibrary.title')
        expect(pageTitleKey({ name: 'admin-jobs', params: {} })).toBe('adminJobs.title')
        expect(pageTitleKey({ name: 'admin-cache', params: {} })).toBe('adminCache.title')
        expect(pageTitleKey({ name: 'admin-files', params: {} })).toBe('adminFiles.title')
    })

    it('shows the files of a song below the file selection, with its title once known', () => {
        const route = { name: 'admin-track-files', params: { id: 'track-1' } }

        expect(buildBreadcrumb(route).map((c) => c.id)).toEqual(['music', 'admin', 'admin-files'])
        expect(buildBreadcrumb(route, 'Song')).toEqual([
            { id: 'music', labelKey: 'breadcrumb.music', to: { name: 'library' } },
            { id: 'admin', labelKey: 'admin.title', to: { name: 'admin' } },
            { id: 'admin-files', labelKey: 'adminFiles.title', to: { name: 'admin-files' } },
            { id: 'admin-track-files', labelKey: 'adminFiles.title', label: 'Song' },
        ])
    })

    it('shows a song of the review inbox below the inbox, with its title once known', () => {
        const route = { name: 'admin-review-track', params: { id: 'track-1' } }

        expect(pageTitleKey({ name: 'admin-review', params: {} })).toBe('reviewInbox.title')
        expect(buildBreadcrumb(route).map((c) => c.id)).toEqual(['music', 'admin', 'admin-review'])
        expect(buildBreadcrumb(route, 'Song').at(-2)).toEqual({
            id: 'admin-review',
            labelKey: 'reviewInbox.title',
            to: { name: 'admin-review' },
        })
        expect(buildBreadcrumb(route, 'Song').at(-1)).toEqual({
            id: 'admin-review-track',
            labelKey: 'reviewInbox.title',
            label: 'Song',
        })
    })

    it('never links the current page', () => {
        const crumbs = buildBreadcrumb({ name: 'playlists', params: {} })

        expect(crumbs.at(-1)?.to).toBeUndefined()
        expect(buildBreadcrumb({ name: 'other', params: {} })).toEqual([
            { id: 'music', labelKey: 'breadcrumb.music' },
        ])
    })
})

describe('pageTitleKey', () => {
    it('is the label of the current page', () => {
        expect(pageTitleKey({ name: 'library-section', params: { section: 'albums' } })).toBe(
            'library.albums',
        )
        expect(pageTitleKey({ name: 'now-playing', params: {} })).toBe('nav.nowPlaying')
        expect(pageTitleKey({ name: 'settings', params: {} })).toBe('settings.title')
        expect(pageTitleKey({ name: 'admin-users', params: {} })).toBe('adminUsers.title')
    })
})

describe('activeTreeId', () => {
    it('selects the section of the library or the playlists', () => {
        expect(activeTreeId({ name: 'library-section', params: { section: 'years' } })).toBe(
            'years',
        )
        expect(activeTreeId({ name: 'playlists', params: {} })).toBe('playlists')
        expect(activeTreeId({ name: 'now-playing', params: {} })).toBeUndefined()
        expect(activeTreeId({ name: 'settings', params: {} })).toBeUndefined()
        expect(
            activeTreeId({ name: 'library-section', params: { section: 'unknown' } }),
        ).toBeUndefined()
    })

    it('selects the node of a playlist page', () => {
        expect(activeTreeId({ name: 'playlists-new', params: {} })).toBe('create-playlist')
        expect(activeTreeId({ name: 'playlists-recently-played', params: {} })).toBe(
            'recently-played',
        )
        expect(activeTreeId({ name: 'playlist', params: { id: 'p1' } })).toBe('playlist:p1')
    })
})

describe('playlist pages', () => {
    const playlists = { id: 'playlists', labelKey: 'nav.playlists', to: { name: 'playlists' } }

    it('show the overview below music, also while a new playlist is created', () => {
        for (const name of ['playlists', 'playlists-new']) {
            expect(buildBreadcrumb({ name, params: {} }).map((c) => c.id)).toEqual([
                'music',
                'playlists',
            ])
        }
    })

    it('show recently played and a playlist below the overview', () => {
        expect(buildBreadcrumb({ name: 'playlists-recently-played', params: {} })).toEqual([
            { id: 'music', labelKey: 'breadcrumb.music', to: { name: 'library' } },
            playlists,
            { id: 'recently-played', labelKey: 'playlists.recentlyPlayed' },
        ])
        expect(buildBreadcrumb({ name: 'playlist', params: { id: 'p1' } }, 'Road Trip')).toEqual([
            { id: 'music', labelKey: 'breadcrumb.music', to: { name: 'library' } },
            playlists,
            { id: 'playlist', labelKey: 'nav.playlists', label: 'Road Trip' },
        ])
    })

    it('show a smart playlist, its editor and a new one below the overview', () => {
        const music = { id: 'music', labelKey: 'breadcrumb.music', to: { name: 'library' } }

        expect(
            buildBreadcrumb({ name: 'smart-playlist', params: { id: 's1' } }, 'Top Rated'),
        ).toEqual([
            music,
            playlists,
            { id: 'playlist', labelKey: 'nav.playlists', label: 'Top Rated' },
        ])
        expect(buildBreadcrumb({ name: 'smart-playlists-new', params: {} })).toEqual([
            music,
            playlists,
            { id: 'smart-playlist-new', labelKey: 'smartPlaylists.newTitle' },
        ])
        expect(
            buildBreadcrumb({ name: 'smart-playlist-edit', params: { id: 's1' } }, 'Top Rated'),
        ).toEqual([
            music,
            playlists,
            {
                id: 'playlist',
                labelKey: 'nav.playlists',
                label: 'Top Rated',
                to: { name: 'smart-playlist', params: { id: 's1' } },
            },
            { id: 'smart-playlist-edit', labelKey: 'smartPlaylists.editTitle' },
        ])
        expect(
            buildBreadcrumb({ name: 'smart-playlist-edit', params: { id: 's1' } }).map((c) => c.id),
        ).toEqual(['music', 'playlists', 'smart-playlist-edit'])
    })

    it('select the node of a smart playlist, also while its rules are edited', () => {
        expect(activeTreeId({ name: 'smart-playlist', params: { id: 's1' } })).toBe(
            'smart-playlist:s1',
        )
        expect(activeTreeId({ name: 'smart-playlist-edit', params: { id: 's1' } })).toBe(
            'smart-playlist:s1',
        )
        expect(activeTreeId({ name: 'smart-playlists-new', params: {} })).toBe('playlists')
    })
})

describe('detail pages', () => {
    it('end the breadcrumb with the name below their section', () => {
        const crumbs = buildBreadcrumb(
            { name: 'library-album', params: { id: 'a' } },
            'First Album',
        )

        expect(crumbs).toEqual([
            { id: 'music', labelKey: 'breadcrumb.music', to: { name: 'library' } },
            { id: 'library', labelKey: 'nav.library', to: { name: 'library' } },
            {
                id: 'albums',
                labelKey: 'library.albums',
                to: { name: 'library-section', params: { section: 'albums' } },
            },
            { id: 'library-album', labelKey: 'library.albums', label: 'First Album' },
        ])
    })

    it('end with their section while the name is unknown', () => {
        const crumbs = buildBreadcrumb({ name: 'library-genre', params: { genre: 'Rock' } })

        expect(crumbs.at(-1)).toEqual({ id: 'genres', labelKey: 'library.genres' })
        expect(pageTitleKey({ name: 'library-track', params: { id: 't' } })).toBe('library.songs')
    })

    it('select the tree node of their section', () => {
        expect(activeTreeId({ name: 'library-album', params: {} })).toBe('albums')
        expect(activeTreeId({ name: 'library-artist', params: {} })).toBe('artists')
        expect(activeTreeId({ name: 'library-genre', params: {} })).toBe('genres')
        expect(activeTreeId({ name: 'library-year', params: {} })).toBe('years')
        expect(activeTreeId({ name: 'library-track', params: {} })).toBe('songs')
        expect(activeTreeId({ name: 'library-search', params: {} })).toBeUndefined()
    })

    it('show the search below the library', () => {
        expect(buildBreadcrumb({ name: 'library-search', params: {} }).map((c) => c.id)).toEqual([
            'music',
            'library',
            'search',
        ])
    })
})

describe('home page', () => {
    it('shows music and the home page and is no node of the sidebar', () => {
        const route = { name: 'home', params: {} }

        expect(buildBreadcrumb(route)).toEqual([
            { id: 'music', labelKey: 'breadcrumb.music', to: { name: 'library' } },
            { id: 'home', labelKey: 'nav.home' },
        ])
        expect(pageTitleKey(route)).toBe('nav.home')
        expect(activeTreeId(route)).toBeUndefined()
        expect(activeTreeId({ name: 'library-section', params: { section: 'favorites' } })).toBe(
            'favorites',
        )
    })
})
