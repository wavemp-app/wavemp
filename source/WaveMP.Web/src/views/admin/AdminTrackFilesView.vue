<script setup lang="ts">
import { computed, ref, useId, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import {
    FileSelectionStateObject,
    type MediaFile,
    type Track,
    type TrackFileSelection,
} from '@/api/generated/models'
import { fetchAllPages } from '@/api/paging'
import { statusOf, validationErrorsOf } from '@/api/problems'
import {
    getTrackFileSelection,
    resetTrackFileSelection,
    setTrackFileSelection,
} from '@/admin/fileSelectionApi'
import {
    categoryOfField,
    changedSelection,
    fileCategories,
    fileChoices,
    hasSelection,
    type FileCategory,
} from '@/admin/fileSelections'
import AdminNav from '@/components/admin/AdminNav.vue'
import FileChoiceGroup from '@/components/admin/FileChoiceGroup.vue'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseLink from '@/components/base/BaseLink.vue'
import DetailHeader from '@/components/library/DetailHeader.vue'
import MetaItem from '@/components/library/MetaItem.vue'
import { useDetailTitle } from '@/composables/useDetailTitle'
import { getTrack, listTrackFiles } from '@/library/api'

/**
 * Admin page of one song: per category (audio, video, cover) every file the song can use with its
 * format and quality, and the automatic choice by quality. Clicking a file selects it for playback
 * and display; a selected file that is gone is shown until it is back or the selection changes.
 */
const props = defineProps<{
    id: string
}>()

const { t } = useI18n()
const titleIds: Record<FileCategory, string> = {
    audio: useId(),
    video: useId(),
    cover: useId(),
}

const track = ref<Track | null>(null)
const selection = ref<TrackFileSelection | null>(null)
const files = ref<MediaFile[]>([])
const status = ref<'loading' | 'ready' | 'notFound' | 'error'>('loading')
const saving = ref(false)
const message = ref<{ text: string; tone: 'error' | 'success' } | null>(null)
let generation = 0

async function loadFiles(): Promise<MediaFile[]> {
    return fetchAllPages((offset, limit) => listTrackFiles(props.id, { offset, limit }))
}

async function load(): Promise<void> {
    const current = ++generation
    status.value = 'loading'
    message.value = null
    try {
        const [loadedTrack, loadedSelection, loadedFiles] = await Promise.all([
            getTrack(props.id),
            getTrackFileSelection(props.id),
            loadFiles(),
        ])
        if (current !== generation) {
            return
        }

        track.value = loadedTrack ?? null
        selection.value = loadedSelection ?? null
        files.value = loadedFiles
        status.value = loadedTrack && loadedSelection ? 'ready' : 'notFound'
    } catch (error) {
        if (current === generation) {
            status.value = statusOf(error) === 404 ? 'notFound' : 'error'
        }
    }
}

watch(() => props.id, load, { immediate: true })

useDetailTitle(() => (status.value === 'ready' ? track.value?.title : null))

const choices = computed(() =>
    fileCategories.map((category) => ({
        category,
        selection: selection.value?.[category] ?? null,
        choices: fileChoices(selection.value?.[category], files.value),
    })),
)
const anySelection = computed(() => hasSelection(selection.value))

/** Applies a changed selection and loads the files again, whose use (primary) changed with it. */
async function apply(change: () => Promise<TrackFileSelection | undefined>): Promise<void> {
    if (saving.value) {
        return
    }

    const current = generation
    saving.value = true
    message.value = null
    try {
        const updated = await change()
        const loadedFiles = await loadFiles()
        if (current !== generation) {
            return
        }

        if (updated) {
            selection.value = updated
        }

        files.value = loadedFiles
        message.value = { text: t('adminFiles.saved'), tone: 'success' }
    } catch (error) {
        if (current !== generation) {
            return
        }

        if (statusOf(error) === 404) {
            status.value = 'notFound'
        } else if (validationErrorsOf(error).some((field) => categoryOfField(field.field))) {
            // A file disappeared since the page was loaded: show the current files.
            await load()
            message.value = { text: t('adminFiles.errors.invalid'), tone: 'error' }
        } else {
            message.value = { text: t('adminFiles.errors.saveFailed'), tone: 'error' }
        }
    } finally {
        saving.value = false
    }
}

function choose(category: FileCategory, fileId: string | null): void {
    const current = selection.value
    if (current) {
        void apply(() =>
            setTrackFileSelection(props.id, changedSelection(current, category, fileId)),
        )
    }
}

function reset(): void {
    void apply(async () => {
        await resetTrackFileSelection(props.id)
        return getTrackFileSelection(props.id)
    })
}
</script>

<template>
    <div
        data-part="view"
        data-view="admin-track-files"
        :aria-busy="status === 'loading' || saving ? 'true' : undefined"
    >
        <AdminNav />
        <div v-if="status === 'notFound'" data-part="empty-state">
            <p data-part="empty-state-text">{{ t('library.trackNotFound') }}</p>
            <BaseLink :to="{ name: 'admin-files' }">{{ t('adminFiles.toOverview') }}</BaseLink>
        </div>
        <div v-else-if="status === 'error'" data-part="empty-state">
            <p data-part="empty-state-title">{{ t('adminFiles.loadError') }}</p>
            <BaseButton :label="t('library.retry')" @click="load" />
        </div>
        <template v-else-if="status === 'ready' && track">
            <DetailHeader :title="track.title ?? ''" :back="{ name: 'admin-files' }">
                <MetaItem v-if="track.artist?.name" field="artist" :label="t('list.artist')">
                    {{ track.artist.name }}
                </MetaItem>
                <MetaItem v-if="track.album?.title" field="album" :label="t('list.album')">
                    {{ track.album.title }}
                </MetaItem>
                <MetaItem field="track" :label="t('adminFiles.song')">
                    <BaseLink :to="{ name: 'library-track', params: { id: track.id ?? id } }">
                        {{ t('adminFiles.openSong') }}
                    </BaseLink>
                </MetaItem>
                <template v-if="anySelection" #actions>
                    <BaseButton
                        data-item="reset-file-selection"
                        icon="reset"
                        :label="t('adminFiles.reset')"
                        :disabled="saving"
                        @click="reset"
                    />
                </template>
            </DetailHeader>
            <BaseAlert v-if="message" :tone="message.tone" :message="message.text" />
            <section
                v-for="group in choices"
                :key="group.category"
                data-part="section"
                :data-item="group.category"
                :aria-labelledby="titleIds[group.category]"
            >
                <h2 :id="titleIds[group.category]" data-part="section-title">
                    {{ t(`adminFiles.categories.${group.category}`) }}
                </h2>
                <BaseAlert
                    v-if="group.selection?.state === FileSelectionStateObject.Missing"
                    tone="info"
                    :message="
                        t('adminFiles.missingAlert', { path: group.selection.selectedPath ?? '' })
                    "
                />
                <div v-if="group.choices.length <= 1" data-part="empty-state">
                    <p data-part="empty-state-text">
                        {{ t(`adminFiles.none.${group.category}`) }}
                    </p>
                </div>
                <FileChoiceGroup
                    v-else
                    :category="group.category"
                    :choices="group.choices"
                    :labelledby="titleIds[group.category]"
                    :disabled="saving"
                    @choose="(fileId) => choose(group.category, fileId)"
                />
            </section>
        </template>
    </div>
</template>
