import { describe, expect, it } from 'vitest'
import { maxPresetNameLength } from '../../../settings'
import {
    aforceStyle,
    aforceStyles,
    ease,
    mixLooks,
    StyleTransition,
    type StyleLook,
} from '../styles'

const first: StyleLook = aforceStyles[0]!
const second: StyleLook = aforceStyles[1]!

describe('aforceStyles', () => {
    it('offers several style variants with unique ids the profile can keep', () => {
        const ids = aforceStyles.map((style) => style.id)

        expect(aforceStyles.length).toBeGreaterThanOrEqual(5)
        expect(new Set(ids).size).toBe(ids.length)
        for (const id of ids) {
            expect(id).toMatch(/^[a-z][a-z-]*$/)
            expect(id.length).toBeLessThanOrEqual(maxPresetNameLength)
        }
    })

    it('has message keys and complete colors and motions for every style', () => {
        const colorKeys = Object.keys(aforceStyles[0]!.colors).sort()
        const motionKeys = Object.keys(aforceStyles[0]!.motion).sort()
        for (const style of aforceStyles) {
            expect(style.labelKey).toBe(`nowPlaying.aforceStyles.${style.id}`)
            expect(Object.keys(style.colors).sort()).toEqual(colorKeys)
            expect(Object.keys(style.motion).sort()).toEqual(motionKeys)
            for (const color of Object.values(style.colors)) {
                expect(color).toHaveLength(3)
                for (const channel of color) {
                    expect(channel).toBeGreaterThanOrEqual(0)
                    expect(channel).toBeLessThanOrEqual(1)
                }
            }

            expect(style.motion.trail).toBeGreaterThanOrEqual(0)
            expect(style.motion.trail).toBeLessThan(1)
        }
    })

    it('finds a style by its id', () => {
        expect(aforceStyle('ember')?.id).toBe('ember')
        expect(aforceStyle('gone')).toBeUndefined()
    })
})

describe('mixLooks', () => {
    it('gives the first look at 0 and the second at 1', () => {
        expect(mixLooks(first, second, 0)).toEqual({ colors: first.colors, motion: first.motion })
        expect(mixLooks(first, second, 1)).toEqual({
            colors: second.colors,
            motion: second.motion,
        })
    })

    it('blends every color and motion value', () => {
        const half = mixLooks(first, second, 0.5)

        expect(half.motion.speed).toBeCloseTo((first.motion.speed + second.motion.speed) / 2)
        expect(half.colors.glow[0]).toBeCloseTo((first.colors.glow[0] + second.colors.glow[0]) / 2)
    })

    it('clamps the amount', () => {
        expect(mixLooks(first, second, -1).motion.speed).toBe(first.motion.speed)
        expect(mixLooks(first, second, 2).motion.speed).toBe(second.motion.speed)
        expect(mixLooks(first, second, Number.NaN).motion.speed).toBe(second.motion.speed)
    })
})

describe('ease', () => {
    it('starts and ends smoothly', () => {
        expect(ease(0)).toBe(0)
        expect(ease(0.5)).toBe(0.5)
        expect(ease(1)).toBe(1)
        expect(ease(0.1)).toBeLessThan(0.1)
        expect(ease(-1)).toBe(0)
        expect(ease(3)).toBe(1)
    })
})

describe('StyleTransition', () => {
    it('shows a look at once without blend seconds', () => {
        const transition = new StyleTransition(first)

        transition.show(second, 0, 10)

        expect(transition.look(10)).toBe(second)
        expect(transition.blending(10)).toBe(false)
    })

    it('blends smoothly into the next look over the blend seconds', () => {
        const transition = new StyleTransition(first)

        transition.show(second, 2, 10)

        expect(transition.look(10).motion.speed).toBeCloseTo(first.motion.speed)
        expect(transition.look(11).motion.speed).toBeCloseTo(
            (first.motion.speed + second.motion.speed) / 2,
        )
        expect(transition.blending(11)).toBe(true)
        expect(transition.look(12)).toBe(second)
        expect(transition.blending(12)).toBe(false)
    })

    it('starts a change during a blend from the look shown at that moment', () => {
        const third = aforceStyles[2]!
        const transition = new StyleTransition(first)
        transition.show(second, 2, 0)
        const shown = transition.look(1)

        transition.show(third, 2, 1)

        expect(transition.look(1)).toEqual(shown)
        expect(transition.look(3)).toBe(third)
    })
})
