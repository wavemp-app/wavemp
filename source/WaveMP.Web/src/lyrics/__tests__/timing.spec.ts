import { describe, expect, it } from 'vitest'
import { LyricsSourceObject, LyricsTypeObject } from '@/api/generated/models'
import {
    activeLineIndex,
    activeWordIndex,
    isSynchronized,
    itemStateOf,
    lineEnd,
    progressBetween,
    staticScrollShare,
    toTrackLyrics,
    wordEnd,
    type LyricsLineTiming,
} from '../timing'

const first: LyricsLineTiming = { start: 1, end: 4, text: 'First', words: [] }
const last: LyricsLineTiming = { start: 9, end: null, text: 'Last', words: [] }
const lines: LyricsLineTiming[] = [first, { start: 4, end: 9, text: '', words: [] }, last]

describe('toTrackLyrics', () => {
    it('converts the milliseconds of the API into seconds', () => {
        const lyrics = toTrackLyrics('track-1', {
            type: LyricsTypeObject.WordSynchronized,
            source: LyricsSourceObject.File,
            lines: [
                {
                    startMilliseconds: 1000,
                    endMilliseconds: 2500,
                    text: 'Hello world',
                    words: [
                        { startMilliseconds: 1000, endMilliseconds: 1400, text: 'Hello ' },
                        { startMilliseconds: 1400, endMilliseconds: null, text: 'world' },
                    ],
                },
            ],
        })

        expect(lyrics).toEqual({
            trackId: 'track-1',
            type: LyricsTypeObject.WordSynchronized,
            source: LyricsSourceObject.File,
            lines: [
                {
                    start: 1,
                    end: 2.5,
                    text: 'Hello world',
                    words: [
                        { start: 1, end: 1.4, text: 'Hello ' },
                        { start: 1.4, end: null, text: 'world' },
                    ],
                },
            ],
        })
        expect(isSynchronized(lyrics)).toBe(true)
    })

    it('treats lyrics without times as static and drops empty answers', () => {
        const lyrics = toTrackLyrics('track-1', {
            type: LyricsTypeObject.LineSynchronized,
            lines: [{ text: 'Plain' }],
        })

        expect(lyrics?.type).toBe(LyricsTypeObject.Static)
        expect(lyrics?.lines).toEqual([{ start: null, end: null, text: 'Plain', words: [] }])
        expect(isSynchronized(lyrics)).toBe(false)
        expect(isSynchronized(null)).toBe(false)
        expect(toTrackLyrics('track-1', { type: LyricsTypeObject.Static, lines: [] })).toBeNull()
    })
})

describe('activeLineIndex', () => {
    it('is the last line that started', () => {
        expect(activeLineIndex(lines, 0.5)).toBe(-1)
        expect(activeLineIndex(lines, 1)).toBe(0)
        expect(activeLineIndex(lines, 5)).toBe(1)
        expect(activeLineIndex(lines, 120)).toBe(2)
        expect(activeLineIndex([], 3)).toBe(-1)
    })

    it('finds no line in static lyrics', () => {
        expect(activeLineIndex([{ start: null, end: null, text: 'Plain', words: [] }], 10)).toBe(-1)
    })
})

describe('activeWordIndex', () => {
    it('is the last word that started', () => {
        const words = [
            { start: 1, end: 1.5, text: 'One ' },
            { start: 1.5, end: null, text: 'two' },
        ]

        expect(activeWordIndex(words, 0.9)).toBe(-1)
        expect(activeWordIndex(words, 1.2)).toBe(0)
        expect(activeWordIndex(words, 3)).toBe(1)
    })
})

describe('ends and progress', () => {
    it('ends the last line with the track or a few seconds after its start', () => {
        expect(lineEnd(first, 30)).toBe(4)
        expect(lineEnd(last, 30)).toBe(30)
        expect(lineEnd(last, 0)).toBe(14)
        expect(wordEnd({ start: 2, end: null, text: 'x' }, 3)).toBe(3)
        expect(wordEnd({ start: 2, end: 2.5, text: 'x' }, 3)).toBe(2.5)
    })

    it('is the share of the time that has passed', () => {
        expect(progressBetween(2, 4, 1)).toBe(0)
        expect(progressBetween(2, 4, 3)).toBe(0.5)
        expect(progressBetween(2, 4, 5)).toBe(1)
        expect(progressBetween(2, 2, 2.1)).toBe(1)
        expect(staticScrollShare(30, 120)).toBe(0.25)
        expect(staticScrollShare(30, 0)).toBe(0)
    })

    it('names lines before, at and after the active one', () => {
        expect(itemStateOf(0, 1)).toBe('past')
        expect(itemStateOf(1, 1)).toBe('active')
        expect(itemStateOf(2, 1)).toBe('upcoming')
        expect(itemStateOf(0, -1)).toBe('upcoming')
    })
})
