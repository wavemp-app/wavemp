import { describe, expect, it } from 'vitest'
import {
    artistLabelKey,
    durationSeconds,
    fileKindKey,
    fileKindToken,
    mediaKey,
    artistLine,
    namesTrackArtist,
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

describe('artistLine', () => {
    const translate = (key: string) =>
        ({
            'artists.separator': ', ',
            'artists.featuring': ' feat. ',
            'library.unknownArtist': 'Unknown artist',
            'library.variousArtists': 'Various artists',
        })[key] ?? key

    it('names every artist, the guests after "feat."', () => {
        expect(
            artistLine(
                {
                    artists: [
                        { id: 'b', name: 'Bob Sinclar', role: 'Main' },
                        { id: 's', name: 'Simon & Garfunkel', role: 'Main' },
                        { id: 'k', name: 'K.Flay', role: 'Featured' },
                    ],
                },
                translate,
            ),
        ).toBe('Bob Sinclar, Simon & Garfunkel feat. K.Flay')
    })

    it('falls back to the first artist and to the texts for unknown artists', () => {
        expect(artistLine({ artist: { id: 'a', name: 'AC/DC' } }, translate)).toBe('AC/DC')
        expect(artistLine({ artists: [] }, translate)).toBe('Unknown artist')
        expect(artistLine(null, translate, true)).toBe('Various artists')
    })
})

describe('namesTrackArtist', () => {
    const alpha = { id: 'alpha', name: 'Alpha Band' }
    const beta = { id: 'beta', name: 'Beta Singer' }
    const album = { artist: alpha, isCompilation: false }

    it('leaves out the artist of the album', () => {
        expect(namesTrackArtist({ artist: alpha }, album)).toBe(false)
        expect(namesTrackArtist({ artist: { name: 'alpha band ' } }, album)).toBe(false)
        expect(namesTrackArtist({ artist: null }, { artist: null, isCompilation: false })).toBe(
            false,
        )
    })

    it('names other artists than the one of the album', () => {
        expect(namesTrackArtist({ artist: beta }, album)).toBe(true)
        expect(namesTrackArtist({ artist: { id: 'alpha-2', name: 'Alpha Band' } }, album)).toBe(
            true,
        )
        expect(namesTrackArtist({ artist: null }, album)).toBe(true)
    })

    it('names the artists of a song with a guest or with other album artists', () => {
        const guest = {
            artist: alpha,
            artists: [
                { ...alpha, role: 'Main' as const },
                { ...beta, role: 'Featured' as const },
            ],
        }
        expect(namesTrackArtist(guest, album)).toBe(true)
        expect(
            namesTrackArtist(guest, {
                artist: alpha,
                artists: [
                    { ...alpha, role: 'Main' },
                    { ...beta, role: 'Main' },
                ],
                isCompilation: false,
            }),
        ).toBe(false)
        expect(
            namesTrackArtist(
                { artist: alpha },
                { artist: alpha, artists: [alpha, beta], isCompilation: false },
            ),
        ).toBe(true)
    })

    it('names every artist of a compilation and of a plain list', () => {
        expect(namesTrackArtist({ artist: alpha }, { artist: alpha, isCompilation: true })).toBe(
            true,
        )
        expect(namesTrackArtist({ artist: null }, { artist: null, isCompilation: true })).toBe(true)
        expect(
            namesTrackArtist(
                { artist: alpha },
                { artist: null, isCompilation: false, untitled: true },
            ),
        ).toBe(true)
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
