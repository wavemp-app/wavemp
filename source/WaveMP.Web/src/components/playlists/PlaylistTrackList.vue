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
import { computed, onBeforeUnmount, ref, type ComponentPublicInstance } from 'vue'
import { useI18n } from 'vue-i18n'
import BaseIcon from '@/components/base/BaseIcon.vue'
import BaseList, { type ListGroup, type ListRowBase } from '@/components/base/BaseList.vue'
import FavoriteButton from '@/components/collection/FavoriteButton.vue'
import RatingStars from '@/components/collection/RatingStars.vue'
import ItemMenuButton from '@/components/menus/ItemMenuButton.vue'
import { useFormat } from '@/composables/useFormat'
import { useSortableList } from '@/composables/useSortableList'
import {
    artistLabelKey,
    durationSeconds,
    trackListState,
    trackRowState,
} from '@/library/presentation'
import type { PlaylistEntryContext } from '@/menus/targets'
import type { PlayerSource } from '@/player/resume'
import { useContextMenuStore } from '@/stores/contextMenu'
import { usePlayerStore } from '@/stores/player'

/**
 * The tracks of a playlist (or of the list "Recently played") in their order, without grouping.
 * A double click, Enter or a tap plays the list from the row on. The context menu applies to all
 * selected rows (Ctrl and Shift with a click choose several). With sortable, the rows are entries
 * of a playlist: each row has a handle to drag it to another place (event move), and the context
 * menu also moves a row with the keyboard and removes the chosen rows (event remove). While the
 * state is "busy", rows are neither dragged nor moved or removed with the menu. The columns
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
/** The list has left the page; a menu opened from it has no rows to act on any more. */
const unmounted = ref(false)
onBeforeUnmount(() => {
    unmounted.value = true
})

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
/** "has-video" keeps a column for the video badge before the titles of all rows. */
const listState = computed(() => trackListState(tracks.value, props.state))

const rowsElement = computed(() => {
    const element: unknown = list.value?.$el
    return element instanceof HTMLElement
        ? element.querySelector<HTMLElement>('[data-part="list-group-rows"]')
        : null
})

useSortableList(rowsElement, {
    draggable: '[data-part="list-row"]',
    handle: '[data-part="drag-handle"]',
    // Like the menu, dragging waits while a change of the playlist runs.
    disabled: computed(() => !props.sortable || busy.value),
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

/** The rows with the ids as the list shows them now, in its order. */
function currentRows(ids: ReadonlySet<string>): Row[] {
    return unmounted.value ? [] : groups.value[0]!.rows.filter((row) => ids.has(row.id))
}

/**
 * What the menu does with the entries with the ids. The menu stays open while the page saves a
 * change or loads the playlist again, so nothing is copied: all is looked up by id when it is used.
 */
function entryContext(ids: ReadonlySet<string>): PlaylistEntryContext {
    function moveBy(offset: -1 | 1): void {
        const row = currentRows(ids)[0]
        if (row) {
            emit('move', row.index, row.index + offset)
        }
    }

    return {
        get busy() {
            return busy.value
        },
        get canMoveUp() {
            const row = currentRows(ids)[0]
            return !!row && row.index > 0
        },
        get canMoveDown() {
            const row = currentRows(ids)[0]
            return !!row && row.index < props.rows.length - 1
        },
        get canRemove() {
            return currentRows(ids).length > 0
        },
        moveUp: () => moveBy(-1),
        moveDown: () => moveBy(1),
        remove() {
            const indexes = currentRows(ids).map((row) => row.index)
            if (indexes.length > 0) {
                emit('remove', indexes)
            }
        },
    }
}

function openMenu(rowId: string, x: number, y: number): void {
    const chosen = new Set(selection.value.includes(rowId) ? selection.value : [rowId])
    const rows = currentRows(chosen)
    if (rows.length === 0) {
        return
    }

    const single = rows.length === 1
    contextMenu.open(
        {
            type: 'song',
            tracks: rows.map((row) => row.entry.track),
            // Once the row is gone there is nothing to play on from, and Play plays the song alone.
            get playFrom() {
                return single && currentRows(chosen).length > 0 ? () => activate(rowId) : undefined
            },
            playlistEntry: props.sortable ? entryContext(chosen) : undefined,
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
        :state="listState"
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
                <span data-part="label">{{ row.entry.track.title }}</span>
                <span data-part="list-subtitle" data-state="other-artist">
                    <span data-part="label">{{ artistOf(row.entry.track) }}</span>
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
