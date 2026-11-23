// The visualization settings of a profile. Pure functions.

import {
    VisualizationIntensityObject,
    VisualizationModuleObject,
    VisualizationQualityObject,
    type VisualizationIntensity,
    type VisualizationModule,
    type VisualizationQuality,
} from '@/api/generated/models'

/** Shortest interval of the automatic change in seconds, as the API allows it. */
export const minAutoChangeSeconds = 5

/** Longest interval of the automatic change in seconds, as the API allows it. */
export const maxAutoChangeSeconds = 600

/** Longest name of a preset the API keeps. */
export const maxPresetNameLength = 200

/** Longest id of a color scheme the API keeps. */
export const maxColorSchemeLength = 64

/** Slowest speed of the Milkdrop presets in percent, as the API allows it. */
export const minMilkdropSpeed = 10

/** Fastest speed of the Milkdrop presets in percent, their original speed at 60 frames a second. */
export const maxMilkdropSpeed = 100

/** Speed of the Milkdrop presets in percent of a new profile. */
export const defaultMilkdropSpeed = 50

/** Speeds of the Milkdrop presets the settings offer, in percent. */
export const milkdropSpeedChoices: readonly number[] = [25, 50, 75, 100]

/** Intervals of the automatic change the settings offer, in seconds. */
export const autoChangeChoices: readonly number[] = [10, 15, 20, 30, 45, 60, 120, 300]

export interface VisualizationSettings {
    /** The visualization of the stage. */
    readonly module: VisualizationModule
    /** Name of the preset chosen last, or null to let the player pick one. */
    readonly preset: string | null
    /** Whether the preset changes automatically; off keeps the seconds. */
    readonly autoChange: boolean
    /** Seconds after which the preset changes while the automatic change is on. */
    readonly autoChangeSeconds: number
    /** How much detail the visualization draws; Auto chooses by the frame rate. */
    readonly quality: VisualizationQuality
    /** Id of the color scheme chosen for the module, or null for its first color scheme. */
    readonly colorScheme: string | null
    /** How strongly the visualization moves and reacts, as the profile chose it. */
    readonly intensity: VisualizationIntensity
    /** Speed of the Milkdrop presets in percent. */
    readonly milkdropSpeed: number
}

export const defaultVisualizationSettings: VisualizationSettings = {
    module: VisualizationModuleObject.Milkdrop,
    preset: null,
    autoChange: true,
    autoChangeSeconds: 30,
    quality: VisualizationQualityObject.Auto,
    colorScheme: null,
    intensity: VisualizationIntensityObject.Normal,
    milkdropSpeed: defaultMilkdropSpeed,
}

const modules = new Set<string>(Object.values(VisualizationModuleObject))

const qualities = new Set<string>(Object.values(VisualizationQualityObject))

const intensities = new Set<string>(Object.values(VisualizationIntensityObject))

/** Whether a speed of the Milkdrop presets lies within the range the API allows. */
export function isValidMilkdropSpeed(value: unknown): value is number {
    return (
        typeof value === 'number' &&
        Number.isInteger(value) &&
        value >= minMilkdropSpeed &&
        value <= maxMilkdropSpeed
    )
}

/**
 * The visualization settings from an answer of the server, or null if they are incomplete. A module
 * this app does not know (from a newer server) falls back to the default module without a preset
 * and color scheme, an unknown or missing quality to Auto, an unknown or missing intensity to
 * Normal and a missing or invalid Milkdrop speed to the default.
 */
export function toVisualizationSettings(value: unknown): VisualizationSettings | null {
    const settings = value as
        Partial<Record<keyof VisualizationSettings, unknown>> | null | undefined
    if (
        !settings ||
        typeof settings.autoChange !== 'boolean' ||
        typeof settings.autoChangeSeconds !== 'number' ||
        !Number.isInteger(settings.autoChangeSeconds) ||
        settings.autoChangeSeconds < minAutoChangeSeconds ||
        settings.autoChangeSeconds > maxAutoChangeSeconds
    ) {
        return null
    }

    const known = typeof settings.module === 'string' && modules.has(settings.module)
    const preset =
        known && typeof settings.preset === 'string' && settings.preset.trim() !== ''
            ? settings.preset
            : null
    const colorScheme =
        known && typeof settings.colorScheme === 'string' && settings.colorScheme.trim() !== ''
            ? settings.colorScheme
            : null
    return {
        module: known
            ? (settings.module as VisualizationModule)
            : defaultVisualizationSettings.module,
        preset,
        autoChange: settings.autoChange,
        autoChangeSeconds: settings.autoChangeSeconds,
        quality:
            typeof settings.quality === 'string' && qualities.has(settings.quality)
                ? (settings.quality as VisualizationQuality)
                : defaultVisualizationSettings.quality,
        colorScheme,
        intensity:
            typeof settings.intensity === 'string' && intensities.has(settings.intensity)
                ? (settings.intensity as VisualizationIntensity)
                : defaultVisualizationSettings.intensity,
        milkdropSpeed: isValidMilkdropSpeed(settings.milkdropSpeed)
            ? settings.milkdropSpeed
            : defaultVisualizationSettings.milkdropSpeed,
    }
}

/** A change of the settings, as the API takes it: missing or null values stay unchanged. */
export interface VisualizationSettingsChange {
    readonly module?: VisualizationModule | null
    readonly preset?: string | null
    readonly autoChange?: boolean | null
    readonly autoChangeSeconds?: number | null
    readonly quality?: VisualizationQuality | null
    readonly colorScheme?: string | null
    readonly intensity?: VisualizationIntensity | null
    readonly milkdropSpeed?: number | null
}

/**
 * The settings after a change, as the server applies it: an empty preset or color scheme forgets
 * it, and another module starts without both unless the change names them.
 */
export function applyVisualizationChange(
    settings: VisualizationSettings,
    change: VisualizationSettingsChange,
): VisualizationSettings {
    const module = change.module ?? settings.module
    let preset = module === settings.module ? settings.preset : null
    if (typeof change.preset === 'string') {
        preset = change.preset.trim() === '' ? null : change.preset
    }

    let colorScheme = module === settings.module ? settings.colorScheme : null
    if (typeof change.colorScheme === 'string') {
        colorScheme = change.colorScheme.trim() === '' ? null : change.colorScheme
    }

    return {
        module,
        preset,
        autoChange: change.autoChange ?? settings.autoChange,
        autoChangeSeconds: change.autoChangeSeconds ?? settings.autoChangeSeconds,
        quality: change.quality ?? settings.quality,
        colorScheme,
        intensity: change.intensity ?? settings.intensity,
        milkdropSpeed: change.milkdropSpeed ?? settings.milkdropSpeed,
    }
}

/**
 * The change with its effect on the preset and the color scheme spelled out: another module than
 * the current one also forgets them (an empty preset or color scheme), unless the change names
 * them. So changes made one after the other can be merged member by member and still have the same
 * effect as one after the other.
 */
export function explicitVisualizationChange(
    settings: VisualizationSettings,
    change: VisualizationSettingsChange,
): VisualizationSettingsChange {
    if (!change.module || change.module === settings.module) {
        return change
    }

    return {
        ...change,
        preset: typeof change.preset === 'string' ? change.preset : '',
        colorScheme: typeof change.colorScheme === 'string' ? change.colorScheme : '',
    }
}
