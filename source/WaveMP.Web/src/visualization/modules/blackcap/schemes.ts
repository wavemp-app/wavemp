// The color schemes of BlackCap: the colors of the background, the floating lights and the shapes
// and the material of the shapes (how glossy they are, how much they mirror their surroundings and
// how strongly their edges and the lights shine). They are chosen apart from the modes (the forms
// and motion, modes.ts) and kept when the mode changes; a change of scheme blends every value
// smoothly. Pure.

import { blendAmount, LookTransition, mixNumber, mixRecord, mixRgb, type Rgb } from '../../blend'

export type { Rgb } from '../../blend'

/** The colors of a scheme. */
export interface SchemeColors {
    /** Background at the top and at the bottom. */
    readonly backgroundTop: Rgb
    readonly backgroundBottom: Rgb
    /** The soft lights floating in the background. */
    readonly light: Rgb
    /** The gradient across the surfaces. */
    readonly surfaceA: Rgb
    readonly surfaceB: Rgb
    /** The light at the edges of the shapes. */
    readonly rim: Rgb
    /** The reflections of the lights on the surfaces. */
    readonly highlight: Rgb
    /** The glow of a beat and the tint of the reflected surroundings. */
    readonly glow: Rgb
}

/** The material of the shapes and the strength of the lights. */
export interface SchemeMaterial {
    /** Sharpness of the reflections of the lights (a shininess exponent). */
    readonly gloss: number
    /** How much the surfaces mirror their surroundings, 0 to 1. */
    readonly mirror: number
    /** Strength of the light at the edges. */
    readonly rim: number
    /** Brightness of the floating lights in the background. */
    readonly lights: number
    /** Strength of the glow around bright parts. */
    readonly bloom: number
}

export interface SchemeLook {
    readonly colors: SchemeColors
    readonly material: SchemeMaterial
}

export interface BlackCapColorScheme extends SchemeLook {
    /** Stable id; the settings of the profile keep it. */
    readonly id: string
    /** Name in English, for the case a message is missing. */
    readonly name: string
    /** Message key of the name. */
    readonly labelKey: string
}

/** The color schemes in the order the visualization offers them. */
export const blackcapColorSchemes: readonly BlackCapColorScheme[] = [
    {
        id: 'lumen',
        name: 'Lumen',
        labelKey: 'nowPlaying.blackcapColorSchemes.lumen',
        colors: {
            backgroundTop: [0.05, 0.08, 0.14],
            backgroundBottom: [0, 0.01, 0.03],
            light: [0.55, 0.7, 1],
            surfaceA: [0.75, 0.82, 0.95],
            surfaceB: [0.35, 0.5, 0.85],
            rim: [0.6, 0.85, 1],
            highlight: [1, 1, 1],
            glow: [0.4, 0.6, 1],
        },
        material: { gloss: 70, mirror: 0.45, rim: 0.9, lights: 0.7, bloom: 0.9 },
    },
    {
        id: 'tide',
        name: 'Tide',
        labelKey: 'nowPlaying.blackcapColorSchemes.tide',
        colors: {
            backgroundTop: [0, 0.08, 0.1],
            backgroundBottom: [0, 0.01, 0.025],
            light: [0.2, 0.8, 0.8],
            surfaceA: [0.05, 0.45, 0.55],
            surfaceB: [0.15, 0.75, 0.65],
            rim: [0.4, 1, 0.9],
            highlight: [0.85, 1, 1],
            glow: [0.1, 0.7, 0.7],
        },
        material: { gloss: 40, mirror: 0.35, rim: 1, lights: 0.6, bloom: 0.8 },
    },
    {
        id: 'orchid',
        name: 'Orchid',
        labelKey: 'nowPlaying.blackcapColorSchemes.orchid',
        colors: {
            backgroundTop: [0.08, 0.02, 0.1],
            backgroundBottom: [0.015, 0, 0.03],
            light: [0.9, 0.4, 0.9],
            surfaceA: [0.85, 0.3, 0.7],
            surfaceB: [0.45, 0.2, 0.85],
            rim: [1, 0.6, 0.95],
            highlight: [1, 0.9, 1],
            glow: [0.8, 0.3, 0.9],
        },
        material: { gloss: 55, mirror: 0.3, rim: 1.1, lights: 0.65, bloom: 1.1 },
    },
    {
        id: 'mercury',
        name: 'Mercury',
        labelKey: 'nowPlaying.blackcapColorSchemes.mercury',
        colors: {
            backgroundTop: [0.06, 0.06, 0.08],
            backgroundBottom: [0.005, 0.005, 0.01],
            light: [0.8, 0.85, 1],
            surfaceA: [0.55, 0.57, 0.62],
            surfaceB: [0.25, 0.27, 0.33],
            rim: [0.85, 0.9, 1],
            highlight: [1, 1, 1],
            glow: [0.7, 0.75, 0.9],
        },
        material: { gloss: 120, mirror: 0.9, rim: 0.6, lights: 0.5, bloom: 0.9 },
    },
    {
        id: 'halo',
        name: 'Halo',
        labelKey: 'nowPlaying.blackcapColorSchemes.halo',
        colors: {
            backgroundTop: [0.1, 0.06, 0.02],
            backgroundBottom: [0.02, 0.01, 0],
            light: [1, 0.75, 0.35],
            surfaceA: [0.95, 0.7, 0.3],
            surfaceB: [0.7, 0.35, 0.1],
            rim: [1, 0.85, 0.5],
            highlight: [1, 0.95, 0.8],
            glow: [1, 0.6, 0.2],
        },
        material: { gloss: 80, mirror: 0.6, rim: 0.9, lights: 0.75, bloom: 1.2 },
    },
    {
        id: 'dusk',
        name: 'Dusk',
        labelKey: 'nowPlaying.blackcapColorSchemes.dusk',
        colors: {
            backgroundTop: [0.12, 0.04, 0.1],
            backgroundBottom: [0.02, 0.01, 0.04],
            light: [1, 0.55, 0.4],
            surfaceA: [1, 0.5, 0.35],
            surfaceB: [0.5, 0.25, 0.6],
            rim: [1, 0.75, 0.6],
            highlight: [1, 0.9, 0.85],
            glow: [1, 0.45, 0.45],
        },
        material: { gloss: 50, mirror: 0.4, rim: 1, lights: 0.8, bloom: 1 },
    },
]

/** The color scheme with the id, or undefined. */
export function blackcapColorScheme(id: string): BlackCapColorScheme | undefined {
    return blackcapColorSchemes.find((scheme) => scheme.id === id)
}

/** Only the look of a scheme (colors and material), without its id and names. */
export function schemeLook(scheme: SchemeLook): SchemeLook {
    return { colors: scheme.colors, material: scheme.material }
}

/** The look between a (t = 0) and b (t = 1); t outside is clamped. */
export function mixSchemes(a: SchemeLook, b: SchemeLook, t: number): SchemeLook {
    const amount = blendAmount(t)
    return {
        colors: mixRecord(a.colors, b.colors, (x: Rgb, y: Rgb) => mixRgb(x, y, amount)),
        material: mixRecord(a.material, b.material, (x: number, y: number) =>
            mixNumber(x, y, amount),
        ),
    }
}

/** The colors and the material over time while the scheme changes; see LookTransition. */
export class SchemeTransition extends LookTransition<SchemeLook> {
    constructor(initial: SchemeLook) {
        super(initial, mixSchemes)
    }
}
