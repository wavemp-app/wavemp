<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { TrackReviewSort } from '@/api/generated/models'
import BaseSearchField from '@/components/base/BaseSearchField.vue'
import BaseSegmentedControl from '@/components/base/BaseSegmentedControl.vue'
import {
    isMissingTagsFilter,
    isReviewSort,
    missingTagsFilters,
    reviewSorts,
    type MissingTagsFilter,
} from '@/review/inbox'

/**
 * Search, filter and sort order of the review inbox: the filter by tags (all songs, songs with
 * missing tags, songs with complete tags) and the sort orders are segmented controls.
 */
const search = defineModel<string>('search', { default: '' })
const filter = defineModel<MissingTagsFilter>('filter', { required: true })
const sort = defineModel<TrackReviewSort>('sort', { required: true })

const { t } = useI18n()

const filterOptions = computed(() =>
    missingTagsFilters.map((option) => ({
        value: option.value,
        item: option.item,
        label: t(option.labelKey),
    })),
)
const sortOptions = computed(() =>
    reviewSorts.map((option) => ({
        value: option.value,
        item: option.item,
        label: t(option.labelKey),
    })),
)

const filterValue = computed({
    get: (): string => filter.value,
    set: (value: string) => {
        if (isMissingTagsFilter(value)) {
            filter.value = value
        }
    },
})
const sortValue = computed({
    get: (): string => sort.value,
    set: (value: string) => {
        if (isReviewSort(value)) {
            sort.value = value
        }
    },
})
</script>

<template>
    <div data-part="review-filters">
        <BaseSearchField
            v-model="search"
            :label="t('reviewInbox.search')"
            :placeholder="t('reviewInbox.searchPlaceholder')"
            :clear-label="t('toolbar.clearSearch')"
        />
        <BaseSegmentedControl
            v-model="filterValue"
            item="review-filter"
            :label="t('reviewInbox.filterLabel')"
            :options="filterOptions"
        />
        <BaseSegmentedControl
            v-model="sortValue"
            item="review-sort"
            :label="t('reviewInbox.sortLabel')"
            :options="sortOptions"
        />
    </div>
</template>
