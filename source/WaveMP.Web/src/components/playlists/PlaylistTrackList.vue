<script lang="ts">
import type { Track } from '@/api/generated/models'

export interface PlaylistTrackRow {
    /** Id of the row: the entry of a playlist, or the track in lists without entries. */
    readonly id: string
    readonly track: Track
    /** Text of the detail column, e.g. when the track was played last. */
    readonly detail?: string
}
</script>

<script setup lang="ts">
import { computed, ref, type ComponentPublicInstance } from 'vue'
import { useI18n } from 'vue-i18n'
import BaseIcon from '@/components/base/BaseIcon.vue'
import BaseList, { type ListGroup, type ListRowBase } from '@/components/base/BaseList.vue'
import FavoriteButton from '@/components/collection/FavoriteButton.vue'
import RatingStars from '@/components/collection/RatingStars.vue'
import ItemMenuButton from '@/components/menus/ItemMenuButton.vue'
import { useFormat } from '@/composables/useFormat'
import { useSortableList } from '@/composables/useSortableList'
import { artistLabelKey, durationSeconds, trackRowState } from '@/library/presentation'
import type { PlayerSource } from '@/player/resume'
import { useContextMenuStore } from '@/stores/contextMenu'
import { usePlayerStore } from '@/stores/player'

/**
 * The tracks of a playlist (or of the list "Recently played") in their order, without grouping.
 * A double click, Enter or a tap plays the list from the row on. The context menu applies to all
 * selected rows (Ctrl and Shift with a click choose several). With sortable, the rows are entries
 * of a playlist: each row has a handle to drag it to another place (event move), and the context
 * menu also moves a row with the keyboard and removes the chosen rows (event remove). The columns
 * rating and favorite show the stars and the heart of the profile; a click changes them.
 */
interface Row extends ListRowBase {
    readonly entry: PlaylistTrackRow
    readonly index: number
}

const props = withDefaults(
    defineProps<{
        rows: readonly PlaylistTrackRow[]
        /** Accessible name of the list. */
        label: string
        sortable?: boolean
        /** Heading of the detail column; without it, the column is left out. */
        detailLabel?: string
        /** data-state tokens of the list, e.g. "busy" while a change of the playlist runs. */
        state?: string
        /** Where the tracks come from (the playlist of the page), saved with the queue. */
        source?: PlayerSource
    }>(),
    { sortable: false, detailLabel: undefined, state: undefined, source: undefined },
)

const emit = defineEmits<{
    move: [from: number, to: number]
    /** Indexes of the rows to remove, in the order of the list. */
    remove: [indexes: number[]]
}>()

/** Id of the selected row. */
const selected = defineModel<string | undefined>('selected', { default: undefined })

const { t } = useI18n()
const format = useFormat()
const player = usePlayerStore()
const list = ref<ComponentPublicInstance | null>(null)
const contextMenu = useContextMenuStore()
/** Ids of the selected rows. */
const selection = ref<readonly string[]>([])
const busy = computed(() => props.state?.split(' ').includes('busy') ?? false)

const columns = computed(() => [
    ...(props.sortable ? [{ id: 'handle', label: t('list.columns.handle') }] : []),
    { id: 'number', label: t('list.columns.number') },
    { id: 'title', label: t('list.columns.title') },
    { id: 'duration', label: t('list.columns.duration') },
    { id: 'artist', label: t('list.columns.artist') },
    { id: 'album', label: t('list.columns.album') },
    ...(props.detailLabel ? [{ id: 'detail', label: props.detailLabel }] : []),
    { id: 'rating', label: t('list.columns.rating') },
    { id: 'favorite', label: t('list.columns.favorite') },
])

const playingId = computed(() => player.state.trackId)

const groups = computed<ListGroup<Row>[]>(() => [
    {
        id: 'tracks',
        title: props.label,
        rows: props.rows.map((entry, index) => ({
            id: entry.id,
            state: [
                ...trackRowState(entry.track),
                ...(entry.track.id && entry.track.id === playingId.value ? ['playing'] : []),
            ],
            entry,
            index,
        })),
    },
])

const rowsById = computed(() => new Map(groups.value[0]!.rows.map((row) => [row.id, row])))
const tracks = computed(() => props.rows.map((row) => row.track))

const rowsElement = computed(() => {
    const element: unknown = list.value?.$el
    return element instanceof HTMLElement
        ? element.querySelector<HTMLElement>('[data-part="list-group-rows"]')
        : null
})

useSortableList(rowsElement, {
    draggable: '[data-part="list-row"]',
    handle: '[data-part="drag-handle"]',
    disabled: computed(() => !props.sortable),
    onMove: (from, to) => emit('move', from, to),
})

function artistOf(track: Track): string {
    const key = artistLabelKey(track.artist)
    return key ? t(key) : (track.artist?.name ?? '')
}

/** Plays the list from the row on; video-only tracks are left out. */
function playFrom(index: number): void {
    const track = tracks.value[index]
    if (track?.hasAudio) {
        player.playTracks(tracks.value, index, { source: props.source })
    }
}

function activate(rowId: string): void {
    const row = rowsById.value.get(rowId)
    if (row) {
        playFrom(row.index)
    }
}

function openMenu(rowId: string, x: number, y: number): void {
    const chosen = new Set(selection.value.includes(rowId) ? selection.value : [rowId])
    const rows = groups.value[0]!.rows.filter((row) => chosen.has(row.id))
    const first = rows[0]
    if (!first) {
        return
    }

    const single = rows.length === 1 ? first : undefined
    contextMenu.open(
        {
            type: 'song',
            tracks: rows.map((row) => row.entry.track),
            playFrom: single ? () => playFrom(single.index) : undefined,
            playlistEntry: props.sortable
                ? {
                      canMoveUp: first.index > 0,
                      canMoveDown: first.index < props.rows.length - 1,
                      busy: busy.value,
                      moveUp: () => emit('move', first.index, first.index - 1),
                      moveDown: () => emit('move', first.index, first.index + 1),
                      remove: () =>
                          emit(
                              'remove',
                              rows.map((row) => row.index),
                          ),
                  }
                : undefined,
        },
        x,
        y,
    )
}

/** The button "More options" of a row: its menu, for the row alone. */
function openRowMenu(rowId: string, x: number, y: number): void {
    selected.value = rowId
    selection.value = [rowId]
    openMenu(rowId, x, y)
}
</script>

<template>
    <BaseList
        ref="list"
        v-model:selected="selected"
        v-model:selection="selection"
        :columns="columns"
        :groups="groups"
        :label="label"
        :state="state"
        row-menu
        activate-on-tap
        multi-select
        @row-menu="openMenu"
        @row-activate="activate"
    >
        <template #cell="{ row, column }">
            <span
                v-if="column.id === 'handle'"
                data-part="drag-handle"
                :title="t('playlists.dragHandle')"
                @click.stop
            >
                <BaseIcon name="drag-handle" />
                <span data-part="label">{{ t('playlists.dragHandle') }}</span>
            </span>
            <template v-else-if="column.id === 'number'">
                {{ format.number(row.index + 1) }}
            </template>
            <template v-else-if="column.id === 'title'">
                <span data-part="label">{{ row.entry.track.title }}</span>
                <span
                    v-if="row.entry.track.hasVideo"
                    data-part="badge"
                    :data-state="trackRowState(row.entry.track).join(' ')"
                    :title="row.entry.track.hasAudio ? t('list.hasVideo') : t('list.videoOnly')"
                >
                    <BaseIcon name="has-video" />
                    <span data-part="label">{{
                        row.entry.track.hasAudio ? t('list.hasVideo') : t('list.videoOnly')
                    }}</span>
                </span>
            </template>
            <template v-else-if="column.id === 'duration'">
                {{
                    durationSeconds(row.entry.track.durationMilliseconds) === null
                        ? ''
                        : format.duration(durationSeconds(row.entry.track.durationMilliseconds)!)
                }}
            </template>
            <template v-else-if="column.id === 'artist'">
                {{ artistOf(row.entry.track) }}
            </template>
            <template v-else-if="column.id === 'album'">
                {{ row.entry.track.album?.title ?? '' }}
            </template>
            <template v-else-if="column.id === 'detail'">{{ row.entry.detail ?? '' }}</template>
            <RatingStars
                v-else-if="column.id === 'rating'"
                :track-id="row.entry.track.id ?? ''"
                :rating="row.entry.track.rating"
                :focusable="false"
            />
            <template v-else-if="column.id === 'favorite'">
                <FavoriteButton
                    type="Track"
                    :id="row.entry.track.id ?? ''"
                    :favorite="row.entry.track.isFavorite"
                    :focusable="false"
                />
                <ItemMenuButton
                    :focusable="false"
                    @open="(x: number, y: number) => openRowMenu(row.id, x, y)"
                />
            </template>
        </template>
    </BaseList>
</template>
