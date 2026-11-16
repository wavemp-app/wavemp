import { describe, expect, it } from 'vitest'
import { blendAmount, ease, LookTransition, mixNumber, mixRecord, mixRgb } from '../blend'

interface Look {
    readonly size: number
    readonly speed: number
}

const mixLook = (a: Look, b: Look, t: number): Look =>
    mixRecord(a, b, (x: number, y: number) => mixNumber(x, y, t))

const small: Look = { size: 0, speed: 1 }
const large: Look = { size: 10, speed: 3 }

describe('blend helpers', () => {
    it('mixes numbers, colors and records', () => {
        expect(mixNumber(2, 4, 0.25)).toBe(2.5)
        expect(mixRgb([0, 0.5, 1], [1, 0.5, 0], 0.5)).toEqual([0.5, 0.5, 0.5])
        expect(mixLook(small, large, 0.5)).toEqual({ size: 5, speed: 2 })
    })

    it('eases in and out and clamps', () => {
        expect(ease(-1)).toBe(0)
        expect(ease(0.25)).toBeLessThan(0.25)
        expect(ease(0.75)).toBeGreaterThan(0.75)
        expect(ease(2)).toBe(1)
    })

    it('clamps the amount of a blend', () => {
        expect(blendAmount(-0.5)).toBe(0)
        expect(blendAmount(0.4)).toBe(0.4)
        expect(blendAmount(3)).toBe(1)
        expect(blendAmount(Number.NaN)).toBe(1)
    })
})

describe('LookTransition', () => {
    it('shows the first look until a change', () => {
        const transition = new LookTransition(small, mixLook)

        expect(transition.look(100)).toBe(small)
        expect(transition.blending(100)).toBe(false)
    })

    it('blends into a new look over the seconds', () => {
        const transition = new LookTransition(small, mixLook)

        transition.show(large, 2, 10)

        expect(transition.blending(11)).toBe(true)
        expect(transition.look(10)).toEqual(small)
        expect(transition.look(11).size).toBeCloseTo(5)
        expect(transition.look(12)).toBe(large)
        expect(transition.blending(12)).toBe(false)
    })

    it('shows a look at once without blend seconds', () => {
        const transition = new LookTransition(small, mixLook)

        transition.show(large, 0, 10)

        expect(transition.look(10)).toBe(large)
    })

    it('starts a change during a blend from the look shown at that moment', () => {
        const transition = new LookTransition(small, mixLook)
        transition.show(large, 2, 0)
        const halfway = transition.look(1)

        transition.show(small, 2, 1)

        expect(transition.look(1)).toEqual(halfway)
        expect(transition.look(3)).toBe(small)
    })
})
