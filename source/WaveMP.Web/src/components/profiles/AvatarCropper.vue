<script setup lang="ts">
import { computed, ref, useId } from 'vue'
import { useI18n } from 'vue-i18n'
import BaseSlider from '@/components/base/BaseSlider.vue'
import {
    clampCrop,
    cropForKey,
    cropVariables,
    maxAvatarZoom,
    panCrop,
    zoomCrop,
    type AvatarCrop,
    type PictureSize,
} from '@/profiles/avatarCrop'

/**
 * Cropping of a chosen picture: the picture lies behind a square frame and can be moved (dragging
 * or the arrow keys on the frame) and zoomed (slider). v-model is the crop. The frame and a small
 * preview show the same square through the runtime variables --wmp-crop-scale (width of the
 * picture in sizes of the frame), --wmp-crop-x and --wmp-crop-y (offset of the picture).
 */
const props = defineProps<{
    /** Object URL of the picture. */
    src: string
    picture: PictureSize
    disabled?: boolean
}>()

const crop = defineModel<AvatarCrop>({ required: true })

const { t } = useI18n()
const hintId = useId()
const frame = ref<HTMLElement | null>(null)
const dragging = ref(false)
let last: { x: number; y: number } | null = null

const view = computed(() => cropVariables(crop.value, props.picture))

const zoom = computed({
    get: () => crop.value.zoom,
    set: (next: number) => {
        crop.value = zoomCrop(crop.value, props.picture, next)
    },
})

const zoomText = computed(() => t('avatars.zoomValue', { percent: Math.round(zoom.value * 100) }))

function frameSize(): number {
    return frame.value?.getBoundingClientRect().width ?? 0
}

function onPointerDown(event: PointerEvent): void {
    if (props.disabled || event.button !== 0) {
        return
    }

    event.preventDefault()
    ;(event.currentTarget as HTMLElement).setPointerCapture?.(event.pointerId)
    frame.value?.focus()
    dragging.value = true
    last = { x: event.clientX, y: event.clientY }
}

function onPointerMove(event: PointerEvent): void {
    if (!dragging.value || !last) {
        return
    }

    crop.value = panCrop(
        crop.value,
        props.picture,
        event.clientX - last.x,
        event.clientY - last.y,
        frameSize(),
    )
    last = { x: event.clientX, y: event.clientY }
}

function onPointerEnd(): void {
    dragging.value = false
    last = null
}

function onKeydown(event: KeyboardEvent): void {
    if (props.disabled) {
        return
    }

    const next = cropForKey(event.key, crop.value, props.picture, frameSize())
    if (next) {
        event.preventDefault()
        crop.value = clampCrop(next, props.picture)
    }
}
</script>

<template>
    <div
        data-part="avatar-crop"
        :data-state="dragging ? 'dragging' : undefined"
        :style="{ '--wmp-crop-scale': view.scale, '--wmp-crop-x': view.x, '--wmp-crop-y': view.y }"
    >
        <div
            ref="frame"
            data-part="avatar-crop-frame"
            role="group"
            :tabindex="disabled ? -1 : 0"
            :aria-label="t('avatars.cropFrame')"
            :aria-describedby="hintId"
            :aria-disabled="disabled ? 'true' : undefined"
            @pointerdown="onPointerDown"
            @pointermove="onPointerMove"
            @pointerup="onPointerEnd"
            @pointercancel="onPointerEnd"
            @lostpointercapture="onPointerEnd"
            @keydown="onKeydown"
        >
            <img data-part="avatar-crop-image" :src="src" alt="" draggable="false" />
        </div>
        <p :id="hintId" data-part="form-note">{{ t('avatars.cropHint') }}</p>
        <div data-part="avatar-crop-preview" role="img" :aria-label="t('avatars.preview')">
            <img data-part="avatar-crop-image" :src="src" alt="" draggable="false" />
        </div>
        <div data-part="field" data-field="zoom">
            <span data-part="label">{{ t('avatars.zoom') }}</span>
            <BaseSlider
                v-model="zoom"
                :label="t('avatars.zoom')"
                :min="1"
                :max="maxAvatarZoom"
                :step="0.05"
                :large-step="0.5"
                :value-text="zoomText"
                :disabled="disabled"
            />
        </div>
    </div>
</template>
