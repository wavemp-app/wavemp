<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { LyricsLookup } from '@/api/generated/models'
import { problemCodeOf } from '@/api/problems'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import { useFormat } from '@/composables/useFormat'
import { downloadLyrics, getLyrics } from '@/lyrics/api'
import { forgetLyricsLookup, getLyricsLookup } from '@/lyrics/lookupApi'
import {
    downloadFailureOf,
    forgetFailureKey,
    lookupNoteKey,
    lyricsSourceKey,
    lyricsTypeKey,
} from '@/lyrics/presentation'
import type { TrackLyrics } from '@/lyrics/timing'

/**
 * The lyrics of a song for admins: which lyrics it has (how they follow the music and where they
 * come from), a button that downloads them from the online lyrics service, while the download is
 * switched on in the library settings, and, if the last lookup found no lyrics, when that was and a
 * button that forgets the result, so the next lyrics download looks the song up again at once. A
 * lyrics file next to the song is never replaced.
 */
const props = defineProps<{
    trackId: string
}>()

const emit = defineEmits<{
    /** New lyrics were saved for the song. */
    downloaded: [lyrics: TrackLyrics | null]
}>()

const { t } = useI18n()
const format = useFormat()

const lyrics = ref<TrackLyrics | null>(null)
const lookup = ref<LyricsLookup | null>(null)
const status = ref<'loading' | 'ready' | 'error'>('loading')
const downloading = ref(false)
const downloaded = ref(false)
const forgetting = ref(false)
const forgotten = ref(false)
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

/** The note of a stored lookup that found no lyrics, or null. */
const lookupNote = computed(() => {
    const key = lookupNoteKey(lookup.value?.status)
    const checkedAt = lookup.value?.checkedAt
    return key && checkedAt ? t(key, { date: format.date(checkedAt) }) : null
})

const busy = computed(() => downloading.value || forgetting.value)

/** The stored lookup of the song; without it (or if it cannot be read) no note is shown. */
async function loadLookup(): Promise<LyricsLookup | null> {
    try {
        return await getLyricsLookup(props.trackId)
    } catch {
        return null
    }
}

function resetMessages(): void {
    downloaded.value = false
    forgotten.value = false
    failure.value = null
}

async function load(): Promise<void> {
    const current = ++generation
    status.value = 'loading'
    resetMessages()
    try {
        const [loaded, loadedLookup] = await Promise.all([getLyrics(props.trackId), loadLookup()])
        if (current === generation) {
            lyrics.value = loaded
            lookup.value = loadedLookup
            status.value = 'ready'
        }
    } catch {
        if (current === generation) {
            lyrics.value = null
            lookup.value = null
            status.value = 'error'
        }
    }
}

async function download(): Promise<void> {
    if (busy.value) {
        return
    }

    const current = generation
    downloading.value = true
    resetMessages()
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
        // The download stored a new result of the lookup.
        const loadedLookup = await loadLookup()
        if (current === generation) {
            lookup.value = loadedLookup
        }

        downloading.value = false
    }
}

async function forget(): Promise<void> {
    if (busy.value) {
        return
    }

    const current = generation
    forgetting.value = true
    resetMessages()
    try {
        await forgetLyricsLookup(props.trackId)
        if (current === generation) {
            lookup.value = null
            forgotten.value = true
        }
    } catch (error) {
        const key = forgetFailureKey(problemCodeOf(error))
        if (current === generation) {
            if (key) {
                failure.value = { labelKey: key, tone: 'error' }
                lookup.value = await loadLookup()
            } else {
                lookup.value = null
                forgotten.value = true
            }
        }
    } finally {
        forgetting.value = false
    }
}

watch(() => props.trackId, load)

onMounted(load)
</script>

<template>
    <BaseAlert v-if="status === 'error'" tone="error" :message="t('trackLyrics.loadError')" />
    <div v-else data-part="form" :aria-busy="status === 'loading' || busy ? 'true' : undefined">
        <p data-part="form-note" data-item="lyrics-status" :data-state="lyrics ? 'found' : 'none'">
            {{ summary }}
        </p>
        <p
            v-if="lookupNote"
            data-part="form-note"
            data-item="lyrics-lookup"
            :data-state="lookup?.status === 'Instrumental' ? 'instrumental' : 'not-found'"
        >
            {{ lookupNote }}
        </p>
        <p data-part="form-note">{{ t('trackLyrics.hint') }}</p>
        <BaseAlert v-if="downloaded" tone="success" :message="t('trackLyrics.downloaded')" />
        <BaseAlert v-if="forgotten" tone="success" :message="t('trackLyrics.lookup.forgotten')" />
        <BaseAlert v-if="failure" :tone="failure.tone" :message="t(failure.labelKey)" />
        <div data-part="view-actions">
            <BaseButton
                data-item="download-lyrics"
                icon="download-lyrics"
                :label="downloading ? t('trackLyrics.downloading') : t('trackLyrics.download')"
                :disabled="status !== 'ready' || busy"
                @click="download"
            />
            <BaseButton
                v-if="lookupNote"
                data-item="forget-lyrics-lookup"
                icon="forget-lyrics-lookup"
                :label="
                    forgetting ? t('trackLyrics.lookup.forgetting') : t('trackLyrics.lookup.forget')
                "
                :disabled="status !== 'ready' || busy"
                @click="forget"
            />
        </div>
    </div>
</template>
