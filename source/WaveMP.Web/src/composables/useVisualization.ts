import { computed, onBeforeUnmount, onMounted, ref, shallowRef, toRaw, watch, type Ref } from 'vue'
import { useInView } from '@/composables/useInView'
import {
    drawingSize,
    VisualizationController,
    type VisualizationState,
} from '@/visualization/controller'
import type { VisualizationPreset } from '@/visualization/module'
import { visualizationDependencies } from '@/visualization/modules'
import type { QualityLevel } from '@/visualization/quality'
import type { VisualizationSettings } from '@/visualization/settings'

export interface VisualizationInput {
    /** Whether the view of the visualization is shown. */
    active: () => boolean
    /** The output of the player; null before its first playback or without Web Audio. */
    audio: () => AnalyserNode | null
    /** The visualization settings of the profile. */
    settings: () => VisualizationSettings
    /** Whether the visualization is shown in full screen; it then draws out of the viewport too. */
    fullscreen?: () => boolean
}

/**
 * The drawing of a visualization into a canvas inside a container: the module of the profile draws
 * the output of the player in sync with the music, at the quality of the profile, in the size of
 * the container. The module loads when the container is mounted; nothing is drawn while the view
 * is hidden, the page is in the background or the container is out of the viewport. Used by the
 * stage of Now Playing (with its controls) and by the full-screen player (the picture only).
 */
export function useVisualization(
    container: Ref<HTMLElement | null>,
    canvas: Ref<HTMLCanvasElement | null>,
    input: VisualizationInput,
) {
    /** Changes when a renderer released its canvas, so Vue creates a new canvas element. */
    const canvasKey = ref(0)
    const state = ref<VisualizationState>('idle')
    const presets = shallowRef<readonly VisualizationPreset[]>([])
    const preset = ref('')
    const quality = ref<QualityLevel>('high')
    const pageVisible = ref(
        typeof document === 'undefined' || document.visibilityState !== 'hidden',
    )
    const inView = useInView(container, '0px')

    const visible = computed(
        () =>
            input.active() &&
            pageVisible.value &&
            (inView.value || (input.fullscreen?.() ?? false)),
    )

    let controller: VisualizationController | null = null
    let resizeObserver: ResizeObserver | null = null

    /** The analyser itself: Web Audio refuses reactive proxies of its nodes. */
    function audioNode(): AnalyserNode | null {
        const audio = input.audio()
        return audio ? toRaw(audio) : null
    }

    function createController(): VisualizationController {
        const settings = input.settings()
        const created = new VisualizationController(
            visualizationDependencies.module(settings.module),
            settings.preset,
            { enabled: settings.autoChange, seconds: settings.autoChangeSeconds },
            {
                state: (value) => (state.value = value),
                presets: (value) => (presets.value = value),
                preset: (value) => (preset.value = value ?? ''),
                canvasReleased: () => canvasKey.value++,
                quality: (value) => (quality.value = value),
            },
            visualizationDependencies.clock,
        )
        created.setQuality(settings.quality)
        state.value = created.currentState
        presets.value = created.presets
        preset.value = created.preset ?? ''
        quality.value = created.quality
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

    /** Whether the preset can be shown now. */
    function hasPreset(id: string): boolean {
        return controller !== null && presets.value.some((item) => item.id === id)
    }

    /** Shows a preset of the module. */
    function selectPreset(id: string): void {
        if (hasPreset(id)) {
            controller?.selectPreset(id)
        }
    }

    function onVisibilityChange(): void {
        pageVisible.value = document.visibilityState !== 'hidden'
    }

    watch(visible, (value) => controller?.setVisible(value))
    watch(
        () => input.audio(),
        () => controller?.setAudio(audioNode()),
    )
    watch(canvas, (value) => controller?.setCanvas(value), { flush: 'post' })
    watch(
        () => [input.settings().autoChange, input.settings().autoChangeSeconds] as const,
        ([enabled, seconds]) => controller?.setAutoChange({ enabled, seconds }),
    )
    watch(
        () => input.settings().preset,
        (value) => controller?.setStoredPreset(value),
    )
    watch(
        () => input.settings().quality,
        (value) => controller?.setQuality(value),
    )
    // Another module: the visualization starts again with it.
    watch(
        () => input.settings().module,
        () => {
            stop()
            canvasKey.value++
            start()
        },
    )

    onMounted(() => {
        const element = container.value!
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
        document.removeEventListener('visibilitychange', onVisibilityChange)
        resizeObserver?.disconnect()
        resizeObserver = null
        stop()
    })

    return { canvasKey, state, presets, preset, quality, visible, hasPreset, selectPreset }
}
