<script setup lang="ts">
import { computed, onActivated, onMounted, useId, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import BaseButton from '@/components/base/BaseButton.vue'
import RecapNav from '@/components/recaps/RecapNav.vue'
import RecapPeriodPicker from '@/components/recaps/RecapPeriodPicker.vue'
import StatisticsChart from '@/components/recaps/StatisticsChart.vue'
import StatisticsTable, {
    type StatisticsColumn,
    type StatisticsRow,
} from '@/components/recaps/StatisticsTable.vue'
import { useFormat } from '@/composables/useFormat'
import { hourBars, minutesOf, timelineBars, type RecapTexts } from '@/recaps/slides'
import { useRecapsStore } from '@/stores/recaps'

/**
 * The statistics of the profile in a period without story: the numbers, the plays over time and
 * per hour of the day as bar charts and the rankings as tables.
 */
const { t, locale } = useI18n()
const format = useFormat()
const recaps = useRecapsStore()
const ids = {
    totals: useId(),
    timeline: useId(),
    hours: useId(),
    rankings: useId(),
}

const period = computed({
    get: () => recaps.period,
    set: (value) => recaps.setPeriod(value),
})
const custom = computed({
    get: () => recaps.custom,
    set: (value) => recaps.setCustom(value),
})

const statistics = computed(() => recaps.statistics)
const texts = computed<RecapTexts>(() => ({
    t: (key, values) => t(key, values ?? {}),
    locale: locale.value,
}))

function listeningTime(milliseconds: number | null | undefined): string {
    const minutes = minutesOf(milliseconds)
    return t('statistics.hoursMinutes', {
        hours: format.number(Math.floor(minutes / 60)),
        minutes: format.number(minutes % 60),
    })
}

const totals = computed(() => {
    const value = statistics.value?.totals ?? {}
    return [
        {
            id: 'listening-time',
            labelKey: 'statistics.totals.listeningTime',
            text: listeningTime(value.listeningMilliseconds),
        },
        { id: 'plays', labelKey: 'statistics.totals.plays', text: format.number(value.plays ?? 0) },
        {
            id: 'tracks',
            labelKey: 'statistics.totals.tracks',
            text: format.number(value.tracks ?? 0),
        },
        {
            id: 'artists',
            labelKey: 'statistics.totals.artists',
            text: format.number(value.artists ?? 0),
        },
        {
            id: 'albums',
            labelKey: 'statistics.totals.albums',
            text: format.number(value.albums ?? 0),
        },
        {
            id: 'genres',
            labelKey: 'statistics.totals.genres',
            text: format.number(value.genres ?? 0),
        },
        {
            id: 'new-artists',
            labelKey: 'statistics.totals.newArtists',
            text: format.number(value.newArtists ?? 0),
        },
        {
            id: 'active-days',
            labelKey: 'statistics.totals.activeDays',
            text: format.number(value.activeDays ?? 0),
        },
    ]
})

const timeline = computed(() =>
    statistics.value ? timelineBars(texts.value, statistics.value) : [],
)
const hours = computed(() => (statistics.value ? hourBars(texts.value, statistics.value) : []))
const hasPlays = computed(() => (statistics.value?.totals?.plays ?? 0) > 0)

const rankColumns = (titleKey: string, extra: StatisticsColumn[] = []): StatisticsColumn[] => [
    { key: 'rank', label: t('statistics.columns.rank'), numeric: true },
    { key: 'title', label: t(titleKey) },
    ...extra,
    { key: 'plays', label: t('statistics.columns.plays'), numeric: true },
    { key: 'time', label: t('statistics.columns.time'), numeric: true },
]

function minutes(milliseconds: number | null | undefined): string {
    return t('statistics.minutes', { minutes: format.number(minutesOf(milliseconds)) })
}

interface Ranking {
    readonly id: string
    readonly titleKey: string
    readonly columns: StatisticsColumn[]
    readonly rows: StatisticsRow[]
}

const rankings = computed<Ranking[]>(() => {
    const value = statistics.value
    if (!value) {
        return []
    }
    const tracks: StatisticsRow[] = (value.topTracks ?? []).map((entry, index) => ({
        key: entry.track?.id ?? String(index),
        to: entry.track?.id ? { name: 'library-track', params: { id: entry.track.id } } : undefined,
        cells: {
            rank: format.number(index + 1),
            title: entry.track?.title ?? '',
            artist: entry.track?.artist?.name ?? '',
            plays: format.number(entry.plays ?? 0),
            time: minutes(entry.listeningMilliseconds),
        },
    }))
    const artists: StatisticsRow[] = (value.topArtists ?? []).map((entry, index) => ({
        key: entry.artist?.id ?? String(index),
        to: entry.artist?.id
            ? { name: 'library-artist', params: { id: entry.artist.id } }
            : undefined,
        cells: {
            rank: format.number(index + 1),
            title: entry.artist?.name ?? '',
            songs: format.number(entry.tracks ?? 0),
            plays: format.number(entry.plays ?? 0),
            time: minutes(entry.listeningMilliseconds),
        },
    }))
    const albums: StatisticsRow[] = (value.topAlbums ?? []).map((entry, index) => ({
        key: entry.album?.id ?? String(index),
        to: entry.album?.id ? { name: 'library-album', params: { id: entry.album.id } } : undefined,
        cells: {
            rank: format.number(index + 1),
            title: entry.album?.title ?? '',
            artist: entry.album?.isCompilation
                ? t('recap.compilation')
                : (entry.album?.artist?.name ?? ''),
            plays: format.number(entry.plays ?? 0),
            time: minutes(entry.listeningMilliseconds),
        },
    }))
    const genres: StatisticsRow[] = (value.topGenres ?? []).map((entry, index) => ({
        key: entry.name ?? String(index),
        to: entry.name ? { name: 'library-genre', params: { genre: entry.name } } : undefined,
        cells: {
            rank: format.number(index + 1),
            title: entry.name ?? '',
            songs: format.number(entry.tracks ?? 0),
            plays: format.number(entry.plays ?? 0),
            time: minutes(entry.listeningMilliseconds),
        },
    }))
    const discovered: StatisticsRow[] = (value.newArtists ?? []).map((entry, index) => ({
        key: entry.artist?.id ?? String(index),
        to: entry.artist?.id
            ? { name: 'library-artist', params: { id: entry.artist.id } }
            : undefined,
        cells: {
            rank: format.number(index + 1),
            title: entry.artist?.name ?? '',
            first: entry.firstPlayedAt
                ? format.date(entry.firstPlayedAt, { dateStyle: 'medium' })
                : '',
            plays: format.number(entry.plays ?? 0),
            time: minutes(entry.listeningMilliseconds),
        },
    }))

    const artistColumn = { key: 'artist', label: t('statistics.columns.artist') }
    const songsColumn = { key: 'songs', label: t('statistics.columns.songs'), numeric: true }
    return [
        {
            id: 'top-tracks',
            titleKey: 'statistics.topTracks',
            columns: rankColumns('statistics.columns.song', [artistColumn]),
            rows: tracks,
        },
        {
            id: 'top-artists',
            titleKey: 'statistics.topArtists',
            columns: rankColumns('statistics.columns.artist', [songsColumn]),
            rows: artists,
        },
        {
            id: 'top-albums',
            titleKey: 'statistics.topAlbums',
            columns: rankColumns('statistics.columns.album', [artistColumn]),
            rows: albums,
        },
        {
            id: 'top-genres',
            titleKey: 'statistics.topGenres',
            columns: rankColumns('statistics.columns.genre', [songsColumn]),
            rows: genres,
        },
        {
            id: 'new-artists',
            titleKey: 'statistics.newArtists',
            columns: rankColumns('statistics.columns.artist', [
                { key: 'first', label: t('statistics.columns.firstPlayed') },
            ]),
            rows: discovered,
        },
    ].filter((ranking) => ranking.rows.length > 0)
})

watch(
    () => recaps.range,
    () => void recaps.load(),
    { deep: true },
)

onMounted(() => void recaps.load())
onActivated(() => void recaps.load())
</script>

<template>
    <div
        data-part="view"
        data-view="statistics"
        :data-state="recaps.status"
        :aria-busy="recaps.status === 'loading' || undefined"
    >
        <RecapNav />
        <RecapPeriodPicker
            v-model="period"
            v-model:custom="custom"
            :invalid="recaps.status === 'invalid'"
        />
        <div v-if="recaps.status === 'error'" data-part="empty-state" data-item="statistics-error">
            <p data-part="empty-state-title">{{ t('statistics.loadError') }}</p>
            <BaseButton data-item="retry" :label="t('library.retry')" @click="recaps.load(true)" />
        </div>
        <template v-else-if="statistics">
            <section data-part="section" data-item="totals" :aria-labelledby="ids.totals">
                <h2 :id="ids.totals" data-part="section-title">
                    {{ t('statistics.totals.title') }}
                </h2>
                <dl data-part="stat-list">
                    <div
                        v-for="item in totals"
                        :key="item.id"
                        data-part="stat-item"
                        :data-item="item.id"
                    >
                        <dt data-part="label">{{ t(item.labelKey) }}</dt>
                        <dd data-part="stat-value">{{ item.text }}</dd>
                    </div>
                </dl>
            </section>
            <div v-if="!hasPlays" data-part="empty-state" data-item="statistics-empty">
                <p data-part="empty-state-title">{{ t('statistics.emptyTitle') }}</p>
                <p data-part="empty-state-text">{{ t('statistics.emptyText') }}</p>
            </div>
            <template v-else>
                <section
                    v-if="timeline.length > 1"
                    data-part="section"
                    data-item="timeline"
                    :aria-labelledby="ids.timeline"
                >
                    <h2 :id="ids.timeline" data-part="section-title">
                        {{ t('statistics.timeline') }}
                    </h2>
                    <StatisticsChart
                        :bars="timeline"
                        :label="t('statistics.timeline')"
                        interactive
                    />
                </section>
                <section data-part="section" data-item="hours" :aria-labelledby="ids.hours">
                    <h2 :id="ids.hours" data-part="section-title">{{ t('statistics.hours') }}</h2>
                    <StatisticsChart :bars="hours" :label="t('statistics.hours')" interactive />
                </section>
                <section
                    v-for="ranking in rankings"
                    :key="ranking.id"
                    data-part="section"
                    :data-item="ranking.id"
                    :aria-labelledby="`${ids.rankings}-${ranking.id}`"
                >
                    <h2 :id="`${ids.rankings}-${ranking.id}`" data-part="section-title">
                        {{ t(ranking.titleKey) }}
                    </h2>
                    <StatisticsTable
                        :labelledby="`${ids.rankings}-${ranking.id}`"
                        :columns="ranking.columns"
                        :rows="ranking.rows"
                    />
                </section>
            </template>
        </template>
    </div>
</template>
