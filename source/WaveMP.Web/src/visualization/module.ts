// The interface of the visualization modules. A module draws the music of the player into a canvas
// with presets of its own: Milkdrop presets, the modes of A-Force or of
// BlackCap. A module may offer color schemes besides, a second list chosen apart from the presets.
// The stage knows modules only through this interface, so further modules plug in without changes
// to the stage, the controller or the settings (besides a value of the API enum
// VisualizationModule).

import type { VisualizationModule as VisualizationModuleId } from '@/api/generated/models'
import type { QualityLevel } from './quality'

export type { VisualizationModuleId }

/**
 * How strongly the visualization moves and reacts: normal, or reduced (the setting of the profile
 * or the wish of the device for reduced motion).
 */
export type VisualizationIntensityLevel = 'normal' | 'reduced'

/** A preset of a module: a look of the visualization the user can choose. */
export interface VisualizationPreset {
    /** Stable id; the settings of the profile keep it (at most maxPresetNameLength characters). */
    readonly id: string
    /** Name to show for presets that carry their own names (Milkdrop); these are not translated. */
    readonly name: string
    /**
     * Message key of the name for presets of the app itself (A-Force, BlackCap); shown instead of
     * name.
     */
    readonly labelKey?: string
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
    /** The level of detail to start with. */
    readonly quality: QualityLevel
    /** The color scheme to start with, for modules with color schemes; null for the first one. */
    readonly colorScheme: string | null
    /** How strongly the visualization moves and reacts. */
    readonly intensity: VisualizationIntensityLevel
    /** Speed of the presets in percent, for modules whose presets have a speed (Milkdrop). */
    readonly speed: number
}

/** A running visualization of a module in one canvas. */
export interface VisualizationRenderer {
    /** Shows a preset of the module, blending from the current one over the given seconds. */
    showPreset(id: string, blendSeconds: number): void
    /** Changes the size of the drawing buffer (device pixels). */
    resize(width: number, height: number): void
    /** Changes the level of detail. */
    setQuality(level: QualityLevel): void
    /** Shows a color scheme of the module, blending from the current one over the given seconds. */
    showColorScheme?(id: string, blendSeconds: number): void
    /** Changes how strongly the visualization moves and reacts, and how far it limits flashes. */
    setIntensity?(level: VisualizationIntensityLevel): void
    /** Changes the speed of the presets in percent (modules whose presets have a speed). */
    setSpeed?(percent: number): void
    /** Draws one frame; called once per animation frame while the visualization is visible. */
    render(): void
    /** Releases the drawing context and disconnects from the audio; the canvas is not used again. */
    dispose(): void
}

/** Seconds a new preset blends into the previous one, at the normal intensity. */
export interface PresetBlendSeconds {
    /** A preset the user chose. */
    readonly manual: number
    /** A preset of the automatic change. */
    readonly automatic: number
}

/** A module whose code and presets are loaded. */
export interface LoadedVisualizationModule {
    /** The presets of the module in the order to offer them; never empty. */
    readonly presets: readonly VisualizationPreset[]
    /**
     * The color schemes of the module in the order to offer them, chosen apart from the presets;
     * missing or empty for modules without color schemes.
     */
    readonly colorSchemes?: readonly VisualizationPreset[]
    /** How long its presets blend into each other; missing for the default of the controller. */
    readonly blendSeconds?: PresetBlendSeconds
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
