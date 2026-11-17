<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import BaseButton from '@/components/base/BaseButton.vue'

/**
 * Actions on a song of the review inbox: "Save and mark as reviewed" submits the metadata editor
 * (the form formId), "Mark as reviewed" marks it without changes and "Skip" leaves it unreviewed.
 */
defineProps<{
    /** id of the form of the metadata editor. */
    formId: string
    /** Whether the editor can save, i.e. the file is writable and nothing is saved right now. */
    canSave: boolean
    busy?: boolean
}>()

const emit = defineEmits<{
    markReviewed: []
    skip: []
}>()

const { t } = useI18n()
</script>

<template>
    <div data-part="review-actions" role="group" :aria-label="t('reviewInbox.actionsLabel')">
        <BaseButton
            type="submit"
            variant="primary"
            data-item="save-and-review"
            icon="review-done"
            :form="formId"
            :label="t('reviewInbox.saveAndReview')"
            :disabled="!canSave || busy"
        />
        <BaseButton
            data-item="mark-reviewed"
            icon="review-done"
            :label="t('reviewInbox.markReviewed')"
            :disabled="busy"
            @click="emit('markReviewed')"
        />
        <BaseButton
            data-item="skip"
            icon="review-skip"
            :label="t('reviewInbox.skip')"
            :disabled="busy"
            @click="emit('skip')"
        />
    </div>
</template>
