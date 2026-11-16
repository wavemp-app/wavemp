<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, shallowRef, toRaw, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseIcon from '@/components/base/BaseIcon.vue'
import BaseSelect, { type SelectOption } from '@/components/base/BaseSelect.vue'
import { useInView } from '@/composables/useInView'
import { canFullscreen, enterFullscreen, exitFullscreen, isFullscreen } from '@/player/fullscreen'
import {
    drawingSize,
    VisualizationController,
    type VisualizationState,
} from '@/visualization/controller'
import type { VisualizationPreset } from '@/visualization/module'
import { visualizationDependencies } from '@/visualization/modules'
import {
    defaultVisualizationSettings,
    type VisualizationSettings,
    type VisualizationSettingsChange,
} from '@/visualization/settings'

/**
 * The visualization of the Now Playing stage: the module of the profile (Milkdrop presets for now)
 * draws the output of the player into a canvas, in sync with the music. Its controls choose the
 * preset, turn the automatic change on and off and show the visualization in full screen (also
 * with a double click). The module loads when the view is shown the first time; nothing is drawn
 * while the view is hidden, the page is in the background or the stage is out of the viewport.
 * Choices are handed to the profile with update.
 */
const props = withDefaults(
    defineProps<{
        /** Whether the stage shows the visualization view. */
        active?: boolean
        /** The output of the player; null before its first playback or without Web Audio. */
        audio?: AnalyserNode | null
        /** The visualization settings of the profile. */
        settings?: VisualizationSettings
    }>(),
    { active: true, audio: null, settings: () => defaultVisualizationSettings },
)

const emit = defineEmits<{
    update: [change: VisualizationSettingsChange]
}>()

const { t } = useI18n()

const container = ref<HTMLElement | null>(null)
const canvas = ref<HTMLCanvasElement | null>(null)
/** Changes when a renderer released its canvas, so Vue creates a new canvas element. */
const canvasKey = ref(0)
const state = ref<VisualizationState>('idle')
const presets = shallowRef<readonly VisualizationPreset[]>([])
const preset = ref('')
const fullscreen = ref(false)
const fullscreenAvailable = ref(false)
const pageVisible = ref(typeof document === 'undefined' || document.visibilityState !== 'hidden')
const inView = useInView(container, '0px')

const visible = computed(
    () => props.active && pageVisible.value && (inView.value || fullscreen.value),
)

const ready = computed(() => presets.value.length > 0)

const stateTokens = computed(() =>
    [state.value, fullscreen.value ? 'fullscreen' : undefined].filter(Boolean).join(' '),
)

const message = computed(() => {
    switch (state.value) {
        case 'idle':
        case 'loading':
            return t('nowPlaying.visualizationLoading')
        case 'waiting':
            return t('nowPlaying.visualizationWaiting')
        case 'unsupported':
            return t('nowPlaying.visualizationUnsupported')
        case 'failed':
            return t('nowPlaying.visualizationFailed')
        default:
            return null
    }
})

const presetOptions = computed<SelectOption[]>(() =>
    presets.value.map((item) => ({ value: item.id, label: item.name })),
)

let controller: VisualizationController | null = null

/** The analyser itself: Web Audio refuses reactive proxies of its nodes. */
function audioNode(): AnalyserNode | null {
    return props.audio ? toRaw(props.audio) : null
}
let resizeObserver: ResizeObserver | null = null

function createController(): VisualizationController {
    const created = new VisualizationController(
        visualizationDependencies.module(props.settings.module),
        props.settings.preset,
        { enabled: props.settings.autoChange, seconds: props.settings.autoChangeSeconds },
        {
            state: (value) => (state.value = value),
            presets: (value) => (presets.value = value),
            preset: (value) => (preset.value = value ?? ''),
            canvasReleased: () => canvasKey.value++,
        },
        visualizationDependencies.clock,
    )
    state.value = created.currentState
    presets.value = created.presets
    preset.value = created.preset ?? ''
    return created
}

/** Hands the size of the visualization in device pixels to the controller. */
function updateSize(width: number, height: number): void {
    if (width > 0 && height > 0) {
        const size = drawingSize(width, height, visualizationDependencies.devicePixelRatio())
        controller?.resize(size.width, size.height)
    }
}

function measure(): void {
    const element = container.value
    if (element) {
        updateSize(element.clientWidth, element.clientHeight)
    }
}

function start(): void {
    controller = createController()
    measure()
    controller.setCanvas(canvas.value)
    controller.setAudio(audioNode())
    controller.setVisible(visible.value)
}

function stop(): void {
    controller?.dispose()
    controller = null
}

function selectPreset(value: string): void {
    if (!controller || !presets.value.some((item) => item.id === value)) {
        return
    }

    controller.selectPreset(value)
    emit('update', { preset: value })
}

function step(offset: -1 | 1): void {
    const id = controller?.step(offset)
    if (id) {
        emit('update', { preset: id })
    }
}

function toggleAutoChange(): void {
    emit('update', { autoChange: !props.settings.autoChange })
}

function updateFullscreen(): void {
    if (container.value) {
        fullscreen.value = isFullscreen(container.value)
    }
}

async function toggleFullscreen(): Promise<void> {
    const element = container.value
    if (!element || !fullscreenAvailable.value) {
        return
    }

    // A hidden visualization never enters full screen; one in full screen may always leave it.
    if (isFullscreen(element)) {
        await exitFullscreen(element)
    } else if (props.active) {
        await enterFullscreen(element)
    }

    updateFullscreen()
}

function onVisibilityChange(): void {
    pageVisible.value = document.visibilityState !== 'hidden'
}

watch(visible, (value) => controller?.setVisible(value))
watch(
    () => props.audio,
    () => controller?.setAudio(audioNode()),
)
watch(canvas, (value) => controller?.setCanvas(value), { flush: 'post' })
watch(
    () => [props.settings.autoChange, props.settings.autoChangeSeconds] as const,
    ([enabled, seconds]) => controller?.setAutoChange({ enabled, seconds }),
)
watch(
    () => props.settings.preset,
    (value) => controller?.setStoredPreset(value),
)
// Another module: the visualization starts again with it.
watch(
    () => props.settings.module,
    () => {
        stop()
        canvasKey.value++
        start()
    },
)

onMounted(() => {
    const element = container.value!
    fullscreenAvailable.value = canFullscreen(element)
    document.addEventListener('fullscreenchange', updateFullscreen)
    document.addEventListener('visibilitychange', onVisibilityChange)
    if (typeof ResizeObserver !== 'undefined') {
        resizeObserver = new ResizeObserver((entries) => {
            const box = entries[0]?.contentRect
            if (box) {
                updateSize(box.width, box.height)
            }
        })
        resizeObserver.observe(element)
    }

    start()
})

onBeforeUnmount(() => {
    document.removeEventListener('fullscreenchange', updateFullscreen)
    document.removeEventListener('visibilitychange', onVisibilityChange)
    resizeObserver?.disconnect()
    resizeObserver = null
    if (fullscreen.value && container.value) {
        void exitFullscreen(container.value)
    }

    stop()
})
</script>

<template>
    <div
        ref="container"
        data-part="visualization"
        :data-state="stateTokens"
        :data-module="settings.module.toLowerCase()"
        @dblclick="toggleFullscreen"
    >
        <canvas
            :key="canvasKey"
            ref="canvas"
            data-part="visualization-canvas"
            aria-hidden="true"
        ></canvas>
        <p v-if="message" data-part="visualization-message" role="status">
            <BaseIcon name="view-visualization" />
            <span data-part="label">{{ message }}</span>
        </p>
        <div
            data-part="visualization-controls"
            role="group"
            :aria-label="t('nowPlaying.visualizationControls')"
            @dblclick.stop
        >
            <BaseButton
                part="btn-preset-previous"
                variant="icon"
                icon="preset-previous"
                :label="t('nowPlaying.previousPreset')"
                :disabled="!ready"
                @click="step(-1)"
            />
            <BaseSelect
                data-field="visualization-preset"
                :label="t('nowPlaying.visualizationPreset')"
                :options="presetOptions"
                :model-value="preset"
                :disabled="!ready"
                @update:model-value="selectPreset"
            />
            <BaseButton
                part="btn-preset-next"
                variant="icon"
                icon="preset-next"
                :label="t('nowPlaying.nextPreset')"
                :disabled="!ready"
                @click="step(1)"
            />
            <BaseButton
                part="btn-auto-change"
                variant="icon"
                icon="auto-change"
                :label="t('nowPlaying.autoChange')"
                :pressed="settings.autoChange"
                :state="settings.autoChange ? 'on' : 'off'"
                @click="toggleAutoChange"
            />
            <BaseButton
                part="btn-fullscreen"
                variant="icon"
                :icon="fullscreen ? 'exit-fullscreen' : 'fullscreen'"
                :label="fullscreen ? t('nowPlaying.exitFullscreen') : t('nowPlaying.fullscreen')"
                :disabled="!fullscreenAvailable || (!active && !fullscreen)"
                @click="toggleFullscreen"
            />
        </div>
    </div>
</template>
