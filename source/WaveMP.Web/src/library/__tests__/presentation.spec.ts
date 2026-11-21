import { describe, expect, it } from 'vitest'
import {
    artistLabelKey,
    durationSeconds,
    fileKindKey,
    fileKindToken,
    mediaKey,
    trackListState,
    trackRowState,
} from '../presentation'

describe('artistLabelKey', () => {
    it('is null for artists with a name', () => {
        expect(artistLabelKey({ id: 'a', name: 'Alpha Band' })).toBeNull()
    })

    it('names compilations and unknown artists', () => {
        expect(artistLabelKey(null, true)).toBe('library.variousArtists')
        expect(artistLabelKey(undefined)).toBe('library.unknownArtist')
        expect(artistLabelKey({ id: 'a', name: '' })).toBe('library.unknownArtist')
    })
})

describe('durationSeconds', () => {
    it('converts milliseconds', () => {
        expect(durationSeconds(2500)).toBe(2.5)
        expect(durationSeconds(-1)).toBe(0)
        expect(durationSeconds(null)).toBeNull()
        expect(durationSeconds(undefined)).toBeNull()
    })
})

describe('trackRowState and mediaKey', () => {
    it('mark videos next to audio and videos only', () => {
        expect(trackRowState({ hasAudio: true, hasVideo: false })).toEqual([])
        expect(trackRowState({ hasAudio: true, hasVideo: true })).toEqual(['has-video'])
        expect(trackRowState({ hasAudio: false, hasVideo: true })).toEqual([
            'has-video',
            'video-only',
        ])
        expect(mediaKey({ hasAudio: true, hasVideo: true })).toBe('track.media.audioAndVideo')
        expect(mediaKey({ hasAudio: true, hasVideo: false })).toBe('track.media.audio')
        expect(mediaKey({ hasAudio: false, hasVideo: true })).toBe('track.media.video')
    })
})

describe('trackListState', () => {
    it('marks lists with a video so they keep the column of the video badge', () => {
        expect(trackListState([{ hasVideo: false }, { hasVideo: true }])).toBe('has-video')
        expect(trackListState([{ hasVideo: true }], 'busy')).toBe('busy has-video')
    })

    it('keeps the given tokens of lists without a video', () => {
        expect(trackListState([{ hasVideo: false }])).toBeUndefined()
        expect(trackListState([])).toBeUndefined()
        expect(trackListState([{ hasVideo: false }], 'placeholder busy')).toBe('placeholder busy')
        expect(trackListState([{ hasVideo: false }], 'has-video')).toBeUndefined()
    })
})

describe('fileKindToken and fileKindKey', () => {
    it('name the kinds of files', () => {
        expect(fileKindToken('Video')).toBe('video')
        expect(fileKindToken('Image')).toBe('image')
        expect(fileKindToken(undefined)).toBe('audio')
        expect(fileKindKey('Image')).toBe('files.kinds.image')
    })
})
