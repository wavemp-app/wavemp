import { describe, expect, it } from 'vitest'
import { aliasFailureKey, aliasSourceOf, sortAliases } from '../artistAliases'

describe('aliasSourceOf', () => {
    it('tells aliases of MusicBrainz from those of an admin', () => {
        expect(aliasSourceOf({ source: 'MusicBrainz' })).toBe('musicbrainz')
        expect(aliasSourceOf({ source: 'Admin' })).toBe('admin')
        expect(aliasSourceOf({})).toBe('admin')
    })
})

describe('aliasFailureKey', () => {
    it.each([
        ['artist.alias_exists', 'artistAliases.errors.exists'],
        ['artist.alias_not_found', 'artistAliases.errors.aliasNotFound'],
        ['artist.not_found', 'artistAliases.errors.artistNotFound'],
        ['artist.picture_lookup_disabled', 'artistAliases.errors.lookupDisabled'],
        ['artist.musicbrainz_artist_missing', 'artistAliases.errors.musicBrainzArtistMissing'],
        ['artist.picture_lookup_unavailable', 'artistAliases.errors.unavailable'],
    ])('names the message of %s', (code, key) => {
        expect(aliasFailureKey(code, 'import')).toBe(key)
    })

    it('falls back to the message of the action', () => {
        expect(aliasFailureKey(undefined, 'load')).toBe('artistAliases.errors.loadFailed')
        expect(aliasFailureKey('server.error', 'add')).toBe('artistAliases.errors.addFailed')
    })
})

describe('sortAliases', () => {
    it('sorts by name without regard to case and accents', () => {
        const sorted = sortAliases([{ name: 'beta' }, { name: 'Älpha' }, { name: 'Alpha' }])

        expect(sorted.map((alias) => alias.name)).toEqual(['Älpha', 'Alpha', 'beta'])
    })
})
