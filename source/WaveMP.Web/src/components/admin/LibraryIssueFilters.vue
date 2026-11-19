<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { LibraryIssueSort, LibraryIssueSummary } from '@/api/generated/models'
import {
    isLibraryIssueFilter,
    isLibraryIssueSort,
    issueTypeOptions,
    libraryIssueSorts,
    type LibraryIssueFilter,
} from '@/admin/libraryIssues'
import BaseSearchField from '@/components/base/BaseSearchField.vue'
import BaseSegmentedControl from '@/components/base/BaseSegmentedControl.vue'
import { useFormat } from '@/composables/useFormat'

/**
 * Search, filter and sort order of the problems of the library check. The filter is a segmented
 * control with every kind of problem and its number (a badge with the tokens "count", "problem" or
 * "hint" and "empty" without problems); the sort orders are a second one.
 */
const props = defineProps<{
    summary: LibraryIssueSummary | null
}>()

const search = defineModel<string>('search', { default: '' })
const filter = defineModel<LibraryIssueFilter>('filter', { required: true })
const sort = defineModel<LibraryIssueSort>('sort', { required: true })

const { t } = useI18n()
const format = useFormat()

const filterOptions = computed(() =>
    issueTypeOptions(props.summary).map((option) => ({
        value: option.value,
        item: option.item,
        label: t(option.labelKey),
        badge: {
            text: format.number(option.count),
            state: ['count', option.severity, ...(option.count === 0 ? ['empty'] : [])].join(' '),
        },
    })),
)
const sortOptions = computed(() =>
    libraryIssueSorts.map((option) => ({
        value: option.value,
        item: option.item,
        label: t(option.labelKey),
    })),
)

const filterValue = computed({
    get: (): string => filter.value,
    set: (value: string) => {
        if (isLibraryIssueFilter(value)) {
            filter.value = value
        }
    },
})
const sortValue = computed({
    get: (): string => sort.value,
    set: (value: string) => {
        if (isLibraryIssueSort(value)) {
            sort.value = value
        }
    },
})
</script>

<template>
    <div data-part="library-issue-filters">
        <BaseSegmentedControl
            v-model="filterValue"
            item="issue-type"
            :label="t('adminChecks.filterLabel')"
            :options="filterOptions"
        />
        <BaseSearchField
            v-model="search"
            :label="t('adminChecks.search')"
            :placeholder="t('adminChecks.searchPlaceholder')"
            :clear-label="t('toolbar.clearSearch')"
        />
        <BaseSegmentedControl
            v-model="sortValue"
            item="issue-sort"
            :label="t('adminChecks.sortLabel')"
            :options="sortOptions"
        />
    </div>
</template>
