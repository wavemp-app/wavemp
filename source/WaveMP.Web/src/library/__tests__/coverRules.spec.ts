import { describe, expect, it } from 'vitest'
import {
    coverPriorities,
    coverPriorityKey,
    coverPriorityOf,
    coverRuleOptionKey,
    coverRuleOptionOf,
    coverRuleOptions,
    coverRuleToken,
} from '../coverRules'

describe('cover rules', () => {
    it('offers inherit before the two priorities', () => {
        expect(coverRuleOptions).toEqual(['Inherit', 'AlbumFirst', 'TrackFirst'])
        expect(coverPriorities).toEqual(['AlbumFirst', 'TrackFirst'])
    })

    it('names the rules and priorities with message keys', () => {
        expect(coverRuleOptionKey('Inherit')).toBe('coverRule.inherit')
        expect(coverRuleOptionKey('AlbumFirst')).toBe('coverRule.albumFirst')
        expect(coverRuleOptionKey('TrackFirst')).toBe('coverRule.trackFirst')
        expect(coverPriorityKey('TrackFirst')).toBe('coverRule.priorities.trackFirst')
        expect(coverPriorityKey(null)).toBe('coverRule.priorities.albumFirst')
    })

    it('turns rules into stable tokens', () => {
        expect(coverRuleToken('Inherit')).toBe('inherit')
        expect(coverRuleToken('AlbumFirst')).toBe('album-first')
        expect(coverRuleToken('TrackFirst')).toBe('track-first')
    })

    it('starts with inherit and album first while nothing is known', () => {
        expect(coverRuleOptionOf(undefined)).toBe('Inherit')
        expect(coverRuleOptionOf(null)).toBe('Inherit')
        expect(coverRuleOptionOf('TrackFirst')).toBe('TrackFirst')
        expect(coverPriorityOf(undefined)).toBe('AlbumFirst')
        expect(coverPriorityOf('TrackFirst')).toBe('TrackFirst')
    })
})
