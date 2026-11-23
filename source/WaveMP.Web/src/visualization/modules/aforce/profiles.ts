// What the quality levels of A-Force draw. Low keeps weak phones at a smooth frame rate, high shows
// the full scene on desktops. Pure.

import type { QualityLevel } from '../../quality'

export interface AForceProfile {
    /** Number of particles of the tunnel. */
    readonly particles: number
    /** Number of waveforms. */
    readonly waves: number
    /** Points per waveform (and per strand of the helix). */
    readonly wavePoints: number
    /** Number of rings. */
    readonly rings: number
    /** Number of streaks. */
    readonly streaks: number
    /** Rows of the spectrum the ridges show. */
    readonly terrainRows: number
    /** Share of the drawing buffer the scene is drawn at (the trails included). */
    readonly resolution: number
    /** Whether bright parts get a soft glow. */
    readonly bloom: boolean
}

export const aforceProfiles: Readonly<Record<QualityLevel, AForceProfile>> = {
    low: {
        particles: 1500,
        waves: 3,
        wavePoints: 160,
        rings: 14,
        streaks: 500,
        terrainRows: 24,
        resolution: 0.5,
        bloom: false,
    },
    medium: {
        particles: 5000,
        waves: 5,
        wavePoints: 256,
        rings: 20,
        streaks: 1200,
        terrainRows: 36,
        resolution: 0.75,
        bloom: true,
    },
    high: {
        particles: 12000,
        waves: 7,
        wavePoints: 384,
        rings: 28,
        streaks: 2400,
        terrainRows: 48,
        resolution: 1,
        bloom: true,
    },
}

/** The most particles, streaks, waveforms and rows of ridges of any level, which buffers are made for. */
export const maxParticles = Math.max(
    ...Object.values(aforceProfiles).map((p) => Math.max(p.particles, p.streaks)),
)
export const maxWaves = Math.max(...Object.values(aforceProfiles).map((p) => p.waves))
export const maxTerrainRows = Math.max(...Object.values(aforceProfiles).map((p) => p.terrainRows))

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
