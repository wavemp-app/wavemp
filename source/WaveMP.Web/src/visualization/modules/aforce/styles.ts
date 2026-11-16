// The style variants of A-Force: colors and motion of waveforms, particles, trails and camera. They
// are the presets of the module; a change of style blends every value smoothly from the current
// look into the next one. Pure.

import { blendAmount, LookTransition, mixNumber, mixRecord, mixRgb, type Rgb } from '../../blend'

export { ease, type Rgb } from '../../blend'

/** The colors of a look. */
export interface StyleColors {
    /** Background at the top and at the bottom. */
    readonly backgroundTop: Rgb
    readonly backgroundBottom: Rgb
    /** Glow in the middle of the background and the flash of a beat. */
    readonly glow: Rgb
    /** The waveforms from their start over their end to the farthest waveform. */
    readonly waveStart: Rgb
    readonly waveEnd: Rgb
    readonly waveFar: Rgb
    /** The particles: two base colors and the sparks that light up with the highs. */
    readonly particleA: Rgb
    readonly particleB: Rgb
    readonly spark: Rgb
}

/** The motion of a look. */
export interface StyleMotion {
    /** How far the waveforms swing with the music. */
    readonly waveAmplitude: number
    /** Distance between the waveforms from top to bottom. */
    readonly waveSpread: number
    /** Thickness of the waveforms. */
    readonly waveThickness: number
    /** How fast the waveforms flow sideways. */
    readonly waveFlow: number
    /** Radius of the tunnel the particles fly through. */
    readonly particleSpread: number
    /** How much the particles swirl around the middle. */
    readonly particleSwirl: number
    /** Size of the particles. */
    readonly particleSize: number
    /** How fast the particles fly towards the viewer. */
    readonly speed: number
    /** Share of the previous frame that stays per frame (trails); 0 leaves none. */
    readonly trail: number
    /** How much the trails grow outwards per frame. */
    readonly trailZoom: number
    /** How much the trails turn per frame (radians). */
    readonly trailTurn: number
    /** How far the camera orbits around the scene. */
    readonly orbit: number
    /** How much the camera sways with the mids. */
    readonly sway: number
    /** How far the camera jumps towards the scene on a beat. */
    readonly punch: number
    /** Strength of the glow around bright parts. */
    readonly bloom: number
}

export interface StyleLook {
    readonly colors: StyleColors
    readonly motion: StyleMotion
}

export interface AForceStyle extends StyleLook {
    /** Stable id; the settings of the profile keep it. */
    readonly id: string
    /** Name in English, for the case a message is missing. */
    readonly name: string
    /** Message key of the name. */
    readonly labelKey: string
}

/** The style variants in the order the visualization offers them. */
export const aforceStyles: readonly AForceStyle[] = [
    {
        id: 'aurora',
        name: 'Aurora',
        labelKey: 'nowPlaying.aforceStyles.aurora',
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
        motion: {
            waveAmplitude: 1.1,
            waveSpread: 1.6,
            waveThickness: 0.06,
            waveFlow: 1,
            particleSpread: 3.2,
            particleSwirl: 0.6,
            particleSize: 1,
            speed: 1,
            trail: 0.9,
            trailZoom: 0.004,
            trailTurn: 0.001,
            orbit: 0.35,
            sway: 1,
            punch: 0.8,
            bloom: 0.9,
        },
    },
    {
        id: 'nebula',
        name: 'Nebula',
        labelKey: 'nowPlaying.aforceStyles.nebula',
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
        motion: {
            waveAmplitude: 0.9,
            waveSpread: 2,
            waveThickness: 0.05,
            waveFlow: 0.6,
            particleSpread: 4,
            particleSwirl: 1.4,
            particleSize: 1.2,
            speed: 0.7,
            trail: 0.93,
            trailZoom: 0.006,
            trailTurn: 0.003,
            orbit: 0.5,
            sway: 0.8,
            punch: 0.6,
            bloom: 1.1,
        },
    },
    {
        id: 'ember',
        name: 'Ember',
        labelKey: 'nowPlaying.aforceStyles.ember',
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
        motion: {
            waveAmplitude: 1.4,
            waveSpread: 1.2,
            waveThickness: 0.08,
            waveFlow: 1.4,
            particleSpread: 2.6,
            particleSwirl: 0.3,
            particleSize: 0.9,
            speed: 1.5,
            trail: 0.86,
            trailZoom: 0.009,
            trailTurn: 0,
            orbit: 0.25,
            sway: 1.3,
            punch: 1.2,
            bloom: 1,
        },
    },
    {
        id: 'lagoon',
        name: 'Lagoon',
        labelKey: 'nowPlaying.aforceStyles.lagoon',
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
        motion: {
            waveAmplitude: 0.8,
            waveSpread: 1.8,
            waveThickness: 0.07,
            waveFlow: 0.5,
            particleSpread: 3.6,
            particleSwirl: 0.4,
            particleSize: 1.1,
            speed: 0.6,
            trail: 0.94,
            trailZoom: 0.002,
            trailTurn: -0.001,
            orbit: 0.6,
            sway: 0.6,
            punch: 0.5,
            bloom: 0.8,
        },
    },
    {
        id: 'prism',
        name: 'Prism',
        labelKey: 'nowPlaying.aforceStyles.prism',
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
        motion: {
            waveAmplitude: 1.2,
            waveSpread: 1.5,
            waveThickness: 0.05,
            waveFlow: 1.2,
            particleSpread: 3,
            particleSwirl: 1,
            particleSize: 1,
            speed: 1.2,
            trail: 0.88,
            trailZoom: 0.005,
            trailTurn: 0.004,
            orbit: 0.45,
            sway: 1.1,
            punch: 1,
            bloom: 1.2,
        },
    },
    {
        id: 'eclipse',
        name: 'Eclipse',
        labelKey: 'nowPlaying.aforceStyles.eclipse',
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
        motion: {
            waveAmplitude: 1,
            waveSpread: 1.4,
            waveThickness: 0.04,
            waveFlow: 0.8,
            particleSpread: 3.4,
            particleSwirl: 0.8,
            particleSize: 0.8,
            speed: 0.9,
            trail: 0.92,
            trailZoom: 0.003,
            trailTurn: -0.002,
            orbit: 0.3,
            sway: 0.7,
            punch: 0.9,
            bloom: 1.3,
        },
    },
]

/** The style with the id, or undefined. */
export function aforceStyle(id: string): AForceStyle | undefined {
    return aforceStyles.find((style) => style.id === id)
}

/** The look between a (t = 0) and b (t = 1); t outside is clamped. */
export function mixLooks(a: StyleLook, b: StyleLook, t: number): StyleLook {
    const amount = blendAmount(t)
    return {
        colors: mixRecord(a.colors, b.colors, (x: Rgb, y: Rgb) => mixRgb(x, y, amount)),
        motion: mixRecord(a.motion, b.motion, (x: number, y: number) => mixNumber(x, y, amount)),
    }
}

/**
 * The look over time while the style changes: from the look shown at the moment of the change into
 * the new style, eased over the blend seconds. A change during a blend starts from the look shown
 * at that moment, so nothing jumps.
 */
export class StyleTransition extends LookTransition<StyleLook> {
    constructor(initial: StyleLook) {
        super(initial, mixLooks)
    }
}
