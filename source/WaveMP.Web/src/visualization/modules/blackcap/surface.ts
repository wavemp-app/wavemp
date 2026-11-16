// The mesh of the shapes of BlackCap: a sphere of directions, which the vertex shader pushes out
// into the organic surface of a scene. Pure.

export interface SphereGrid {
    /** Three values per vertex: the unit direction from the middle of the shape. */
    readonly directions: Float32Array<ArrayBuffer>
    /** Three vertices per triangle, counter-clockwise seen from outside. */
    readonly indices: Uint16Array<ArrayBuffer>
}

/** Most vertices an index of 16 bits reaches. */
const maxVertices = 65536

/**
 * A sphere of columns segments around and rows segments from the top (+y) to the bottom (-y). The
 * first and the last column meet at the same directions, so the surface has no seam.
 */
export function sphereGrid(columns: number, rows: number): SphereGrid {
    const across = Math.max(3, Math.floor(columns))
    const down = Math.max(2, Math.floor(rows))
    const vertices = (across + 1) * (down + 1)
    if (vertices > maxVertices) {
        throw new Error('The sphere has too many vertices for 16-bit indices.')
    }

    const directions = new Float32Array(vertices * 3)
    for (let row = 0; row <= down; row++) {
        const polar = (row / down) * Math.PI
        const ring = Math.sin(polar)
        const y = Math.cos(polar)
        for (let column = 0; column <= across; column++) {
            const azimuth = (column / across) * Math.PI * 2
            const offset = (row * (across + 1) + column) * 3
            directions[offset] = ring * Math.cos(azimuth)
            directions[offset + 1] = y
            directions[offset + 2] = ring * Math.sin(azimuth)
        }
    }

    const indices = new Uint16Array(across * down * 6)
    let next = 0
    for (let row = 0; row < down; row++) {
        for (let column = 0; column < across; column++) {
            const a = row * (across + 1) + column
            const b = a + 1
            const c = a + across + 1
            const d = c + 1
            indices.set([a, b, c, b, d, c], next)
            next += 6
        }
    }

    return { directions, indices }
}

/**
 * Spreads every level over its neighbours into out ([1, 2, 1] / 4, passes times; the ends keep
 * their share), so a single loud band lifts a broad, soft swell of the surface instead of a ridge.
 */
export function spreadLevels(
    levels: Float32Array,
    out: Float32Array,
    passes: number,
): Float32Array {
    const count = Math.min(levels.length, out.length)
    out.set(levels.subarray(0, count))
    for (let pass = 0; pass < passes; pass++) {
        // In place: left keeps the value of the previous band before this pass changed it.
        let left = out[0] ?? 0
        for (let index = 0; index < count; index++) {
            const current = out[index] ?? 0
            const right = out[Math.min(count - 1, index + 1)] ?? 0
            out[index] = (left + 2 * current + right) / 4
            left = current
        }
    }

    return out
}
