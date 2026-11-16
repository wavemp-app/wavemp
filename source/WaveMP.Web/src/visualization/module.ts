// The interface of the visualization modules. A module draws the music of the player into a canvas
// with presets of its own: Milkdrop presets, later the style variants of A-Force or the scenes of
// BlackCap. The stage knows modules only through this interface, so further modules plug in
// without changes to the stage, the controller or the settings (besides a value of the API enum
// VisualizationModule).

import type { VisualizationModule as VisualizationModuleId } from '@/api/generated/models'

export type { VisualizationModuleId }

/** A preset of a module: a look of the visualization the user can choose. */
export interface VisualizationPreset {
    /** Stable id; the settings of the profile keep it (at most maxPresetNameLength characters). */
    readonly id: string
    /** Name to show; presets carry their own names, they are not translated. */
    readonly name: string
}

export interface VisualizationRendererOptions {
    /** The canvas to draw into; the renderer owns its drawing context. */
    readonly canvas: HTMLCanvasElement
    /**
     * The output of the player: the analyser after volume and equalizer, so the visualization sees
     * the sound as it is heard. Its context is the audio context of the player.
     */
    readonly audio: AnalyserNode
    /** Size of the drawing buffer in device pixels. */
    readonly width: number
    readonly height: number
}

/** A running visualization of a module in one canvas. */
export interface VisualizationRenderer {
    /** Shows a preset of the module, blending from the current one over the given seconds. */
    showPreset(id: string, blendSeconds: number): void
    /** Changes the size of the drawing buffer (device pixels). */
    resize(width: number, height: number): void
    /** Draws one frame; called once per animation frame while the visualization is visible. */
    render(): void
    /** Releases the drawing context and disconnects from the audio; the canvas is not used again. */
    dispose(): void
}

/** A module whose code and presets are loaded. */
export interface LoadedVisualizationModule {
    /** The presets of the module in the order to offer them; never empty. */
    readonly presets: readonly VisualizationPreset[]
    createRenderer(options: VisualizationRendererOptions): VisualizationRenderer
}

/** A visualization module. Its code and presets load only when it is shown the first time. */
export interface VisualizationModule {
    readonly id: VisualizationModuleId
    /** Whether the browser can show the module (e.g. WebGL 2 and Web Audio). */
    isSupported(): boolean
    /** Loads code and presets of the module; the promise is shared by all callers. */
    load(): Promise<LoadedVisualizationModule>
}
