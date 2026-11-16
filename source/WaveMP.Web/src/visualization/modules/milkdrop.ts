// The module Milkdrop: Milkdrop presets drawn with WebGL 2 by butterchurn (MIT), with the presets of
// the package butterchurn-presets (MIT). Both are loaded on demand in a chunk of their own, so the
// app only loads them when the visualization is shown.

import type { Butterchurn, ButterchurnRendererOptions } from 'butterchurn'
import type { ButterchurnPresetPack } from 'butterchurn-presets'
import { VisualizationModuleObject } from '@/api/generated/models'
import type {
    LoadedVisualizationModule,
    VisualizationModule,
    VisualizationPreset,
    VisualizationRenderer,
    VisualizationRendererOptions,
} from '../module'
import { sortPresets } from '../presets'
import type { QualityLevel } from '../quality'
import { maxPresetNameLength } from '../settings'
import { supportsWebGl2Visualization } from '../support'

/** What the module needs from the browser and the packages; tests replace it. */
export interface MilkdropEnvironment {
    /** Whether WebGL 2 and Web Audio exist. */
    supported(): boolean
    loadButterchurn(): Promise<Butterchurn>
    loadPresets(): Promise<Record<string, object>>
}

/**
 * The export of a CommonJS package as an ES module: depending on the bundler the object itself or
 * its default, possibly twice wrapped.
 */
function exported<T>(module: unknown, has: (value: Record<string, unknown>) => boolean): T {
    let value = module as Record<string, unknown> | undefined
    for (let depth = 0; depth < 3 && value; depth++) {
        if (has(value)) {
            return value as T
        }

        value = value.default as Record<string, unknown> | undefined
    }

    throw new Error('The package has an unexpected export.')
}

export const browserMilkdropEnvironment: MilkdropEnvironment = {
    supported: supportsWebGl2Visualization,
    loadButterchurn: async () =>
        exported<Butterchurn>(
            await import('butterchurn'),
            (value) => typeof value.createVisualizer === 'function',
        ),
    loadPresets: async () =>
        exported<ButterchurnPresetPack>(
            await import('butterchurn-presets'),
            (value) => typeof value.getPresets === 'function',
        ).getPresets(),
}

/** The presets of a pack, sorted by name; names the API cannot keep are left out. */
export function milkdropPresets(pack: Record<string, object>): VisualizationPreset[] {
    return sortPresets(
        Object.keys(pack)
            .filter((name) => name.trim() !== '' && name.length <= maxPresetNameLength)
            .map((name) => ({ id: name, name })),
    )
}

/**
 * What the quality levels change: the resolution of the textures of butterchurn (the drawing
 * buffer keeps its size) and the mesh its per-vertex equations run on. High is the default of
 * butterchurn.
 */
export const milkdropQuality: Readonly<
    Record<
        QualityLevel,
        Required<Pick<ButterchurnRendererOptions, 'textureRatio' | 'meshWidth' | 'meshHeight'>>
    >
> = {
    low: { textureRatio: 0.5, meshWidth: 32, meshHeight: 24 },
    medium: { textureRatio: 0.75, meshWidth: 48, meshHeight: 36 },
    high: { textureRatio: 1, meshWidth: 48, meshHeight: 36 },
}

class MilkdropRenderer implements VisualizationRenderer {
    private readonly visualizer
    private readonly audio: AnalyserNode
    private readonly canvas: HTMLCanvasElement
    private quality: QualityLevel

    constructor(
        butterchurn: Butterchurn,
        private readonly pack: Record<string, object>,
        options: VisualizationRendererOptions,
    ) {
        this.canvas = options.canvas
        this.audio = options.audio
        this.canvas.width = options.width
        this.canvas.height = options.height
        this.quality = options.quality
        // The size is given in device pixels; butterchurn should not multiply it once more.
        this.visualizer = butterchurn.createVisualizer(options.audio.context, options.canvas, {
            width: options.width,
            height: options.height,
            pixelRatio: 1,
            ...milkdropQuality[options.quality],
        })
        this.visualizer.connectAudio(options.audio)
    }

    showPreset(id: string, blendSeconds: number): void {
        const preset = this.pack[id]
        if (preset) {
            this.visualizer.loadPreset(preset, blendSeconds)
        }
    }

    resize(width: number, height: number): void {
        this.canvas.width = width
        this.canvas.height = height
        this.visualizer.setRendererSize(width, height, {
            pixelRatio: 1,
            ...milkdropQuality[this.quality],
        })
    }

    setQuality(level: QualityLevel): void {
        if (level !== this.quality) {
            this.quality = level
            this.resize(this.canvas.width, this.canvas.height)
        }
    }

    render(): void {
        this.visualizer.render()
    }

    dispose(): void {
        try {
            this.visualizer.disconnectAudio(this.audio)
        } catch {
            // Already disconnected.
        }

        // Frees the WebGL context at once instead of waiting for the garbage collector; browsers
        // only keep a few contexts.
        this.canvas.getContext('webgl2')?.getExtension('WEBGL_lose_context')?.loseContext()
    }
}

/** Creates the module Milkdrop; the app uses milkdropModule, tests pass their own environment. */
export function createMilkdropModule(
    environment: MilkdropEnvironment = browserMilkdropEnvironment,
): VisualizationModule {
    let supported: boolean | undefined
    let loading: Promise<LoadedVisualizationModule> | undefined

    async function load(): Promise<LoadedVisualizationModule> {
        const [butterchurn, pack] = await Promise.all([
            environment.loadButterchurn(),
            environment.loadPresets(),
        ])
        const presets = milkdropPresets(pack)
        return {
            presets,
            createRenderer: (options) => new MilkdropRenderer(butterchurn, pack, options),
        }
    }

    return {
        id: VisualizationModuleObject.Milkdrop,
        isSupported: () => (supported ??= environment.supported()),
        load: () => {
            loading ??= load().catch((error: unknown) => {
                // A later attempt (e.g. after a network error) loads again.
                loading = undefined
                throw error
            })
            return loading
        },
    }
}

export const milkdropModule = createMilkdropModule()
