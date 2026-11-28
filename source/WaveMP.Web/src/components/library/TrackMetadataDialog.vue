<script setup lang="ts">
import { computed, ref, useId } from 'vue'
import { useI18n } from 'vue-i18n'
import type { TrackMetadata, TrackSongSegments } from '@/api/generated/models'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseDialog from '@/components/base/BaseDialog.vue'
import BackButton from './BackButton.vue'
import CoverRuleFields from './CoverRuleFields.vue'
import SongSegmentFields from './SongSegmentFields.vue'
import TrackMetadataForm from './TrackMetadataForm.vue'

/**
 * Metadata editor of a track for admins in a dialog: the tags (see TrackMetadataForm), the
 * exception of the song from the cover rules (see CoverRuleFields) and where the song starts and
 * ends in its files (see SongSegmentFields). Saving writes what changed and closes the dialog, also
 * if nothing changed; cover rule and song segments can be saved also when the file cannot be
 * written. The back button in its header returns to the page the editor was opened
 * from: the previous page if the editor came with the address (the menu of a song in a list),
 * otherwise it closes and shows the song.
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
    /** Corrections of the song segments were saved; the player applies them at once. */
    songSegmentsSaved: [segments: TrackSongSegments]
}>()

const open = defineModel<boolean>('open', { default: false })

const { t } = useI18n()
const formId = useId()
const editor = ref<InstanceType<typeof TrackMetadataForm> | null>(null)
const coverRule = ref<InstanceType<typeof CoverRuleFields> | null>(null)
const songSegments = ref<InstanceType<typeof SongSegmentFields> | null>(null)
const finishing = ref(false)

const canSave = computed(
    () =>
        !finishing.value &&
        !editor.value?.saving &&
        !coverRule.value?.saving &&
        !songSegments.value?.saving &&
        (!!editor.value?.editable ||
            coverRule.value?.state === 'ready' ||
            songSegments.value?.state === 'ready'),
)

/** Saves cover rule and song segments after the tags; closes the dialog once all are done. */
async function finish(): Promise<void> {
    if (finishing.value) {
        return
    }

    finishing.value = true
    try {
        const coverSaved = (await coverRule.value?.save()) ?? true
        const segmentsSaved = (await songSegments.value?.save()) ?? true
        if (coverSaved && segmentsSaved) {
            open.value = false
        }
    } finally {
        finishing.value = false
    }
}

/**
 * The button submits the form of the tags, which then reports saved or unchanged; without a
 * writable file it saves only the cover rule and the song segments.
 */
function onSave(): void {
    if (!editor.value?.editable) {
        void finish()
    }
}

function onSaved(metadata: TrackMetadata): void {
    emit('saved', metadata)
    void finish()
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
            @unchanged="finish"
        />
        <CoverRuleFields ref="coverRule" owner="track" :id="props.trackId" :active="open" />
        <SongSegmentFields
            ref="songSegments"
            :track-id="props.trackId"
            :active="open"
            @saved="emit('songSegmentsSaved', $event)"
        />
        <template #footer>
            <BaseButton :label="t('actions.cancel')" @click="open = false" />
            <BaseButton
                :type="editor?.editable ? 'submit' : 'button'"
                variant="primary"
                :form="editor?.editable ? formId : undefined"
                :label="t('trackMetadata.save')"
                :disabled="!canSave"
                @click="onSave"
            />
        </template>
    </BaseDialog>
</template>
