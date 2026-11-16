// The scenes of BlackCap: colors, forms and motion of the floating shapes, their light and the
// background. They are the presets of the module. A change of scene blends every value from the
// scene shown at that moment into the next one, so colors flow into each other and the shapes morph
// into their new forms; the blend takes longer than for other visualizations to keep BlackCap
// calm. Pure.

import { blendAmount, LookTransition, mixNumber, mixRecord, mixRgb, type Rgb } from '../../blend'

/** Amplitudes of the four lobe patterns of a surface (see the vertex shader of the shapes). */
export type Lobes = readonly [number, number, number, number]

/** A stretch of a shape along x, y and z. */
export type Stretch = readonly [number, number, number]

/** The colors of a scene. */
export interface SceneColors {
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

/** The form of the surfaces of a scene. */
export interface SceneForm {
    /**
     * Amplitudes of the lobe patterns: two lobes around, three twisting lobes, five spiralling
     * petals and waves from top to bottom.
     */
    readonly lobes: Lobes
    /** How fast the lobes turn about the shape. */
    readonly lobeSpin: number
    /** Height of the organic waves of the surface. */
    readonly waves: number
    /** Size of the organic waves: larger values give more, smaller waves. */
    readonly waveScale: number
    /** How far the spectrum lifts the surface, the bass at the bottom, the highs at the top. */
    readonly spectrum: number
    /** Stretch of the shapes along x, y and z. */
    readonly stretch: Stretch
}

/** The motion and the light of a scene. */
export interface SceneMotion {
    /** Number of shapes, from 1 (only the shape in the middle) to 6; a blend fades shapes in and out. */
    readonly shapes: number
    /** Size of the shape in the middle. */
    readonly size: number
    /** Size of the other shapes relative to the one in the middle. */
    readonly companionSize: number
    /** Distance of the other shapes from the middle. */
    readonly orbit: number
    /** How fast the other shapes float about the middle. */
    readonly orbitSpeed: number
    /** How fast the shapes turn about themselves. */
    readonly spin: number
    /** How far the shapes float up and down. */
    readonly float: number
    /** How fast the surfaces flow. */
    readonly flow: number
    /** How much the shapes swell on a beat. */
    readonly swell: number
    /** How much the shapes breathe with the tempo. */
    readonly breath: number
    /** How far the camera drifts about the scene. */
    readonly drift: number
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

export interface SceneLook {
    readonly colors: SceneColors
    readonly form: SceneForm
    readonly motion: SceneMotion
}

export interface BlackCapScene extends SceneLook {
    /** Stable id; the settings of the profile keep it. */
    readonly id: string
    /** Name in English, for the case a message is missing. */
    readonly name: string
    /** Message key of the name. */
    readonly labelKey: string
}

/** Most shapes a scene shows. */
export const maxSceneShapes = 6

/** The scenes in the order the visualization offers them. */
export const blackcapScenes: readonly BlackCapScene[] = [
    {
        id: 'lumen',
        name: 'Lumen',
        labelKey: 'nowPlaying.blackcapScenes.lumen',
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
        form: {
            lobes: [0.12, 0.08, 0, 0.05],
            lobeSpin: 0.15,
            waves: 0.1,
            waveScale: 1,
            spectrum: 0.22,
            stretch: [1, 1, 1],
        },
        motion: {
            shapes: 1,
            size: 1.35,
            companionSize: 0.35,
            orbit: 2.6,
            orbitSpeed: 0.08,
            spin: 0.12,
            float: 0.15,
            flow: 0.35,
            swell: 0.06,
            breath: 0.05,
            drift: 0.6,
            gloss: 70,
            mirror: 0.45,
            rim: 0.9,
            lights: 0.7,
            bloom: 0.9,
        },
    },
    {
        id: 'tide',
        name: 'Tide',
        labelKey: 'nowPlaying.blackcapScenes.tide',
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
        form: {
            lobes: [0.05, 0.12, 0.04, 0.12],
            lobeSpin: 0.25,
            waves: 0.14,
            waveScale: 1.2,
            spectrum: 0.28,
            stretch: [1.35, 0.55, 1.35],
        },
        motion: {
            shapes: 3,
            size: 1.05,
            companionSize: 0.6,
            orbit: 2.3,
            orbitSpeed: 0.12,
            spin: 0.18,
            float: 0.35,
            flow: 0.5,
            swell: 0.07,
            breath: 0.07,
            drift: 0.8,
            gloss: 40,
            mirror: 0.35,
            rim: 1,
            lights: 0.6,
            bloom: 0.8,
        },
    },
    {
        id: 'orchid',
        name: 'Orchid',
        labelKey: 'nowPlaying.blackcapScenes.orchid',
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
        form: {
            lobes: [0, 0.06, 0.32, 0.04],
            lobeSpin: 0.2,
            waves: 0.08,
            waveScale: 1.2,
            spectrum: 0.25,
            stretch: [1.1, 0.8, 1.1],
        },
        motion: {
            shapes: 2,
            size: 1.25,
            companionSize: 0.45,
            orbit: 2.5,
            orbitSpeed: 0.1,
            spin: 0.1,
            float: 0.2,
            flow: 0.3,
            swell: 0.08,
            breath: 0.06,
            drift: 0.5,
            gloss: 55,
            mirror: 0.3,
            rim: 1.1,
            lights: 0.65,
            bloom: 1.1,
        },
    },
    {
        id: 'mercury',
        name: 'Mercury',
        labelKey: 'nowPlaying.blackcapScenes.mercury',
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
        form: {
            lobes: [0.06, 0.04, 0, 0.03],
            lobeSpin: 0.3,
            waves: 0.12,
            waveScale: 0.9,
            spectrum: 0.18,
            stretch: [1, 1, 1],
        },
        motion: {
            shapes: 5,
            size: 0.95,
            companionSize: 0.5,
            orbit: 2.1,
            orbitSpeed: 0.16,
            spin: 0.2,
            float: 0.3,
            flow: 0.45,
            swell: 0.06,
            breath: 0.05,
            drift: 0.7,
            gloss: 120,
            mirror: 0.9,
            rim: 0.6,
            lights: 0.5,
            bloom: 0.9,
        },
    },
    {
        id: 'halo',
        name: 'Halo',
        labelKey: 'nowPlaying.blackcapScenes.halo',
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
        form: {
            lobes: [0.18, 0.15, 0, 0],
            lobeSpin: 0.12,
            waves: 0.08,
            waveScale: 1.1,
            spectrum: 0.2,
            stretch: [1.45, 0.42, 1.45],
        },
        motion: {
            shapes: 3,
            size: 1.15,
            companionSize: 0.55,
            orbit: 2.4,
            orbitSpeed: 0.09,
            spin: 0.14,
            float: 0.25,
            flow: 0.3,
            swell: 0.07,
            breath: 0.06,
            drift: 0.6,
            gloss: 80,
            mirror: 0.6,
            rim: 0.9,
            lights: 0.75,
            bloom: 1.2,
        },
    },
    {
        id: 'dusk',
        name: 'Dusk',
        labelKey: 'nowPlaying.blackcapScenes.dusk',
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
        form: {
            lobes: [0.04, 0.05, 0.03, 0.08],
            lobeSpin: 0.2,
            waves: 0.14,
            waveScale: 1.1,
            spectrum: 0.3,
            stretch: [1, 1.1, 1],
        },
        motion: {
            shapes: 6,
            size: 0.8,
            companionSize: 0.6,
            orbit: 2.2,
            orbitSpeed: 0.13,
            spin: 0.16,
            float: 0.45,
            flow: 0.4,
            swell: 0.08,
            breath: 0.08,
            drift: 0.9,
            gloss: 50,
            mirror: 0.4,
            rim: 1,
            lights: 0.8,
            bloom: 1,
        },
    },
]

/** The scene with the id, or undefined. */
export function blackcapScene(id: string): BlackCapScene | undefined {
    return blackcapScenes.find((scene) => scene.id === id)
}

function mixTuple<T extends readonly number[]>(a: T, b: T, t: number): T {
    return a.map((value, index) => mixNumber(value, b[index] ?? value, t)) as unknown as T
}

/** The look between a (t = 0) and b (t = 1); t outside is clamped. */
export function mixScenes(a: SceneLook, b: SceneLook, t: number): SceneLook {
    const amount = blendAmount(t)
    return {
        colors: mixRecord(a.colors, b.colors, (x: Rgb, y: Rgb) => mixRgb(x, y, amount)),
        form: mixRecord(a.form, b.form, (x: number | readonly number[], y: typeof x) =>
            typeof x === 'number'
                ? mixNumber(x, y as number, amount)
                : mixTuple(x, y as readonly number[], amount),
        ),
        motion: mixRecord(a.motion, b.motion, (x: number, y: number) => mixNumber(x, y, amount)),
    }
}

/** Shortest blend of a scene change in seconds (except the start, which shows a scene at once). */
export const minSceneBlendSeconds = 2

/** How much longer a scene change blends than the visualization asks for, to stay calm. */
export const sceneBlendFactor = 1.5

/**
 * Seconds a scene change blends for the blend seconds the visualization asks for: longer, at least
 * minSceneBlendSeconds; 0 stays 0 (the first scene shows at once).
 */
export function sceneBlendSeconds(blendSeconds: number): number {
    if (!(blendSeconds > 0)) {
        return 0
    }

    return Math.max(minSceneBlendSeconds, blendSeconds * sceneBlendFactor)
}

/** The look over time while the scene changes; see LookTransition. */
export class SceneTransition extends LookTransition<SceneLook> {
    constructor(initial: SceneLook) {
        super(initial, mixScenes)
    }
}
