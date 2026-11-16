// What the quality levels of A-Force draw. Low keeps weak phones at a smooth frame rate, high shows
// the full scene on desktops. Pure.

import type { QualityLevel } from '../../quality'

export interface AForceProfile {
    /** Number of particles. */
    readonly particles: number
    /** Number of waveforms. */
    readonly waves: number
    /** Points per waveform. */
    readonly wavePoints: number
    /** Share of the drawing buffer the scene is drawn at (the trails included). */
    readonly resolution: number
    /** Whether bright parts get a soft glow. */
    readonly bloom: boolean
}

export const aforceProfiles: Readonly<Record<QualityLevel, AForceProfile>> = {
    low: { particles: 1500, waves: 3, wavePoints: 160, resolution: 0.5, bloom: false },
    medium: { particles: 5000, waves: 5, wavePoints: 256, resolution: 0.75, bloom: true },
    high: { particles: 12000, waves: 7, wavePoints: 384, resolution: 1, bloom: true },
}

/** The most particles and waveforms of any level, which the buffers are made for. */
export const maxParticles = Math.max(...Object.values(aforceProfiles).map((p) => p.particles))
export const maxWaves = Math.max(...Object.values(aforceProfiles).map((p) => p.waves))

/** Size of the scene in pixels for a drawing buffer of the given size. */
export function sceneSize(
    width: number,
    height: number,
    profile: AForceProfile,
): { width: number; height: number } {
    return {
        width: Math.max(1, Math.round(width * profile.resolution)),
        height: Math.max(1, Math.round(height * profile.resolution)),
    }
}
