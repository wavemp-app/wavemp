import { describe, expect, it } from 'vitest'
import {
    albumCoverFallbackKey,
    albumCoverFallbackOf,
    albumCoverFallbackOptionOf,
    albumCoverFallbackOptions,
    albumCoverFallbacks,
    coverPriorities,
    coverPriorityKey,
    coverPriorityOf,
    coverRuleOptionOf,
    coverRuleOptions,
    coverRuleToken,
} from '../coverRules'

describe('cover rules', () => {
    it('offers the default first and inherit before the rules of an exception', () => {
        expect(coverPriorities).toEqual(['TrackFirst', 'AlbumFirst', 'TrackOnly'])
        expect(coverRuleOptions).toEqual(['Inherit', 'TrackFirst', 'AlbumFirst', 'TrackOnly'])
        expect(albumCoverFallbacks).toEqual(['ArtistPicture', 'None'])
        expect(albumCoverFallbackOptions).toEqual(['Inherit', 'ArtistPicture', 'None'])
    })

    it('names the rules with message keys', () => {
        expect(coverPriorityKey('TrackFirst')).toBe('coverRule.priorities.trackFirst')
        expect(coverPriorityKey('AlbumFirst')).toBe('coverRule.priorities.albumFirst')
        expect(coverPriorityKey('TrackOnly')).toBe('coverRule.priorities.trackOnly')
        expect(coverPriorityKey(null)).toBe('coverRule.priorities.trackFirst')
        expect(albumCoverFallbackKey('None')).toBe('coverRule.fallbacks.none')
        expect(albumCoverFallbackKey(undefined)).toBe('coverRule.fallbacks.artistPicture')
    })

    it('turns rules into stable tokens', () => {
        expect(coverRuleToken('Inherit')).toBe('inherit')
        expect(coverRuleToken('AlbumFirst')).toBe('album-first')
        expect(coverRuleToken('TrackOnly')).toBe('track-only')
        expect(coverRuleToken('ArtistPicture')).toBe('artist-picture')
    })

    it('starts with inherit and the defaults while nothing is known', () => {
        expect(coverRuleOptionOf(undefined)).toBe('Inherit')
        expect(coverRuleOptionOf(null)).toBe('Inherit')
        expect(coverRuleOptionOf('TrackOnly')).toBe('TrackOnly')
        expect(albumCoverFallbackOptionOf(undefined)).toBe('Inherit')
        expect(albumCoverFallbackOptionOf('None')).toBe('None')
        expect(coverPriorityOf(undefined)).toBe('TrackFirst')
        expect(coverPriorityOf('AlbumFirst')).toBe('AlbumFirst')
        expect(albumCoverFallbackOf(null)).toBe('ArtistPicture')
        expect(albumCoverFallbackOf('None')).toBe('None')
    })
})
