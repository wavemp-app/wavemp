// Choice of presets. Pure functions.

import type { VisualizationPreset } from './module'

const collator = new Intl.Collator('en', { numeric: true, sensitivity: 'base' })

/** The presets sorted by name, ignoring case and with numbers in their natural order. */
export function sortPresets(presets: readonly VisualizationPreset[]): VisualizationPreset[] {
    return [...presets].sort((a, b) => collator.compare(a.name, b.name))
}

/**
 * The preset to start with: the stored one if the module has it, otherwise a random one (the
 * player picks one, as the API describes a missing or unknown preset).
 */
export function startPreset(
    presets: readonly VisualizationPreset[],
    stored: string | null,
    random: () => number,
): VisualizationPreset | undefined {
    return presets.find((preset) => preset.id === stored) ?? randomPreset(presets, null, random)
}

/** A random preset other than the current one (the current one only if it is the only preset). */
export function randomPreset(
    presets: readonly VisualizationPreset[],
    current: string | null,
    random: () => number,
): VisualizationPreset | undefined {
    const others = presets.filter((preset) => preset.id !== current)
    const choices = others.length > 0 ? others : presets
    if (choices.length === 0) {
        return undefined
    }

    const index = Math.min(choices.length - 1, Math.floor(random() * choices.length))
    return choices[Math.max(0, index)]
}

/** The preset before (-1) or after (1) the current one in the list, wrapping at both ends. */
export function adjacentPreset<T extends { readonly id: string }>(
    presets: readonly T[],
    current: string | null,
    offset: -1 | 1,
): T | undefined {
    if (presets.length === 0) {
        return undefined
    }

    const index = presets.findIndex((preset) => preset.id === current)
    if (index < 0) {
        return offset > 0 ? presets[0] : presets[presets.length - 1]
    }

    return presets[(index + offset + presets.length) % presets.length]
}
