<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { useRouter, type RouteLocationRaw } from 'vue-router'
import BaseButton, { type ButtonVariant } from '@/components/base/BaseButton.vue'
import { goBack } from '@/navigation/back'

/**
 * Back button of a detail page and of the metadata editor: back to the previous page at its
 * position, like the back button of the browser. Without a previous page (opened directly) it
 * opens the overview the page belongs to (fallback), or emits close if there is none.
 */
const props = withDefaults(
    defineProps<{
        fallback?: RouteLocationRaw
        /** Whether the previous page of the history may be used. */
        history?: boolean
        variant?: ButtonVariant
    }>(),
    { fallback: undefined, history: true, variant: 'default' },
)

const emit = defineEmits<{
    close: []
}>()

const { t } = useI18n()
const router = useRouter()

function back(): void {
    if (!goBack(router, { fallback: props.fallback, history: props.history })) {
        emit('close')
    }
}
</script>

<template>
    <BaseButton
        part="btn-detail-back"
        :variant="variant"
        icon="detail-back"
        :label="t('library.back')"
        @click="back"
    />
</template>
