<script setup lang="ts" generic="T">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { ListGroupPage } from '@/api/generated/models'
import BaseButton from '@/components/base/BaseButton.vue'
import GroupOverview from './GroupOverview.vue'
import LibraryEmpty from './LibraryEmpty.vue'
import LoadMore from './LoadMore.vue'
import LoadPrevious from './LoadPrevious.vue'
import TileGroupList from './TileGroupList.vue'
import TileList, { type Tile } from './TileList.vue'
import type { PageLike } from '@/api/paging'
import { useGroupJump } from '@/composables/useGroupJump'
import { useListGroups } from '@/composables/useListGroups'
import { usePagedList, type PagedListStatus } from '@/composables/usePagedList'
import { tileOverviewGroups } from '@/library/groupOverview'
import { groupTiles, type TileGrouping } from '@/library/tileGrouping'

/**
 * Albums, artists, genres or years as tiles, loaded page by page. The list loads again whenever
 * the reload key changes (e.g. the search text or the sort order). With a grouping (albums by album
 * artist, letter or year, artists by letter) the tiles stand in groups with headings; the groups of
 * the whole list come from fetchGroups when they are needed, and the list can start at any group:
 * a click on a heading, Ctrl+Minus, Escape or pinching the tiles opens their overview, which jumps
 * to any group without loading the list up to it.
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

const tileGroupList = ref<InstanceType<typeof TileGroupList> | null>(null)
const groupsElement = computed(() => {
    const element: unknown = tileGroupList.value?.$el
    return element instanceof HTMLElement ? element : null
})
/**
 * The quick jump needs two groups: in the loaded part, or in the whole list as long as only a
 * part of it is loaded and its groups are not known yet.
 */
const jumpAvailable = computed(() => {
    if (!tileGroups.value || !props.fetchGroups) {
        return false
    }

    if (groups.status.value === 'ready') {
        return groups.groups.value.length >= 2
    }

    return tileGroups.value.length >= 2 || list.hasMore.value || list.hasPrevious.value
})
const jump = useGroupJump({
    list: groupsElement,
    available: jumpAvailable,
    loadGroups: groups.load,
    jumpTo: list.jumpTo,
})
/** Albums or artists; unknown groups miss the album artist (sorted by artist) or the year. */
const unit = computed(() => (tiles.value[0]?.kind === 'artist' ? 'artists' : 'albums'))
const overviewGroups = computed(() =>
    tileOverviewGroups(
        groups.groups.value,
        tileGroups.value?.[0]?.kind === 'year' ? 'year' : 'artist',
    ),
)
const groupsState = computed(() => (jump.open.value ? 'zoomed-out' : undefined))

watch(
    () => props.reloadKey,
    () => {
        groups.invalidate()
        void list.reload()
    },
    { immediate: true },
)

watch(list.status, (status) => emit('status', status), { immediate: true })

defineExpose({ list, groups, jump })
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
        <TileGroupList
            v-if="tileGroups"
            ref="tileGroupList"
            :groups="tileGroups"
            :label="label"
            :state="groupsState"
            :group-jump="jumpAvailable"
            :group-jump-open="jump.open.value"
            @group-jump="(position, opener) => jump.openOverview({ position, opener })"
        />
        <TileList v-else :tiles="tiles" :label="label" />
        <LoadMore
            v-if="list.hasMore.value"
            :loading="list.loadingMore.value"
            @load="list.loadMore()"
        />
        <GroupOverview
            v-if="tileGroups"
            v-model:open="jump.open.value"
            :groups="overviewGroups"
            :status="groups.status.value"
            :current="jump.current.value"
            :unit="unit"
            @choose="(group) => jump.choose(group.position)"
            @retry="groups.load()"
        />
    </template>
</template>
