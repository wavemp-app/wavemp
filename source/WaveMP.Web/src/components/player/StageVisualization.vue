<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import BaseIcon from '@/components/base/BaseIcon.vue'
import { useVisualization } from '@/composables/useVisualization'
import { canFullscreen, enterFullscreen, exitFullscreen, isFullscreen } from '@/player/fullscreen'
import type { PresetChoice } from './visualizationControls'
import {
    defaultVisualizationSettings,
    type VisualizationSettings,
    type VisualizationSettingsChange,
} from '@/visualization/settings'

/**
 * The visualization of the Now Playing stage: the module of the profile (Milkdrop presets, the
 * modes of A-Force or the scenes of BlackCap) draws the output of the player into a canvas,
 * in sync with the music, at the quality of the profile (useVisualization). It holds no controls,
 * so nothing covers the picture: the stage shows VisualizationControls below it, which use the
 * presets, the preset choice and the full screen this component exposes. A double click shows the
 * visualization in full screen and leaves it again. The module loads when the view is shown the
 * first time; nothing is drawn while the view is hidden, the page is in the background or the stage
 * is out of the viewport. A chosen preset or color scheme is handed to the profile with update. The
 * intensity it draws with (data-intensity) is reduced by the profile or by the wish of the device
 * for reduced motion.
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
const fullscreen = ref(false)
const fullscreenAvailable = ref(false)

const visualization = useVisualization(container, canvas, {
    active: () => props.active,
    audio: () => props.audio,
    settings: () => props.settings,
    fullscreen: () => fullscreen.value,
})
const { canvasKey, state, presets, preset, colorSchemes, colorScheme, quality, intensity } =
    visualization

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

/** The presets of the module with their labels in the language of the app. */
const presetChoices = computed<PresetChoice[]>(() =>
    presets.value.map((item) => ({
        id: item.id,
        label: item.labelKey ? t(item.labelKey) : item.name,
    })),
)

/** The color schemes of the module with their labels; empty for modules without color schemes. */
const colorSchemeChoices = computed<PresetChoice[]>(() =>
    colorSchemes.value.map((item) => ({
        id: item.id,
        label: item.labelKey ? t(item.labelKey) : item.name,
    })),
)

/** Shows a color scheme of the module and hands the choice to the profile. */
function selectColorScheme(value: string): void {
    if (!visualization.hasColorScheme(value)) {
        return
    }

    emit('update', { colorScheme: value })
    visualization.selectColorScheme(value)
}

/** Shows a preset of the module and hands the choice to the profile. */
function selectPreset(value: string): void {
    if (!visualization.hasPreset(value)) {
        return
    }

    // The choice goes to the profile first: showing a preset may take a moment (its shaders compile).
    emit('update', { preset: value })
    visualization.selectPreset(value)
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

onMounted(() => {
    fullscreenAvailable.value = canFullscreen(container.value!)
    document.addEventListener('fullscreenchange', updateFullscreen)
})

onBeforeUnmount(() => {
    document.removeEventListener('fullscreenchange', updateFullscreen)
    if (fullscreen.value && container.value) {
        void exitFullscreen(container.value)
    }
})

/**
 * The presets, the color schemes and the full screen for the controls and the context menu of the
 * stage.
 */
defineExpose({
    presetChoices,
    preset,
    selectPreset,
    colorSchemeChoices,
    colorScheme,
    selectColorScheme,
    fullscreen,
    fullscreenAvailable,
    toggleFullscreen,
})
</script>

<template>
    <div
        ref="container"
        data-part="visualization"
        :data-state="stateTokens"
        :data-module="settings.module.toLowerCase()"
        :data-quality="state === 'running' || state === 'paused' ? quality : undefined"
        :data-intensity="intensity"
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
    </div>
</template>
