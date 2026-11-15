<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import BaseButton from '@/components/base/BaseButton.vue'
import { canFullscreen, enterFullscreen, exitFullscreen, isFullscreen } from '@/player/fullscreen'
import { videoAspectRatio, videoOrientation } from '@/player/videoOrientation'

/**
 * The video of the current track on the Now Playing stage, muted: the player plays the audio and
 * the video follows it. The element is handed to the player when it is shown (attach) and taken
 * back before it closes (detach); the song simply goes on. While the video view is active, a double
 * click or the button shows the stage in full screen. The video is always shown whole and
 * undistorted in the middle of the stage; the orientation of its picture (data-orientation) and its
 * aspect ratio (--wmp-video-aspect-ratio) let the theme design the free areas around it, for
 * example with the video-backdrop behind it.
 */
const props = withDefaults(
    defineProps<{
        /** Whether the stage shows the video view. */
        active?: boolean
    }>(),
    { active: true },
)

const emit = defineEmits<{
    attach: [element: HTMLVideoElement]
    detach: [element: HTMLVideoElement]
}>()

const { t } = useI18n()

const container = ref<HTMLElement | null>(null)
const video = ref<HTMLVideoElement | null>(null)
const fullscreen = ref(false)
const fullscreenAvailable = ref(false)
const pictureWidth = ref(0)
const pictureHeight = ref(0)

const orientation = computed(() => videoOrientation(pictureWidth.value, pictureHeight.value))
const aspectRatio = computed(() => videoAspectRatio(pictureWidth.value, pictureHeight.value))

function update(): void {
    if (container.value && video.value) {
        fullscreen.value = isFullscreen(container.value, video.value)
    }
}

/** Reads the size of the picture: when the metadata of a video is loaded and when it changes. */
function updateSize(): void {
    pictureWidth.value = video.value?.videoWidth ?? 0
    pictureHeight.value = video.value?.videoHeight ?? 0
}

async function toggleFullscreen(): Promise<void> {
    if (!container.value || !video.value || !fullscreenAvailable.value) {
        return
    }

    // A hidden video never enters full screen; one in full screen may always leave it.
    if (!props.active && !isFullscreen(container.value, video.value)) {
        return
    }

    if (isFullscreen(container.value, video.value)) {
        await exitFullscreen(container.value, video.value)
    } else {
        await enterFullscreen(container.value, video.value)
    }

    update()
}

const sizeEvents = ['loadedmetadata', 'resize', 'emptied'] as const

onMounted(() => {
    const element = video.value!
    fullscreenAvailable.value = canFullscreen(container.value!, element)
    document.addEventListener('fullscreenchange', update)
    element.addEventListener('webkitbeginfullscreen', update)
    element.addEventListener('webkitendfullscreen', update)
    for (const name of sizeEvents) {
        element.addEventListener(name, updateSize)
    }

    updateSize()
    emit('attach', element)
})

onBeforeUnmount(() => {
    const element = video.value!
    document.removeEventListener('fullscreenchange', update)
    element.removeEventListener('webkitbeginfullscreen', update)
    element.removeEventListener('webkitendfullscreen', update)
    for (const name of sizeEvents) {
        element.removeEventListener(name, updateSize)
    }

    if (fullscreen.value) {
        void exitFullscreen(container.value!, element)
    }

    emit('detach', element)
})
</script>

<template>
    <div
        ref="container"
        data-part="video-stage"
        :data-state="fullscreen ? 'fullscreen' : undefined"
        :data-orientation="orientation"
        :style="{ '--wmp-video-aspect-ratio': aspectRatio }"
        @dblclick="toggleFullscreen"
    >
        <span data-part="video-backdrop" aria-hidden="true"></span>
        <video
            ref="video"
            data-part="video"
            muted
            playsinline
            disablepictureinpicture
            :aria-label="t('nowPlaying.video')"
        ></video>
        <BaseButton
            part="btn-fullscreen"
            variant="icon"
            :icon="fullscreen ? 'exit-fullscreen' : 'fullscreen'"
            :label="fullscreen ? t('nowPlaying.exitFullscreen') : t('nowPlaying.fullscreen')"
            :disabled="!fullscreenAvailable || (!active && !fullscreen)"
            @click.stop="toggleFullscreen"
            @dblclick.stop
        />
    </div>
</template>
