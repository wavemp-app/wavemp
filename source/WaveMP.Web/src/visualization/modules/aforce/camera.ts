// The camera of A-Force. The camera slowly orbits the scene, sways with the mids and jumps towards
// the scene on a beat; the mode sets how far it looks down. The matrices to draw with it are the shared ones of the
// visualizations. Pure.

import type { Vec3 } from '../../matrices'

export { lookAt, multiply, perspective, transformPoint, type Vec3 } from '../../matrices'

export interface CameraPose {
    readonly eye: Vec3
    readonly target: Vec3
    readonly up: Vec3
    /** Vertical field of view in radians. */
    readonly fov: number
}

export interface CameraMotion {
    /** How far the camera orbits. */
    readonly orbit: number
    /** How much it sways with the mids. */
    readonly sway: number
    /** How far it jumps towards the scene on a beat. */
    readonly punch: number
    /** How far it looks down on the scene (radians); defaultPitch without it. */
    readonly pitch?: number
}

export interface CameraAudio {
    readonly mid: number
    readonly pulse: number
}

/** Distance of the camera from its target without a beat. */
export const cameraDistance = 5.2

/** Point the camera looks at, a little into the scene. */
export const cameraTarget: Vec3 = [0, 0, -1.2]

/** Field of view without a beat (55 degrees). */
export const cameraFov = (55 * Math.PI) / 180

/** How far the camera looks down without a pitch of its own. */
export const defaultPitch = 0.06

/** The camera at the time in seconds for the motion of the mode and the music. */
export function cameraPose(time: number, motion: CameraMotion, audio: CameraAudio): CameraPose {
    const yaw =
        Math.sin(time * 0.11) * motion.orbit +
        Math.sin(time * 0.37) * 0.08 * motion.sway * (0.5 + audio.mid)
    const pitch = (motion.pitch ?? defaultPitch) + Math.sin(time * 0.07) * 0.18 * motion.orbit
    const distance = cameraDistance - motion.punch * audio.pulse * 0.9
    const roll = Math.sin(time * 0.05) * 0.06 * motion.sway
    const [x, y, z] = cameraTarget
    return {
        eye: [
            x + Math.sin(yaw) * Math.cos(pitch) * distance,
            y + Math.sin(pitch) * distance,
            z + Math.cos(yaw) * Math.cos(pitch) * distance,
        ],
        target: cameraTarget,
        up: [Math.sin(roll), Math.cos(roll), 0],
        fov: cameraFov - motion.punch * audio.pulse * 0.07,
    }
}
