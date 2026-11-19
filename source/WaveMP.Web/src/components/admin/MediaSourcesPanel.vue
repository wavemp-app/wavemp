<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { ProblemCodeObject, type MediaSource } from '@/api/generated/models'
import { problemCodeOf } from '@/api/problems'
import MediaSourceDeleteDialog from './MediaSourceDeleteDialog.vue'
import MediaSourceDialog from './MediaSourceDialog.vue'
import MediaSourceList from './MediaSourceList.vue'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import { useMediaSourcesStore } from '@/stores/mediaSources'

/**
 * The media folders on the library page of the administration: list, add, edit, switch on or off
 * and remove them. Tells the page when the server may have started a scan, so it can follow it.
 */
const emit = defineEmits<{
    changed: []
}>()

const { t } = useI18n()
const store = useMediaSourcesStore()

const editOpen = ref(false)
const editing = ref<MediaSource | null>(null)
const deleteOpen = ref(false)
const deleting = ref<MediaSource | null>(null)
const deleteBusy = ref(false)
const busyId = ref<string | null>(null)
const message = ref<string | null>(null)
const notice = ref<string | null>(null)

function clearMessages(): void {
    message.value = null
    notice.value = null
}

function openCreate(): void {
    clearMessages()
    editing.value = null
    editOpen.value = true
}

function openEdit(source: MediaSource): void {
    clearMessages()
    editing.value = source
    editOpen.value = true
}

function openDelete(source: MediaSource): void {
    clearMessages()
    deleting.value = source
    deleteOpen.value = true
}

function onSaved(source: MediaSource): void {
    notice.value = t('adminSources.saved', { name: source.name ?? '' })
    emit('changed')
}

function failureMessage(failure: unknown, fallback: string): string {
    switch (problemCodeOf(failure)) {
        case ProblemCodeObject.SourceNot_found:
            return t('adminSources.errors.notFound')
        case ProblemCodeObject.ScanAlready_running:
            return t('adminSources.errors.scanRunning')
        default:
            return t(fallback)
    }
}

async function toggle(source: MediaSource): Promise<void> {
    if (!source.id || busyId.value) {
        return
    }

    clearMessages()
    busyId.value = source.id
    try {
        const saved = await store.update(source.id, { isActive: !source.isActive })
        notice.value = saved?.isActive
            ? t('adminSources.activated', { name: source.name ?? '' })
            : t('adminSources.deactivated', { name: source.name ?? '' })
        emit('changed')
    } catch (failure) {
        message.value = failureMessage(failure, 'adminSources.errors.saveFailed')
        await store.load()
    } finally {
        busyId.value = null
    }
}

async function confirmDelete(): Promise<void> {
    const source = deleting.value
    if (!source?.id || deleteBusy.value) {
        return
    }

    deleteBusy.value = true
    try {
        await store.remove(source.id)
        deleteOpen.value = false
        notice.value = t('adminSources.removed', { name: source.name ?? '' })
        emit('changed')
    } catch (failure) {
        deleteOpen.value = false
        message.value = failureMessage(failure, 'adminSources.errors.deleteFailed')
        await store.load()
    } finally {
        deleteBusy.value = false
    }
}

onMounted(() => {
    void store.load()
})
</script>

<template>
    <div data-part="view-actions">
        <BaseButton
            variant="primary"
            icon="add"
            data-item="add-source"
            :label="t('adminSources.add')"
            @click="openCreate"
        />
    </div>
    <BaseAlert v-if="message" tone="error" :message="message" />
    <BaseAlert v-if="notice" tone="success" :message="notice" />
    <template v-if="store.status === 'error'">
        <BaseAlert tone="error" :message="t('adminSources.loadError')" />
        <div data-part="view-actions">
            <BaseButton data-item="retry" :label="t('library.retry')" @click="store.load()" />
        </div>
    </template>
    <MediaSourceList
        v-if="store.sources.length > 0"
        :sources="store.sources"
        :busy-id="busyId"
        @edit="openEdit"
        @toggle="toggle"
        @delete="openDelete"
    />
    <BaseAlert
        v-else-if="store.status === 'ready'"
        tone="info"
        :message="t('adminSources.empty')"
    />
    <MediaSourceDialog v-model:open="editOpen" :source="editing" @saved="onSaved" />
    <MediaSourceDeleteDialog
        v-model:open="deleteOpen"
        :source="deleting"
        :busy="deleteBusy"
        @confirm="confirmDelete"
    />
</template>
