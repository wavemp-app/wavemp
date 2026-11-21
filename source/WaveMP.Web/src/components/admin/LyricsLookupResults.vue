<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { LyricsLookupSummary } from '@/api/generated/models'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import { useFormat } from '@/composables/useFormat'
import { lookUpAgainWait } from '@/lyrics/presentation'

/**
 * The stored results of the online lyrics lookup that found no lyrics: how many songs LRCLIB had
 * no lyrics for or lists as instrumental, how long the lyrics download waits before it looks them
 * up again, and the button that forgets these results. Null while the numbers load.
 */
const props = withDefaults(
    defineProps<{
        summary: LyricsLookupSummary | null
        /** Whether the results are being forgotten. */
        forgetting?: boolean
        /** What the last attempt to forget the results did. */
        result?: 'forgotten' | 'error' | null
    }>(),
    { forgetting: false, result: null },
)

const emit = defineEmits<{
    forget: []
}>()

const { t } = useI18n()
const format = useFormat()

const notFound = computed(() => props.summary?.notFoundCount ?? 0)
const instrumental = computed(() => props.summary?.instrumentalCount ?? 0)
const total = computed(() => notFound.value + instrumental.value)

const state = computed(() => {
    if (!props.summary) {
        return 'loading'
    }

    return total.value > 0 ? 'has-results' : 'empty'
})

const waitText = computed(() => {
    const wait = lookUpAgainWait(props.summary?.lookUpAgainAfterMinutes)
    return wait
        ? format.number(wait.value, { style: 'unit', unit: wait.unit, unitDisplay: 'long' })
        : t('lyricsLookups.stats.never')
})

const statistics = computed(() => [
    {
        id: 'not-found',
        labelKey: 'lyricsLookups.stats.notFound',
        text: format.number(notFound.value),
    },
    {
        id: 'instrumental',
        labelKey: 'lyricsLookups.stats.instrumental',
        text: format.number(instrumental.value),
    },
    { id: 'wait', labelKey: 'lyricsLookups.stats.wait', text: waitText.value },
])
</script>

<template>
    <div
        data-part="lyrics-lookups"
        :data-state="state"
        :aria-busy="state === 'loading' || forgetting ? 'true' : undefined"
    >
        <span v-if="state === 'loading'" data-part="label">{{ t('lyricsLookups.loading') }}</span>
        <dl v-else data-part="stat-list" :aria-label="t('lyricsLookups.stats.label')">
            <div
                v-for="item in statistics"
                :key="item.id"
                data-part="stat-item"
                :data-item="item.id"
            >
                <dt data-part="label">{{ t(item.labelKey) }}</dt>
                <dd data-part="stat-value">{{ item.text }}</dd>
            </div>
        </dl>
        <p data-part="form-note">{{ t('lyricsLookups.hint') }}</p>
        <BaseAlert
            v-if="result === 'forgotten'"
            tone="success"
            :message="t('lyricsLookups.forgotten')"
        />
        <BaseAlert
            v-else-if="result === 'error'"
            tone="error"
            :message="t('lyricsLookups.forgetError')"
        />
        <div data-part="view-actions">
            <BaseButton
                data-item="forget-lyrics-lookups"
                icon="forget-lyrics-lookup"
                :label="forgetting ? t('lyricsLookups.forgetting') : t('lyricsLookups.forget')"
                :disabled="state !== 'has-results' || forgetting"
                @click="emit('forget')"
            />
        </div>
    </div>
</template>
