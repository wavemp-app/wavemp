// The motion of BlackCap: the breathing of the shapes with the tempo of the music, where the shapes
// float and the drifting camera. Everything moves slowly and smoothly, so the picture stays calm;
// beats only let the shapes swell a little. Pure.

import { ease } from '../../blend'
import type { Vec3 } from '../../matrices'
import { maxSceneShapes, type SceneMotion } from './scenes'

/** Breaths per second without a tempo (one every five seconds). */
export const restingBreathRate = 0.2

/** Beats per breath: one breath spans two bars of four beats. */
export const beatsPerBreath = 8

/** Slowest and fastest tempo the breathing follows; others are folded into this range. */
const slowestBpm = 60
const fastestBpm = 180

/** Seconds the breathing needs to follow a new tempo (to about two thirds). */
const breathFollowSeconds = 2

/** Breaths per second for a tempo in beats per minute, or the resting rate without one. */
export function breathRate(bpm: number | null): number {
    if (bpm === null || !Number.isFinite(bpm) || bpm <= 0) {
        return restingBreathRate
    }

    let tempo = bpm
    while (tempo < slowestBpm) {
        tempo *= 2
    }

    while (tempo > fastestBpm) {
        tempo /= 2
    }

    return tempo / 60 / beatsPerBreath
}

/**
 * The breathing of the shapes: a phase that advances with the tempo of the music. A new tempo
 * changes the speed of the breathing gradually, so it never jumps.
 */
export class Breath {
    private rate = restingBreathRate
    private phase = 0

    /** Advances by the seconds of a frame at the tempo; returns the breath from -1 to 1. */
    update(seconds: number, bpm: number | null): number {
        const step = Number.isFinite(seconds) ? Math.max(0, seconds) : 0
        const target = breathRate(bpm)
        this.rate += (target - this.rate) * (1 - Math.exp(-step / breathFollowSeconds))
        this.phase = (this.phase + step * this.rate * Math.PI * 2) % (Math.PI * 2)
        return Math.sin(this.phase)
    }

    /** Breaths per second at the moment. */
    get currentRate(): number {
        return this.rate
    }
}

/** Where and how a shape floats in a frame. */
export interface ShapePlacement {
    readonly position: Vec3
    /** Turn about y and about x in radians. */
    readonly yaw: number
    readonly pitch: number
    /** Size of the shape; 0 for a shape that is not shown. */
    readonly scale: number
    /** A number of its own, so every shape has its own surface. */
    readonly seed: number
    /** How far the shape is shown, 0 to 1; shapes fade in and out with a change of scene. */
    readonly presence: number
}

/** Angle between two neighbouring companions (the golden angle), so they never line up. */
const goldenAngle = Math.PI * (3 - Math.sqrt(5))

/**
 * The shapes of a frame: the first floats in the middle, the others float around it on slow,
 * wavering paths. time is the time of the flowing motion in seconds, breath the breath (-1 to 1)
 * and pulse the last beat fading out (0 to 1). At most limit shapes are placed (the quality level);
 * a fractional number of shapes of the scene fades the last one in or out.
 */
export function shapePlacements(
    motion: SceneMotion,
    limit: number,
    time: number,
    breath: number,
    pulse: number,
): ShapePlacement[] {
    const count = Math.min(motion.shapes, limit, maxSceneShapes)
    const swell = 1 + motion.breath * breath + motion.swell * pulse
    const placements: ShapePlacement[] = []
    for (let index = 0; index < Math.min(limit, maxSceneShapes); index++) {
        const presence = index === 0 ? 1 : Math.min(1, Math.max(0, count - index))
        if (presence <= 0) {
            break
        }

        const shown = ease(presence)
        const yaw = time * motion.spin * (1 + index * 0.23) + index * 1.7
        const pitch = Math.sin(time * 0.17 + index * 2.1) * 0.45
        if (index === 0) {
            placements.push({
                position: [0, Math.sin(time * 0.29) * motion.float * 0.4, 0],
                yaw,
                pitch,
                scale: motion.size * swell,
                seed: 0,
                presence: 1,
            })
            continue
        }

        const angle = index * goldenAngle + time * motion.orbitSpeed * (1 + index * 0.11)
        const radius = motion.orbit * (0.85 + 0.15 * Math.sin(time * 0.13 + index * 1.3))
        placements.push({
            position: [
                Math.cos(angle) * radius,
                Math.sin(time * 0.37 + index * 1.9) * motion.float,
                Math.sin(angle) * radius * 0.7 - 0.6,
            ],
            yaw,
            pitch,
            scale: motion.size * motion.companionSize * swell * shown,
            seed: index,
            presence,
        })
    }

    return placements
}

export interface CameraPose {
    readonly eye: Vec3
    readonly target: Vec3
    readonly up: Vec3
    /** Vertical field of view in radians. */
    readonly fov: number
}

/** Distance of the camera from the middle of the scene. */
export const cameraDistance = 5.6

/** Field of view (48 degrees). */
export const cameraFov = (48 * Math.PI) / 180

/** The camera at the time in seconds: it drifts slowly about the middle as far as drift says. */
export function cameraPose(time: number, drift: number): CameraPose {
    const yaw = Math.sin(time * 0.043) * 0.55 * drift
    const pitch = 0.12 + Math.sin(time * 0.031) * 0.14 * drift
    const roll = Math.sin(time * 0.023) * 0.03 * drift
    return {
        eye: [
            Math.sin(yaw) * Math.cos(pitch) * cameraDistance,
            Math.sin(pitch) * cameraDistance,
            Math.cos(yaw) * Math.cos(pitch) * cameraDistance,
        ],
        target: [0, 0, -0.3],
        up: [Math.sin(roll), Math.cos(roll), 0],
        fov: cameraFov,
    }
}
