import { describe, expect, it } from 'vitest'
import type { Vec3 } from '../../../matrices'
import {
    beatsPerBreath,
    Breath,
    breathRate,
    cameraDistance,
    cameraFov,
    cameraPose,
    restingBreathRate,
    shapePlacements,
    type ShapeReaction,
} from '../motion'
import { blackcapProfiles, maxLights, sceneSize } from '../profiles'
import { blackcapModes, maxModeShapes, type ModeMotion } from '../modes'
import { shaderLights } from '../shaders'
import { sphereGrid, spreadLevels } from '../surface'

const motion: ModeMotion = { ...blackcapModes[0]!.motion, shapes: 4 }

/** The shapes at rest: their own size, no swell, the breath of the mode, their own distance. */
const rest: ShapeReaction = { size: 1, swell: 0, breath: motion.breath, orbitScale: 1 }

function point(directions: Float32Array, index: number): Vec3 {
    return [directions[index * 3]!, directions[index * 3 + 1]!, directions[index * 3 + 2]!]
}

function distance(a: Vec3, b: Vec3): number {
    return Math.hypot(a[0] - b[0], a[1] - b[1], a[2] - b[2])
}

describe('sphereGrid', () => {
    it('has a unit direction per vertex and two triangles per segment', () => {
        const grid = sphereGrid(8, 6)

        expect(grid.directions.length).toBe(9 * 7 * 3)
        expect(grid.indices.length).toBe(8 * 6 * 6)
        for (let index = 0; index < 9 * 7; index++) {
            expect(Math.hypot(...point(grid.directions, index))).toBeCloseTo(1)
        }

        expect(Math.max(...grid.indices)).toBe(9 * 7 - 1)
    })

    it('runs from the top to the bottom and closes its seam', () => {
        const grid = sphereGrid(8, 6)

        expect(point(grid.directions, 0)[1]).toBeCloseTo(1)
        expect(point(grid.directions, 9 * 7 - 1)[1]).toBeCloseTo(-1)
        // The last column of a row meets the first one.
        expect(distance(point(grid.directions, 9 + 8), point(grid.directions, 9))).toBeCloseTo(0)
    })

    it('winds every triangle counter-clockwise seen from outside', () => {
        const grid = sphereGrid(12, 8)
        for (let triangle = 0; triangle < grid.indices.length; triangle += 3) {
            const [a, b, c] = [0, 1, 2].map((corner) =>
                point(grid.directions, grid.indices[triangle + corner]!),
            ) as [Vec3, Vec3, Vec3]
            const ab: Vec3 = [b[0] - a[0], b[1] - a[1], b[2] - a[2]]
            const ac: Vec3 = [c[0] - a[0], c[1] - a[1], c[2] - a[2]]
            const normal: Vec3 = [
                ab[1] * ac[2] - ab[2] * ac[1],
                ab[2] * ac[0] - ab[0] * ac[2],
                ab[0] * ac[1] - ab[1] * ac[0],
            ]
            const outward = normal[0] * a[0] + normal[1] * a[1] + normal[2] * a[2]
            // Triangles at the poles have two corners at the same point.
            expect(outward).toBeGreaterThanOrEqual(-1e-9)
        }
    })

    it('fits the meshes of all quality levels into 16-bit indices', () => {
        for (const profile of Object.values(blackcapProfiles)) {
            expect((profile.columns + 1) * (profile.rows + 1)).toBeLessThanOrEqual(65536)
            expect(() => sphereGrid(profile.columns, profile.rows)).not.toThrow()
        }

        expect(() => sphereGrid(400, 400)).toThrow()
    })
})

describe('blackcapProfiles', () => {
    it('draws more detail at each higher level', () => {
        const { low, medium, high } = blackcapProfiles

        expect(low.columns * low.rows).toBeLessThan(medium.columns * medium.rows)
        expect(medium.columns * medium.rows).toBeLessThan(high.columns * high.rows)
        expect(low.shapes).toBeLessThan(high.shapes)
        expect(low.resolution).toBeLessThan(high.resolution)
        expect(low.bloom).toBe(false)
        expect(high.bloom).toBe(true)
        expect(high.shapes).toBeLessThanOrEqual(maxModeShapes)
        expect(maxLights).toBeLessThanOrEqual(shaderLights)
    })

    it('draws the scene at the share of the drawing buffer of the level', () => {
        expect(sceneSize(800, 450, blackcapProfiles.low)).toEqual({ width: 400, height: 225 })
        expect(sceneSize(1, 1, blackcapProfiles.low)).toEqual({ width: 1, height: 1 })
    })
})

describe('breathRate', () => {
    it('breathes once in two bars of the tempo', () => {
        expect(breathRate(120)).toBeCloseTo(120 / 60 / beatsPerBreath)
    })

    it('rests without a tempo', () => {
        expect(breathRate(null)).toBe(restingBreathRate)
        expect(breathRate(0)).toBe(restingBreathRate)
        expect(breathRate(Number.NaN)).toBe(restingBreathRate)
    })

    it('folds very slow and very fast tempos into a calm range', () => {
        expect(breathRate(40)).toBeCloseTo(breathRate(80))
        expect(breathRate(320)).toBeCloseTo(breathRate(160))
        expect(breathRate(1000)).toBeLessThanOrEqual(180 / 60 / beatsPerBreath)
    })
})

describe('Breath', () => {
    it('stays between -1 and 1 and changes its speed gradually', () => {
        const breath = new Breath()
        const values: number[] = []
        for (let frame = 0; frame < 600; frame++) {
            values.push(breath.update(1 / 60, 160))
        }

        expect(Math.max(...values)).toBeLessThanOrEqual(1)
        expect(Math.min(...values)).toBeGreaterThanOrEqual(-1)
        // After ten seconds it nearly follows the tempo, and never jumped there.
        expect(breath.currentRate).toBeCloseTo(breathRate(160), 2)
        for (let index = 1; index < values.length; index++) {
            expect(Math.abs(values[index]! - values[index - 1]!)).toBeLessThan(0.05)
        }
    })

    it('ignores frames without time', () => {
        const breath = new Breath()
        const before = breath.update(0.5, null)

        expect(breath.update(0, null)).toBeCloseTo(before)
        expect(breath.update(Number.NaN, null)).toBeCloseTo(before)
    })
})

describe('shapePlacements', () => {
    it('places the first shape in the middle and the others around it', () => {
        const shapes = shapePlacements(motion, rest, 6, 12, 0)

        expect(shapes).toHaveLength(4)
        expect(shapes[0]!.position[0]).toBe(0)
        expect(shapes[0]!.position[2]).toBe(0)
        expect(shapes[0]!.scale).toBeCloseTo(motion.size)
        for (const shape of shapes.slice(1)) {
            const [x, , z] = shape.position
            expect(Math.hypot(x, z + 0.6)).toBeGreaterThan(motion.orbit * 0.4)
            expect(Math.hypot(x, z + 0.6)).toBeLessThanOrEqual(motion.orbit * 1.01)
            expect(shape.scale).toBeCloseTo(motion.size * motion.companionSize)
        }

        expect(new Set(shapes.map((shape) => shape.seed)).size).toBe(4)
    })

    it('draws no more shapes than the quality level allows', () => {
        expect(shapePlacements({ ...motion, shapes: 6 }, rest, 3, 0, 0)).toHaveLength(3)
        expect(shapePlacements({ ...motion, shapes: 1 }, rest, 6, 0, 0)).toHaveLength(1)
    })

    it('fades a shape in while the number of shapes blends', () => {
        const shapes = shapePlacements({ ...motion, shapes: 2.25 }, rest, 6, 0, 0)

        expect(shapes).toHaveLength(3)
        expect(shapes[1]!.presence).toBe(1)
        expect(shapes[2]!.presence).toBeCloseTo(0.25)
        expect(shapes[2]!.scale).toBeLessThan(shapes[1]!.scale)
    })

    it('breathes, grows, swells on a beat and moves the others away as the music says', () => {
        const calm = shapePlacements(motion, rest, 6, 3, 0)
        const inhaled = shapePlacements(motion, rest, 6, 3, 1)
        const beat = shapePlacements(motion, { ...rest, swell: 0.15 }, 6, 3, 0)
        const larger = shapePlacements(motion, { ...rest, size: 1.2 }, 6, 3, 0)
        const apart = shapePlacements(motion, { ...rest, orbitScale: 1.2 }, 6, 3, 0)
        const radius = (shape: { position: Vec3 }) =>
            Math.hypot(shape.position[0], shape.position[2] + 0.6)

        expect(inhaled[0]!.scale).toBeGreaterThan(calm[0]!.scale)
        expect(beat[0]!.scale / calm[0]!.scale).toBeCloseTo(1.15)
        expect(larger[1]!.scale / calm[1]!.scale).toBeCloseTo(1.2)
        expect(radius(apart[1]!)).toBeGreaterThan(radius(calm[1]!) * 1.1)
        expect(apart[0]!.position).toEqual(calm[0]!.position)
    })

    it('floats slowly', () => {
        const now = shapePlacements(motion, rest, 6, 10, 0)
        const soon = shapePlacements(motion, rest, 6, 10 + 1 / 60, 0)

        for (let index = 0; index < now.length; index++) {
            expect(distance(now[index]!.position, soon[index]!.position)).toBeLessThan(0.02)
        }
    })
})

describe('cameraPose', () => {
    it('looks at the middle from its distance', () => {
        const pose = cameraPose(17, 1)

        expect(distance(pose.eye, [0, 0, 0])).toBeCloseTo(cameraDistance)
        expect(pose.fov).toBe(cameraFov)
        expect(Math.hypot(...pose.up)).toBeCloseTo(1)
    })

    it('drifts over time only as far as it is asked to', () => {
        expect(distance(cameraPose(0, 1).eye, cameraPose(30, 1).eye)).toBeGreaterThan(0.5)
        expect(distance(cameraPose(0, 0).eye, cameraPose(30, 0).eye)).toBeCloseTo(0)
    })
})

describe('spreadLevels', () => {
    it('spreads a single loud band over its neighbours and keeps the sum', () => {
        const levels = new Float32Array([0, 0, 0, 1, 0, 0, 0])
        const spread = spreadLevels(levels, new Float32Array(7), 2)

        expect([...spread]).toEqual([0, 0.0625, 0.25, 0.375, 0.25, 0.0625, 0])
        expect([...levels]).toEqual([0, 0, 0, 1, 0, 0, 0])
    })

    it('keeps even levels and the ends', () => {
        expect([
            ...spreadLevels(new Float32Array([0.5, 0.5, 0.5]), new Float32Array(3), 3),
        ]).toEqual([0.5, 0.5, 0.5])
        expect([...spreadLevels(new Float32Array([1, 0]), new Float32Array(2), 1)]).toEqual([
            0.75, 0.25,
        ])
    })

    it('copies the levels without passes', () => {
        expect([...spreadLevels(new Float32Array([0.2, 0.8]), new Float32Array(2), 0)]).toEqual([
            expect.closeTo(0.2),
            expect.closeTo(0.8),
        ])
    })
})
