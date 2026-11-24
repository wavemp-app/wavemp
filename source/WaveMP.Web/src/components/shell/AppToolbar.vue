<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseContextMenu, { type MenuItem } from '@/components/base/BaseContextMenu.vue'
import BaseSearchField from '@/components/base/BaseSearchField.vue'
import AppBreadcrumb from './AppBreadcrumb.vue'
import { useLayoutOptions } from '@/composables/useLayoutOptions'
import { useSearchField } from '@/composables/useSearchField'
import { listViews, parseListView, viewContext, viewItemId, withListView } from '@/library/listView'
import { queryText } from '@/library/search'
import {
    effectiveSort,
    parseSort,
    sortContext,
    sortItemId,
    sortLabelKey,
    sortOptions,
} from '@/library/sorting'
import type { RouteInfo } from '@/navigation/breadcrumb'

const props = defineProps<{
    /** Location of the breadcrumb and the sort options; by default the current route. */
    location?: RouteInfo
}>()

const { t } = useI18n()
const route = useRoute()
const router = useRouter()

// The breadcrumb stands here unless the theme moves it into the header (layout option).
const layout = useLayoutOptions()

// View options of the list of the current page (grouped by album or a plain list), offered only
// where the theme switches them on; the choice is part of the query of the route, like the sort.
const viewMenu = ref<InstanceType<typeof BaseContextMenu> | null>(null)
const viewList = computed(() =>
    layout.value.viewOptions === 'on' ? viewContext(props.location ?? route) : null,
)
const viewItems = computed<MenuItem[]>(() => {
    const view = parseListView(route.query)
    return listViews.map((option) => ({
        id: viewItemId(option),
        label: t(`listView.${option}`),
        icon: viewItemId(option),
        checked: option === view,
    }))
})

function openView(event: MouseEvent): void {
    const rect = (event.currentTarget as HTMLElement).getBoundingClientRect()
    void viewMenu.value?.openAt(rect.left, rect.bottom + 4)
}

function chooseView(id: string): void {
    const view = listViews.find((option) => viewItemId(option) === id)
    if (viewList.value && view) {
        void router.replace({ query: withListView(route.query, view) })
    }
}

// Search: typing opens the search page of the library.
const { text: searchText, submit: submitSearch } = useSearchField()

// Sort options of the list of the current page; the choice is part of the query of the route.
const sortMenu = ref<InstanceType<typeof BaseContextMenu> | null>(null)
const sortList = computed(() => sortContext(props.location ?? route))
const hasSearch = computed(() => queryText(route.query) !== '')
const current = computed(() =>
    sortList.value
        ? effectiveSort(
              sortList.value,
              parseSort(sortList.value, route.query, hasSearch.value),
              hasSearch.value,
          )
        : null,
)
const sortItems = computed<MenuItem[]>(() => {
    const list = sortList.value
    if (!list) {
        return []
    }

    const keys = sortOptions(list, hasSearch.value)
    const directions: MenuItem[] = [
        {
            id: 'direction-asc',
            label: t('sort.ascending'),
            checked: current.value?.direction === 'Asc',
        },
        {
            id: 'direction-desc',
            label: t('sort.descending'),
            checked: current.value?.direction === 'Desc',
        },
    ]
    return keys.length === 0
        ? directions
        : [
              ...keys.map((key) => ({
                  id: sortItemId(key),
                  label: t(sortLabelKey(key)),
                  checked: current.value?.sort === key,
              })),
              { id: 'separator', separator: true },
              ...directions,
          ]
})

function openSort(event: MouseEvent): void {
    const rect = (event.currentTarget as HTMLElement).getBoundingClientRect()
    void sortMenu.value?.openAt(rect.left, rect.bottom + 4)
}

function chooseSort(id: string): void {
    const list = sortList.value
    if (!list) {
        return
    }

    const query = { ...route.query }
    if (id === 'direction-asc' || id === 'direction-desc') {
        query.direction = id === 'direction-asc' ? 'Asc' : 'Desc'
    } else {
        const key = sortOptions(list, hasSearch.value).find((option) => sortItemId(option) === id)
        if (!key) {
            return
        }

        // A new key starts in its own default direction.
        query.sort = key
        delete query.direction
    }

    void router.replace({ query })
}
</script>

<template>
    <div data-part="toolbar">
        <AppBreadcrumb v-if="layout.breadcrumb === 'toolbar'" :location="location" />
        <!-- Disabled unless the theme offers the view options and the page shows a list of songs. -->
        <BaseButton
            part="view-switch"
            variant="icon"
            icon="view-details"
            dropdown
            :label="t('toolbar.view')"
            aria-haspopup="menu"
            :disabled="!viewList"
            @click="openView"
        />
        <BaseContextMenu
            v-if="viewList"
            ref="viewMenu"
            :items="viewItems"
            :label="t('listView.menu')"
            @select="chooseView"
        />
        <BaseButton
            part="sort"
            variant="icon"
            icon="sort"
            dropdown
            :label="t('toolbar.sort')"
            aria-haspopup="menu"
            :disabled="!sortList"
            @click="openSort"
        />
        <BaseContextMenu
            ref="sortMenu"
            :items="sortItems"
            :label="t('sort.menu')"
            @select="chooseSort"
        />
        <BaseSearchField
            v-model="searchText"
            :label="t('toolbar.search')"
            :placeholder="t('toolbar.searchPlaceholder')"
            :clear-label="t('toolbar.clearSearch')"
            @keydown.enter="submitSearch()"
        />
    </div>
</template>
