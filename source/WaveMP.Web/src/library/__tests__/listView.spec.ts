import { describe, expect, it } from 'vitest'
import {
    defaultListView,
    listViews,
    parseListView,
    viewContext,
    viewItemId,
    withListView,
} from '../listView'

describe('viewContext', () => {
    it.each([
        ['library-section', { section: 'songs' }],
        ['library-section', { section: 'recently-added' }],
        ['library-artist', { id: 'artist' }],
        ['library-genre', { genre: 'Rock' }],
        ['library-year', { year: '2001' }],
    ])('offers the views of the songs on %s %o', (name, params) => {
        expect(viewContext({ name, params })).toBe('tracks')
    })

    it.each([
        ['library-section', { section: 'albums' }],
        ['library-section', { section: 'artists' }],
        ['library-section', { section: 'genres' }],
        ['library-section', { section: 'years' }],
        ['library-section', { section: 'favorites' }],
        ['library-album', { id: 'album' }],
        ['library-search', {}],
        ['library-track', { id: 'track' }],
        ['home', {}],
        ['now-playing', {}],
        ['playlist', { id: 'playlist' }],
    ])('offers no views on %s %o', (name, params) => {
        expect(viewContext({ name, params })).toBeNull()
    })

    it('offers no views without a route name', () => {
        expect(viewContext({ name: undefined, params: {} })).toBeNull()
    })
})

describe('parseListView', () => {
    it('is grouped by album without a view in the query', () => {
        expect(defaultListView).toBe('grouped')
        expect(parseListView({})).toBe('grouped')
    })

    it('reads the plain list', () => {
        expect(parseListView({ view: 'list' })).toBe('list')
        expect(parseListView({ view: ['list', 'grouped'] })).toBe('list')
    })

    it('treats unknown values as the default', () => {
        expect(parseListView({ view: 'tiles' })).toBe('grouped')
        expect(parseListView({ view: 'LIST' })).toBe('grouped')
        expect(parseListView({ view: null })).toBe('grouped')
        expect(parseListView({ view: [] })).toBe('grouped')
    })
})

describe('withListView', () => {
    it('adds the plain list to the query and keeps the rest', () => {
        expect(withListView({ sort: 'Title', q: 'rock' }, 'list')).toEqual({
            sort: 'Title',
            q: 'rock',
            view: 'list',
        })
    })

    it('leaves the default view out of the query', () => {
        expect(withListView({ sort: 'Title', view: 'list' }, 'grouped')).toEqual({ sort: 'Title' })
    })

    it('does not change the query it gets', () => {
        const query = { view: 'list' }
        withListView(query, 'grouped')
        expect(query).toEqual({ view: 'list' })
    })
})

describe('viewItemId', () => {
    it('names the menu entries after the views, in the order of the menu', () => {
        expect(listViews.map(viewItemId)).toEqual(['view-grouped', 'view-list'])
    })
})
