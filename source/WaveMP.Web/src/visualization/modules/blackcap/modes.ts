// The modes of BlackCap: the forms and the motion of the floating shapes, the presets of the module.
// A mode is a record of numbers: the form of the surfaces (lobes, waves, stretch, how far the
// spectrum lifts them), the motion of the shapes (how many there are, their sizes and paths) and
// how strongly they react to the music. A change of mode blends every number from the mode shown
// at that moment into the next one, so the shapes morph into their new forms; the blend takes
// longer than for other visualizations to keep BlackCap calm. The colors and the material are not
// part of a mode; they are the color schemes (schemes.ts). Pure.

import { blendAmount, LookTransition, mixNumber, mixRecord } from '../../blend'

/** Amplitudes of the four lobe patterns of a surface (see the vertex shader of the shapes). */
export type Lobes = readonly [number, number, number, number]

/** A stretch of a shape along x, y and z. */
export type Stretch = readonly [number, number, number]

/** The form of the surfaces of a mode. */
export interface ModeForm {
    /**
     * Amplitudes of the lobe patterns: two lobes around, three twisting lobes, five spiralling
     * petals and waves from top to bottom.
     */
    readonly lobes: Lobes
    /** How fast the lobes turn about the shape at rest (radians per second). */
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

/** The motion of the shapes of a mode. */
export interface ModeMotion {
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
    /** How much the shapes breathe with the tempo. */
    readonly breath: number
    /** How far the camera drifts about the scene. */
    readonly drift: number
}

/** How strongly a mode reacts to the music. */
export interface ModeReaction {
    /** Bass: how far it swells the surfaces from the bottom and the shapes as a whole. */
    readonly lift: number
    /** Beats: how far the shapes swell on a beat (share of their size). */
    readonly swell: number
    /** Mids: how much faster the lobes turn and twist. */
    readonly twist: number
    /** Highs: fine ripples and glints at the top of the surfaces. */
    readonly ripple: number
    /** Loudness and beats: how far the other shapes move away from the middle. */
    readonly spread: number
    /** Loudness: how far the lobes open (petals of a bloom); 0 keeps them as they are. */
    readonly open: number
    /** Quiet passages: how much the shapes contract, slow down and dim, 0 to 1. */
    readonly calm: number
}

export interface ModeLook {
    readonly form: ModeForm
    readonly motion: ModeMotion
    readonly reaction: ModeReaction
}

export interface BlackCapMode extends ModeLook {
    /** Stable id; the settings of the profile keep it. */
    readonly id: string
    /** Name in English, for the case a message is missing. */
    readonly name: string
    /** Message key of the name. */
    readonly labelKey: string
}

/** Most shapes a mode shows. */
export const maxModeShapes = 6

/** The modes in the order the visualization offers them. */
export const blackcapModes: readonly BlackCapMode[] = [
    {
        // One large shape breathing in the middle.
        id: 'drift',
        name: 'Drift',
        labelKey: 'nowPlaying.blackcapModes.drift',
        form: {
            lobes: [0.12, 0.08, 0, 0.05],
            lobeSpin: 0.15,
            waves: 0.1,
            waveScale: 1,
            spectrum: 0.6,
            stretch: [1, 1, 1],
        },
        motion: {
            shapes: 1,
            size: 1.3,
            companionSize: 0.35,
            orbit: 2.6,
            orbitSpeed: 0.08,
            spin: 0.12,
            float: 0.15,
            flow: 0.35,
            breath: 0.05,
            drift: 0.6,
        },
        reaction: { lift: 1.2, swell: 0.15, twist: 1, ripple: 1, spread: 0.5, open: 0.2, calm: 1 },
    },
    {
        // A shape in the middle with companions circling it; their distance pulses with beats.
        id: 'orbit',
        name: 'Orbit',
        labelKey: 'nowPlaying.blackcapModes.orbit',
        form: {
            lobes: [0.06, 0.04, 0, 0.03],
            lobeSpin: 0.3,
            waves: 0.12,
            waveScale: 0.9,
            spectrum: 0.5,
            stretch: [1, 1, 1],
        },
        motion: {
            shapes: 4,
            size: 0.95,
            companionSize: 0.42,
            orbit: 2.3,
            orbitSpeed: 0.24,
            spin: 0.2,
            float: 0.3,
            flow: 0.45,
            breath: 0.05,
            drift: 0.7,
        },
        reaction: { lift: 1, swell: 0.14, twist: 1, ripple: 1, spread: 1, open: 0.1, calm: 1 },
    },
    {
        // Six smaller shapes that gather in quiet parts and drift apart when it gets loud.
        id: 'cluster',
        name: 'Cluster',
        labelKey: 'nowPlaying.blackcapModes.cluster',
        form: {
            lobes: [0.04, 0.05, 0.03, 0.08],
            lobeSpin: 0.2,
            waves: 0.14,
            waveScale: 1.1,
            spectrum: 0.55,
            stretch: [1, 1.1, 1],
        },
        motion: {
            shapes: 6,
            size: 0.78,
            companionSize: 0.65,
            orbit: 2.1,
            orbitSpeed: 0.13,
            spin: 0.16,
            float: 0.45,
            flow: 0.4,
            breath: 0.06,
            drift: 0.9,
        },
        reaction: { lift: 1, swell: 0.13, twist: 1, ripple: 1, spread: 1.4, open: 0.1, calm: 1 },
    },
    {
        // Stretched, ring-like forms whose lobes the mids twist.
        id: 'ribbon',
        name: 'Ribbon',
        labelKey: 'nowPlaying.blackcapModes.ribbon',
        form: {
            lobes: [0.18, 0.15, 0, 0],
            lobeSpin: 0.25,
            waves: 0.08,
            waveScale: 1.1,
            spectrum: 0.5,
            stretch: [1.45, 0.42, 1.45],
        },
        motion: {
            shapes: 3,
            size: 1.15,
            companionSize: 0.55,
            orbit: 2.4,
            orbitSpeed: 0.1,
            spin: 0.14,
            float: 0.25,
            flow: 0.3,
            breath: 0.06,
            drift: 0.6,
        },
        reaction: {
            lift: 1,
            swell: 0.12,
            twist: 1.8,
            ripple: 0.8,
            spread: 0.6,
            open: 0.2,
            calm: 1,
        },
    },
    {
        // Spiralling petal lobes that open with the loudness and close in quiet passages.
        id: 'bloom',
        name: 'Bloom',
        labelKey: 'nowPlaying.blackcapModes.bloom',
        form: {
            lobes: [0, 0.06, 0.34, 0.04],
            lobeSpin: 0.2,
            waves: 0.08,
            waveScale: 1.2,
            spectrum: 0.55,
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
            breath: 0.06,
            drift: 0.5,
        },
        reaction: { lift: 1, swell: 0.15, twist: 1, ripple: 1, spread: 0.5, open: 1, calm: 1 },
    },
]

/** The mode with the id, or undefined. */
export function blackcapMode(id: string): BlackCapMode | undefined {
    return blackcapModes.find((mode) => mode.id === id)
}

/** Only the look of a mode (form, motion, reaction), without its id and names. */
export function modeLook(mode: ModeLook): ModeLook {
    return { form: mode.form, motion: mode.motion, reaction: mode.reaction }
}

function mixTuple<T extends readonly number[]>(a: T, b: T, t: number): T {
    return a.map((value, index) => mixNumber(value, b[index] ?? value, t)) as unknown as T
}

/** The look between a (t = 0) and b (t = 1); t outside is clamped. */
export function mixModes(a: ModeLook, b: ModeLook, t: number): ModeLook {
    const amount = blendAmount(t)
    const numbers = (x: number, y: number) => mixNumber(x, y, amount)
    return {
        form: mixRecord(a.form, b.form, (x: number | readonly number[], y: typeof x) =>
            typeof x === 'number'
                ? mixNumber(x, y as number, amount)
                : mixTuple(x, y as readonly number[], amount),
        ),
        motion: mixRecord(a.motion, b.motion, numbers),
        reaction: mixRecord(a.reaction, b.reaction, numbers),
    }
}

/** Shortest blend of a mode change in seconds (except the start, which shows a mode at once). */
export const minModeBlendSeconds = 2

/** How much longer a mode change blends than the visualization asks for, to stay calm. */
export const modeBlendFactor = 1.5

/**
 * Seconds a mode change blends for the blend seconds the visualization asks for: longer, at least
 * minModeBlendSeconds; 0 stays 0 (the first mode shows at once).
 */
export function modeBlendSeconds(blendSeconds: number): number {
    if (!(blendSeconds > 0)) {
        return 0
    }

    return Math.max(minModeBlendSeconds, blendSeconds * modeBlendFactor)
}

/** The look over time while the mode changes; see LookTransition. */
export class ModeTransition extends LookTransition<ModeLook> {
    constructor(initial: ModeLook) {
        super(initial, mixModes)
    }
}
