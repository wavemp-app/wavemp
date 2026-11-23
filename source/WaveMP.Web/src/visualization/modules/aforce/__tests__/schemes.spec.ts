import { describe, expect, it } from 'vitest'
import { maxPresetNameLength } from '../../../settings'
import {
    aforceColorScheme,
    aforceColorSchemes,
    ColorTransition,
    mixColors,
    type SchemeColors,
} from '../schemes'

const first: SchemeColors = aforceColorSchemes[0]!.colors
const second: SchemeColors = aforceColorSchemes[1]!.colors

describe('aforceColorSchemes', () => {
    it('offers the six color schemes with unique ids the profile can keep', () => {
        const ids = aforceColorSchemes.map((scheme) => scheme.id)

        expect(ids).toEqual(['aurora', 'nebula', 'ember', 'lagoon', 'prism', 'eclipse'])
        expect(new Set(ids).size).toBe(ids.length)
        for (const id of ids) {
            expect(id).toMatch(/^[a-z][a-z-]*$/)
            expect(id.length).toBeLessThanOrEqual(maxPresetNameLength)
        }
    })

    it('has message keys and complete colors for every scheme', () => {
        const colorKeys = Object.keys(first).sort()
        for (const scheme of aforceColorSchemes) {
            expect(scheme.labelKey).toBe(`nowPlaying.aforceColorSchemes.${scheme.id}`)
            expect(Object.keys(scheme.colors).sort()).toEqual(colorKeys)
            for (const color of Object.values(scheme.colors)) {
                expect(color).toHaveLength(3)
                for (const channel of color) {
                    expect(channel).toBeGreaterThanOrEqual(0)
                    expect(channel).toBeLessThanOrEqual(1)
                }
            }
        }
    })

    it('finds a scheme by its id', () => {
        expect(aforceColorScheme('ember')?.id).toBe('ember')
        expect(aforceColorScheme('gone')).toBeUndefined()
    })
})

describe('mixColors', () => {
    it('gives the first colors at 0 and the second at 1', () => {
        expect(mixColors(first, second, 0)).toEqual(first)
        expect(mixColors(first, second, 1)).toEqual(second)
    })

    it('blends every color', () => {
        const half = mixColors(first, second, 0.5)

        expect(half.glow[0]).toBeCloseTo((first.glow[0] + second.glow[0]) / 2)
        expect(half.spark[2]).toBeCloseTo((first.spark[2] + second.spark[2]) / 2)
    })

    it('clamps the amount', () => {
        expect(mixColors(first, second, -1).glow).toEqual(first.glow)
        expect(mixColors(first, second, 2).glow).toEqual(second.glow)
        expect(mixColors(first, second, Number.NaN).glow).toEqual(second.glow)
    })
})

describe('ColorTransition', () => {
    it('shows colors at once without blend seconds', () => {
        const transition = new ColorTransition(first)

        transition.show(second, 0, 10)

        expect(transition.look(10)).toBe(second)
        expect(transition.blending(10)).toBe(false)
    })

    it('blends smoothly into the next colors over the blend seconds', () => {
        const transition = new ColorTransition(first)

        transition.show(second, 1.5, 10)

        expect(transition.look(10).glow).toEqual(first.glow)
        expect(transition.look(10.75).glow[1]).toBeCloseTo((first.glow[1] + second.glow[1]) / 2)
        expect(transition.blending(11)).toBe(true)
        expect(transition.look(11.5)).toBe(second)
    })

    it('starts a change during a blend from the colors shown at that moment', () => {
        const third = aforceColorSchemes[2]!.colors
        const transition = new ColorTransition(first)
        transition.show(second, 2, 0)
        const shown = transition.look(1)

        transition.show(third, 2, 1)

        expect(transition.look(1)).toEqual(shown)
        expect(transition.look(3)).toBe(third)
    })
})
