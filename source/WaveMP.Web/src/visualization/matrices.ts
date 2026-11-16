// Matrices of the 3D visualizations (A-Force, BlackCap): column-major Float32Arrays as WebGL takes
// them. Pure.

export type Vec3 = readonly [number, number, number]

/** A perspective projection (like gluPerspective) into out. */
export function perspective(
    out: Float32Array,
    fov: number,
    aspect: number,
    near: number,
    far: number,
): Float32Array {
    const f = 1 / Math.tan(fov / 2)
    const range = 1 / (near - far)
    out.fill(0)
    out[0] = f / Math.max(aspect, Number.EPSILON)
    out[5] = f
    out[10] = (far + near) * range
    out[11] = -1
    out[14] = 2 * far * near * range
    return out
}

function normalize(v: Vec3): Vec3 {
    const length = Math.hypot(v[0], v[1], v[2]) || 1
    return [v[0] / length, v[1] / length, v[2] / length]
}

function cross(a: Vec3, b: Vec3): Vec3 {
    return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]
}

function dot(a: Vec3, b: Vec3): number {
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
}

/** A view matrix that puts eye at the origin, looking at target along -z, into out. */
export function lookAt(out: Float32Array, eye: Vec3, target: Vec3, up: Vec3): Float32Array {
    const forward = normalize([target[0] - eye[0], target[1] - eye[1], target[2] - eye[2]])
    const side = normalize(cross(forward, up))
    const upward = cross(side, forward)
    out[0] = side[0]
    out[1] = upward[0]
    out[2] = -forward[0]
    out[3] = 0
    out[4] = side[1]
    out[5] = upward[1]
    out[6] = -forward[1]
    out[7] = 0
    out[8] = side[2]
    out[9] = upward[2]
    out[10] = -forward[2]
    out[11] = 0
    out[12] = -dot(side, eye)
    out[13] = -dot(upward, eye)
    out[14] = dot(forward, eye)
    out[15] = 1
    return out
}

/** The product a * b of two 4x4 matrices into out (out may not be a or b). */
export function multiply(out: Float32Array, a: Float32Array, b: Float32Array): Float32Array {
    for (let column = 0; column < 4; column++) {
        for (let row = 0; row < 4; row++) {
            let sum = 0
            for (let k = 0; k < 4; k++) {
                sum += (a[k * 4 + row] ?? 0) * (b[column * 4 + k] ?? 0)
            }

            out[column * 4 + row] = sum
        }
    }

    return out
}

/** A point transformed by a matrix (w = 1), divided by its w. */
export function transformPoint(matrix: Float32Array, point: Vec3): Vec3 {
    const m = (index: number): number => matrix[index] ?? 0
    const x = m(0) * point[0] + m(4) * point[1] + m(8) * point[2] + m(12)
    const y = m(1) * point[0] + m(5) * point[1] + m(9) * point[2] + m(13)
    const z = m(2) * point[0] + m(6) * point[1] + m(10) * point[2] + m(14)
    const w = m(3) * point[0] + m(7) * point[1] + m(11) * point[2] + m(15)
    const divisor = w === 0 ? 1 : w
    return [x / divisor, y / divisor, z / divisor]
}

/**
 * A model matrix into out: scaled evenly, turned about x (pitch), then about y (yaw), then moved to
 * the position. An even scale keeps the normals of the model valid with the upper 3x3 of the matrix.
 */
export function modelMatrix(
    out: Float32Array,
    position: Vec3,
    yaw: number,
    pitch: number,
    scale: number,
): Float32Array {
    const sy = Math.sin(yaw)
    const cy = Math.cos(yaw)
    const sp = Math.sin(pitch)
    const cp = Math.cos(pitch)
    out[0] = cy * scale
    out[1] = 0
    out[2] = -sy * scale
    out[3] = 0
    out[4] = sy * sp * scale
    out[5] = cp * scale
    out[6] = cy * sp * scale
    out[7] = 0
    out[8] = sy * cp * scale
    out[9] = -sp * scale
    out[10] = cy * cp * scale
    out[11] = 0
    out[12] = position[0]
    out[13] = position[1]
    out[14] = position[2]
    out[15] = 1
    return out
}
