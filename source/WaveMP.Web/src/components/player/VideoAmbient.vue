<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useInView } from '@/composables/useInView'
import { useMediaQuery } from '@/composables/useMediaQuery'
import {
    AmbientLight,
    ambientState,
    hasPicture,
    type AmbientConditions,
} from '@/player/ambientLight'

/**
 * The ambient light of the video stage: soft light in the colors of the current picture, between
 * the video-backdrop and the video, so it shows in the free area around the picture. It draws tiny
 * pictures of the video into two canvases by turns; the base CSS enlarges, blurs and cross-fades
 * them. It draws only while the video plays and is visible, keeps the last picture while the video
 * is paused, and shows one static color for prefers-reduced-motion (--wmp-video-ambient-color).
 * The full-screen player on phones shows it around its video as well; there it lies in a button,
 * so it is a span instead of a div.
 */
const props = withDefaults(
    defineProps<{
        /** The video element of the stage; null until it is mounted. */
        video?: HTMLVideoElement | null
        /** Whether the profile shows the ambient light. */
        enabled?: boolean
        /** Whether the stage shows the video view. */
        active?: boolean
        /** Whether the video stage is in full screen. */
        fullscreen?: boolean
        /** The element of the ambient light: a div, or a span where only phrasing content fits. */
        tag?: 'div' | 'span'
    }>(),
    { video: null, enabled: true, active: true, fullscreen: false, tag: 'div' },
)

const container = ref<HTMLElement | null>(null)
const first = ref<HTMLCanvasElement | null>(null)
const second = ref<HTMLCanvasElement | null>(null)
const current = ref<number | null>(null)
const color = ref<string | null>(null)
const ready = ref(false)
const playing = ref(false)
const pageVisible = ref(typeof document === 'undefined' || document.visibilityState !== 'hidden')
const inView = useInView(container, '0px')
const reducedMotion = useMediaQuery('(prefers-reduced-motion: reduce)')

const conditions = computed<AmbientConditions>(() => ({
    enabled: props.enabled,
    ready: ready.value,
    reducedMotion: reducedMotion.value,
    shown: props.active,
    visible: pageVisible.value && (inView.value || props.fullscreen),
    playing: playing.value,
}))

const state = computed(() => ambientState(conditions.value))

/** data-state of a canvas: the newest picture is current, the one before it previous. */
function canvasState(index: number): string | undefined {
    if (current.value === null) {
        return undefined
    }

    return index === current.value ? 'current' : 'previous'
}

/** --wmp-video-ambient-color: the color of the picture, only for the static light. */
const ambientColor = computed(() =>
    state.value === 'static' && color.value ? color.value : undefined,
)

let light: AmbientLight | null = null
let observed: HTMLVideoElement | null = null

function readVideo(): void {
    const video = props.video
    ready.value = video ? hasPicture(video) : false
    playing.value = video ? !video.paused && !video.ended : false
}

/** A new source: the pictures of the previous one fade out until the new one has a picture. */
function onEmptied(): void {
    light?.reset()
    current.value = null
    readVideo()
}

function onSeeked(): void {
    readVideo()
    light?.refresh()
}

const videoEvents = [
    'loadeddata',
    'canplay',
    'play',
    'playing',
    'pause',
    'ended',
    'waiting',
] as const

function observe(video: HTMLVideoElement | null): void {
    if (observed) {
        for (const name of videoEvents) {
            observed.removeEventListener(name, readVideo)
        }
        observed.removeEventListener('emptied', onEmptied)
        observed.removeEventListener('seeked', onSeeked)
    }

    light?.dispose()
    light = null
    observed = video
    current.value = null
    color.value = null
    if (!video || !first.value || !second.value) {
        ready.value = false
        playing.value = false
        return
    }

    for (const name of videoEvents) {
        video.addEventListener(name, readVideo)
    }
    video.addEventListener('emptied', onEmptied)
    video.addEventListener('seeked', onSeeked)
    light = new AmbientLight([first.value, second.value], video, {
        frame: (index) => {
            current.value = index
        },
        color: (value) => {
            color.value = value
        },
    })
    readVideo()
    light.update(conditions.value)
}

function onVisibilityChange(): void {
    pageVisible.value = document.visibilityState !== 'hidden'
}

watch(conditions, (value) => light?.update(value))

onMounted(() => {
    document.addEventListener('visibilitychange', onVisibilityChange)
    watch(() => props.video, observe, { immediate: true })
})

onBeforeUnmount(() => {
    document.removeEventListener('visibilitychange', onVisibilityChange)
    observe(null)
})
</script>

<template>
    <component
        :is="tag"
        ref="container"
        data-part="video-ambient"
        :data-state="state"
        :style="{ '--wmp-video-ambient-color': ambientColor }"
        aria-hidden="true"
    >
        <canvas ref="first" data-part="video-ambient-canvas" :data-state="canvasState(0)"></canvas>
        <canvas ref="second" data-part="video-ambient-canvas" :data-state="canvasState(1)"></canvas>
    </component>
</template>
