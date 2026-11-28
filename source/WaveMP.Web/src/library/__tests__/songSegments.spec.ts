import { describe, expect, it } from 'vitest'
import type { FileSongSegment } from '@/api/generated/models'
import {
    confidencePercent,
    methodLabelKey,
    parseSeconds,
    previewRange,
    secondsText,
    segmentState,
    stripPieces,
} from '../songSegments'

function file(extra: Partial<FileSongSegment> = {}): FileSongSegment {
    return {
        durationMilliseconds: 200_000,
        pending: false,
        status: 'Analyzed',
        detected: {
            method: 'Alignment',
            confidence: 0.92,
            confident: true,
            startMilliseconds: 20_000,
            endMilliseconds: 180_000,
            pieces: [
                {
                    fileStartMilliseconds: 20_000,
                    songStartMilliseconds: 0,
                    durationMilliseconds: 100_000,
                },
                {
                    fileStartMilliseconds: 130_000,
                    songStartMilliseconds: 100_000,
                    durationMilliseconds: 50_000,
                },
            ],
        },
        ...extra,
        effective: extra.effective ?? {
            method: 'Alignment',
            confidence: 0.92,
            confident: true,
            startMilliseconds: 20_000,
            endMilliseconds: 180_000,
            pieces: [
                {
                    fileStartMilliseconds: 20_000,
                    songStartMilliseconds: 0,
                    durationMilliseconds: 100_000,
                },
                {
                    fileStartMilliseconds: 130_000,
                    songStartMilliseconds: 100_000,
                    durationMilliseconds: 50_000,
                },
            ],
        },
    }
}

describe('song segments of the editor', () => {
    describe('parseSeconds', () => {
        it('reads seconds with a point or comma as milliseconds', () => {
            expect(parseSeconds('12.5')).toBe(12_500)
            expect(parseSeconds(' 12,25 ')).toBe(12_250)
            expect(parseSeconds('-1.2')).toBe(-1_200)
            expect(parseSeconds('.5')).toBe(500)
            expect(parseSeconds('7')).toBe(7_000)
        })

        it('is null for an empty field and undefined for text that is no number', () => {
            expect(parseSeconds('')).toBeNull()
            expect(parseSeconds('   ')).toBeNull()
            expect(parseSeconds('abc')).toBeUndefined()
            expect(parseSeconds('1:20')).toBeUndefined()
            expect(parseSeconds('1.2345')).toBeUndefined()
        })
    })

    it('writes milliseconds as seconds without trailing zeros', () => {
        expect(secondsText(12_500)).toBe('12.5')
        expect(secondsText(-1_200)).toBe('-1.2')
        expect(secondsText(0)).toBe('0')
        expect(secondsText(null)).toBe('')
        expect(secondsText(undefined)).toBe('')
    })

    describe('stripPieces', () => {
        it('shows the pieces of the song as shares of the file', () => {
            expect(stripPieces(file())).toEqual([
                { start: 0.1, length: 0.5 },
                { start: 0.65, length: 0.25 },
            ])
        })

        it('fills the whole strip without pieces and nothing without a duration', () => {
            expect(stripPieces(file({ effective: { method: 'WholeFile', pieces: [] } }))).toEqual([
                { start: 0, length: 1 },
            ])
            expect(stripPieces(file({ durationMilliseconds: 0 }))).toEqual([])
        })

        it('cuts a piece that starts before the file', () => {
            const early = file({
                effective: {
                    pieces: [
                        {
                            fileStartMilliseconds: -10_000,
                            songStartMilliseconds: 0,
                            durationMilliseconds: 110_000,
                        },
                    ],
                },
            })
            expect(stripPieces(early)).toEqual([{ start: 0, length: 0.5 }])
        })
    })

    describe('segmentState', () => {
        it('names a correction, a pending file and failed analyses first', () => {
            expect(segmentState(file({ effective: { manual: true } }))).toBe('corrected')
            expect(segmentState(file({ pending: true }))).toBe('pending')
            expect(segmentState(file({ status: 'NoSound' }))).toBe('no-sound')
            expect(segmentState(file({ status: 'Failed' }))).toBe('failed')
        })

        it('tells a mismatch, the whole file, a sure and an unsure result apart', () => {
            expect(segmentState(file({ effective: { mismatch: true } }))).toBe('mismatch')
            expect(
                segmentState(file({ effective: { method: 'WholeFile', confident: true } })),
            ).toBe('whole-file')
            expect(segmentState(file())).toBe('sure')
            expect(
                segmentState(file({ effective: { method: 'Structure', confident: false } })),
            ).toBe('unsure')
        })
    })

    it('names the method of the analysis and its confidence in percent', () => {
        expect(methodLabelKey(file())).toBe('trackSongSegments.methods.alignment')
        expect(methodLabelKey(file({ detected: { method: 'Lyrics' } }))).toBe(
            'trackSongSegments.methods.lyrics',
        )
        expect(methodLabelKey(file({ detected: null }))).toBe('trackSongSegments.methods.wholeFile')
        expect(confidencePercent(file())).toBe(92)
        expect(confidencePercent(file({ detected: { confidence: 1.4 } }))).toBe(100)
        expect(confidencePercent(file({ detected: {} }))).toBeNull()
    })

    describe('previewRange', () => {
        it('plays a little before the start and the first seconds of the song', () => {
            expect(previewRange('start', 20_000, 180_000, 200_000)).toEqual({ from: 17, to: 25 })
            expect(previewRange('start', 1_000, 180_000, 200_000)).toEqual({ from: 0, to: 6 })
        })

        it('plays the last seconds of the song and a little after its end', () => {
            expect(previewRange('end', 20_000, 180_000, 200_000)).toEqual({ from: 175, to: 183 })
            expect(previewRange('end', 20_000, null, 200_000)).toEqual({ from: 195, to: 200 })
        })
    })
})
