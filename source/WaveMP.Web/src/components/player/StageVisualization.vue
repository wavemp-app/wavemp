<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseIcon from '@/components/base/BaseIcon.vue'
import BaseSegmentedControl, {
    type SegmentOption,
} from '@/components/base/BaseSegmentedControl.vue'
import BaseSelect, { type SelectOption } from '@/components/base/BaseSelect.vue'
import { useVisualization } from '@/composables/useVisualization'
import { canFullscreen, enterFullscreen, exitFullscreen, isFullscreen } from '@/player/fullscreen'
import { adjacentPreset } from '@/visualization/presets'
import { visualizationModuleChoices } from '@/visualization/modules'
import {
    defaultVisualizationSettings,
    type VisualizationSettings,
    type VisualizationSettingsChange,
} from '@/visualization/settings'

/**
 * The visualization of the Now Playing stage: the module of the profile (Milkdrop presets, the
 * style variants of A-Force or the scenes of BlackCap) draws the output of the player into a canvas,
 * in sync with the music, at the quality of the profile (useVisualization). Its controls choose the
 * module and the preset, turn the automatic change on and off and show the visualization in full
 * screen (also with a double click). The module loads when the view is shown the first time;
 * nothing is drawn while the view is hidden, the page is in the background or the stage is out of
 * the viewport. Choices are handed to the profile with update.
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
const { canvasKey, state, presets, preset, quality } = visualization

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
    presets.value.map((item) => ({
        value: item.id,
        label: item.labelKey ? t(item.labelKey) : item.name,
    })),
)

const moduleOptions = computed<SegmentOption[]>(() =>
    visualizationModuleChoices.map((choice) => ({
        value: choice.id,
        item: choice.id.toLowerCase(),
        label: t(choice.labelKey),
    })),
)

function selectPreset(value: string): void {
    if (!visualization.hasPreset(value)) {
        return
    }

    // The choice goes to the profile first: showing a preset may take a moment (its shaders compile).
    emit('update', { preset: value })
    visualization.selectPreset(value)
}

function step(offset: -1 | 1): void {
    const next = adjacentPreset(presets.value, preset.value || null, offset)
    if (next) {
        selectPreset(next.id)
    }
}

function selectModule(value: string): void {
    const choice = visualizationModuleChoices.find((item) => item.id === value)
    if (choice && choice.id !== props.settings.module) {
        emit('update', { module: choice.id })
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

/** Full screen for the context menu of the stage. */
defineExpose({ fullscreen, fullscreenAvailable, toggleFullscreen })
</script>

<template>
    <div
        ref="container"
        data-part="visualization"
        :data-state="stateTokens"
        :data-module="settings.module.toLowerCase()"
        :data-quality="state === 'running' || state === 'paused' ? quality : undefined"
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
            <BaseSegmentedControl
                item="visualization-module"
                :label="t('nowPlaying.visualizationModule')"
                :options="moduleOptions"
                :model-value="settings.module"
                @update:model-value="selectModule"
            />
        </div>
    </div>
</template>
