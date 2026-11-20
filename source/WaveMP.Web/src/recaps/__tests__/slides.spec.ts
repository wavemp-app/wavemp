import { describe, expect, it } from 'vitest'
import { DateOnly } from '@microsoft/kiota-abstractions'
import { TimelineGranularityObject, type ListeningStatistics } from '@/api/generated/models'
import { createWaveMpI18n } from '@/i18n'
import { coverTrackIdsOf } from '../covers'
import {
    buildRecapSlides,
    counted,
    hourBars,
    minutesOf,
    timelineBars,
    toBars,
    type RecapTexts,
} from '../slides'

function textsIn(locale: 'en' | 'de'): RecapTexts {
    const i18n = createWaveMpI18n(locale)
    return {
        t: (key, values) => i18n.global.t(key, values ?? {}),
        locale,
    }
}

const english = textsIn('en')
const subject = { owner: 'Ada', periodLabel: 'Last month' }

function day(year: number, month: number, dayOfMonth: number): DateOnly {
    return new DateOnly({ year, month, day: dayOfMonth })
}

function track(id: string, title: string, artist: string, hasCover = true) {
    return { id, title, hasCover, artist: { id: `artist-${artist}`, name: artist } }
}

const statistics: ListeningStatistics = {
    from: day(2026, 10, 21),
    to: day(2026, 11, 20),
    timeZone: 'Europe/Berlin',
    totals: {
        plays: 42,
        tracks: 12,
        artists: 5,
        albums: 4,
        genres: 3,
        newArtists: 2,
        activeDays: 9,
        listeningMilliseconds: 125 * 60_000 + 59_000,
    },
    topTracks: [
        { plays: 10, track: track('t1', 'First Song', 'Nova') },
        { plays: 7, track: track('t2', 'Second Song', 'Lumen', false) },
    ],
    topArtists: [
        { plays: 17, tracks: 1, coverTrackId: 't1', artist: { id: 'a1', name: 'Nova' } },
        { plays: 9, tracks: 3, coverTrackId: null, artist: { id: 'a2', name: 'Lumen' } },
    ],
    topAlbums: [
        {
            plays: 12,
            coverTrackId: 't1',
            album: { id: 'b1', title: 'Mixed', isCompilation: true, artist: null },
        },
    ],
    topGenres: [{ name: 'Synthpop', plays: 20, tracks: 4, coverTrackId: 't2' }],
    timeline: {
        granularity: TimelineGranularityObject.Week,
        buckets: [
            { start: day(2026, 10, 19), plays: 4 },
            { start: day(2026, 10, 26), plays: 20 },
            { start: day(2026, 11, 2), plays: 8 },
        ],
    },
    hours: [
        { hour: 8, plays: 5 },
        { hour: 21, plays: 30 },
    ],
    newArtists: [
        {
            plays: 3,
            coverTrackId: 't2',
            firstPlayedAt: new Date(Date.UTC(2026, 10, 2, 12)),
            artist: { id: 'a2', name: 'Lumen' },
        },
    ],
}

describe('recap slides', () => {
    it('tells the story of the period section by section', () => {
        const slides = buildRecapSlides(statistics, subject, english)
        expect(slides.map((slide) => slide.kind)).toEqual([
            'intro',
            'listening-time',
            'top-track',
            'top-tracks',
            'top-artists',
            'top-albums',
            'top-genres',
            'timeline',
            'hours',
            'new-artists',
            'summary',
        ])
        for (const slide of slides) {
            expect(slide.title, slide.kind).not.toMatch(/^recap\./)
        }
    })

    it('shows big numbers with their units', () => {
        const slides = buildRecapSlides(statistics, subject, english)
        const listening = slides.find((slide) => slide.kind === 'listening-time')
        expect(listening?.figure).toBe('125')
        expect(listening?.figureLabel).toBe('minutes of music')
        expect(listening?.facts?.map((fact) => [fact.key, fact.value])).toEqual([
            ['plays', '42'],
            ['tracks', '12'],
            ['active-days', '9'],
        ])

        const top = slides.find((slide) => slide.kind === 'top-track')
        expect(top?.figure).toBe('First Song')
        expect(top?.figureLabel).toBe('Nova')
        expect(top?.caption).toBe('10 plays')
        expect(top?.coverTrackId).toBe('t1')
    })

    it('ranks songs, artists, albums and genres', () => {
        const slides = buildRecapSlides(statistics, subject, english)
        const artists = slides.find((slide) => slide.kind === 'top-artists')?.items ?? []
        expect(artists.map((item) => [item.rank, item.title, item.subtitle, item.value])).toEqual([
            [1, 'Nova', '1 song', '17 plays'],
            [2, 'Lumen', '3 songs', '9 plays'],
        ])
        const albums = slides.find((slide) => slide.kind === 'top-albums')?.items ?? []
        expect(albums[0]?.subtitle).not.toBe('')
        expect(albums[0]?.subtitle).not.toMatch(/^recap\./)
        const tracks = slides.find((slide) => slide.kind === 'top-tracks')?.items ?? []
        expect(tracks.map((item) => item.coverTrackId)).toEqual(['t1', null])
    })

    it('shows the course over time and the favorite hour', () => {
        const slides = buildRecapSlides(statistics, subject, english)
        const timeline = slides.find((slide) => slide.kind === 'timeline')
        expect(timeline?.bars?.map((bar) => bar.ratio)).toEqual([0.2, 1, 0.4])
        expect(timeline?.bars?.map((bar) => bar.peak)).toEqual([false, true, false])
        expect(timeline?.caption).toContain('Oct 26')

        const hours = slides.find((slide) => slide.kind === 'hours')
        expect(hours?.bars).toHaveLength(24)
        expect(hours?.figure).toBe('9 PM')
    })

    it('sums the period up', () => {
        const slides = buildRecapSlides(statistics, subject, english)
        const summary = slides.at(-1)
        expect(summary?.kind).toBe('summary')
        expect(summary?.facts?.map((fact) => fact.key)).toEqual([
            'minutes',
            'tracks',
            'artists',
            'top-artist',
            'top-track',
            'top-genre',
        ])
        expect(summary?.coverTrackId).toBe('t1')
        expect(summary?.caption).toContain('Last month')
    })

    it('has only the intro and a note without plays', () => {
        const slides = buildRecapSlides(
            { ...statistics, totals: { plays: 0 }, topTracks: [] },
            subject,
            english,
        )
        expect(slides.map((slide) => slide.kind)).toEqual(['intro', 'empty'])
    })

    it('leaves out a ranking of one song and a timeline of one bar', () => {
        const slides = buildRecapSlides(
            {
                ...statistics,
                topTracks: statistics.topTracks?.slice(0, 1),
                timeline: { granularity: 'Day', buckets: [{ start: day(2026, 11, 1), plays: 3 }] },
            },
            subject,
            english,
        )
        expect(slides.map((slide) => slide.kind)).not.toContain('top-tracks')
        expect(slides.map((slide) => slide.kind)).not.toContain('timeline')
    })

    it('speaks German', () => {
        const slides = buildRecapSlides(statistics, subject, textsIn('de'))
        const listening = slides.find((slide) => slide.kind === 'listening-time')
        expect(listening?.figureLabel).not.toBe('minutes of music')
        expect(slides.find((slide) => slide.kind === 'hours')?.figure).toBe('21 Uhr')
    })

    it('collects the covers of all sections once', () => {
        const slides = buildRecapSlides(statistics, subject, english)
        expect(coverTrackIdsOf(slides).sort()).toEqual(['t1', 't2'])
    })
})

describe('recap numbers', () => {
    it('counts whole minutes and picks the plural form', () => {
        expect(minutesOf(119_999)).toBe(1)
        expect(minutesOf(null)).toBe(0)
        expect(counted(english, 'recap.plays', 1)).toBe('1 play')
        expect(counted(english, 'recap.plays', 1234)).toBe('1,234 plays')
        expect(counted(textsIn('de'), 'recap.plays', 1234)).toContain('1.234')
    })

    it('scales bars to the highest one and labels some of a long chart', () => {
        const bars = toBars(
            english,
            Array.from({ length: 13 }, (_, index) => ({
                key: String(index),
                label: String(index),
                plays: index === 4 ? 8 : 2,
            })),
            6,
        )
        expect(bars[4]?.peak).toBe(true)
        expect(bars[0]?.ratio).toBe(0.25)
        expect(bars.filter((bar) => bar.labelled).map((bar) => bar.key)).toEqual(['0', '6', '12'])
        expect(toBars(english, [{ key: 'a', label: 'a', plays: 0 }])[0]).toMatchObject({
            ratio: 0,
            peak: false,
        })
    })

    it('builds the bars of the timeline and of the hours', () => {
        expect(timelineBars(english, statistics)).toHaveLength(3)
        expect(timelineBars(english, { ...statistics, timeline: null })).toEqual([])
        const hours = hourBars(english, statistics)
        expect(hours.filter((bar) => bar.labelled).map((bar) => bar.key)).toEqual([
            '0',
            '6',
            '12',
            '18',
            '23',
        ])
    })
})
