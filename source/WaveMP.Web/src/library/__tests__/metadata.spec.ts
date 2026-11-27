import { describe, expect, it } from 'vitest'
import type { TrackMetadata } from '@/api/generated/models'
import {
    metadataChanges,
    normalizeText,
    parseMetadataNumber,
    toMetadataForm,
    validateMetadataForm,
    type MetadataForm,
} from '../metadata'

const metadata: TrackMetadata = {
    trackId: 'song',
    status: 'Editable',
    audioFilePath: 'Band/Album/01 - Song.mp3',
    title: 'Song',
    artist: 'Band',
    album: 'Album',
    albumArtist: null,
    year: 2001,
    trackNumber: 1,
    discNumber: null,
    genre: 'Rock',
}

function form(values: Partial<MetadataForm> = {}): MetadataForm {
    return { ...toMetadataForm(metadata), ...values }
}

describe('toMetadataForm', () => {
    it('turns every value into text and missing values into empty fields', () => {
        expect(toMetadataForm(metadata)).toEqual({
            title: 'Song',
            artists: [{ name: 'Band', role: 'Main' }],
            album: 'Album',
            albumArtists: [],
            year: '2001',
            trackNumber: '1',
            discNumber: '',
            genre: 'Rock',
        })
    })

    it('takes the artists as the server read them, with their roles', () => {
        const values = toMetadataForm({
            ...metadata,
            artist: 'Bob Sinclar feat. K.Flay',
            artists: [
                { name: 'Bob Sinclar', role: 'Main' },
                { name: 'K.Flay', role: 'Featured' },
            ],
            albumArtist: 'Simon & Garfunkel; AC/DC',
            albumArtists: ['Simon & Garfunkel', 'AC/DC'],
        })

        expect(values.artists).toEqual([
            { name: 'Bob Sinclar', role: 'Main' },
            { name: 'K.Flay', role: 'Featured' },
        ])
        expect(values.albumArtists).toEqual([
            { name: 'Simon & Garfunkel', role: 'Main' },
            { name: 'AC/DC', role: 'Main' },
        ])
    })

    it('keeps an album artist field without values as one artist', () => {
        expect(
            toMetadataForm({ ...metadata, albumArtist: 'Earth, Wind & Fire' }).albumArtists,
        ).toEqual([{ name: 'Earth, Wind & Fire', role: 'Main' }])
    })
})

describe('normalizeText', () => {
    it('trims and joins white space like the server', () => {
        expect(normalizeText('  New \t  Title \n')).toBe('New Title')
        expect(normalizeText('   ')).toBe('')
    })
})

describe('parseMetadataNumber', () => {
    it.each([
        ['', null],
        ['  ', null],
        ['1', 1],
        [' 2001 ', 2001],
        ['9999', 9999],
        ['0', undefined],
        ['10000', undefined],
        ['1.5', undefined],
        ['-3', undefined],
        ['abc', undefined],
        ['1e3', undefined],
    ])('reads "%s" as %s', (value, expected) => {
        expect(parseMetadataNumber(value)).toBe(expected)
    })
})

describe('validateMetadataForm', () => {
    it('accepts empty numbers and whole numbers from 1 to 9999', () => {
        expect(
            validateMetadataForm(form({ year: '', trackNumber: '9999', discNumber: '1' })),
        ).toEqual({})
    })

    it('marks every number field with an invalid value', () => {
        expect(
            validateMetadataForm(form({ year: '0', trackNumber: '10000', discNumber: 'abc' })),
        ).toEqual({
            year: 'number',
            trackNumber: 'number',
            discNumber: 'number',
        })
        expect(validateMetadataForm(form({ year: '1.5' }))).toEqual({ year: 'number' })
    })
})

describe('metadataChanges', () => {
    it('is empty without changes', () => {
        expect(metadataChanges(form(), form())).toEqual({})
    })

    it('ignores changes of white space only', () => {
        expect(metadataChanges(form(), form({ title: '  Song ', genre: 'Rock  ' }))).toEqual({})
    })

    it('sends normalized texts and numbers as numbers', () => {
        expect(
            metadataChanges(
                form(),
                form({
                    title: ' New   Title ',
                    year: '1999',
                    discNumber: '2',
                }),
            ),
        ).toEqual({ title: 'New Title', year: 1999, discNumber: 2 })
    })

    it('removes cleared values with null', () => {
        expect(metadataChanges(form(), form({ genre: '  ', year: '', trackNumber: '' }))).toEqual({
            genre: null,
            year: null,
            trackNumber: null,
        })
    })

    it('leaves out invalid numbers', () => {
        expect(metadataChanges(form(), form({ year: 'abc' }))).toEqual({})
    })

    it('sends the whole list of artists in its order once one changed', () => {
        const changed = form({
            artists: [
                { name: 'Band', role: 'Main' },
                { name: ' K.Flay ', role: 'Featured', id: 'kflay' },
            ],
        })

        expect(metadataChanges(form(), changed)).toEqual({
            artists: [
                { name: 'Band', role: 'Main' },
                { name: 'K.Flay', role: 'Featured' },
            ],
        })
    })

    it('sees a new order and a new role as a change, white space not', () => {
        const original = form({
            artists: [
                { name: 'A', role: 'Main' },
                { name: 'B', role: 'Main' },
            ],
        })

        expect(
            metadataChanges(
                original,
                form({
                    artists: [
                        { name: 'B', role: 'Main' },
                        { name: 'A', role: 'Main' },
                    ],
                }),
            ).artists,
        ).toEqual([
            { name: 'B', role: 'Main' },
            { name: 'A', role: 'Main' },
        ])
        expect(
            metadataChanges(
                original,
                form({
                    artists: [
                        { name: 'A', role: 'Main' },
                        { name: 'B', role: 'Featured' },
                    ],
                }),
            ).artists,
        ).toHaveLength(2)
        expect(
            metadataChanges(
                original,
                form({
                    artists: [
                        { name: ' A ', role: 'Main' },
                        { name: 'B', role: 'Main' },
                    ],
                }),
            ),
        ).toEqual({})
    })

    it('sends album artists by name and an empty list to remove all artists', () => {
        const changes = metadataChanges(
            form(),
            form({
                artists: [],
                albumArtists: [
                    { name: 'Simon & Garfunkel', role: 'Main' },
                    { name: 'AC/DC', role: 'Main' },
                ],
            }),
        )

        expect(changes).toEqual({
            artists: [],
            albumArtists: [{ name: 'Simon & Garfunkel' }, { name: 'AC/DC' }],
        })
    })
})
