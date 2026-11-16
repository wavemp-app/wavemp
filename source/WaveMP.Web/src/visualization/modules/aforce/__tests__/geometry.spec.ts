import { describe, expect, it } from 'vitest'
import {
    cameraDistance,
    cameraFov,
    cameraPose,
    cameraTarget,
    lookAt,
    multiply,
    perspective,
    transformPoint,
    type Vec3,
} from '../camera'
import { aforceProfiles, maxParticles, maxWaves, sceneSize } from '../profiles'
import { particleSeeds, seededRandom, waveStrip } from '../renderer'
import { risingZeroCrossing, smoothPoints, waveformPoints } from '../waveform'

const motion = { orbit: 0.4, sway: 1, punch: 1 }

function distance(a: Vec3, b: Vec3): number {
    return Math.hypot(a[0] - b[0], a[1] - b[1], a[2] - b[2])
}

describe('cameraPose', () => {
    it('looks at the scene from its distance without a beat', () => {
        const pose = cameraPose(12.3, motion, { mid: 0.5, pulse: 0 })

        expect(pose.target).toEqual(cameraTarget)
        expect(distance(pose.eye, pose.target)).toBeCloseTo(cameraDistance)
        expect(pose.fov).toBeCloseTo(cameraFov)
        expect(Math.hypot(...pose.up)).toBeCloseTo(1)
    })

    it('jumps towards the scene and widens on a beat', () => {
        const calm = cameraPose(4, motion, { mid: 0.5, pulse: 0 })
        const beat = cameraPose(4, motion, { mid: 0.5, pulse: 1 })

        expect(distance(beat.eye, beat.target)).toBeLessThan(distance(calm.eye, calm.target))
        expect(beat.fov).toBeLessThan(calm.fov)
    })

    it('moves over time', () => {
        const a = cameraPose(0, motion, { mid: 0.5, pulse: 0 })
        const b = cameraPose(10, motion, { mid: 0.5, pulse: 0 })

        expect(distance(a.eye, b.eye)).toBeGreaterThan(0.1)
    })

    it('stays still without orbit and sway', () => {
        const still = { orbit: 0, sway: 0, punch: 0 }
        const a = cameraPose(0, still, { mid: 1, pulse: 1 })
        const b = cameraPose(20, still, { mid: 0, pulse: 0 })

        expect(distance(a.eye, b.eye)).toBeCloseTo(0)
    })
})

describe('matrices', () => {
    it('puts the eye at the origin and the target in front of it', () => {
        const view = lookAt(new Float32Array(16), [0, 0, 5], [0, 0, 0], [0, 1, 0])

        expect(transformPoint(view, [0, 0, 5])).toEqual([0, 0, 0])
        const target = transformPoint(view, [0, 0, 0])
        expect(target[2]).toBeCloseTo(-5)
        expect(transformPoint(view, [1, 0, 0])[0]).toBeCloseTo(1)
    })

    it('projects the near and far plane to -1 and 1', () => {
        const projection = perspective(new Float32Array(16), Math.PI / 2, 2, 1, 10)

        expect(transformPoint(projection, [0, 0, -1])[2]).toBeCloseTo(-1)
        expect(transformPoint(projection, [0, 0, -10])[2]).toBeCloseTo(1)
        // 90 degrees: the edge of the view at distance 1 is at height 1, the width doubles.
        expect(transformPoint(projection, [0, 1, -1])[1]).toBeCloseTo(1)
        expect(transformPoint(projection, [2, 0, -1])[0]).toBeCloseTo(1)
    })

    it('multiplies so the right matrix applies first', () => {
        const view = lookAt(new Float32Array(16), [0, 0, 5], [0, 0, 0], [0, 1, 0])
        const projection = perspective(new Float32Array(16), Math.PI / 2, 1, 1, 10)
        const both = multiply(new Float32Array(16), projection, view)

        const direct = transformPoint(both, [0.5, 0.25, 1])
        const stepwise = transformPoint(projection, transformPoint(view, [0.5, 0.25, 1]))

        for (let index = 0; index < 3; index++) {
            expect(direct[index]).toBeCloseTo(stepwise[index]!)
        }
    })
})

describe('aforceProfiles', () => {
    it('draws more with every level', () => {
        const { low, medium, high } = aforceProfiles

        expect(low.particles).toBeLessThan(medium.particles)
        expect(medium.particles).toBeLessThan(high.particles)
        expect(low.waves).toBeLessThan(high.waves)
        expect(low.resolution).toBeLessThan(high.resolution)
        expect(low.bloom).toBe(false)
        expect(high.bloom).toBe(true)
        expect(maxParticles).toBe(high.particles)
        expect(maxWaves).toBe(high.waves)
    })

    it('scales the scene with the resolution of the level', () => {
        expect(sceneSize(1920, 1080, aforceProfiles.low)).toEqual({ width: 960, height: 540 })
        expect(sceneSize(1920, 1080, aforceProfiles.high)).toEqual({ width: 1920, height: 1080 })
        expect(sceneSize(1, 1, aforceProfiles.low)).toEqual({ width: 1, height: 1 })
    })
})

describe('waveform', () => {
    it('starts at the first rising zero crossing', () => {
        expect(risingZeroCrossing([0.5, -0.3, -0.1, 0.2, 0.4, -0.2, 0.3, 0.1])).toBe(3)
        expect(risingZeroCrossing([0.1, 0.2, 0.3, 0.4])).toBe(0)
    })

    it('keeps a steady sine still from frame to frame', () => {
        const sine = (offset: number) =>
            Float32Array.from({ length: 2048 }, (_, index) => Math.sin((index + offset) / 20))
        const a = new Float32Array(64)
        const b = new Float32Array(64)

        waveformPoints(sine(0), a, 1024)
        waveformPoints(sine(37), b, 1024)

        for (let index = 0; index < a.length; index++) {
            expect(b[index]).toBeCloseTo(a[index]!, 1)
        }
    })

    it('averages the samples of each point and clamps them', () => {
        const samples = Float32Array.from([-1, 0, 2, 2, 4, 4, -3, -3])
        const out = new Float32Array(3)

        waveformPoints(samples, out, 6)

        expect([...out]).toEqual([1, 1, 0.5])
        const quiet = new Float32Array(4)
        waveformPoints(Float32Array.from([-0.5, 0.5, 0.5, -0.5, -0.5]), quiet, 4)
        expect(quiet[0]).toBeCloseTo(0.5)
        expect(quiet[3]).toBeCloseTo(-0.5)
    })

    it('moves points a share towards their target', () => {
        const current = Float32Array.from([0, 1])
        smoothPoints(current, Float32Array.from([1, 0]), 0.25)

        expect([...current]).toEqual([0.25, 0.75])
    })
})

describe('buffers of the renderer', () => {
    it('gives every particle four random values, the same every time', () => {
        const a = particleSeeds(100, seededRandom(7))
        const b = particleSeeds(100, seededRandom(7))

        expect(a).toHaveLength(400)
        expect(a).toEqual(b)
        for (const value of a) {
            expect(value).toBeGreaterThanOrEqual(0)
            expect(value).toBeLessThan(1)
        }

        expect(new Set(a).size).toBeGreaterThan(390)
    })

    it('builds a strip with both edges per point', () => {
        expect([...waveStrip(3)]).toEqual([0, -1, 0, 1, 0.5, -1, 0.5, 1, 1, -1, 1, 1])
    })
})
