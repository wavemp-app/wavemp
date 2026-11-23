// The modes of A-Force: the shapes and the motion of the picture, the presets of the module. A mode
// is a record of numbers: how much of each layer it shows (flowing waveforms, the particle tunnel,
// rings of the spectrum, streaks flying at the viewer, a landscape of the spectrum, a helix of two
// waveforms), the geometry and motion of the layers and how strongly they react to the music. A
// change of mode blends every number, so the layers cross-fade and the shapes morph into each
// other. The colors are not part of a mode; they are the color schemes (schemes.ts). Pure.

import { blendAmount, LookTransition, mixNumber, mixRecord } from '../../blend'

export { ease } from '../../blend'

/** How much of each layer a mode shows, 0 (not drawn) to 1. */
export interface ModeLayers {
    /** Waveforms of the last moments flowing into the depth. */
    readonly waves: number
    /** Particles flying through a swirling tunnel. */
    readonly particles: number
    /** Concentric rings of the spectrum around the middle. */
    readonly rings: number
    /** Particles as streaks flying at the viewer. */
    readonly streaks: number
    /** The spectrum of the last moments as ridges of a landscape. */
    readonly terrain: number
    /** Two waveforms twisted into a helix around the depth. */
    readonly helix: number
}

/** Geometry and motion of the layers and of the camera. */
export interface ModeMotion {
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
    /** How fast the scene moves towards the viewer. */
    readonly speed: number
    /** Share of the previous frame that stays per frame of 60 Hz (trails); 0 leaves none. */
    readonly trail: number
    /** How much the trails grow outwards per frame of 60 Hz. */
    readonly trailZoom: number
    /** How much the trails turn per frame of 60 Hz (radians). */
    readonly trailTurn: number
    /** How far the camera orbits around the scene. */
    readonly orbit: number
    /** How much the camera sways with the mids. */
    readonly sway: number
    /** How far the camera jumps towards the scene on a beat. */
    readonly punch: number
    /** How far the camera looks down on the scene (radians). */
    readonly pitch: number
    /** Strength of the glow around bright parts. */
    readonly bloom: number
    /** Outer radius of the rings (share of the height of the picture). */
    readonly ringRadius: number
    /** Length of the streaks. */
    readonly streakLength: number
    /** Radius of the tunnel the streaks fly through. */
    readonly streakSpread: number
    /** Height of the ridges. */
    readonly terrainHeight: number
    /** Turns of the helix along its length. */
    readonly helixTurns: number
    /** Radius of the helix. */
    readonly helixRadius: number
}

/** How strongly a mode reacts to the music. */
export interface ModeReaction {
    /** Bass: size and swing of the shapes (mass). */
    readonly mass: number
    /** Bass and beats: speed of the scene. */
    readonly drive: number
    /** Mids: sway of the shapes and the camera. */
    readonly sway: number
    /** Mids: twist and turn of the shapes (radians per second at full mids). */
    readonly twist: number
    /** Highs: sparkle and fine detail. */
    readonly sparkle: number
    /** Beats: punch with a short afterglow. */
    readonly punch: number
    /** Quiet passages: how much the scene slows down, dims and widens its trails, 0 to 1. */
    readonly calm: number
}

export interface ModeLook {
    readonly layers: ModeLayers
    readonly motion: ModeMotion
    readonly reaction: ModeReaction
}

export interface AForceMode extends ModeLook {
    /** Stable id; the settings of the profile keep it. */
    readonly id: string
    /** Name in English, for the case a message is missing. */
    readonly name: string
    /** Message key of the name. */
    readonly labelKey: string
}

/** Motion of the layers a mode does not show, so blends between modes stay calm. */
const restingMotion: ModeMotion = {
    waveAmplitude: 1,
    waveSpread: 1.6,
    waveThickness: 0.06,
    waveFlow: 1,
    particleSpread: 3.2,
    particleSwirl: 0.6,
    particleSize: 1,
    speed: 1,
    trail: 0.88,
    trailZoom: 0.004,
    trailTurn: 0,
    orbit: 0.35,
    sway: 1,
    punch: 0.8,
    pitch: 0.06,
    bloom: 1,
    ringRadius: 0.62,
    streakLength: 1,
    streakSpread: 4,
    terrainHeight: 1,
    helixTurns: 1.5,
    helixRadius: 1,
}

const noLayers: ModeLayers = {
    waves: 0,
    particles: 0,
    rings: 0,
    streaks: 0,
    terrain: 0,
    helix: 0,
}

/** The modes in the order the visualization offers them. */
export const aforceModes: readonly AForceMode[] = [
    {
        id: 'flow',
        name: 'Flow',
        labelKey: 'nowPlaying.aforceModes.flow',
        layers: { ...noLayers, waves: 1, particles: 1 },
        motion: {
            ...restingMotion,
            waveAmplitude: 1.2,
            waveSpread: 1.6,
            waveThickness: 0.06,
            waveFlow: 1,
            particleSpread: 3.2,
            particleSwirl: 0.7,
            speed: 1,
            trail: 0.9,
            trailZoom: 0.004,
            trailTurn: 0.001,
            orbit: 0.35,
            sway: 1,
            punch: 0.9,
            bloom: 0.9,
        },
        reaction: {
            mass: 1.1,
            drive: 1.3,
            sway: 1,
            twist: 0.6,
            sparkle: 1.1,
            punch: 1,
            calm: 0.7,
        },
    },
    {
        id: 'rings',
        name: 'Rings',
        labelKey: 'nowPlaying.aforceModes.rings',
        layers: { ...noLayers, rings: 1, particles: 0.35 },
        motion: {
            ...restingMotion,
            particleSpread: 4,
            particleSwirl: 1.2,
            particleSize: 0.8,
            speed: 0.7,
            trail: 0.86,
            trailZoom: 0.012,
            trailTurn: 0,
            orbit: 0.08,
            sway: 0.4,
            punch: 0.5,
            pitch: 0,
            bloom: 1.1,
            ringRadius: 0.62,
        },
        reaction: {
            mass: 1.2,
            drive: 1,
            sway: 0.8,
            twist: 1.2,
            sparkle: 1.2,
            punch: 1,
            calm: 0.7,
        },
    },
    {
        id: 'warp',
        name: 'Warp',
        labelKey: 'nowPlaying.aforceModes.warp',
        layers: { ...noLayers, streaks: 1, waves: 0.4 },
        motion: {
            ...restingMotion,
            waveAmplitude: 0.35,
            waveSpread: 0.05,
            waveThickness: 0.025,
            waveFlow: 0.4,
            speed: 1.6,
            trail: 0.8,
            trailZoom: 0.02,
            trailTurn: 0,
            orbit: 0.15,
            sway: 0.6,
            punch: 1.1,
            pitch: 0.02,
            bloom: 1,
            streakLength: 1.2,
            streakSpread: 4.2,
        },
        reaction: {
            mass: 0.9,
            drive: 1.8,
            sway: 0.7,
            twist: 0.4,
            sparkle: 1.3,
            punch: 1.2,
            calm: 0.8,
        },
    },
    {
        id: 'ridges',
        name: 'Ridges',
        labelKey: 'nowPlaying.aforceModes.ridges',
        layers: { ...noLayers, terrain: 1, particles: 0.25 },
        motion: {
            ...restingMotion,
            particleSpread: 4.5,
            particleSwirl: 0.3,
            particleSize: 0.7,
            speed: 0.9,
            trail: 0.72,
            trailZoom: 0.001,
            trailTurn: 0,
            orbit: 0.2,
            sway: 0.7,
            punch: 0.6,
            pitch: 0.16,
            bloom: 0.9,
            terrainHeight: 1.1,
        },
        reaction: {
            mass: 1.2,
            drive: 1,
            sway: 0.8,
            twist: 0.5,
            sparkle: 1.2,
            punch: 1.1,
            calm: 0.7,
        },
    },
    {
        id: 'helix',
        name: 'Helix',
        labelKey: 'nowPlaying.aforceModes.helix',
        layers: { ...noLayers, helix: 1, particles: 0.5 },
        motion: {
            ...restingMotion,
            particleSpread: 2.6,
            particleSwirl: 1.6,
            particleSize: 0.9,
            speed: 0.9,
            trail: 0.88,
            trailZoom: 0.003,
            trailTurn: 0.004,
            orbit: 0.12,
            sway: 0.8,
            punch: 0.8,
            pitch: 0.02,
            bloom: 1.1,
            helixTurns: 1.6,
            helixRadius: 1,
        },
        reaction: {
            mass: 1,
            drive: 1,
            sway: 0.9,
            twist: 1.6,
            sparkle: 1.2,
            punch: 1,
            calm: 0.7,
        },
    },
]

/** The mode with the id, or undefined. */
export function aforceMode(id: string): AForceMode | undefined {
    return aforceModes.find((mode) => mode.id === id)
}

/** The look of a mode, without its names (what a blend mixes). */
export function modeLook(mode: ModeLook): ModeLook {
    return { layers: mode.layers, motion: mode.motion, reaction: mode.reaction }
}

/** The mode between a (t = 0) and b (t = 1); t outside is clamped. */
export function mixModes(a: ModeLook, b: ModeLook, t: number): ModeLook {
    const amount = blendAmount(t)
    const mix = (x: number, y: number) => mixNumber(x, y, amount)
    return {
        layers: mixRecord(a.layers, b.layers, mix),
        motion: mixRecord(a.motion, b.motion, mix),
        reaction: mixRecord(a.reaction, b.reaction, mix),
    }
}

/**
 * The mode over time while it changes: from the look shown at the moment of the change into the
 * new mode, eased over the blend seconds; the layers cross-fade and the shapes morph.
 */
export class ModeTransition extends LookTransition<ModeLook> {
    constructor(initial: ModeLook) {
        super(initial, mixModes)
    }
}
