<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import BaseButton from '@/components/base/BaseButton.vue'
import { useFormat } from '@/composables/useFormat'

/**
 * Way through the list of the review inbox: the previous and the next song and the position of the
 * current song in the list (missing while it is not in the list).
 */
const props = defineProps<{
    /** Position of the song in the list, from 0; negative if it is not in the list. */
    index: number
    /** Number of songs in the list. */
    count: number
    hasPrevious: boolean
    hasNext: boolean
    busy?: boolean
}>()

const emit = defineEmits<{
    previous: []
    next: []
}>()

const { t } = useI18n()
const format = useFormat()

const position = computed(() =>
    props.index < 0
        ? null
        : t('reviewInbox.position', {
              position: format.number(props.index + 1),
              count: format.number(props.count),
          }),
)
</script>

<template>
    <nav data-part="review-nav" :aria-label="t('reviewInbox.navLabel')">
        <BaseButton
            data-item="previous"
            icon="review-previous"
            :label="t('reviewInbox.previous')"
            :disabled="busy || !hasPrevious"
            @click="emit('previous')"
        />
        <span v-if="position" data-part="label" data-field="position">{{ position }}</span>
        <BaseButton
            data-item="next"
            icon="review-next"
            :label="t('reviewInbox.next')"
            :disabled="busy || !hasNext"
            @click="emit('next')"
        />
    </nav>
</template>
