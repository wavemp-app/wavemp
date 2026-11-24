<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import {
    OrganizeItemFilterObject,
    type OrganizeItem,
    type OrganizeItemFilter,
    type OrganizeOperation,
} from '@/api/generated/models'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseCheckbox from '@/components/base/BaseCheckbox.vue'
import BaseSegmentedControl from '@/components/base/BaseSegmentedControl.vue'
import { useFormat } from '@/composables/useFormat'
import OrganizeItemList from './OrganizeItemList.vue'

/**
 * The content of the preview dialog (organize-panel, state preview or changes): the numbers of
 * songs, conflicts and warnings (organize-summary, state clean or conflicts, plus warnings), the
 * filter All, Conflicts, Warnings as a segmented control with counts, the manifest of the items
 * with "Show more", and in a preview with conflicts the checkbox "Skip songs with conflicts". The
 * dialog loads the items and applies the preview.
 */
const props = withDefaults(
    defineProps<{
        operation: OrganizeOperation
        items: readonly OrganizeItem[]
        /** Number of items of the current filter. */
        total: number
        mode?: 'preview' | 'changes'
        loading?: boolean
        loadFailed?: boolean
        disabled?: boolean
    }>(),
    { mode: 'preview', loading: false, loadFailed: false, disabled: false },
)

const emit = defineEmits<{
    more: []
}>()

const filter = defineModel<OrganizeItemFilter>('filter', {
    default: OrganizeItemFilterObject.All,
})
const skipConflicts = defineModel<boolean>('skipConflicts', { default: false })

const { t } = useI18n()
const format = useFormat()

const conflicts = computed(() => props.operation.conflictItems ?? 0)
const warnings = computed(() => props.operation.warningItems ?? 0)
const songs = computed(() => props.operation.totalItems ?? 0)
const isPreview = computed(() => props.mode === 'preview')

const filterOptions = computed(() => [
    {
        value: OrganizeItemFilterObject.All,
        item: 'all',
        label: t('organize.preview.filters.all'),
        badge: { text: format.number(songs.value), state: 'count all' },
    },
    {
        value: OrganizeItemFilterObject.Conflicts,
        item: 'conflicts',
        label: t('organize.preview.filters.conflicts'),
        badge: {
            text: format.number(conflicts.value),
            state: `count conflict${conflicts.value === 0 ? ' empty' : ''}`,
        },
    },
    {
        value: OrganizeItemFilterObject.Warnings,
        item: 'warnings',
        label: t('organize.preview.filters.warnings'),
        badge: {
            text: format.number(warnings.value),
            state: `count warning${warnings.value === 0 ? ' empty' : ''}`,
        },
    },
])

const summaryState = computed(() =>
    [conflicts.value > 0 ? 'conflicts' : 'clean', ...(warnings.value > 0 ? ['warnings'] : [])].join(
        ' ',
    ),
)
</script>

<template>
    <div data-part="organize-panel" :data-state="isPreview ? 'preview' : 'changes'">
        <p data-part="organize-summary" :data-state="summaryState">
            <span data-part="label" data-field="songs">
                {{ t('organize.preview.songs', { count: format.number(songs) }) }}
            </span>
            <span data-part="label" data-field="conflicts">
                {{ t('organize.preview.conflicts', { count: format.number(conflicts) }) }}
            </span>
            <span data-part="label" data-field="warnings">
                {{ t('organize.preview.warnings', { count: format.number(warnings) }) }}
            </span>
        </p>
        <p v-if="isPreview" data-part="form-note" data-item="organize-explain">
            {{ t('organize.preview.explain') }}
        </p>
        <BaseSegmentedControl
            v-model="filter"
            item="organize-filter"
            :options="filterOptions"
            :label="t('organize.preview.filterLabel')"
        />
        <BaseAlert v-if="loadFailed" tone="error" :message="t('organize.preview.loadError')" />
        <OrganizeItemList
            v-else-if="items.length > 0"
            :items="items"
            :label="t('organize.preview.listLabel')"
            :show-status="!isPreview"
            :aria-busy="loading ? 'true' : undefined"
        />
        <div v-else-if="!loading" data-part="empty-state">
            <p data-part="empty-state-text">
                {{
                    filter === OrganizeItemFilterObject.All
                        ? t('organize.preview.empty')
                        : t('organize.preview.emptyFiltered')
                }}
            </p>
        </div>
        <div v-if="items.length < total" data-part="view-actions">
            <BaseButton
                data-item="more"
                :label="t('organize.preview.more')"
                :disabled="loading"
                @click="emit('more')"
            />
        </div>
        <BaseCheckbox
            v-if="isPreview && conflicts > 0"
            v-model="skipConflicts"
            data-field="skip-conflicts"
            :label="t('organize.preview.skipConflicts')"
            :hint="t('organize.preview.skipConflictsHint')"
            :disabled="disabled"
        />
    </div>
</template>
