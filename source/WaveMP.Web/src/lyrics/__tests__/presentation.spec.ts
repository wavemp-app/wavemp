import { describe, expect, it } from 'vitest'
import {
    downloadFailureOf,
    forgetFailureKey,
    lookUpAgainWait,
    lookupNoteKey,
    lyricsSourceKey,
    lyricsTypeKey,
} from '../presentation'

describe('lyrics presentation', () => {
    it('describes only lookups that found no lyrics', () => {
        expect(lookupNoteKey('NotFound')).toBe('trackLyrics.lookup.notFound')
        expect(lookupNoteKey('Instrumental')).toBe('trackLyrics.lookup.instrumental')
        expect(lookupNoteKey('SavedToFile')).toBeNull()
        expect(lookupNoteKey('NotNeeded')).toBeNull()
        expect(lookupNoteKey(undefined)).toBeNull()
    })

    it('shows the wait before songs are looked up again in days or hours', () => {
        expect(lookUpAgainWait(43_200)).toEqual({ value: 30, unit: 'day' })
        expect(lookUpAgainWait(1_440)).toEqual({ value: 1, unit: 'day' })
        expect(lookUpAgainWait(360)).toEqual({ value: 6, unit: 'hour' })
        expect(lookUpAgainWait(5)).toEqual({ value: 1, unit: 'hour' })
        expect(lookUpAgainWait(0)).toBeNull()
        expect(lookUpAgainWait(null)).toBeNull()
        expect(lookUpAgainWait(undefined)).toBeNull()
    })

    it('treats a result that is already gone as forgotten', () => {
        expect(forgetFailureKey('lyrics.lookup_not_found')).toBeNull()
        expect(forgetFailureKey('lyrics.lookup_found_lyrics')).toBe(
            'trackLyrics.lookup.errors.foundLyrics',
        )
        expect(forgetFailureKey('track.not_found')).toBe('trackLyrics.lookup.errors.failed')
        expect(forgetFailureKey(null)).toBe('trackLyrics.lookup.errors.failed')
    })

    it('names the type and the source of lyrics', () => {
        expect(lyricsTypeKey('WordSynchronized')).toBe('trackLyrics.types.wordSynchronized')
        expect(lyricsTypeKey('LineSynchronized')).toBe('trackLyrics.types.lineSynchronized')
        expect(lyricsTypeKey(undefined)).toBe('trackLyrics.types.static')
        expect(lyricsSourceKey('Tags')).toBe('trackLyrics.sources.tags')
        expect(lyricsSourceKey('Download')).toBe('trackLyrics.sources.download')
        expect(lyricsSourceKey(null)).toBe('trackLyrics.sources.file')
    })

    it('tells failed downloads from downloads that found nothing', () => {
        expect(downloadFailureOf('lyrics.download_not_found')).toEqual({
            labelKey: 'trackLyrics.errors.notFound',
            tone: 'info',
        })
        expect(downloadFailureOf('lyrics.instrumental').tone).toBe('info')
        expect(downloadFailureOf('lyrics.service_unavailable')).toEqual({
            labelKey: 'trackLyrics.errors.serviceUnavailable',
            tone: 'error',
        })
        expect(downloadFailureOf('lyrics.download_disabled').labelKey).toBe(
            'trackLyrics.errors.downloadDisabled',
        )
        expect(downloadFailureOf('track.not_found')).toEqual({
            labelKey: 'trackLyrics.errors.failed',
            tone: 'error',
        })
    })
})
