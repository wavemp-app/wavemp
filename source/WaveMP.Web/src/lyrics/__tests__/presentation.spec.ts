import { describe, expect, it } from 'vitest'
import { downloadFailureOf, lyricsSourceKey, lyricsTypeKey } from '../presentation'

describe('lyrics presentation', () => {
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
