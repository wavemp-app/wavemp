<script setup lang="ts">
import { computed, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { SortDirection, Track, TrackSort } from '@/api/generated/models'
import BaseButton from '@/components/base/BaseButton.vue'
import LibraryEmpty from './LibraryEmpty.vue'
import LoadMore from './LoadMore.vue'
import TrackGroupList from './TrackGroupList.vue'
import { useLayoutOptions } from '@/composables/useLayoutOptions'
import { usePagedList, type PagedListStatus } from '@/composables/usePagedList'
import { useViewRoute } from '@/composables/useViewRoute'
import { listTracks } from '@/library/api'
import { groupTracksAsList, groupTracksByAlbum } from '@/library/grouping'
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

const list = usePagedList<Track>((offset, limit) =>
    listTracks({
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
    }),
)

const route = useViewRoute()
const layout = useLayoutOptions()
const view = computed(() =>
    layout.value.viewOptions === 'on' ? parseListView(route.value.query) : 'grouped',
)

// The groups come from all loaded pages, so a group that spans two pages joins up.
const groups = computed(() =>
    view.value === 'list'
        ? groupTracksAsList(list.items.value)
        : groupTracksByAlbum(list.items.value),
)
const filtered = computed(() => Object.values(props.filter).some((value) => value !== undefined))

watch(
    () => JSON.stringify([props.filter, props.search, props.sort, props.direction]),
    () => void list.reload(),
    { immediate: true },
)

watch(list.status, (status) => emit('status', status), { immediate: true })
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
        <TrackGroupList :groups="groups" :label="label" />
        <LoadMore
            v-if="list.hasMore.value"
            :loading="list.loadingMore.value"
            @load="list.loadMore()"
        />
    </template>
</template>
