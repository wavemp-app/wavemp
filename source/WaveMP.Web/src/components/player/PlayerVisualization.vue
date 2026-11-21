<script setup lang="ts">
import { ref } from 'vue'
import { useVisualization } from '@/composables/useVisualization'
import { defaultVisualizationSettings, type VisualizationSettings } from '@/visualization/settings'

/**
 * The visualization in the full-screen player (mobile): the same drawing as on the stage of Now
 * Playing (module, preset and quality of the profile), as a picture only, without controls, in the
 * place of the cover. It lies inside the button now-playing-mini, so it holds nothing interactive;
 * the stage of Now Playing chooses the preset and the module.
 */
const props = withDefaults(
    defineProps<{
        /** Whether the player shows the visualization. */
        active?: boolean
        /** The output of the player; null before its first playback or without Web Audio. */
        audio?: AnalyserNode | null
        /** The visualization settings of the profile. */
        settings?: VisualizationSettings
    }>(),
    { active: true, audio: null, settings: () => defaultVisualizationSettings },
)

const container = ref<HTMLElement | null>(null)
const canvas = ref<HTMLCanvasElement | null>(null)

const { canvasKey, state } = useVisualization(container, canvas, {
    active: () => props.active,
    audio: () => props.audio,
    settings: () => props.settings,
})
</script>

<template>
    <span
        ref="container"
        data-part="player-visualization"
        :data-state="state"
        :data-module="settings.module.toLowerCase()"
        aria-hidden="true"
    >
        <canvas :key="canvasKey" ref="canvas" data-part="visualization-canvas"></canvas>
    </span>
</template>
