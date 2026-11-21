<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { LyricsLookupSummary } from '@/api/generated/models'
import LyricsLookupResults from '@/components/admin/LyricsLookupResults.vue'
import BaseAlert from '@/components/base/BaseAlert.vue'
import { forgetLyricsLookups, getLyricsLookupSummary } from '@/lyrics/lookupApi'

/**
 * Loads the stored results of the online lyrics lookup that found no lyrics and forgets them on
 * request, so the next lyrics download looks these songs up again at once.
 */
const { t } = useI18n()

const summary = ref<LyricsLookupSummary | null>(null)
const status = ref<'loading' | 'ready' | 'error'>('loading')
const forgetting = ref(false)
const result = ref<'forgotten' | 'error' | null>(null)

async function load(): Promise<void> {
    try {
        summary.value = await getLyricsLookupSummary()
        status.value = 'ready'
    } catch {
        status.value = 'error'
    }
}

async function forget(): Promise<void> {
    if (forgetting.value) {
        return
    }

    forgetting.value = true
    result.value = null
    try {
        await forgetLyricsLookups()
        result.value = 'forgotten'
        await load()
    } catch {
        result.value = 'error'
    } finally {
        forgetting.value = false
    }
}

onMounted(load)
</script>

<template>
    <BaseAlert v-if="status === 'error'" tone="error" :message="t('lyricsLookups.loadError')" />
    <LyricsLookupResults
        v-else
        :summary="summary"
        :forgetting="forgetting"
        :result="result"
        @forget="forget"
    />
</template>
