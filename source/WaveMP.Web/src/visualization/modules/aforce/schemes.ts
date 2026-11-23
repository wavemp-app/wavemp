// The color schemes of A-Force: the colors of the background, the glow, the waveforms, rings,
// ridges and helix and of the particles and streaks. They are chosen apart from the modes (the
// shapes and motion, modes.ts) and kept when the mode changes; a change of scheme blends every
// color smoothly. Pure.

import { blendAmount, LookTransition, mixRecord, mixRgb, type Rgb } from '../../blend'

export type { Rgb } from '../../blend'

/** The colors of a scheme. */
export interface SchemeColors {
    /** Background at the top and at the bottom. */
    readonly backgroundTop: Rgb
    readonly backgroundBottom: Rgb
    /** Glow in the middle of the background, which breathes with the bass and the beats. */
    readonly glow: Rgb
    /**
     * The lines: waveforms from their start over their end to the farthest one; the inner over the
     * outer rings; the near over the far ridges; the two strands of the helix.
     */
    readonly waveStart: Rgb
    readonly waveEnd: Rgb
    readonly waveFar: Rgb
    /** The particles and streaks: two base colors and the sparks that light up with the highs. */
    readonly particleA: Rgb
    readonly particleB: Rgb
    readonly spark: Rgb
}

export interface AForceColorScheme {
    /** Stable id; the settings of the profile keep it. */
    readonly id: string
    /** Name in English, for the case a message is missing. */
    readonly name: string
    /** Message key of the name. */
    readonly labelKey: string
    readonly colors: SchemeColors
}

/** The color schemes in the order the visualization offers them. */
export const aforceColorSchemes: readonly AForceColorScheme[] = [
    {
        id: 'aurora',
        name: 'Aurora',
        labelKey: 'nowPlaying.aforceColorSchemes.aurora',
        colors: {
            backgroundTop: [0.01, 0.06, 0.1],
            backgroundBottom: [0, 0.01, 0.03],
            glow: [0.1, 0.75, 0.6],
            waveStart: [0.2, 1, 0.7],
            waveEnd: [0.3, 0.6, 1],
            waveFar: [0.65, 0.3, 1],
            particleA: [0.5, 1, 0.8],
            particleB: [0.4, 0.7, 1],
            spark: [1, 1, 1],
        },
    },
    {
        id: 'nebula',
        name: 'Nebula',
        labelKey: 'nowPlaying.aforceColorSchemes.nebula',
        colors: {
            backgroundTop: [0.07, 0.01, 0.11],
            backgroundBottom: [0.01, 0, 0.03],
            glow: [0.7, 0.2, 0.9],
            waveStart: [1, 0.3, 0.8],
            waveEnd: [0.5, 0.3, 1],
            waveFar: [0.2, 0.6, 1],
            particleA: [1, 0.5, 0.9],
            particleB: [0.6, 0.5, 1],
            spark: [1, 0.9, 1],
        },
    },
    {
        id: 'ember',
        name: 'Ember',
        labelKey: 'nowPlaying.aforceColorSchemes.ember',
        colors: {
            backgroundTop: [0.1, 0.03, 0],
            backgroundBottom: [0.02, 0, 0],
            glow: [1, 0.4, 0.1],
            waveStart: [1, 0.6, 0.1],
            waveEnd: [1, 0.25, 0.05],
            waveFar: [1, 0.85, 0.4],
            particleA: [1, 0.5, 0.1],
            particleB: [1, 0.3, 0.05],
            spark: [1, 0.95, 0.7],
        },
    },
    {
        id: 'lagoon',
        name: 'Lagoon',
        labelKey: 'nowPlaying.aforceColorSchemes.lagoon',
        colors: {
            backgroundTop: [0, 0.07, 0.1],
            backgroundBottom: [0, 0.015, 0.03],
            glow: [0.1, 0.6, 0.9],
            waveStart: [0.2, 0.85, 1],
            waveEnd: [0.1, 0.5, 0.9],
            waveFar: [0.6, 1, 0.9],
            particleA: [0.6, 0.95, 1],
            particleB: [0.2, 0.6, 1],
            spark: [1, 1, 1],
        },
    },
    {
        id: 'prism',
        name: 'Prism',
        labelKey: 'nowPlaying.aforceColorSchemes.prism',
        colors: {
            backgroundTop: [0.04, 0.04, 0.07],
            backgroundBottom: [0, 0, 0.01],
            glow: [0.8, 0.8, 1],
            waveStart: [1, 0.25, 0.35],
            waveEnd: [0.25, 1, 0.4],
            waveFar: [0.3, 0.4, 1],
            particleA: [1, 0.9, 0.3],
            particleB: [0.3, 0.9, 1],
            spark: [1, 0.4, 0.9],
        },
    },
    {
        id: 'eclipse',
        name: 'Eclipse',
        labelKey: 'nowPlaying.aforceColorSchemes.eclipse',
        colors: {
            backgroundTop: [0.02, 0.02, 0.03],
            backgroundBottom: [0, 0, 0],
            glow: [1, 0.75, 0.4],
            waveStart: [1, 0.8, 0.5],
            waveEnd: [0.9, 0.9, 1],
            waveFar: [0.6, 0.6, 0.7],
            particleA: [1, 0.85, 0.6],
            particleB: [0.8, 0.8, 0.9],
            spark: [1, 1, 1],
        },
    },
]

/** The color scheme with the id, or undefined. */
export function aforceColorScheme(id: string): AForceColorScheme | undefined {
    return aforceColorSchemes.find((scheme) => scheme.id === id)
}

/** The colors between a (t = 0) and b (t = 1); t outside is clamped. */
export function mixColors(a: SchemeColors, b: SchemeColors, t: number): SchemeColors {
    const amount = blendAmount(t)
    return mixRecord(a, b, (x: Rgb, y: Rgb) => mixRgb(x, y, amount))
}

/**
 * The colors over time while the scheme changes: from the colors shown at the moment of the change
 * into the new scheme, eased over the blend seconds.
 */
export class ColorTransition extends LookTransition<SchemeColors> {
    constructor(initial: SchemeColors) {
        super(initial, mixColors)
    }
}
