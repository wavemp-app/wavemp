// What the quality levels of BlackCap draw. Low keeps weak phones at a smooth frame rate, high shows
// finely curved surfaces with all shapes on desktops. Pure.

import type { QualityLevel } from '../../quality'

export interface BlackCapProfile {
    /** Segments of the surface of a shape around (longitude) and from top to bottom (latitude). */
    readonly columns: number
    readonly rows: number
    /** Most shapes drawn; a mode with more shows only these. */
    readonly shapes: number
    /** Layers of organic waves on the surfaces (1 or 2). */
    readonly detail: number
    /** Number of soft lights floating in the background. */
    readonly lights: number
    /** Share of the drawing buffer the scene is drawn at. */
    readonly resolution: number
    /** Whether bright parts get a soft glow. */
    readonly bloom: boolean
}

export const blackcapProfiles: Readonly<Record<QualityLevel, BlackCapProfile>> = {
    low: { columns: 48, rows: 32, shapes: 3, detail: 1, lights: 4, resolution: 0.5, bloom: false },
    medium: {
        columns: 96,
        rows: 64,
        shapes: 5,
        detail: 2,
        lights: 8,
        resolution: 0.75,
        bloom: true,
    },
    high: {
        columns: 144,
        rows: 96,
        shapes: 6,
        detail: 2,
        lights: 12,
        resolution: 1,
        bloom: true,
    },
}

/** The most background lights of any level, which the shader is made for. */
export const maxLights = Math.max(...Object.values(blackcapProfiles).map((p) => p.lights))

/** Size of the scene in pixels for a drawing buffer of the given size. */
export function sceneSize(
    width: number,
    height: number,
    profile: BlackCapProfile,
): { width: number; height: number } {
    return {
        width: Math.max(1, Math.round(width * profile.resolution)),
        height: Math.max(1, Math.round(height * profile.resolution)),
    }
}
