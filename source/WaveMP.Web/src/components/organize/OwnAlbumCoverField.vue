<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { coverUploadTypes } from '@/playlists/presentation'
import BaseButton from '@/components/base/BaseButton.vue'
import OwnAlbumCover from './OwnAlbumCover.vue'

/**
 * The cover of an own album in its editor: a drop zone (cover-drop) that shows the cover and takes
 * a picture by drag and drop or the button "Choose picture", and the button "Remove cover". The
 * state of the zone is empty or filled, plus dragging while a file is dragged over it. The picture
 * is checked and uploaded by the editor (event choose).
 */
const props = withDefaults(
    defineProps<{
        id?: string | null
        hasCover?: boolean
        version?: string | null
        /** The picture chosen but not saved yet. */
        file?: Blob | null
        disabled?: boolean
    }>(),
    { id: null, hasCover: false, version: null, file: null, disabled: false },
)

const emit = defineEmits<{
    choose: [file: File]
    remove: []
}>()

const { t } = useI18n()
const input = ref<HTMLInputElement | null>(null)
const dragging = ref(false)

const filled = computed(() => !!props.file || props.hasCover)
const state = computed(() =>
    [filled.value ? 'filled' : 'empty', ...(dragging.value ? ['dragging'] : [])].join(' '),
)

function pick(): void {
    if (!props.disabled) {
        input.value?.click()
    }
}

function onChange(event: Event): void {
    const element = event.target as HTMLInputElement
    const file = element.files?.[0]
    element.value = ''
    if (file) {
        emit('choose', file)
    }
}

function onDragOver(event: DragEvent): void {
    if (props.disabled) {
        return
    }
    event.preventDefault()
    if (event.dataTransfer) {
        event.dataTransfer.dropEffect = 'copy'
    }
    dragging.value = true
}

function onDrop(event: DragEvent): void {
    dragging.value = false
    if (props.disabled) {
        return
    }
    event.preventDefault()
    const file = event.dataTransfer?.files?.[0]
    if (file) {
        emit('choose', file)
    }
}
</script>

<template>
    <div
        data-part="cover-drop"
        :data-state="state"
        :aria-disabled="disabled ? 'true' : undefined"
        @dragover="onDragOver"
        @dragleave="dragging = false"
        @drop="onDrop"
    >
        <OwnAlbumCover :id="id" :has-cover="hasCover" :version="version" :file="file" />
        <span data-part="label" data-field="drop-hint">{{ t('ownAlbums.cover.dropHint') }}</span>
        <input
            ref="input"
            type="file"
            data-field="cover-file"
            :accept="coverUploadTypes.join(',')"
            hidden
            @change="onChange"
        />
        <div data-part="view-actions">
            <BaseButton
                data-item="choose-cover"
                icon="change-cover"
                :label="t('ownAlbums.cover.choose')"
                :disabled="disabled"
                @click="pick"
            />
            <BaseButton
                v-if="filled"
                data-item="remove-cover"
                icon="delete"
                :label="t('ownAlbums.cover.remove')"
                :disabled="disabled"
                @click="emit('remove')"
            />
        </div>
    </div>
</template>
