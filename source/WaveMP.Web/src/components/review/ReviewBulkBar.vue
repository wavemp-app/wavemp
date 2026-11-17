<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseCheckbox from '@/components/base/BaseCheckbox.vue'
import { useFormat } from '@/composables/useFormat'

/**
 * Bulk action of the review inbox: a checkbox that selects all loaded songs that are not reviewed
 * yet, the number of selected songs and the button that marks them as reviewed.
 */
const props = defineProps<{
    /** Number of selected songs. */
    selectedCount: number
    /** Number of loaded songs that can be selected. */
    selectableCount: number
    busy?: boolean
}>()

const emit = defineEmits<{
    selectAll: [selected: boolean]
    markReviewed: []
}>()

const { t } = useI18n()
const format = useFormat()

const allSelected = computed({
    get: () => props.selectableCount > 0 && props.selectedCount >= props.selectableCount,
    set: (value: boolean) => emit('selectAll', value),
})
</script>

<template>
    <div
        data-part="review-bulk"
        role="group"
        :aria-label="t('reviewInbox.bulkLabel')"
        :data-state="selectedCount > 0 ? 'selected' : 'empty'"
    >
        <BaseCheckbox
            v-model="allSelected"
            :label="t('reviewInbox.selectAll')"
            :disabled="selectableCount === 0 || busy"
        />
        <span data-part="label" data-field="selected-count" aria-live="polite">
            {{ t('reviewInbox.selectedCount', { count: format.number(selectedCount) }) }}
        </span>
        <BaseButton
            data-item="mark-selected-reviewed"
            icon="review-done"
            :label="t('reviewInbox.markSelected')"
            :disabled="selectedCount === 0 || busy"
            @click="emit('markReviewed')"
        />
    </div>
</template>
