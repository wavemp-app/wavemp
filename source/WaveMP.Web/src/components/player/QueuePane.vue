<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import BaseSegmentedControl, {
    type SegmentOption,
} from '@/components/base/BaseSegmentedControl.vue'
import LibraryCover from '@/components/library/LibraryCover.vue'
import LyricsStage from './LyricsStage.vue'
import PlayQueue from './PlayQueue.vue'
import { useFormat } from '@/composables/useFormat'
import ArtistNames from '@/components/library/ArtistNames.vue'
import { artistsOf } from '@/library/artists'
import type { TrackLyrics } from '@/lyrics/timing'
import { queueSummary, type QueueItem } from '@/player/queue'

/**
 * The play queue as a pane beside the content, where a theme docks it (layout option
 * --wmp-layout-queue-pane, THEMING.md). At the top a segmented control chooses between the queue
 * and the lyrics of the current song, below it the cover, title and artist of the current song.
 * The queue is the one of Now Playing (without the songs autoplay plays next), the lyrics are the
 * lyrics view of the stage, which only follows the music while it is chosen. The foot names the
 * number of songs in the queue and their total duration.
 */
const props = withDefaults(
    defineProps<{
        items: readonly QueueItem[]
        currentIndex: number
        /** The lyrics of the current song; null while they load and for songs without lyrics. */
        lyrics?: TrackLyrics | null
        playing?: boolean
        /** Position of the music in seconds. */
        position?: number
        /** Duration of the current song in seconds; 0 if unknown. */
        duration?: number
        /** The exact position of the music right now, in seconds. */
        clock?: () => number
    }>(),
    { lyrics: null, playing: false, position: 0, duration: 0, clock: undefined },
)

const emit = defineEmits<{
    play: [key: string]
    move: [key: string, offset: -1 | 1]
    remove: [key: string]
    clear: []
    seek: [seconds: number]
}>()

type PaneView = 'queue' | 'lyrics'

const { t, locale } = useI18n()
const format = useFormat()

const view = ref<PaneView>('queue')
const views = computed<SegmentOption[]>(() => [
    { value: 'queue', label: t('queuePane.queue') },
    { value: 'lyrics', label: t('queuePane.lyrics') },
])

const current = computed(() =>
    props.currentIndex >= 0 ? props.items[props.currentIndex] : undefined,
)

const hasLyrics = computed(
    () =>
        props.lyrics !== null &&
        current.value !== undefined &&
        props.lyrics.trackId === current.value.track.id,
)

const summary = computed(() => queueSummary(props.items))
/** The number of songs in the plural form of the language (One for one, otherwise Other). */
const countText = computed(() => {
    const count = summary.value.count
    const one = new Intl.PluralRules(locale.value).select(count) === 'one'
    return t(one ? 'queuePane.songCountOne' : 'queuePane.songCountOther', {
        count: format.number(count),
    })
})
const durationText = computed(() =>
    t(summary.value.incomplete ? 'queuePane.durationAtLeast' : 'queuePane.duration', {
        duration: format.duration(summary.value.seconds),
    }),
)

function chooseView(value: string): void {
    view.value = value === 'lyrics' ? 'lyrics' : 'queue'
}
</script>

<template>
    <aside data-part="queue-pane" :aria-label="t('queuePane.label')">
        <BaseSegmentedControl
            item="queue-pane-view"
            :label="t('queuePane.viewLabel')"
            :options="views"
            :model-value="view"
            @update:model-value="chooseView"
        />
        <div data-part="queue-pane-header" :data-state="current ? undefined : 'empty'">
            <template v-if="current">
                <LibraryCover
                    kind="track"
                    :id="current.track.id"
                    :available="current.track.hasCover ?? false"
                />
                <p data-part="queue-pane-title">{{ current.track.title }}</p>
                <p data-part="queue-pane-subtitle">
                    <ArtistNames
                        :artists="artistsOf(current.track)"
                        :fallback="t('library.unknownArtist')"
                        part="artist-link"
                    />
                </p>
            </template>
            <p v-else data-part="queue-pane-title">{{ t('nowPlaying.emptyTitle') }}</p>
        </div>
        <PlayQueue
            v-if="items.length > 0"
            :hidden="view !== 'queue' || undefined"
            :items="items"
            :current-index="currentIndex"
            @play="emit('play', $event)"
            @move="(key: string, offset: -1 | 1) => emit('move', key, offset)"
            @remove="emit('remove', $event)"
            @clear="emit('clear')"
        />
        <div v-else-if="view === 'queue'" data-part="empty-state">
            <p data-part="empty-state-title">{{ t('queuePane.emptyTitle') }}</p>
            <p data-part="empty-state-text">{{ t('queuePane.emptyText') }}</p>
        </div>
        <LyricsStage
            v-if="hasLyrics"
            :hidden="view !== 'lyrics' || undefined"
            :lyrics="lyrics"
            :active="view === 'lyrics'"
            :playing="playing"
            :position="position"
            :duration="duration"
            :clock="clock"
            @seek="emit('seek', $event)"
        />
        <div v-else-if="view === 'lyrics'" data-part="empty-state">
            <p data-part="empty-state-text">{{ t('queuePane.noLyrics') }}</p>
        </div>
        <footer data-part="queue-pane-summary">
            <span data-part="label" data-field="count">{{ countText }}</span>
            <span data-part="label" data-field="duration">{{ durationText }}</span>
        </footer>
    </aside>
</template>
