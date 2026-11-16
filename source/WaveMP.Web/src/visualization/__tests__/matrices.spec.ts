import { describe, expect, it } from 'vitest'
import { modelMatrix, transformPoint, type Vec3 } from '../matrices'

function expectPoint(actual: Vec3, expected: Vec3): void {
    actual.forEach((value, index) => expect(value).toBeCloseTo(expected[index]!))
}

describe('modelMatrix', () => {
    it('moves and scales a point without turns', () => {
        const model = modelMatrix(new Float32Array(16), [1, 2, 3], 0, 0, 2)

        expectPoint(transformPoint(model, [1, 1, 1]), [3, 4, 5])
    })

    it('turns about y by the yaw', () => {
        const model = modelMatrix(new Float32Array(16), [0, 0, 0], Math.PI / 2, 0, 1)

        // A quarter turn about y moves +x to -z and +z to +x.
        expectPoint(transformPoint(model, [1, 0, 0]), [0, 0, -1])
        expectPoint(transformPoint(model, [0, 0, 1]), [1, 0, 0])
    })

    it('turns about x by the pitch before the yaw', () => {
        const model = modelMatrix(new Float32Array(16), [0, 0, 0], Math.PI / 2, Math.PI / 2, 1)

        // The pitch moves +y to +z, the yaw then moves +z to +x.
        expectPoint(transformPoint(model, [0, 1, 0]), [1, 0, 0])
    })

    it('keeps lengths apart from the even scale', () => {
        const model = modelMatrix(new Float32Array(16), [0, 0, 0], 0.7, -0.4, 1.5)
        const moved = transformPoint(model, [0.3, -0.5, 0.8])

        expect(Math.hypot(...moved)).toBeCloseTo(Math.hypot(0.3, -0.5, 0.8) * 1.5)
    })
})
