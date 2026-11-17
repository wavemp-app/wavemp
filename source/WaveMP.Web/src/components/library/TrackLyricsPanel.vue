<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { problemCodeOf } from '@/api/problems'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import { downloadLyrics, getLyrics } from '@/lyrics/api'
import { downloadFailureOf, lyricsSourceKey, lyricsTypeKey } from '@/lyrics/presentation'
import type { TrackLyrics } from '@/lyrics/timing'

/**
 * The lyrics of a song for admins: which lyrics it has (how they follow the music and where they
 * come from) and a button that downloads them from the online lyrics service, while the download is
 * switched on in the library settings. A lyrics file next to the song is never replaced.
 */
const props = defineProps<{
    trackId: string
}>()

const emit = defineEmits<{
    /** New lyrics were saved for the song. */
    downloaded: [lyrics: TrackLyrics | null]
}>()

const { t } = useI18n()

const lyrics = ref<TrackLyrics | null>(null)
const status = ref<'loading' | 'ready' | 'error'>('loading')
const downloading = ref(false)
const downloaded = ref(false)
const failure = ref<{ labelKey: string; tone: 'info' | 'error' } | null>(null)
let generation = 0

const summary = computed(() => {
    if (status.value === 'loading') {
        return t('trackLyrics.loading')
    }

    if (!lyrics.value) {
        return t('trackLyrics.none')
    }

    return t('trackLyrics.summary', {
        type: t(lyricsTypeKey(lyrics.value.type)),
        source: t(lyricsSourceKey(lyrics.value.source)),
        count: lyrics.value.lines.length,
    })
})

async function load(): Promise<void> {
    const current = ++generation
    status.value = 'loading'
    downloaded.value = false
    failure.value = null
    try {
        const loaded = await getLyrics(props.trackId)
        if (current === generation) {
            lyrics.value = loaded
            status.value = 'ready'
        }
    } catch {
        if (current === generation) {
            lyrics.value = null
            status.value = 'error'
        }
    }
}

async function download(): Promise<void> {
    if (downloading.value) {
        return
    }

    const current = generation
    downloading.value = true
    downloaded.value = false
    failure.value = null
    try {
        const result = await downloadLyrics(props.trackId)
        if (current === generation) {
            lyrics.value = result
            downloaded.value = true
            emit('downloaded', result)
        }
    } catch (error) {
        if (current === generation) {
            failure.value = downloadFailureOf(problemCodeOf(error))
        }
    } finally {
        downloading.value = false
    }
}

watch(() => props.trackId, load)

onMounted(load)
</script>

<template>
    <BaseAlert v-if="status === 'error'" tone="error" :message="t('trackLyrics.loadError')" />
    <div
        v-else
        data-part="form"
        :aria-busy="status === 'loading' || downloading ? 'true' : undefined"
    >
        <p data-part="form-note" data-item="lyrics-status" :data-state="lyrics ? 'found' : 'none'">
            {{ summary }}
        </p>
        <p data-part="form-note">{{ t('trackLyrics.hint') }}</p>
        <BaseAlert v-if="downloaded" tone="success" :message="t('trackLyrics.downloaded')" />
        <BaseAlert v-if="failure" :tone="failure.tone" :message="t(failure.labelKey)" />
        <div data-part="view-actions">
            <BaseButton
                data-item="download-lyrics"
                icon="download-lyrics"
                :label="downloading ? t('trackLyrics.downloading') : t('trackLyrics.download')"
                :disabled="status !== 'ready' || downloading"
                @click="download"
            />
        </div>
    </div>
</template>
