import { describe, expect, it, vi } from 'vitest'
import { RetryHandlerOptions } from '@microsoft/kiota-http-fetchlibrary'

const client = vi.hoisted(() => {
    const get = vi.fn().mockResolvedValue(undefined)
    const post = vi.fn().mockResolvedValue(undefined)
    const item = { onlineMatches: { get }, onlineMetadata: { post } }
    return {
        get,
        post,
        apiClient: {
            api: {
                v1: {
                    tracks: { byId: () => item },
                    albums: { byId: () => item },
                    onlineReleases: { byReleaseId: () => ({ cover: { get } }) },
                },
            },
        },
    }
})

vi.mock('@/api/client', () => ({ apiClient: client.apiClient }))
import {
    albumValues,
    defaultSelection,
    differenceOf,
    fromAlbumMatch,
    fromTrackMatch,
    isApplicable,
    onlineAlbumFields,
    onlineTrackFields,
    applyAlbumOnline,
    applyTrackOnline,
    compareRows,
    fetchReleaseCover,
    releaseYear,
    searchAlbumOnline,
    searchTrackOnline,
    tagsOf,
    trackValues,
} from '../onlineMetadata'

describe('onlineMetadata', () => {
    describe('differenceOf', () => {
        it('tells a missing value from another and the same one', () => {
            expect(differenceOf(null, 'Song')).toBe('missing')
            expect(differenceOf('  ', 'Song')).toBe('missing')
            expect(differenceOf('Song', 'Other')).toBe('different')
            expect(differenceOf(' Song  Title ', 'Song Title')).toBe('same')
            expect(differenceOf(2001, 2001)).toBe('same')
            expect(differenceOf(2001, 2002)).toBe('different')
        })

        it('marks a value the match does not know as unknown', () => {
            expect(differenceOf('Song', null)).toBe('unknown')
            expect(differenceOf(null, '')).toBe('unknown')
            expect(differenceOf('Song', undefined)).toBe('unknown')
        })

        it('only applies values that change something', () => {
            expect(isApplicable('missing')).toBe(true)
            expect(isApplicable('different')).toBe(true)
            expect(isApplicable('same')).toBe(false)
            expect(isApplicable('unknown')).toBe(false)
        })
    })

    describe('compareRows', () => {
        it('compares every field of the owner', () => {
            expect(
                compareRows(
                    onlineAlbumFields,
                    { album: 'Album', year: null, genre: 'Rock' },
                    { album: 'Album', albumArtist: 'Band', year: 2001, genre: 'Pop' },
                ),
            ).toEqual([
                { field: 'album', difference: 'same', current: 'Album', suggested: 'Album' },
                {
                    field: 'albumArtist',
                    difference: 'missing',
                    current: undefined,
                    suggested: 'Band',
                },
                { field: 'year', difference: 'missing', current: null, suggested: 2001 },
                { field: 'genre', difference: 'different', current: 'Rock', suggested: 'Pop' },
            ])
        })
    })

    describe('defaultSelection', () => {
        it('chooses only the fields the song is missing', () => {
            const current = { title: 'Song', artist: null, album: '', year: 1999, genre: null }
            const suggested = {
                title: 'Song (Remastered)',
                artist: 'Band',
                album: 'Album',
                year: 2001,
                genre: null,
            }

            expect([...defaultSelection(onlineTrackFields, current, suggested)]).toEqual([
                'artist',
                'album',
            ])
        })
    })

    describe('tagsOf', () => {
        it('builds the request of the chosen fields that change something', () => {
            const current = { title: 'Song', artist: null, year: 2001 }
            const suggested = { title: 'Other', artist: 'Band', year: 2001, genre: 'Rock' }

            expect(
                tagsOf(
                    onlineTrackFields,
                    new Set(['title', 'artist', 'year'] as const),
                    current,
                    suggested,
                ),
            ).toEqual({ title: 'Other', artist: 'Band' })
        })

        it('is null without a field to apply', () => {
            expect(tagsOf(onlineTrackFields, new Set(), {}, { title: 'Song' })).toBeNull()
            expect(
                tagsOf(
                    onlineAlbumFields,
                    new Set(['year'] as const),
                    { year: 2001 },
                    { year: 2001 },
                ),
            ).toBeNull()
        })

        it('leaves out fields outside the owner', () => {
            expect(
                tagsOf(
                    onlineAlbumFields,
                    new Set(['title', 'album'] as const),
                    {},
                    { title: 'Song', album: 'Album' },
                ),
            ).toEqual({ album: 'Album' })
        })
    })

    describe('matches', () => {
        it('reads a song match with a unique key and a clamped agreement', () => {
            const first = fromTrackMatch(
                {
                    recordingId: 'rec',
                    release: { id: 'rel', date: '2001-03-04', trackCount: 10 },
                    agreement: 87.6,
                    title: 'Song',
                    artist: 'Band',
                    album: 'Album',
                    trackNumber: 3,
                },
                0,
            )
            const second = fromTrackMatch({ recordingId: 'rec', agreement: 140 }, 1)

            expect(first.key).toBe('rec-rel-0')
            expect(first.agreement).toBe(88)
            expect(first.values).toMatchObject({ title: 'Song', trackNumber: 3, genre: null })
            expect(second.key).toBe('rec-none-1')
            expect(second.agreement).toBe(100)
            expect(second.release).toBeNull()
        })

        it('reads an album match', () => {
            const match = fromAlbumMatch(
                {
                    release: { id: 'rel' },
                    agreement: null,
                    values: { album: 'Album', albumArtist: 'Band', year: 2001 },
                },
                2,
            )

            expect(match.key).toBe('rel-2')
            expect(match.agreement).toBe(0)
            expect(match.values).toEqual({
                album: 'Album',
                albumArtist: 'Band',
                year: 2001,
                genre: null,
            })
        })

        it('reads the current values', () => {
            expect(trackValues({ title: 'Song', year: 2001 })).toMatchObject({
                title: 'Song',
                artist: null,
                year: 2001,
            })
            expect(albumValues(undefined)).toEqual({
                album: null,
                albumArtist: null,
                year: null,
                genre: null,
            })
        })
    })

    describe('releaseYear', () => {
        it('reads the year of a full or partial date', () => {
            expect(releaseYear({ date: '2001-03-04' })).toBe(2001)
            expect(releaseYear({ date: '1999' })).toBe(1999)
            expect(releaseYear({ date: '' })).toBeNull()
            expect(releaseYear(null)).toBeNull()
        })
    })

    describe('requests', () => {
        it('never repeats an answer of the online search, the server already did', async () => {
            await searchTrackOnline('song')
            await searchAlbumOnline('album')
            await fetchReleaseCover('release')
            await applyTrackOnline('song', { year: 2001 }, null)
            await applyAlbumOnline('album', null, 'release')

            const configurations = [
                ...client.get.mock.calls.map((call) => call[0]),
                ...client.post.mock.calls.map((call) => call[1]),
            ]
            expect(configurations).toHaveLength(5)
            for (const configuration of configurations) {
                const retry = configuration.options[0]
                expect(retry).toBeInstanceOf(RetryHandlerOptions)
                expect(retry.maxRetries).toBe(0)
            }
            expect(client.post.mock.calls[0]![0]).toEqual({
                tags: { year: 2001 },
                coverReleaseId: null,
            })
            expect(client.get.mock.calls[2]![0].queryParameters).toEqual({ size: 'Large' })
        })
    })
})
