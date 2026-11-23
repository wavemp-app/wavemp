// How BlackCap reacts to the music: from the look of the mode, the features of the sound and the
// intensity, the numbers of a frame that become the uniforms of its shaders and the placement of
// its shapes. BlackCap stays calm and organic, but clearly alive:
// - bass: the surfaces swell from their bottom, the shapes grow a little and the background glows,
// - mids: the lobes turn and twist faster, the edges and the floating lights shine brighter,
// - highs: fine ripples and glints at the top of the surfaces,
// - beats: a soft swell of the shapes and a pulse of light at their edges,
// - loudness: the other shapes move away from the middle and petals open,
// - quiet passages: the shapes contract, slow down and the lights dim.
// At the reduced intensity every gain is halved and the camera and the shapes move more slowly.
// Pure, so the tests measure the reaction on these inputs.

import type { AudioFeatures } from '../../analysis/features'
import type { VisualizationIntensityLevel } from '../../module'
import type { Lobes, ModeLook } from './modes'

/** The numbers of a frame of BlackCap. */
export interface BlackCapFrameInputs {
    /** The lobes of the surfaces (opened or closed by the loudness). */
    readonly lobes: Lobes
    /** How fast the lobes turn (radians per second). */
    readonly lobeSpeed: number
    /** Height of the organic waves. */
    readonly waves: number
    /** How far the spectrum lifts the surfaces. */
    readonly surfaceLift: number
    /** How far the bass swells the surfaces from their bottom, 0 at rest. */
    readonly bassLift: number
    /** Height of the fine ripples at the top of the surfaces by the highs, 0 at rest. */
    readonly ripple: number
    /** Strength of the reflections of the lights (glints of the highs). */
    readonly glint: number
    /** Size of the shapes as a factor: larger with the bass, smaller in quiet passages. */
    readonly size: number
    /** How far the shapes swell on the beat at this moment (share of their size), 0 at rest. */
    readonly swell: number
    /** How far the shapes breathe with the tempo (share of their size). */
    readonly breath: number
    /** Distance of the other shapes from the middle as a factor. */
    readonly orbitScale: number
    /** How fast the surfaces flow (units of flow per second). */
    readonly flowSpeed: number
    /** How fast the shapes float and turn: 1 is their own speed. */
    readonly motionSpeed: number
    /** Brightness of the picture: 1 in loud parts, lower in quiet ones. */
    readonly brightness: number
    /** Strength of the light at the edges of the shapes. */
    readonly rim: number
    /** Strength of the floating lights of the background. */
    readonly lights: number
    /** Strength of the glow in the middle of the background. */
    readonly glow: number
    /** The beat with its afterglow (the light of a beat on the surfaces), 0 at rest. */
    readonly pulse: number
    /** How far the camera drifts about the scene. */
    readonly drift: number
    /** How far the gradient of the surfaces moves on by a beat (radians). */
    readonly shadeStep: number
}

function clamp01(value: number): number {
    return Math.min(1, Math.max(0, Number.isFinite(value) ? value : 0))
}

/** The numbers of a frame for the look of the mode, the features of the sound and the intensity. */
export function blackcapFrameInputs(
    look: ModeLook,
    features: AudioFeatures,
    intensity: VisualizationIntensityLevel,
): BlackCapFrameInputs {
    const { form, motion, reaction } = look
    const reduced = intensity === 'reduced'
    const gain = reduced ? 0.5 : 1
    const pace = reduced ? 0.6 : 1
    const bass = clamp01(features.bass)
    const mid = clamp01(features.mid)
    const treble = clamp01(features.treble)
    const level = clamp01(features.level)
    const pulse = clamp01(features.pulse)
    const quiet = clamp01(features.quiet) * clamp01(reaction.calm)

    // Quiet passages slow the shapes down, let them contract and dim the light.
    const slow = 1 - quiet * 0.7
    const brightness = 1 - quiet * 0.5
    const open = clamp01(reaction.open)
    const openness =
        1 - open + open * ((0.3 + 0.7 * level) * (1 - quiet * 0.5) + gain * pulse * 0.2)

    return {
        lobes: form.lobes.map((lobe) => lobe * openness) as unknown as Lobes,
        lobeSpeed: form.lobeSpin * (0.5 + gain * mid * 1.5 * reaction.twist) * slow * pace,
        waves: form.waves * (1 + gain * treble * 0.4 * reaction.ripple),
        surfaceLift: form.spectrum * (reduced ? 0.6 : 1) * (1 - quiet * 0.5),
        bassLift: gain * bass * 0.35 * reaction.lift,
        ripple: gain * treble * 0.06 * reaction.ripple,
        glint: (0.6 + gain * treble * reaction.ripple) * brightness,
        size: (1 + gain * bass * 0.12 * reaction.lift) * (1 - quiet * 0.15),
        swell: gain * pulse * reaction.swell,
        breath: motion.breath * gain,
        orbitScale: Math.max(
            0.4,
            1 + reaction.spread * (gain * (0.4 * (level - 0.5) + 0.15 * pulse) - 0.3 * quiet),
        ),
        flowSpeed: motion.flow * (0.5 + gain * (0.6 * level + 0.5 * mid)) * slow * pace,
        motionSpeed: (0.6 + gain * 0.6 * level) * slow * pace,
        brightness,
        rim: (0.45 + gain * (0.5 * mid + 0.7 * pulse)) * brightness,
        lights: (0.6 + gain * 0.6 * mid) * brightness,
        glow: (0.04 + gain * (0.18 * bass + 0.12 * pulse)) * brightness,
        pulse: gain * pulse,
        drift: motion.drift * gain,
        shadeStep: gain * 0.35,
    }
}
