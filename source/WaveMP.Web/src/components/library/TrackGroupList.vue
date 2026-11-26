<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { Track } from '@/api/generated/models'
import BaseCover from '@/components/base/BaseCover.vue'
import BaseIcon from '@/components/base/BaseIcon.vue'
import BaseLink from '@/components/base/BaseLink.vue'
import BaseList, { type ListGroup, type ListRowBase } from '@/components/base/BaseList.vue'
import FavoriteButton from '@/components/collection/FavoriteButton.vue'
import RatingStars from '@/components/collection/RatingStars.vue'
import LibraryCover from './LibraryCover.vue'
import MetaItem from './MetaItem.vue'
import ItemMenuButton from '@/components/menus/ItemMenuButton.vue'
import { useFormat } from '@/composables/useFormat'
import type { TrackGroup } from '@/library/grouping'
import {
    artistLabelKey,
    durationSeconds,
    namesTrackArtist,
    trackListState,
    trackRowState,
} from '@/library/presentation'
import type { PlayerSource } from '@/player/resume'
import { useContextMenuStore } from '@/stores/contextMenu'
import { usePlayerStore } from '@/stores/player'

/**
 * Tracks grouped by album (or by disc on the album page), with cover and details of each group,
 * or as a plain list (one group without title from groupTracksAsList, state "ungrouped").
 * Details link to the pages of album, artist, genre and year; the context menu of a row (right
 * click, long press, the button "More options" or the keyboard) applies to all selected tracks
 * (Ctrl and Shift with a click choose several). The columns rating and favorite
 * show the stars and the heart of the profile; a click changes them. A double click, Enter or a tap
 * on a touch screen plays the tracks of the list from the row on; the playing track is marked.
 */
interface TrackRow extends ListRowBase {
    readonly track: Track
    /** The artist differs from the album artist of the group (or the group has none to go by). */
    readonly otherArtist: boolean
}

const props = withDefaults(
    defineProps<{
        groups: readonly TrackGroup[]
        /** Accessible name of the list. */
        label: string
        /** Shows cover and details of each group; off where the page shows them already. */
        showGroupInfo?: boolean
        /** data-state tokens of the list, e.g. "placeholder". */
        state?: string
        /** Where the tracks come from (the album of the page), saved with the queue. */
        source?: PlayerSource
        /** The headings open the overview of the groups (lists with at least two groups). */
        groupJump?: boolean
        /** Whether the overview of the groups is open. */
        groupJumpOpen?: boolean
    }>(),
    {
        showGroupInfo: true,
        state: undefined,
        source: undefined,
        groupJump: false,
        groupJumpOpen: false,
    },
)

const emit = defineEmits<{
    /** The heading of a group was chosen: open the overview of the groups. */
    'group-jump': [position: number | undefined, opener: HTMLElement]
}>()

/** Id of the selected track. */
const selected = defineModel<string | undefined>('selected', { default: undefined })

const { t } = useI18n()
const format = useFormat()
const contextMenu = useContextMenuStore()
/** Ids of the selected tracks. */
const selection = ref<readonly string[]>([])
const player = usePlayerStore()
const playingId = computed(() => player.state.trackId)

const columns = computed(() => [
    { id: 'number', label: t('list.columns.number') },
    { id: 'title', label: t('list.columns.title') },
    { id: 'duration', label: t('list.columns.duration') },
    { id: 'artist', label: t('list.columns.artist') },
    { id: 'rating', label: t('list.columns.rating') },
    { id: 'favorite', label: t('list.columns.favorite') },
])

function groupTitle(group: TrackGroup): string | undefined {
    if (group.untitled) {
        return undefined
    }

    if (group.disc !== undefined) {
        return t('library.disc', { number: format.number(group.disc) })
    }

    return group.title ?? t('library.unknownAlbum')
}

const listGroups = computed<ListGroup<TrackRow>[]>(() =>
    props.groups.map((group) => ({
        id: group.id,
        title: groupTitle(group),
        position: group.position,
        rows: group.tracks.map((track) => ({
            id: track.id ?? '',
            state: [
                ...trackRowState(track),
                ...(track.id && track.id === playingId.value ? ['playing'] : []),
            ],
            track,
            otherArtist: namesTrackArtist(track, group),
        })),
    })),
)

const groupsById = computed(() => new Map(props.groups.map((group) => [group.id, group])))

function infoOf(group: ListGroup<TrackRow>): TrackGroup | undefined {
    return groupsById.value.get(group.id)
}

function artistName(group: TrackGroup): string {
    const key = artistLabelKey(group.artist, group.isCompilation)
    return key ? t(key) : (group.artist?.name ?? '')
}

function trackArtist(track: Track): string {
    const key = artistLabelKey(track.artist)
    return key ? t(key) : (track.artist?.name ?? '')
}

const allTracks = computed(() => props.groups.flatMap((group) => group.tracks))
/** A plain list (the view "List"): its group has no heading, cover or details. */
const ungrouped = computed(
    () => props.groups.length > 0 && props.groups.every((group) => group.untitled),
)
/**
 * "has-video" keeps a column for the video badge before the titles of all rows; "ungrouped" tells
 * a plain list, whose rows take the whole width.
 */
const listState = computed(() => {
    const state = trackListState(allTracks.value, props.state)
    return ungrouped.value ? [state, 'ungrouped'].filter(Boolean).join(' ') : state
})

function openMenu(rowId: string, x: number, y: number): void {
    const chosen = new Set(selection.value.includes(rowId) ? selection.value : [rowId])
    const tracks = allTracks.value.filter((item) => item.id && chosen.has(item.id))
    const track = tracks.length === 1 ? tracks[0] : undefined
    if (tracks.length > 0) {
        contextMenu.open(
            { type: 'song', tracks, playFrom: track ? () => playFrom(track) : undefined },
            x,
            y,
        )
    }
}

/** The button "More options" of a row: its menu, for the row alone. */
function openRowMenu(rowId: string, x: number, y: number): void {
    selected.value = rowId
    selection.value = [rowId]
    openMenu(rowId, x, y)
}

/** Plays the tracks of the list from the track on; video-only tracks are left out. */
function playFrom(track: Track): void {
    if (!track.hasAudio || props.state?.split(' ').includes('placeholder')) {
        return
    }

    player.playTracks(allTracks.value, allTracks.value.indexOf(track), { source: props.source })
}

function activate(rowId: string): void {
    const track = allTracks.value.find((item) => item.id === rowId)
    if (track) {
        playFrom(track)
    }
}
</script>

<template>
    <BaseList
        v-model:selected="selected"
        v-model:selection="selection"
        :columns="columns"
        :groups="listGroups"
        :label="label"
        :state="listState"
        :group-jump="groupJump && !ungrouped"
        :group-jump-open="groupJumpOpen"
        :group-jump-hint="t('list.groupJump')"
        row-menu
        activate-on-tap
        multi-select
        @row-menu="openMenu"
        @row-activate="activate"
        @group-jump="(position, opener) => emit('group-jump', position, opener)"
    >
        <template v-if="showGroupInfo && !ungrouped" #group-info="{ group }">
            <template v-if="infoOf(group)">
                <LibraryCover
                    v-if="infoOf(group)?.cover"
                    :kind="infoOf(group)!.cover!.kind"
                    :id="infoOf(group)!.cover!.id"
                    :available="infoOf(group)!.cover!.available"
                />
                <BaseCover v-else />
                <div data-part="list-group-meta" role="gridcell" :aria-colspan="columns.length">
                    <MetaItem v-if="infoOf(group)!.albumId" field="album" :label="t('list.album')">
                        <BaseLink
                            :to="{ name: 'library-album', params: { id: infoOf(group)!.albumId } }"
                        >
                            {{ group.title }}
                        </BaseLink>
                    </MetaItem>
                    <MetaItem field="artist" :label="t('list.artist')">
                        <BaseLink
                            v-if="infoOf(group)!.artist?.id"
                            :to="{
                                name: 'library-artist',
                                params: { id: infoOf(group)!.artist!.id },
                            }"
                        >
                            {{ artistName(infoOf(group)!) }}
                        </BaseLink>
                        <template v-else>{{ artistName(infoOf(group)!) }}</template>
                    </MetaItem>
                    <MetaItem v-if="infoOf(group)!.genre" field="genre" :label="t('list.genre')">
                        <BaseLink
                            :to="{ name: 'library-genre', params: { genre: infoOf(group)!.genre } }"
                        >
                            {{ infoOf(group)!.genre }}
                        </BaseLink>
                    </MetaItem>
                    <MetaItem v-if="infoOf(group)!.year" field="year" :label="t('list.year')">
                        <BaseLink
                            :to="{
                                name: 'library-year',
                                params: { year: String(infoOf(group)!.year) },
                            }"
                        >
                            {{ format.year(infoOf(group)!.year!) }}
                        </BaseLink>
                    </MetaItem>
                </div>
            </template>
        </template>
        <template #cell="{ row, column }">
            <template v-if="column.id === 'number'">
                {{ row.track.trackNumber ? format.number(row.track.trackNumber) : '' }}
            </template>
            <template v-else-if="column.id === 'title'">
                <span
                    v-if="row.track.hasVideo"
                    data-part="badge"
                    :data-state="trackRowState(row.track).join(' ')"
                    :title="row.track.hasAudio ? t('list.hasVideo') : t('list.videoOnly')"
                >
                    <BaseIcon name="has-video" />
                    <span data-part="label">{{
                        row.track.hasAudio ? t('list.hasVideo') : t('list.videoOnly')
                    }}</span>
                </span>
                <span data-part="label">{{ row.track.title }}</span>
                <span
                    data-part="list-subtitle"
                    :data-state="row.otherArtist ? 'other-artist' : undefined"
                >
                    <span data-part="label">{{ trackArtist(row.track) }}</span>
                </span>
            </template>
            <template v-else-if="column.id === 'duration'">
                {{
                    durationSeconds(row.track.durationMilliseconds) === null
                        ? ''
                        : format.duration(durationSeconds(row.track.durationMilliseconds)!)
                }}
            </template>
            <template v-else-if="column.id === 'artist'">{{ trackArtist(row.track) }}</template>
            <RatingStars
                v-else-if="column.id === 'rating'"
                :track-id="row.track.id ?? ''"
                :rating="row.track.rating"
                :focusable="false"
            />
            <template v-else-if="column.id === 'favorite'">
                <FavoriteButton
                    type="Track"
                    :id="row.track.id ?? ''"
                    :favorite="row.track.isFavorite"
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
