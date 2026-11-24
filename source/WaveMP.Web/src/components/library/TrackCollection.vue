<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { SortDirection, Track, TrackSort } from '@/api/generated/models'
import BaseButton from '@/components/base/BaseButton.vue'
import GroupOverview from './GroupOverview.vue'
import LibraryEmpty from './LibraryEmpty.vue'
import LoadMore from './LoadMore.vue'
import LoadPrevious from './LoadPrevious.vue'
import TrackGroupList from './TrackGroupList.vue'
import { useGroupJump } from '@/composables/useGroupJump'
import { useLayoutOptions } from '@/composables/useLayoutOptions'
import { useListGroups } from '@/composables/useListGroups'
import { usePagedList, type PagedListStatus } from '@/composables/usePagedList'
import { useViewRoute } from '@/composables/useViewRoute'
import { listTrackGroups, listTracks, type TrackQuery } from '@/library/api'
import { groupTracksAsList, groupTracksByAlbum } from '@/library/grouping'
import { trackOverviewGroups } from '@/library/groupOverview'
import { parseListView } from '@/library/listView'

export interface TrackFilter {
    readonly artistId?: string
    readonly albumId?: string
    readonly genre?: string
    readonly year?: number
    /** Only the "my music" of the profile. */
    readonly myMusic?: boolean
    /** Only the songs of this media folder. */
    readonly sourceId?: string
}

/**
 * Tracks of the library grouped by album, loaded page by page: all tracks or those of a filter or a
 * search text, in the requested order (the defaults of the server otherwise). Where the theme offers
 * the view options (layout option --wmp-layout-view-options), the view of the route (?view=list)
 * shows them as a plain list instead; otherwise the query is ignored and the list stays grouped.
 * The list can start at any position (a jump to a group); the groups of the whole list come from
 * the group endpoint when they are needed: a click on a heading, Ctrl+Minus, Escape or pinching the
 * list opens their overview, which jumps to any group without loading the list up to it.
 */
const props = withDefaults(
    defineProps<{
        /** Accessible name of the list. */
        label: string
        filter?: TrackFilter
        search?: string
        sort?: string
        direction?: SortDirection
    }>(),
    { filter: () => ({}), search: '', sort: undefined, direction: undefined },
)

const emit = defineEmits<{
    status: [status: PagedListStatus]
}>()

const { t } = useI18n()

function query(offset: number, limit: number): TrackQuery {
    return {
        offset,
        limit,
        q: props.search || undefined,
        sort: props.sort as TrackSort | undefined,
        direction: props.direction,
        artistId: props.filter.artistId,
        albumId: props.filter.albumId,
        genre: props.filter.genre,
        year: props.filter.year,
        myMusic: props.filter.myMusic,
        sourceId: props.filter.sourceId,
    }
}

const list = usePagedList<Track>((offset, limit) => listTracks(query(offset, limit)))
const trackGroups = useListGroups((offset, limit) => listTrackGroups(query(offset, limit)))

const route = useViewRoute()
const layout = useLayoutOptions()
const view = computed(() =>
    layout.value.viewOptions === 'on' ? parseListView(route.value.query) : 'grouped',
)

// The groups come from all loaded pages, so a group that spans two pages joins up.
const groups = computed(() =>
    view.value === 'list'
        ? groupTracksAsList(list.items.value, list.start.value)
        : groupTracksByAlbum(list.items.value, list.start.value),
)
const filtered = computed(() => Object.values(props.filter).some((value) => value !== undefined))

const trackList = ref<InstanceType<typeof TrackGroupList> | null>(null)
const listElement = computed(() => {
    const element: unknown = trackList.value?.$el
    return element instanceof HTMLElement ? element : null
})
/**
 * The quick jump needs two groups: in the loaded part, or in the whole list as long as only a
 * part of it is loaded and its groups are not known yet.
 */
const jumpAvailable = computed(() => {
    if (view.value !== 'grouped') {
        return false
    }

    if (trackGroups.status.value === 'ready') {
        return trackGroups.groups.value.length >= 2
    }

    return groups.value.length >= 2 || list.hasMore.value || list.hasPrevious.value
})
const jump = useGroupJump({
    list: listElement,
    available: jumpAvailable,
    loadGroups: trackGroups.load,
    jumpTo: list.jumpTo,
})
const overviewGroups = computed(() => trackOverviewGroups(trackGroups.groups.value))
const listState = computed(() => (jump.open.value ? 'zoomed-out' : undefined))

watch(
    () => JSON.stringify([props.filter, props.search, props.sort, props.direction]),
    () => {
        trackGroups.invalidate()
        void list.reload()
    },
    { immediate: true },
)

watch(list.status, (status) => emit('status', status), { immediate: true })

defineExpose({ list, groups: trackGroups, jump })
</script>

<template>
    <div v-if="list.status.value === 'error'" data-part="empty-state">
        <p data-part="empty-state-title">{{ t('library.loadError') }}</p>
        <BaseButton :label="t('library.retry')" @click="list.reload()" />
    </div>
    <LibraryEmpty
        v-else-if="list.status.value === 'ready' && list.items.value.length === 0"
        :search="search"
        :filtered="filtered"
    />
    <template v-else-if="list.items.value.length > 0">
        <LoadPrevious
            v-if="list.hasPrevious.value"
            :load="list.loadPrevious"
            :loading="list.loadingPrevious.value"
            anchor="[data-part='list-row']"
        />
        <TrackGroupList
            ref="trackList"
            :groups="groups"
            :label="label"
            :state="listState"
            :group-jump="jumpAvailable"
            :group-jump-open="jump.open.value"
            @group-jump="(position, opener) => jump.openOverview({ position, opener })"
        />
        <LoadMore
            v-if="list.hasMore.value"
            :loading="list.loadingMore.value"
            @load="list.loadMore()"
        />
        <GroupOverview
            v-model:open="jump.open.value"
            :groups="overviewGroups"
            :status="trackGroups.status.value"
            :current="jump.current.value"
            unit="songs"
            @choose="(group) => jump.choose(group.position)"
            @retry="trackGroups.load()"
        />
    </template>
</template>
