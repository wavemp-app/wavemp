import { afterEach, describe, expect, it } from 'vitest'
import {
    applyChange,
    canRotate,
    CoverChangeGate,
    coverChangeMilliseconds,
    coversStandStill,
    edgesOf,
    firstCoverChangeMilliseconds,
    initialTiles,
    nextChangeDelay,
    planChange,
    skipChange,
    startRotation,
    stillCoversFlag,
} from '../coverGrid'

/** A random source that returns the given numbers one after the other. */
function sequence(...values: number[]): () => number {
    let index = 0
    return () => values[index++ % values.length]!
}

describe('initialTiles', () => {
    it('fills the four tiles with the first four covers', () => {
        expect(initialTiles(['a', 'b', 'c', 'd', 'e'])).toEqual(['a', 'b', 'c', 'd'])
        expect(initialTiles(['a', 'b', 'c', 'd'])).toEqual(['a', 'b', 'c', 'd'])
    })

    it('repeats fewer covers on the diagonal, never next to each other where it can be avoided', () => {
        expect(initialTiles(['a', 'b', 'c'])).toEqual(['a', 'b', 'c', 'a'])
        expect(initialTiles(['a', 'b'])).toEqual(['a', 'b', 'b', 'a'])
        expect(initialTiles(['a'])).toEqual(['a', 'a', 'a', 'a'])
    })

    it('has no tiles without covers', () => {
        expect(initialTiles([])).toEqual([])
    })
})

describe('edgesOf', () => {
    it('names the outer edges of each tile', () => {
        expect(edgesOf(0)).toEqual(['start', 'top'])
        expect(edgesOf(1)).toEqual(['end', 'top'])
        expect(edgesOf(2)).toEqual(['start', 'bottom'])
        expect(edgesOf(3)).toEqual(['end', 'bottom'])
    })
})

describe('startRotation', () => {
    it('lets the covers after the first four wait in their order', () => {
        const rotation = startRotation(['a', 'b', 'c', 'd', 'e', 'f'])

        expect(rotation.tiles).toEqual(['a', 'b', 'c', 'd'])
        expect(rotation.waiting).toEqual(['e', 'f'])
        expect(rotation.lastTile).toBeNull()
        expect(canRotate(rotation)).toBe(true)
    })

    it('does not rotate with four covers or fewer', () => {
        expect(canRotate(startRotation(['a', 'b', 'c', 'd']))).toBe(false)
        expect(canRotate(startRotation(['a', 'b']))).toBe(false)
        expect(planChange(startRotation(['a', 'b', 'c', 'd']), Math.random)).toBeNull()
    })
})

describe('planChange', () => {
    const rotation = startRotation(['a', 'b', 'c', 'd', 'e', 'f'])
    const changeOf = (random: () => number, busy?: ReadonlySet<number>) =>
        planChange(rotation, random, busy)?.change

    it('moves the cover waiting longest into a random tile from one of its outer edges', () => {
        expect(changeOf(sequence(0, 0))).toEqual({ tile: 0, trackId: 'e', from: 'start' })
        expect(changeOf(sequence(0, 0.5))).toEqual({ tile: 0, trackId: 'e', from: 'top' })
        expect(changeOf(sequence(0.3, 0.9))).toEqual({ tile: 1, trackId: 'e', from: 'top' })
        expect(changeOf(sequence(0.5, 0.2))).toEqual({ tile: 2, trackId: 'e', from: 'start' })
        expect(changeOf(sequence(0.99, 0.99))).toEqual({ tile: 3, trackId: 'e', from: 'bottom' })
    })

    it('always comes from an outer edge of the tile', () => {
        for (let tile = 0; tile < 4; tile += 1) {
            for (const side of [0, 0.75]) {
                const change = changeOf(sequence((tile + 0.5) / 4, side))!
                expect(change.tile).toBe(tile)
                expect(edgesOf(tile)).toContain(change.from)
            }
        }
    })

    it('takes the cover out of the queue while it loads', () => {
        const planned = planChange(rotation, sequence(0, 0))!

        expect(planned.rotation.tiles).toEqual(rotation.tiles)
        expect(planned.rotation.waiting).toEqual(['f'])
        expect(planned.rotation.lastTile).toBe(0)
        expect(planChange(planned.rotation, sequence(0, 0), new Set([0]))?.change.trackId).toBe('f')
    })

    it('skips the tile that changed last and tiles still changing', () => {
        const planned = planChange(rotation, sequence(0, 0))!.rotation

        expect(planChange(planned, sequence(0, 0))?.change.tile).toBe(1)
        expect(planChange(planned, sequence(0, 0), new Set([1, 2]))?.change.tile).toBe(3)
        expect(planChange(planned, sequence(0, 0), new Set([1, 2, 3]))).toBeNull()
    })

    it('has nothing to change once every waiting cover is on its way', () => {
        const first = planChange(rotation, sequence(0, 0))!.rotation
        const second = planChange(first, sequence(0, 0))!.rotation

        expect(planChange(second, sequence(0, 0))).toBeNull()
    })
})

describe('applyChange', () => {
    it('shows the new cover in its tile and lets the replaced one wait at the end', () => {
        const planned = planChange(startRotation(['a', 'b', 'c', 'd', 'e', 'f']), sequence(0.5, 0))!

        const changed = applyChange(planned.rotation, planned.change)

        expect(changed.tiles).toEqual(['a', 'b', 'e', 'd'])
        expect(changed.waiting).toEqual(['f', 'c'])
        expect(changed.lastTile).toBe(2)
    })

    it('brings every cover around before one comes back', () => {
        let rotation = startRotation(['a', 'b', 'c', 'd', 'e', 'f', 'g'])
        const shown = new Set(rotation.tiles)
        const order: string[] = []
        for (let step = 0; step < 3; step += 1) {
            const planned = planChange(rotation, sequence(0.5, 0))!
            rotation = applyChange(planned.rotation, planned.change)
            shown.add(planned.change.trackId)
            order.push(planned.change.trackId)
        }

        expect(order).toEqual(['e', 'f', 'g'])
        expect([...shown].sort()).toEqual(['a', 'b', 'c', 'd', 'e', 'f', 'g'])
    })
})

describe('skipChange', () => {
    it('lets a cover that did not load wait again at the end', () => {
        const rotation = startRotation(['a', 'b', 'c', 'd', 'e', 'f'])
        const planned = planChange(rotation, sequence(0.3, 0))!

        const skipped = skipChange(planned.rotation, planned.change)

        expect(skipped.tiles).toEqual(rotation.tiles)
        expect(skipped.waiting).toEqual(['f', 'e'])
    })
})

describe('nextChangeDelay', () => {
    it('waits calmly, a little different each time', () => {
        expect(nextChangeDelay(() => 0)).toBe(coverChangeMilliseconds.min)
        expect(nextChangeDelay(() => 0.999)).toBeLessThan(
            coverChangeMilliseconds.min + coverChangeMilliseconds.spread,
        )
        expect(nextChangeDelay(() => 0, true)).toBe(firstCoverChangeMilliseconds.min)
        expect(coverChangeMilliseconds.min).toBeGreaterThanOrEqual(4000)
    })
})

describe('CoverChangeGate', () => {
    it('lets one change through per gap', () => {
        const gate = new CoverChangeGate(1500)

        expect(gate.claim(10_000)).toBe(0)
        expect(gate.claim(10_400)).toBe(1100)
        expect(gate.claim(11_499)).toBe(1)
        expect(gate.claim(11_500)).toBe(0)
        expect(gate.claim(12_000)).toBe(1000)
    })
})

describe('coversStandStill', () => {
    afterEach(() => {
        delete (globalThis as Record<string, unknown>)[stillCoversFlag]
    })

    it('follows the flag of the automated screenshots', () => {
        expect(coversStandStill()).toBe(false)
        ;(globalThis as Record<string, unknown>)[stillCoversFlag] = true
        expect(coversStandStill()).toBe(true)
    })
})
