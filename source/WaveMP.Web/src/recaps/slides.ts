// The story of a recap: which sections it shows for the statistics of a period and their texts.
// The page (RecapStory.vue) and the pictures and videos (canvas.ts) show the same view models, so
// a picture says exactly what the page says. Sections without data are left out.

import {
    TimelineGranularityObject,
    type ListeningStatistics,
    type TimelineGranularity,
} from '@/api/generated/models'
import { dayToDate, type CalendarDay } from './periods'

export type RecapSlideKind =
    | 'intro'
    | 'listening-time'
    | 'top-track'
    | 'top-tracks'
    | 'top-artists'
    | 'top-albums'
    | 'top-genres'
    | 'timeline'
    | 'hours'
    | 'new-artists'
    | 'summary'
    | 'empty'

/** An entry of a ranking: rank, name, a second line and the number of plays. */
export interface RecapItem {
    readonly key: string
    readonly rank: number
    readonly title: string
    readonly subtitle: string
    readonly value: string
    readonly coverTrackId: string | null
}

/** A bar of a chart: its share of the highest bar (0 to 1) and its texts. */
export interface RecapBar {
    readonly key: string
    readonly label: string
    /** Accessible description, e.g. "Nov 3: 12 plays". */
    readonly description: string
    readonly ratio: number
    readonly peak: boolean
    /** Whether the axis shows the label (every bar of a short chart, some of a long one). */
    readonly labelled: boolean
}

/** A number of the summary. */
export interface RecapFact {
    readonly key: string
    readonly label: string
    readonly value: string
}

/** One section of the story with all its texts. */
export interface RecapSlide {
    readonly kind: RecapSlideKind
    readonly title: string
    /** Large number or name, e.g. the minutes or the favorite hour. */
    readonly figure?: string
    /** Line below the figure, e.g. "minutes" or the artist of the song. */
    readonly figureLabel?: string
    readonly caption?: string
    /** Track whose cover the section shows large. */
    readonly coverTrackId?: string | null
    readonly items?: readonly RecapItem[]
    readonly bars?: readonly RecapBar[]
    readonly facts?: readonly RecapFact[]
}

export type Translate = (key: string, values?: Record<string, unknown>) => string

/** Translation and formatting in the language of the page. */
export interface RecapTexts {
    readonly t: Translate
    readonly locale: string
}

/** Who and which days the recap is about. */
export interface RecapSubject {
    /** Name of the profile the recap belongs to. */
    readonly owner: string
    /** Name of the period, e.g. "Last month". */
    readonly periodLabel: string
}

/** Number of entries a ranking of the story shows. */
export const storyRankingLength = 5

function number(texts: RecapTexts, value: number): string {
    return new Intl.NumberFormat(texts.locale).format(value)
}

function day(texts: RecapTexts, value: CalendarDay, options: Intl.DateTimeFormatOptions): string {
    return new Intl.DateTimeFormat(texts.locale, { ...options, timeZone: 'UTC' }).format(
        dayToDate(value),
    )
}

/** The days of a period, e.g. "Oct 21 – Nov 20, 2026". */
export function formatRange(texts: RecapTexts, from: CalendarDay, to: CalendarDay): string {
    return new Intl.DateTimeFormat(texts.locale, {
        dateStyle: 'medium',
        timeZone: 'UTC',
    }).formatRange(dayToDate(from), dayToDate(to))
}

/** Whole minutes of a number of milliseconds. */
export function minutesOf(milliseconds: number | null | undefined): number {
    return Math.floor((milliseconds ?? 0) / 60_000)
}

/**
 * A message for a number in the plural form of the language: the key with the suffix One for one
 * (e.g. "1 play"), otherwise Other ("2 plays"). The number is the placeholder count.
 */
export function counted(texts: RecapTexts, key: string, count: number | null | undefined): string {
    const value = count ?? 0
    const form = new Intl.PluralRules(texts.locale).select(value) === 'one' ? 'One' : 'Other'
    return texts.t(`${key}${form}`, { count: number(texts, value) })
}

function plays(texts: RecapTexts, count: number | null | undefined): string {
    return counted(texts, 'recap.plays', count)
}

/** Label of a bar of the timeline: the day, the first day of the week, the month or the year. */
export function timelineLabel(
    texts: RecapTexts,
    granularity: TimelineGranularity | null | undefined,
    start: CalendarDay,
): string {
    switch (granularity) {
        case TimelineGranularityObject.Year:
            return day(texts, start, { year: 'numeric' })
        case TimelineGranularityObject.Month:
            return day(texts, start, { month: 'short', year: 'numeric' })
        default:
            return day(texts, start, { month: 'short', day: 'numeric' })
    }
}

/** Label of an hour of the day, e.g. "9 PM" or "21 Uhr". */
export function hourLabel(texts: RecapTexts, hour: number): string {
    return new Intl.DateTimeFormat(texts.locale, { hour: 'numeric', timeZone: 'UTC' }).format(
        new Date(Date.UTC(2000, 0, 1, hour)),
    )
}

interface BarSource {
    readonly key: string
    readonly label: string
    readonly plays: number
}

/** Bars scaled to the highest one; the first highest is the peak. A long chart labels some bars. */
export function toBars(
    texts: RecapTexts,
    sources: readonly BarSource[],
    labelEvery = 1,
): RecapBar[] {
    const max = Math.max(0, ...sources.map((source) => source.plays))
    const peak = max > 0 ? sources.findIndex((source) => source.plays === max) : -1
    const step = Math.max(1, labelEvery)
    return sources.map((source, index) => ({
        key: source.key,
        label: source.label,
        description: texts.t('recap.barDescription', {
            label: source.label,
            plays: plays(texts, source.plays),
        }),
        ratio: max > 0 ? source.plays / max : 0,
        peak: index === peak,
        labelled: index % step === 0 || index === sources.length - 1,
    }))
}

/** The day of a DateOnly of the API, or null. */
export function dayOf(value: unknown): CalendarDay | null {
    const candidate = value as Partial<CalendarDay> | null | undefined
    return typeof candidate?.year === 'number' &&
        typeof candidate.month === 'number' &&
        typeof candidate.day === 'number'
        ? { year: candidate.year, month: candidate.month, day: candidate.day }
        : null
}

/** Labels at most about six bars of the axis. */
function labelStep(count: number): number {
    return Math.max(1, Math.ceil(count / 6))
}

/** The bars of the timeline of the statistics. */
export function timelineBars(texts: RecapTexts, statistics: ListeningStatistics): RecapBar[] {
    const granularity = statistics.timeline?.granularity
    const sources = (statistics.timeline?.buckets ?? []).flatMap((bucket) => {
        const start = dayOf(bucket.start)
        return start
            ? [
                  {
                      key: `${start.year}-${start.month}-${start.day}`,
                      label: timelineLabel(texts, granularity, start),
                      plays: bucket.plays ?? 0,
                  },
              ]
            : []
    })
    return toBars(texts, sources, labelStep(sources.length))
}

/** The 24 bars of the hours of the day. */
export function hourBars(texts: RecapTexts, statistics: ListeningStatistics): RecapBar[] {
    const byHour = new Map(
        (statistics.hours ?? []).map((hour) => [hour.hour ?? 0, hour.plays ?? 0]),
    )
    const sources = Array.from({ length: 24 }, (_, hour) => ({
        key: String(hour),
        label: hourLabel(texts, hour),
        plays: byHour.get(hour) ?? 0,
    }))
    return toBars(texts, sources, 6)
}

/** The sections of the story for the statistics of a period. */
export function buildRecapSlides(
    statistics: ListeningStatistics,
    subject: RecapSubject,
    texts: RecapTexts,
): RecapSlide[] {
    const { t } = texts
    const totals = statistics.totals ?? {}
    const from = dayOf(statistics.from)
    const to = dayOf(statistics.to)
    const range = from && to ? formatRange(texts, from, to) : ''
    const intro: RecapSlide = {
        kind: 'intro',
        title: t('recap.slides.intro.title', { name: subject.owner }),
        figure: subject.periodLabel,
        caption: range,
    }

    if ((totals.plays ?? 0) === 0) {
        return [
            intro,
            {
                kind: 'empty',
                title: t('recap.slides.empty.title'),
                caption: t('recap.slides.empty.caption'),
            },
        ]
    }

    const slides: RecapSlide[] = [intro]
    const minutes = minutesOf(totals.listeningMilliseconds)
    slides.push({
        kind: 'listening-time',
        title: t('recap.slides.listeningTime.title'),
        figure: number(texts, minutes),
        figureLabel: counted(texts, 'recap.slides.listeningTime.unit', minutes),
        facts: [
            {
                key: 'plays',
                label: t('recap.facts.plays'),
                value: number(texts, totals.plays ?? 0),
            },
            {
                key: 'tracks',
                label: t('recap.facts.tracks'),
                value: number(texts, totals.tracks ?? 0),
            },
            {
                key: 'active-days',
                label: t('recap.facts.activeDays'),
                value: number(texts, totals.activeDays ?? 0),
            },
        ],
    })

    const topTracks = (statistics.topTracks ?? []).filter((entry) => entry.track?.id)
    const first = topTracks[0]
    if (first?.track) {
        slides.push({
            kind: 'top-track',
            title: t('recap.slides.topTrack.title'),
            figure: first.track.title ?? '',
            figureLabel: first.track.artist?.name ?? '',
            caption: plays(texts, first.plays),
            coverTrackId: first.track.hasCover ? (first.track.id ?? null) : null,
        })
    }

    const tracks: RecapItem[] = topTracks.slice(0, storyRankingLength).map((entry, index) => ({
        key: entry.track?.id ?? String(index),
        rank: index + 1,
        title: entry.track?.title ?? '',
        subtitle: entry.track?.artist?.name ?? '',
        value: plays(texts, entry.plays),
        coverTrackId: entry.track?.hasCover ? (entry.track.id ?? null) : null,
    }))
    if (tracks.length > 1) {
        slides.push({ kind: 'top-tracks', title: t('recap.slides.topTracks.title'), items: tracks })
    }

    const artists: RecapItem[] = (statistics.topArtists ?? [])
        .slice(0, storyRankingLength)
        .map((entry, index) => ({
            key: entry.artist?.id ?? String(index),
            rank: index + 1,
            title: entry.artist?.name ?? '',
            subtitle: counted(texts, 'recap.songs', entry.tracks),
            value: plays(texts, entry.plays),
            coverTrackId: entry.coverTrackId ?? null,
        }))
    if (artists.length > 0) {
        slides.push({
            kind: 'top-artists',
            title: t('recap.slides.topArtists.title'),
            items: artists,
        })
    }

    const albums: RecapItem[] = (statistics.topAlbums ?? [])
        .slice(0, storyRankingLength)
        .map((entry, index) => ({
            key: entry.album?.id ?? String(index),
            rank: index + 1,
            title: entry.album?.title ?? '',
            subtitle: entry.album?.isCompilation
                ? t('recap.compilation')
                : (entry.album?.artist?.name ?? ''),
            value: plays(texts, entry.plays),
            coverTrackId: entry.coverTrackId ?? null,
        }))
    if (albums.length > 0) {
        slides.push({ kind: 'top-albums', title: t('recap.slides.topAlbums.title'), items: albums })
    }

    const genres: RecapItem[] = (statistics.topGenres ?? [])
        .slice(0, storyRankingLength)
        .map((entry, index) => ({
            key: entry.name ?? String(index),
            rank: index + 1,
            title: entry.name ?? '',
            subtitle: counted(texts, 'recap.songs', entry.tracks),
            value: plays(texts, entry.plays),
            coverTrackId: entry.coverTrackId ?? null,
        }))
    if (genres.length > 0) {
        slides.push({ kind: 'top-genres', title: t('recap.slides.topGenres.title'), items: genres })
    }

    const timeline = timelineBars(texts, statistics)
    const busiest = timeline.find((bar) => bar.peak)
    if (timeline.length > 1 && busiest) {
        const granularity = statistics.timeline?.granularity ?? TimelineGranularityObject.Day
        slides.push({
            kind: 'timeline',
            title: t('recap.slides.timeline.title'),
            bars: timeline,
            caption: t(`recap.slides.timeline.peak${granularity}`, { label: busiest.label }),
        })
    }

    const hours = hourBars(texts, statistics)
    const favoriteHour = hours.find((bar) => bar.peak)
    if (favoriteHour) {
        slides.push({
            kind: 'hours',
            title: t('recap.slides.hours.title'),
            figure: favoriteHour.label,
            figureLabel: t('recap.slides.hours.unit'),
            bars: hours,
        })
    }

    const discovered = (statistics.newArtists ?? []).slice(0, storyRankingLength)
    if (discovered.length > 0) {
        slides.push({
            kind: 'new-artists',
            title: t('recap.slides.newArtists.title'),
            figure: number(texts, totals.newArtists ?? discovered.length),
            figureLabel: counted(
                texts,
                'recap.slides.newArtists.unit',
                totals.newArtists ?? discovered.length,
            ),
            items: discovered.map((entry, index) => ({
                key: entry.artist?.id ?? String(index),
                rank: index + 1,
                title: entry.artist?.name ?? '',
                subtitle: entry.firstPlayedAt
                    ? t('recap.firstPlayed', {
                          date: new Intl.DateTimeFormat(texts.locale, {
                              dateStyle: 'medium',
                          }).format(entry.firstPlayedAt),
                      })
                    : '',
                value: plays(texts, entry.plays),
                coverTrackId: entry.coverTrackId ?? null,
            })),
        })
    }

    const facts: RecapFact[] = [
        { key: 'minutes', label: t('recap.facts.minutes'), value: number(texts, minutes) },
        { key: 'tracks', label: t('recap.facts.tracks'), value: number(texts, totals.tracks ?? 0) },
        {
            key: 'artists',
            label: t('recap.facts.artists'),
            value: number(texts, totals.artists ?? 0),
        },
    ]
    if (artists[0]) {
        facts.push({
            key: 'top-artist',
            label: t('recap.facts.topArtist'),
            value: artists[0].title,
        })
    }
    if (tracks[0]) {
        facts.push({ key: 'top-track', label: t('recap.facts.topTrack'), value: tracks[0].title })
    }
    if (genres[0]) {
        facts.push({ key: 'top-genre', label: t('recap.facts.topGenre'), value: genres[0].title })
    }
    slides.push({
        kind: 'summary',
        title: t('recap.slides.summary.title', { name: subject.owner }),
        caption: t('recap.slides.summary.caption', { period: subject.periodLabel, range }),
        facts,
        coverTrackId: artists[0]?.coverTrackId ?? tracks[0]?.coverTrackId ?? null,
    })

    return slides
}
