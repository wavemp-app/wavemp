<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { FileSelectionFilter, FileSelectionSort } from '@/api/generated/models'
import {
    fileSelectionFilters,
    fileSelectionSorts,
    isFileSelectionFilter,
    isFileSelectionSort,
} from '@/admin/fileSelections'
import BaseSearchField from '@/components/base/BaseSearchField.vue'
import BaseSegmentedControl from '@/components/base/BaseSegmentedControl.vue'

/**
 * Search, filter and sort order of the file selection: the filters (all songs, several files,
 * several audio files, ...) and the sort orders are segmented controls.
 */
const search = defineModel<string>('search', { default: '' })
const filter = defineModel<FileSelectionFilter>('filter', { required: true })
const sort = defineModel<FileSelectionSort>('sort', { required: true })

const { t } = useI18n()

const filterOptions = computed(() =>
    fileSelectionFilters.map((option) => ({
        value: option.value,
        item: option.item,
        label: t(option.labelKey),
    })),
)
const sortOptions = computed(() =>
    fileSelectionSorts.map((option) => ({
        value: option.value,
        item: option.item,
        label: t(option.labelKey),
    })),
)

const filterValue = computed({
    get: (): string => filter.value,
    set: (value: string) => {
        if (isFileSelectionFilter(value)) {
            filter.value = value
        }
    },
})
const sortValue = computed({
    get: (): string => sort.value,
    set: (value: string) => {
        if (isFileSelectionSort(value)) {
            sort.value = value
        }
    },
})
</script>

<template>
    <div data-part="file-selection-filters">
        <BaseSearchField
            v-model="search"
            :label="t('adminFiles.search')"
            :placeholder="t('adminFiles.searchPlaceholder')"
            :clear-label="t('toolbar.clearSearch')"
        />
        <BaseSegmentedControl
            v-model="filterValue"
            item="file-filter"
            :label="t('adminFiles.filterLabel')"
            :options="filterOptions"
        />
        <BaseSegmentedControl
            v-model="sortValue"
            item="file-sort"
            :label="t('adminFiles.sortLabel')"
            :options="sortOptions"
        />
    </div>
</template>
