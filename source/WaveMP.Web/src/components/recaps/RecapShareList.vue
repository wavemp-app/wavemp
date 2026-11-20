<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterLink } from 'vue-router'
import type { RecapShare } from '@/api/generated/models'
import BaseButton from '@/components/base/BaseButton.vue'
import { useFormat } from '@/composables/useFormat'
import { periodLabelKey } from '@/recaps/labels'
import { shareDays } from '@/recaps/shares'
import type { RecapTexts } from '@/recaps/slides'

/**
 * Shared recaps: received ones (state received) name their profile and open the recap; own ones
 * (state own) name the period and can be stopped sharing.
 */
const props = withDefaults(
    defineProps<{
        shares: readonly RecapShare[]
        own?: boolean
        /** Id of the share that is being stopped. */
        stopping?: string | null
    }>(),
    { own: false, stopping: null },
)

const emit = defineEmits<{ stop: [share: RecapShare] }>()

const { t, locale } = useI18n()
const format = useFormat()

const texts = computed<RecapTexts>(() => ({
    t: (key, values) => t(key, values ?? {}),
    locale: locale.value,
}))

function title(share: RecapShare): string {
    const period = t(periodLabelKey(share.period))
    return props.own ? period : t('recap.sharedTitle', { name: share.owner?.name ?? '', period })
}

function updated(share: RecapShare): string {
    return share.updatedAt
        ? t('recap.sharedAt', { date: format.date(share.updatedAt, { dateStyle: 'medium' }) })
        : ''
}
</script>

<template>
    <ul data-part="recap-share-list">
        <li
            v-for="share in shares"
            :key="share.id ?? ''"
            data-part="recap-share-item"
            :data-item="share.id"
            :data-state="own ? 'own' : 'received'"
        >
            <span data-part="recap-share-info">
                <RouterLink
                    data-part="recap-share-title"
                    :to="{ name: 'shared-recap', params: { id: share.id } }"
                >
                    {{ title(share) }}
                </RouterLink>
                <span data-part="recap-share-meta">
                    <span data-part="label">{{ shareDays(texts, share) }}</span>
                    <span data-part="label">{{ updated(share) }}</span>
                </span>
            </span>
            <span v-if="own" data-part="recap-share-actions">
                <BaseButton
                    data-item="stop-sharing"
                    icon="delete"
                    :label="t('recap.stopSharing')"
                    :disabled="stopping === share.id"
                    @click="emit('stop', share)"
                />
            </span>
        </li>
    </ul>
</template>
