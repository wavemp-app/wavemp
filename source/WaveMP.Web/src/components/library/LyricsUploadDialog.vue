<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { problemCodeOf, validationErrorsOf } from '@/api/problems'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseCheckbox from '@/components/base/BaseCheckbox.vue'
import BaseDialog from '@/components/base/BaseDialog.vue'
import BaseTextArea from '@/components/base/BaseTextArea.vue'
import { uploadLyrics } from '@/lyrics/api'
import { decodeLyricsFile, hasTimestamps, lyricsFileTypes, maxLyricsLength } from '@/lyrics/files'
import { downloadFailureOf, publicationMessageOf } from '@/lyrics/presentation'
import type { TrackLyrics } from '@/lyrics/timing'

/**
 * Own lyrics of a song for admins: pasted or read from a file (.lrc with times or plain text) and
 * edited in a text field, then saved; they replace the lyrics of the song, also a lyrics file. On
 * request they are also published to the online lyrics service, which needs title, artist, album
 * and duration of the song. A failed publication keeps the saved lyrics.
 */
const props = defineProps<{
    trackId: string
}>()

const emit = defineEmits<{
    /** The lyrics were saved. */
    saved: [lyrics: TrackLyrics | null]
}>()

const open = defineModel<boolean>('open', { default: false })

const { t } = useI18n()

const state = ref<'editing' | 'saving' | 'saved'>('editing')
const text = ref('')
const publish = ref(false)
const fileName = ref<string | null>(null)
const textError = ref<string | undefined>(undefined)
const failure = ref<string | null>(null)
const publication = ref<{ labelKey: string; tone: 'success' | 'error' } | null>(null)
const fileInput = ref<HTMLInputElement | null>(null)
let generation = 0

const saving = computed(() => state.value === 'saving')
const saveLabel = computed(() => {
    if (!saving.value) {
        return t('trackLyrics.upload.save')
    }

    // Publishing solves a challenge of the service first, which can take a few minutes.
    return publish.value ? t('trackLyrics.upload.publishing') : t('trackLyrics.upload.saving')
})
const format = computed(() => {
    if (text.value.trim() === '') {
        return null
    }

    return hasTimestamps(text.value)
        ? t('trackLyrics.upload.synchronized')
        : t('trackLyrics.upload.static')
})

function pickFile(): void {
    if (fileInput.value && !saving.value) {
        fileInput.value.value = ''
        fileInput.value.click()
    }
}

async function onFile(event: Event): Promise<void> {
    const file = (event.target as HTMLInputElement).files?.[0]
    if (!file) {
        return
    }

    failure.value = null
    textError.value = undefined
    try {
        const content = decodeLyricsFile(await file.arrayBuffer())
        if (content.length > maxLyricsLength) {
            textError.value = t('trackLyrics.upload.tooLong')
            return
        }

        text.value = content
        fileName.value = file.name
    } catch {
        failure.value = t('trackLyrics.upload.fileUnreadable')
    }
}

async function save(): Promise<void> {
    if (saving.value) {
        return
    }

    if (text.value.trim() === '') {
        textError.value = t('trackLyrics.upload.empty')
        return
    }

    const current = ++generation
    state.value = 'saving'
    textError.value = undefined
    failure.value = null
    try {
        const result = await uploadLyrics(props.trackId, text.value, publish.value)
        if (current !== generation) {
            return
        }

        publication.value = publicationMessageOf(result.publication)
        state.value = 'saved'
        emit('saved', result.lyrics)
    } catch (error) {
        if (current !== generation) {
            return
        }

        if (validationErrorsOf(error).some((item) => item.field === 'text')) {
            textError.value = t('trackLyrics.upload.empty')
        } else {
            failure.value = t(downloadFailureOf(problemCodeOf(error)).labelKey)
        }
        state.value = 'editing'
    }
}

watch(
    [open, () => props.trackId],
    ([isOpen]) => {
        generation++
        if (isOpen) {
            state.value = 'editing'
            text.value = ''
            publish.value = false
            fileName.value = null
            textError.value = undefined
            failure.value = null
            publication.value = null
        }
    },
    { immediate: true },
)
</script>

<template>
    <BaseDialog
        v-model:open="open"
        data-item="lyrics-upload"
        :title="t('trackLyrics.upload.title')"
    >
        <template v-if="state === 'saved'">
            <BaseAlert tone="success" :message="t('trackLyrics.upload.saved')" />
            <BaseAlert
                v-if="publication"
                data-item="lyrics-publication"
                :tone="publication.tone"
                :message="t(publication.labelKey)"
            />
        </template>
        <form
            v-else
            data-part="form"
            data-item="lyrics-upload"
            novalidate
            :aria-busy="saving ? 'true' : undefined"
            @submit.prevent="save"
        >
            <p data-part="form-note">{{ t('trackLyrics.upload.hint') }}</p>
            <BaseAlert v-if="failure" tone="error" :message="failure" />
            <div data-part="view-actions">
                <BaseButton
                    data-item="choose-lyrics-file"
                    icon="upload"
                    :label="t('trackLyrics.upload.chooseFile')"
                    :disabled="saving"
                    @click="pickFile"
                />
                <input
                    ref="fileInput"
                    type="file"
                    data-field="lyrics-file"
                    :accept="lyricsFileTypes"
                    hidden
                    @change="onFile"
                />
            </div>
            <p v-if="fileName" data-part="form-note" data-item="lyrics-file">
                {{ t('trackLyrics.upload.fileChosen', { name: fileName }) }}
            </p>
            <BaseTextArea
                v-model="text"
                data-field="lyrics-text"
                :label="t('trackLyrics.upload.textLabel')"
                :placeholder="t('trackLyrics.upload.placeholder')"
                :hint="format ?? undefined"
                :error="textError"
                :maxlength="maxLyricsLength"
                :rows="12"
                :disabled="saving"
            />
            <BaseCheckbox
                v-model="publish"
                data-field="publish-lyrics"
                :label="t('trackLyrics.upload.publish')"
                :hint="t('trackLyrics.upload.publishHint')"
                :disabled="saving"
            />
        </form>
        <template #footer>
            <BaseButton
                v-if="state === 'saved'"
                variant="primary"
                :label="t('dialog.close')"
                @click="open = false"
            />
            <template v-else>
                <BaseButton :label="t('actions.cancel')" @click="open = false" />
                <BaseButton
                    data-item="save-lyrics"
                    variant="primary"
                    :label="saveLabel"
                    :disabled="saving"
                    @click="save"
                />
            </template>
        </template>
    </BaseDialog>
</template>
