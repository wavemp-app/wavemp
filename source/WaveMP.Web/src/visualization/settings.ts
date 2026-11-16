// The visualization settings of a profile. Pure functions.

import { VisualizationModuleObject, type VisualizationModule } from '@/api/generated/models'

/** Shortest interval of the automatic change in seconds, as the API allows it. */
export const minAutoChangeSeconds = 5

/** Longest interval of the automatic change in seconds, as the API allows it. */
export const maxAutoChangeSeconds = 600

/** Longest name of a preset the API keeps. */
export const maxPresetNameLength = 200

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
}

export const defaultVisualizationSettings: VisualizationSettings = {
    module: VisualizationModuleObject.Milkdrop,
    preset: null,
    autoChange: true,
    autoChangeSeconds: 30,
}

const modules = new Set<string>(Object.values(VisualizationModuleObject))

/**
 * The visualization settings from an answer of the server, or null if they are incomplete. A module
 * this app does not know (from a newer server) falls back to the default module without a preset.
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
    return {
        module: known
            ? (settings.module as VisualizationModule)
            : defaultVisualizationSettings.module,
        preset,
        autoChange: settings.autoChange,
        autoChangeSeconds: settings.autoChangeSeconds,
    }
}

/** A change of the settings, as the API takes it: missing or null values stay unchanged. */
export interface VisualizationSettingsChange {
    readonly module?: VisualizationModule | null
    readonly preset?: string | null
    readonly autoChange?: boolean | null
    readonly autoChangeSeconds?: number | null
}

/**
 * The settings after a change, as the server applies it: an empty preset forgets the preset, and
 * another module starts without one unless the change names one.
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

    return {
        module,
        preset,
        autoChange: change.autoChange ?? settings.autoChange,
        autoChangeSeconds: change.autoChangeSeconds ?? settings.autoChangeSeconds,
    }
}
