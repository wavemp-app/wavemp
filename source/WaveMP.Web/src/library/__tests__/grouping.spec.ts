import { describe, expect, it } from 'vitest'
import type { Track } from '@/api/generated/models'
import { groupTracksAsList, groupTracksByAlbum, groupTracksByDisc } from '../grouping'

const alpha = { id: 'alpha', name: 'Alpha Band' }
const beta = { id: 'beta', name: 'Beta Singer' }
const first = { id: 'first', title: 'First Album', artist: alpha, isCompilation: false }
const hits = { id: 'hits', title: 'Hits', artist: null, isCompilation: true }

function track(id: string, values: Partial<Track> = {}): Track {
    return { id, title: id, hasAudio: true, hasVideo: false, hasCover: false, ...values }
}

describe('groupTracksByAlbum', () => {
    it('groups consecutive tracks of an album with its details', () => {
        const groups = groupTracksByAlbum([
            track('one', {
                album: first,
                artist: alpha,
                year: 2001,
                genre: 'Rock',
                hasCover: true,
            }),
            track('two', { album: first, artist: alpha }),
            track('hit', { album: hits, artist: beta, year: 2005 }),
        ])

        expect(groups).toHaveLength(2)
        expect(groups[0]).toMatchObject({
            id: 'album:first',
            albumId: 'first',
            title: 'First Album',
            artist: alpha,
            isCompilation: false,
            year: 2001,
            genre: 'Rock',
            cover: { kind: 'album', id: 'first', available: true },
        })
        expect(groups[0]?.tracks.map((item) => item.id)).toEqual(['one', 'two'])
        expect(groups[1]).toMatchObject({
            title: 'Hits',
            artist: null,
            isCompilation: true,
            cover: { kind: 'album', id: 'hits', available: false },
        })
    })

    it('groups tracks without album by their artist and uses the cover of a track', () => {
        const groups = groupTracksByAlbum([
            track('clip', { artist: beta, hasVideo: true }),
            track('song', { artist: beta, hasCover: true }),
            track('loose'),
        ])

        expect(groups.map((group) => group.id)).toEqual(['artist:beta', 'unknown'])
        expect(groups[0]).toMatchObject({
            title: null,
            artist: beta,
            cover: { kind: 'track', id: 'song', available: true },
        })
        expect(groups[1]?.cover).toBeNull()
    })

    it('treats an album without id as no album', () => {
        const groups = groupTracksByAlbum([track('clip', { artist: beta, album: {} })])

        expect(groups[0]).toMatchObject({ id: 'artist:beta', title: null, artist: beta })
    })

    it('keeps the ids unique when an album appears twice', () => {
        const groups = groupTracksByAlbum([
            track('one', { album: first }),
            track('hit', { album: hits }),
            track('two', { album: first }),
        ])

        expect(groups.map((group) => group.id)).toEqual([
            'album:first',
            'album:hits',
            'album:first:1',
        ])
    })

    it('takes year and genre from the first track that has them', () => {
        const groups = groupTracksByAlbum([
            track('one', { album: first }),
            track('two', { album: first, year: 2003, genre: 'Pop' }),
        ])

        expect(groups[0]).toMatchObject({ year: 2003, genre: 'Pop' })
    })
})

describe('groupTracksByDisc', () => {
    it('keeps one group with the album title for a single disc', () => {
        const groups = groupTracksByDisc([track('one', { discNumber: 1 }), track('two')], 'Album')

        expect(groups).toHaveLength(1)
        expect(groups[0]).toMatchObject({ id: 'album', title: 'Album' })
        expect(groups[0]?.disc).toBeUndefined()
    })

    it('makes one group per disc from two discs on', () => {
        const groups = groupTracksByDisc(
            [
                track('a', { discNumber: 2 }),
                track('b', { discNumber: 1 }),
                track('c', { discNumber: 2 }),
            ],
            'Album',
        )

        expect(groups.map((group) => [group.disc, group.tracks.map((item) => item.id)])).toEqual([
            [1, ['b']],
            [2, ['a', 'c']],
        ])
    })
})

describe('groupTracksAsList', () => {
    it('puts all tracks into one group without title, cover and details, in their order', () => {
        const tracks = [
            track('two', { album: first, artist: alpha, year: 2001, genre: 'Rock' }),
            track('hit', { album: hits, artist: beta, hasCover: true }),
            track('loose', { artist: beta }),
            track('one', { album: first, artist: alpha }),
        ]

        const groups = groupTracksAsList(tracks)

        expect(groups).toHaveLength(1)
        expect(groups[0]).toMatchObject({
            id: 'list',
            title: null,
            untitled: true,
            artist: null,
            isCompilation: false,
            year: null,
            genre: null,
            cover: null,
        })
        expect(groups[0]?.albumId).toBeUndefined()
        expect(groups[0]?.tracks.map((item) => item.id)).toEqual(['two', 'hit', 'loose', 'one'])
    })

    it('keeps growing with the pages of a long list in one group', () => {
        const page = (start: number) =>
            Array.from({ length: 100 }, (_, index) =>
                track(`song-${start + index}`, {
                    album: { ...first, id: `album-${Math.floor((start + index) / 10)}` },
                }),
            )

        const loaded = [...page(0), ...page(100)]
        const groups = groupTracksAsList(loaded)

        expect(groups).toHaveLength(1)
        expect(groups[0]?.tracks).toHaveLength(200)
        expect(groups[0]?.tracks[150]?.id).toBe('song-150')
    })

    it('has no group for an empty list', () => {
        expect(groupTracksAsList([])).toEqual([])
    })

    it('is never untitled when grouped by album or disc', () => {
        const tracks = [track('one', { album: first, artist: alpha })]

        expect(groupTracksByAlbum(tracks)[0]?.untitled).toBeUndefined()
        expect(groupTracksByDisc(tracks, 'First Album')[0]?.untitled).toBeUndefined()
    })
})
