<script setup lang="ts">
import { computed, ref, useId } from 'vue'
import { useI18n } from 'vue-i18n'
import type { TrackMetadata } from '@/api/generated/models'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseDialog from '@/components/base/BaseDialog.vue'
import BackButton from './BackButton.vue'
import TrackMetadataForm from './TrackMetadataForm.vue'

/**
 * Metadata editor of a track for admins in a dialog (see TrackMetadataForm). Saving closes the
 * dialog, also if nothing changed. The back button in its header returns to the page the editor
 * was opened from: the previous page if the editor came with the address (the menu of a song in a
 * list), otherwise it closes and shows the song.
 */
const props = withDefaults(
    defineProps<{
        trackId: string
        /** Opened with the address from another page: back returns to that page. */
        returnToPrevious?: boolean
    }>(),
    { returnToPrevious: false },
)

const emit = defineEmits<{
    saved: [metadata: TrackMetadata]
}>()

const open = defineModel<boolean>('open', { default: false })

const { t } = useI18n()
const formId = useId()
const editor = ref<InstanceType<typeof TrackMetadataForm> | null>(null)

const canSave = computed(() => !!editor.value?.editable && !editor.value.saving)

function onSaved(metadata: TrackMetadata): void {
    open.value = false
    emit('saved', metadata)
}
</script>

<template>
    <BaseDialog v-model:open="open" :title="t('trackMetadata.title')">
        <template #header-start>
            <BackButton variant="icon" :history="returnToPrevious" @close="open = false" />
        </template>
        <TrackMetadataForm
            ref="editor"
            :track-id="props.trackId"
            :active="open"
            :form-id="formId"
            @saved="onSaved"
            @unchanged="open = false"
        />
        <template #footer>
            <BaseButton :label="t('actions.cancel')" @click="open = false" />
            <BaseButton
                type="submit"
                variant="primary"
                :form="formId"
                :label="t('trackMetadata.save')"
                :disabled="!canSave"
            />
        </template>
    </BaseDialog>
</template>
