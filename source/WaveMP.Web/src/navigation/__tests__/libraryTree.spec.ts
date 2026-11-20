import { describe, expect, it } from 'vitest'
import {
    buildLibraryTree,
    initiallyExpanded,
    librarySections,
    libraryTree,
    mainNavigation,
    playlistNodeId,
    smartPlaylistNodeId,
} from '../libraryTree'

describe('buildLibraryTree', () => {
    it('leads to creating a playlist and to the recently played tracks', () => {
        const playlists = libraryTree.find((node) => node.id === 'playlists')!

        expect(playlists.children?.map((node) => [node.id, node.to, node.disabled])).toEqual([
            ['create-playlist', { name: 'playlists-new' }, undefined],
            ['recently-played', { name: 'playlists-recently-played' }, undefined],
        ])
    })

    it('lists the playlists of the profile below "Playlists" in their order', () => {
        const tree = buildLibraryTree([
            { id: 'b', name: 'Bravo' },
            { id: null, name: 'Without id' },
            { id: 'a', name: 'Alpha' },
        ])

        const children = tree.find((node) => node.id === 'playlists')!.children!
        expect(children.slice(2)).toEqual([
            {
                id: 'playlist:b',
                labelKey: 'sidebar.playlist',
                label: 'Bravo',
                icon: 'tree-playlist',
                to: { name: 'playlist', params: { id: 'b' } },
            },
            {
                id: 'playlist:a',
                labelKey: 'sidebar.playlist',
                label: 'Alpha',
                icon: 'tree-playlist',
                to: { name: 'playlist', params: { id: 'a' } },
            },
        ])
        expect(tree.find((node) => node.id === 'library')).toBe(
            libraryTree.find((node) => node.id === 'library'),
        )
    })

    it('merges the smart playlists into the playlists by name, with their own icon', () => {
        const tree = buildLibraryTree(
            [
                { id: 'a', name: 'Alpha' },
                { id: 'c', name: 'charlie' },
            ],
            [
                { id: 's1', name: 'Bravo' },
                { id: null, name: 'Without id' },
                { id: 's2', name: 'Delta' },
            ],
        )

        const children = tree.find((node) => node.id === 'playlists')!.children!.slice(2)
        expect(children.map((node) => node.id)).toEqual([
            'playlist:a',
            'smart-playlist:s1',
            'playlist:c',
            'smart-playlist:s2',
        ])
        expect(children[1]).toEqual({
            id: smartPlaylistNodeId('s1'),
            labelKey: 'sidebar.smartPlaylist',
            label: 'Bravo',
            icon: 'tree-smart-playlist',
            to: { name: 'smart-playlist', params: { id: 's1' } },
        })
    })

    it('keeps the fixed tree without playlists', () => {
        expect(buildLibraryTree([])).toEqual(libraryTree)
        expect(playlistNodeId('x')).toBe('playlist:x')
    })
})

describe('main navigation', () => {
    it('starts with the home page', () => {
        expect(mainNavigation.map((item) => item.id)).toEqual([
            'home',
            'now-playing',
            'library',
            'playlists',
        ])
        expect(mainNavigation[0]).toMatchObject({ icon: 'home', to: { name: 'home' } })
    })

    it('offers the favorites as a section of the library', () => {
        expect(librarySections.map((section) => section.id)).toContain('favorites')
        const library = libraryTree.find((node) => node.id === 'library')!
        expect(library.children?.find((node) => node.id === 'favorites')).toMatchObject({
            icon: 'tree-favorites',
            to: { name: 'library-section', params: { section: 'favorites' } },
        })
    })
    it('ends with the recap, its statistics and the shared recaps, collapsed', () => {
        const recap = libraryTree.at(-1)!
        expect(recap).toMatchObject({ id: 'recap', icon: 'tree-recap', to: { name: 'recap' } })
        expect(recap.children?.map((node) => [node.id, node.icon])).toEqual([
            ['statistics', 'tree-statistics'],
            ['shared-recaps', 'tree-shared-recaps'],
        ])
        expect(initiallyExpanded).not.toContain('recap')
    })
})
