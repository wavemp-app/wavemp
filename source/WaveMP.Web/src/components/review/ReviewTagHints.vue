<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { MetadataTag } from '@/api/generated/models'
import BaseIcon from '@/components/base/BaseIcon.vue'
import { missingTagHints } from '@/review/inbox'

/**
 * Hint which tag fields the file of a song lacks: an icon, a label and one badge per field
 * (data-field as in the metadata editor). Nothing while the tags are complete.
 */
const props = defineProps<{
    tags: readonly MetadataTag[] | null | undefined
}>()

const { t } = useI18n()

const hints = computed(() => missingTagHints(props.tags))
</script>

<template>
    <span v-if="hints.length > 0" data-part="review-tags">
        <BaseIcon name="tags-missing" />
        <span data-part="label">{{ t('reviewInbox.missingTags') }}</span>
        <span
            v-for="hint in hints"
            :key="hint.tag"
            data-part="badge"
            data-state="missing-tag"
            :data-field="hint.field"
        >
            <span data-part="label">{{ t(hint.labelKey) }}</span>
        </span>
    </span>
</template>
