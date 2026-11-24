import { describe, expect, it } from 'vitest'
import { ProblemCodeObject, type ArtistPicture } from '@/api/generated/models'
import {
    artistPictureViewOf,
    creditPartsOf,
    httpsUrlOf,
    lifeSpanOf,
    maxCollageCovers,
    pictureFailureKey,
    pictureSourceRows,
    providerKey,
    takeOverCropOf,
} from '../artistPictures'

describe('artistPictureViewOf', () => {
    it('shows the own picture of an artist with the version of the API', () => {
        for (const kind of ['Local', 'Uploaded', 'Online'] as const) {
            expect(artistPictureViewOf({ kind, version: `${kind}-1` })).toEqual({
                kind: 'picture',
                version: `${kind}-1`,
            })
        }
    })

    it('shows the covers of up to four albums without a picture', () => {
        expect(
            artistPictureViewOf({ kind: 'Albums', albumIds: ['a', 'b', 'a', '', 'c', 'd', 'e'] }),
        ).toEqual({ kind: 'albums', albumIds: ['a', 'b', 'c', 'd'] })
        expect(maxCollageCovers).toBe(4)
    })

    it('shows the icon without a picture and album covers or for incomplete summaries', () => {
        expect(artistPictureViewOf(null)).toEqual({ kind: 'none' })
        expect(artistPictureViewOf(undefined)).toEqual({ kind: 'none' })
        expect(artistPictureViewOf({ kind: 'None' })).toEqual({ kind: 'none' })
        expect(artistPictureViewOf({ kind: 'Albums', albumIds: [] })).toEqual({ kind: 'none' })
        expect(artistPictureViewOf({ kind: 'Uploaded', version: null })).toEqual({ kind: 'none' })
    })
})

describe('pictureSourceRows', () => {
    it('lists the sources in the order in which they win', () => {
        expect(pictureSourceRows(null).map((row) => row.source)).toEqual([
            'local',
            'uploaded',
            'online',
            'albums',
        ])
    })

    it('marks the shown source and the stored ones it hides', () => {
        const picture: ArtistPicture = {
            kind: 'Local',
            version: 'l-1',
            hasLocalPicture: true,
            hasUploadedPicture: true,
            hasOnlinePicture: true,
        }
        expect(pictureSourceRows(picture)).toEqual([
            { source: 'local', state: 'active', hiddenBy: null },
            { source: 'uploaded', state: 'available', hiddenBy: 'local' },
            { source: 'online', state: 'available', hiddenBy: 'local' },
            { source: 'albums', state: 'available', hiddenBy: 'local' },
        ])
    })

    it('names the uploaded picture as the one that hides the online picture', () => {
        const rows = pictureSourceRows({
            kind: 'Uploaded',
            version: 'u-1',
            hasUploadedPicture: true,
            hasOnlinePicture: true,
        })
        expect(rows.map((row) => [row.source, row.state, row.hiddenBy])).toEqual([
            ['local', 'missing', null],
            ['uploaded', 'active', null],
            ['online', 'available', 'uploaded'],
            ['albums', 'available', 'uploaded'],
        ])
    })

    it('shows the collage as active and nothing as missing without album covers', () => {
        expect(
            pictureSourceRows({ kind: 'Albums', albumIds: ['a'] }).map((row) => row.state),
        ).toEqual(['missing', 'missing', 'missing', 'active'])
        expect(pictureSourceRows({ kind: 'None' }).map((row) => row.state)).toEqual([
            'missing',
            'missing',
            'missing',
            'missing',
        ])
    })
})

describe('httpsUrlOf', () => {
    it('keeps absolute https addresses only', () => {
        expect(httpsUrlOf('https://creativecommons.org/licenses/by-sa/4.0/')).toBe(
            'https://creativecommons.org/licenses/by-sa/4.0/',
        )
        expect(httpsUrlOf('http://example.org/')).toBeNull()
        expect(httpsUrlOf('javascript:alert(1)')).toBeNull()
        expect(httpsUrlOf('JaVaScRiPt:alert(1)')).toBeNull()
        expect(httpsUrlOf('data:text/html,<script>alert(1)</script>')).toBeNull()
        expect(httpsUrlOf('//commons.wikimedia.org/wiki/File:A.jpg')).toBeNull()
        expect(httpsUrlOf('/relative')).toBeNull()
        expect(httpsUrlOf('')).toBeNull()
        expect(httpsUrlOf(null)).toBeNull()
    })
})

describe('creditPartsOf', () => {
    it('joins author, license and source that exist', () => {
        expect(
            creditPartsOf({
                provider: 'Wikimedia',
                author: '  Jane   Doe ',
                license: 'CC BY-SA 4.0',
                licenseUrl: 'https://creativecommons.org/licenses/by-sa/4.0/',
                sourceUrl: 'https://commons.wikimedia.org/wiki/File:Jane.jpg',
            }),
        ).toEqual({
            key: 'artistPictures.credit.authorLicense',
            author: 'Jane Doe',
            license: 'CC BY-SA 4.0',
            licenseUrl: 'https://creativecommons.org/licenses/by-sa/4.0/',
            providerKey: 'artistPictures.providers.wikimedia',
            sourceUrl: 'https://commons.wikimedia.org/wiki/File:Jane.jpg',
        })
    })

    it('chooses the sentence for the parts the service names', () => {
        expect(creditPartsOf({ provider: 'Wikimedia', author: 'A' })?.key).toBe(
            'artistPictures.credit.author',
        )
        expect(creditPartsOf({ provider: 'Wikimedia', license: 'CC0' })?.key).toBe(
            'artistPictures.credit.license',
        )
        expect(creditPartsOf({ provider: 'FanartTv', author: ' ' })).toMatchObject({
            key: 'artistPictures.credit.source',
            author: null,
            providerKey: 'artistPictures.providers.fanartTv',
        })
        expect(creditPartsOf(null)).toBeNull()
    })

    it('keeps markup as text and drops links that are not https', () => {
        const parts = creditPartsOf({
            provider: 'Wikimedia',
            author: '<script>alert(1)</script>',
            license: '<b>CC BY</b>',
            licenseUrl: 'javascript:alert(1)',
            sourceUrl: 'http://commons.wikimedia.org/wiki/File:A.jpg',
        })
        expect(parts?.author).toBe('<script>alert(1)</script>')
        expect(parts?.license).toBe('<b>CC BY</b>')
        expect(parts?.licenseUrl).toBeNull()
        expect(parts?.sourceUrl).toBeNull()
    })

    it('links no license without a license text', () => {
        expect(
            creditPartsOf({ provider: 'Wikimedia', licenseUrl: 'https://example.org/l' })
                ?.licenseUrl,
        ).toBeNull()
    })
})

describe('providerKey', () => {
    it('names the services', () => {
        expect(providerKey('Wikimedia')).toBe('artistPictures.providers.wikimedia')
        expect(providerKey('FanartTv')).toBe('artistPictures.providers.fanartTv')
        expect(providerKey(null)).toBe('artistPictures.providers.wikimedia')
    })
})

describe('takeOverCropOf', () => {
    it('gives the square as fractions of the preview', () => {
        // A landscape preview of 500 x 250: the square of 125 pixels at 250, 50.
        expect(takeOverCropOf({ x: 250, y: 50, zoom: 2 }, { width: 500, height: 250 })).toEqual({
            x: 0.5,
            y: 0.2,
            size: 0.5,
        })
    })

    it('gives the middle square without zoom', () => {
        expect(takeOverCropOf({ x: 100, y: 0, zoom: 1 }, { width: 400, height: 200 })).toEqual({
            x: 0.25,
            y: 0,
            size: 1,
        })
    })

    it('keeps the square inside the picture and gives nothing without a size', () => {
        expect(takeOverCropOf({ x: 900, y: -20, zoom: 1 }, { width: 400, height: 200 })).toEqual({
            x: 0.5,
            y: 0,
            size: 1,
        })
        expect(takeOverCropOf({ x: 0, y: 0, zoom: 1 }, { width: 0, height: 200 })).toBeNull()
    })
})

describe('lifeSpanOf', () => {
    it('gives the years of the life span', () => {
        expect(lifeSpanOf({ lifeSpanBegin: '1991-03', lifeSpanEnd: '2004' })).toEqual({
            begin: '1991',
            end: '2004',
        })
        expect(lifeSpanOf({ lifeSpanBegin: '1965' })).toEqual({ begin: '1965', end: null })
        expect(lifeSpanOf({ lifeSpanEnd: '2004' })).toBeNull()
    })
})

describe('pictureFailureKey', () => {
    it('translates the problem codes of the artist pictures', () => {
        expect(pictureFailureKey(ProblemCodeObject.ArtistPicture_too_large)).toBe(
            'artistPictures.errors.size',
        )
        expect(pictureFailureKey(ProblemCodeObject.ArtistPicture_unsupported_format)).toBe(
            'artistPictures.errors.type',
        )
        expect(pictureFailureKey(ProblemCodeObject.ArtistPicture_unreadable)).toBe(
            'artistPictures.errors.unreadable',
        )
        expect(pictureFailureKey(ProblemCodeObject.ArtistNot_found)).toBe(
            'artistPictures.errors.artistNotFound',
        )
        expect(pictureFailureKey(ProblemCodeObject.ArtistPicture_lookup_disabled)).toBe(
            'artistPictures.online.disabled',
        )
        expect(pictureFailureKey(ProblemCodeObject.ArtistPicture_lookup_unavailable)).toBe(
            'artistPictures.errors.unavailable',
        )
        expect(pictureFailureKey(ProblemCodeObject.ArtistPicture_candidate_not_found)).toBe(
            'artistPictures.errors.candidateExpired',
        )
        expect(pictureFailureKey(ProblemCodeObject.ServiceUnavailable)).toBe(
            'artistPictures.errors.processing',
        )
        expect(pictureFailureKey(undefined)).toBe('artistPictures.errors.saveFailed')
    })
})
