// How A-Force reacts to the music: from the look of the mode, the features of the sound and the
// intensity, the numbers of a frame that become the uniforms of its shaders.
// - bass: mass (size and swing of the shapes) and speed,
// - mids: sway and twist,
// - highs: sparkle and fine detail,
// - beats: a punch with a short afterglow (the pulse of the beat fading out),
// - quiet passages: slower, dimmer, longer trails.
// At the reduced intensity every gain is halved, the camera moves half as much and does not punch.
// Pure, so the tests measure the reaction on these inputs.

import type { AudioFeatures } from '../../analysis/features'
import type { VisualizationIntensityLevel } from '../../module'
import type { ModeLayers, ModeLook } from './modes'

/** The numbers of a frame of A-Force. */
export interface AForceFrameInputs {
    /** How much of each layer is drawn (from the mode). */
    readonly layers: ModeLayers
    /** How fast the scene moves towards the viewer (units of flow per second). */
    readonly flowSpeed: number
    /** How fast the shapes turn (radians per second). */
    readonly spin: number
    /** Size and swing of the shapes by the bass: 1 at rest. */
    readonly mass: number
    /** Sway of the shapes by the mids, 0 at rest. */
    readonly sway: number
    /** Sparkle of the highs, 0 at rest. */
    readonly sparkle: number
    /** The beat with its afterglow, 0 at rest. */
    readonly beat: number
    /** Brightness of the scene: 1 in loud parts, lower in quiet ones. */
    readonly brightness: number
    /** Strength of the glow of the background. */
    readonly glow: number

    /** Swing of the waveforms. */
    readonly waveAmplitude: number
    /** Sideways flow of the waveforms by the mids. */
    readonly waveSwing: number
    /** Thickness of the waveforms. */
    readonly waveThickness: number
    /** Radius of the particle tunnel as a factor. */
    readonly particleScale: number
    /** How far the inner rings swell with the bass. */
    readonly ringSwell: number
    /** Length of the streaks (from the loudness and the beats). */
    readonly streakLength: number
    /** Height of the ridges. */
    readonly terrainLift: number
    /** How far the front ridges rise on a beat. */
    readonly terrainFront: number
    /** Radius of the helix as a factor. */
    readonly helixScale: number
    /** Turns of the helix (a beat tightens the twist). */
    readonly helixTurns: number

    /** Trails: share of the previous frame that stays per frame of 60 Hz. */
    readonly trail: number
    /** Trails: growth per frame of 60 Hz. */
    readonly trailZoom: number
    /** Trails: turn per frame of 60 Hz. */
    readonly trailTurn: number

    /** The camera: orbit, sway, pitch and the punch of a beat (0 at the reduced intensity). */
    readonly cameraOrbit: number
    readonly cameraSway: number
    readonly cameraPitch: number
    readonly cameraMid: number
    readonly punch: number
    /** Strength of the bloom. */
    readonly bloom: number
}

/** Longest trail at all, so quiet parts never smear the picture into a blur. */
const longestTrail = 0.96

function clamp01(value: number): number {
    return Math.min(1, Math.max(0, Number.isFinite(value) ? value : 0))
}

/** The numbers of a frame for the look of the mode, the features of the sound and the intensity. */
export function aforceFrameInputs(
    look: ModeLook,
    features: AudioFeatures,
    intensity: VisualizationIntensityLevel,
): AForceFrameInputs {
    const { layers, motion, reaction } = look
    const reduced = intensity === 'reduced'
    const gain = reduced ? 0.5 : 1
    const bass = clamp01(features.bass)
    const mid = clamp01(features.mid)
    const treble = clamp01(features.treble)
    const level = clamp01(features.level)
    const pulse = clamp01(features.pulse)
    const quiet = clamp01(features.quiet) * clamp01(reaction.calm)

    const beat = pulse * gain
    const mass = 1 + gain * (bass * 0.8 + pulse * 0.4) * reaction.mass
    const sway = gain * mid * reaction.sway
    const sparkle = gain * treble * reaction.sparkle
    // Quiet passages slow the scene down to a drift and dim it.
    const slow = 1 - quiet * 0.75
    const brightness = 1 - quiet * 0.55
    const flowSpeed =
        motion.speed *
        (reduced ? 0.6 : 1) *
        slow *
        (0.35 + gain * (bass * 1.1 + pulse * 0.7) * reaction.drive)
    const spin = (0.15 + gain * mid * reaction.twist) * slow * (reduced ? 0.6 : 1)
    const trail = Math.min(longestTrail, motion.trail + (longestTrail - motion.trail) * quiet * 0.6)

    return {
        layers,
        flowSpeed,
        spin,
        mass,
        sway,
        sparkle,
        beat,
        brightness,
        glow: (0.1 + gain * (0.3 * bass + 0.25 * pulse)) * brightness,

        waveAmplitude:
            motion.waveAmplitude * (0.45 + gain * (bass * 1.1 + pulse * 0.6) * reaction.mass),
        waveSwing: 0.3 + sway * 0.9,
        waveThickness: motion.waveThickness * (0.7 + sparkle * 0.9 + beat * 0.5),
        particleScale: 1 + gain * (bass * 0.35 + pulse * 0.3) * reaction.mass,
        ringSwell: gain * (bass * 0.9 + pulse * 0.3) * reaction.mass,
        streakLength: motion.streakLength * (0.2 + gain * (level * 1.2 + pulse * 0.8)),
        terrainLift: motion.terrainHeight * (0.35 + gain * bass * 1.1 * reaction.mass),
        terrainFront: beat * reaction.punch * 0.8,
        helixScale: 1 + gain * (bass * 0.45 + pulse * 0.2) * reaction.mass,
        helixTurns: motion.helixTurns * (1 + beat * 0.5 * reaction.punch),

        trail,
        trailZoom: (motion.trailZoom + gain * (bass * 0.004 + pulse * 0.006)) * slow,
        trailTurn: motion.trailTurn * slow,

        cameraOrbit: motion.orbit * gain,
        cameraSway: motion.sway * gain,
        cameraPitch: motion.pitch,
        cameraMid: mid,
        punch: reduced ? 0 : pulse * motion.punch * reaction.punch,
        bloom: motion.bloom,
    }
}
