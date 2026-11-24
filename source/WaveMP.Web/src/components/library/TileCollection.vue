<script setup lang="ts" generic="T">
import { computed, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { ListGroupPage } from '@/api/generated/models'
import BaseButton from '@/components/base/BaseButton.vue'
import LibraryEmpty from './LibraryEmpty.vue'
import LoadMore from './LoadMore.vue'
import LoadPrevious from './LoadPrevious.vue'
import TileGroupList from './TileGroupList.vue'
import TileList, { type Tile } from './TileList.vue'
import type { PageLike } from '@/api/paging'
import { useListGroups } from '@/composables/useListGroups'
import { usePagedList, type PagedListStatus } from '@/composables/usePagedList'
import { groupTiles, type TileGrouping } from '@/library/tileGrouping'

/**
 * Albums, artists, genres or years as tiles, loaded page by page. The list loads again whenever
 * the reload key changes (e.g. the search text or the sort order). With a grouping (albums by album
 * artist, letter or year, artists by letter) the tiles stand in groups with headings; the groups of
 * the whole list come from fetchGroups when they are needed, and the list can start at any group.
 */
const props = withDefaults(
    defineProps<{
        /** Accessible name of the list. */
        label: string
        fetchPage: (offset: number, limit: number) => Promise<PageLike<T> | undefined>
        toTile: (item: T) => Tile
        /** Any value that changes when the list must load again. */
        reloadKey?: string
        search?: string
        filtered?: boolean
        /** The group of each entry in the current sort order; none shows the tiles ungrouped. */
        grouping?: TileGrouping<T>
        /** The groups of the whole list with the same query (the group endpoint of the list). */
        fetchGroups?: (offset: number, limit: number) => Promise<ListGroupPage | undefined>
    }>(),
    {
        reloadKey: '',
        search: '',
        filtered: false,
        grouping: undefined,
        fetchGroups: undefined,
    },
)

const emit = defineEmits<{
    status: [status: PagedListStatus]
}>()

const { t } = useI18n()
const list = usePagedList<T>((offset, limit) => props.fetchPage(offset, limit))
const groups = useListGroups((offset, limit) =>
    props.fetchGroups ? props.fetchGroups(offset, limit) : Promise.resolve(undefined),
)
const tiles = computed(() => list.items.value.map((item) => props.toTile(item)))
const tileGroups = computed(() => {
    const grouping = props.grouping
    if (!grouping) {
        return undefined
    }

    // The tiles are made once per entry; the groups take the tile with its entry.
    const entries = list.items.value.map((item, index) => ({ item, tile: tiles.value[index] }))
    return groupTiles(entries, (entry) => grouping(entry.item), list.start.value).map((group) => ({
        ...group,
        items: group.items.flatMap((entry) => (entry.tile ? [entry.tile] : [])),
    }))
})

watch(
    () => props.reloadKey,
    () => {
        groups.invalidate()
        void list.reload()
    },
    { immediate: true },
)

watch(list.status, (status) => emit('status', status), { immediate: true })

defineExpose({ list, groups })
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
    <template v-else-if="tiles.length > 0">
        <LoadPrevious
            v-if="list.hasPrevious.value"
            :load="list.loadPrevious"
            :loading="list.loadingPrevious.value"
            anchor="[data-part='tile-item']"
        />
        <TileGroupList v-if="tileGroups" :groups="tileGroups" :label="label" />
        <TileList v-else :tiles="tiles" :label="label" />
        <LoadMore
            v-if="list.hasMore.value"
            :loading="list.loadingMore.value"
            @load="list.loadMore()"
        />
    </template>
</template>
