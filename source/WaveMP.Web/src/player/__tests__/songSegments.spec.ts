import { describe, expect, it } from 'vitest'
import type { SongSegment } from '@/api/generated/models'
import {
    filePosition,
    loudnessMatchFactor,
    mapPosition,
    nearestSongPosition,
    skipWindow,
    songPosition,
    soundForView,
} from '../songSegments'

/** A piece in seconds: from file on, the file plays the song from song on, for length seconds. */
function piece(file: number, song: number, length: number) {
    return {
        fileStartMilliseconds: file * 1000,
        songStartMilliseconds: song * 1000,
        durationMilliseconds: length * 1000,
    }
}

/** A music video with an intro of 20 s, the song of 200 s and an outro after 220 s. */
const video: SongSegment = {
    confident: true,
    method: 'Alignment',
    startMilliseconds: 20_000,
    endMilliseconds: 220_000,
    pieces: [piece(20, 0, 200)],
}

/** A radio edit: the first 60 s of the song, then it skips 30 s of the song. */
const edit: SongSegment = {
    confident: true,
    method: 'Alignment',
    startMilliseconds: 0,
    endMilliseconds: 170_000,
    pieces: [piece(0, 0, 60), piece(60, 90, 110)],
}

describe('song segments', () => {
    describe('songPosition', () => {
        it('is the position itself without pieces', () => {
            expect(songPosition(null, 12.5)).toBe(12.5)
            expect(songPosition({ pieces: [] }, 12.5)).toBe(12.5)
        })

        it('is the time of the song inside a piece and NaN in intro and outro', () => {
            expect(songPosition(video, 10)).toBeNaN()
            expect(songPosition(video, 20)).toBe(0)
            expect(songPosition(video, 50)).toBe(30)
            expect(songPosition(video, 225)).toBeNaN()
        })

        it('follows the pieces of a file that cuts parts of the song', () => {
            expect(songPosition(edit, 59)).toBe(59)
            expect(songPosition(edit, 61)).toBe(91)
        })
    })

    describe('nearestSongPosition', () => {
        it('takes the start of the song in the intro and its end in the outro', () => {
            expect(nearestSongPosition(video, 5)).toBe(0)
            expect(nearestSongPosition(video, 230)).toBe(200)
            expect(nearestSongPosition(null, -1)).toBe(0)
        })
    })

    describe('filePosition', () => {
        it('finds the moment of the song in the file', () => {
            expect(filePosition(video, 30)).toBe(50)
            expect(filePosition(null, 30)).toBe(30)
        })

        it('goes to the nearest piece for a part the file lacks', () => {
            expect(filePosition(edit, 70)).toBe(60)
            expect(filePosition(edit, 85)).toBe(60)
            expect(filePosition(edit, 300)).toBe(170)
        })
    })

    describe('mapPosition', () => {
        it('maps the audio file to the video and back', () => {
            expect(mapPosition(null, video, 42)).toBe(62)
            expect(mapPosition(video, null, 62)).toBe(42)
        })

        it('maps the intro and outro of the video to the nearest position of the song', () => {
            expect(mapPosition(video, null, 3)).toBe(0)
            expect(mapPosition(video, null, 250)).toBe(200)
        })

        it('maps between two files with different pieces', () => {
            expect(mapPosition(edit, video, 61)).toBe(111)
        })
    })

    describe('skipWindow', () => {
        it('is the song within the file when the analysis is sure and the setting is on', () => {
            expect(skipWindow(video, true)).toEqual({ start: 20, end: 220 })
        })

        it('is null with the setting off, an unsure result or the whole file', () => {
            expect(skipWindow(video, false)).toBeNull()
            expect(skipWindow({ ...video, confident: false }, true)).toBeNull()
            expect(skipWindow({ ...video, pieces: [] }, true)).toBeNull()
            expect(
                skipWindow({ ...video, method: 'WholeFile', startMilliseconds: 0 }, true),
            ).toBeNull()
        })

        it('uses the correction of an admin also for the whole file', () => {
            const manual = { ...video, method: 'WholeFile' as const, manual: true }
            expect(skipWindow(manual, true)).toEqual({ start: 20, end: 220 })
        })

        it('starts at 0 for a song that starts before the file', () => {
            const early = { ...video, startMilliseconds: -4_000, pieces: [piece(0, 4, 196)] }
            expect(skipWindow(early, true)).toEqual({ start: 0, end: 220 })
        })
    })

    it('wants the sound of the video only while the video is shown and the song has one', () => {
        expect(soundForView('Video', true)).toBe('Video')
        expect(soundForView('Video', false)).toBe('Audio')
        expect(soundForView('Cover', true)).toBe('Audio')
        expect(soundForView('Visualization', true)).toBe('Audio')
    })

    it('turns a loudness match in dB into a factor', () => {
        expect(loudnessMatchFactor(null)).toBe(1)
        expect(loudnessMatchFactor(0)).toBe(1)
        expect(loudnessMatchFactor(-6)).toBeCloseTo(0.501, 3)
        expect(loudnessMatchFactor(6)).toBeCloseTo(1.995, 3)
    })
})
