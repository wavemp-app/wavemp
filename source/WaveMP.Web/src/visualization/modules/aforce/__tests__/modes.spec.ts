import { describe, expect, it } from 'vitest'
import { maxPresetNameLength } from '../../../settings'
import {
    aforceMode,
    aforceModes,
    ease,
    mixModes,
    modeLook,
    ModeTransition,
    type ModeLayers,
    type ModeLook,
} from '../modes'
import { aforceColorSchemes } from '../schemes'

const first: ModeLook = modeLook(aforceModes[0]!)
const second: ModeLook = modeLook(aforceModes[1]!)

/** The layer a mode shows most. */
function mainLayer(layers: ModeLayers): keyof ModeLayers {
    return (Object.entries(layers) as [keyof ModeLayers, number][]).reduce((a, b) =>
        b[1] > a[1] ? b : a,
    )[0]
}

describe('aforceModes', () => {
    it('offers the five modes with unique ids the profile can keep', () => {
        const ids = aforceModes.map((mode) => mode.id)

        expect(ids).toEqual(['flow', 'rings', 'warp', 'ridges', 'helix'])
        expect(new Set(ids).size).toBe(ids.length)
        for (const id of ids) {
            expect(id).toMatch(/^[a-z][a-z-]*$/)
            expect(id.length).toBeLessThanOrEqual(maxPresetNameLength)
        }
    })

    it('uses no id of a color scheme, so a stored old style is never taken for a mode', () => {
        const schemes = new Set(aforceColorSchemes.map((scheme) => scheme.id))

        for (const mode of aforceModes) {
            expect(schemes.has(mode.id)).toBe(false)
        }
    })

    it('has message keys and complete layers, motions and reactions for every mode', () => {
        const keys = (look: ModeLook) => ({
            layers: Object.keys(look.layers).sort(),
            motion: Object.keys(look.motion).sort(),
            reaction: Object.keys(look.reaction).sort(),
        })
        for (const mode of aforceModes) {
            expect(mode.labelKey).toBe(`nowPlaying.aforceModes.${mode.id}`)
            expect(keys(mode)).toEqual(keys(first))
            for (const weight of Object.values(mode.layers)) {
                expect(weight).toBeGreaterThanOrEqual(0)
                expect(weight).toBeLessThanOrEqual(1)
            }

            expect(mode.motion.trail).toBeGreaterThanOrEqual(0)
            expect(mode.motion.trail).toBeLessThan(1)
            // Every mode reacts to all of the music.
            for (const gain of Object.values(mode.reaction)) {
                expect(gain).toBeGreaterThan(0)
            }
        }
    })

    it('gives every mode a shape of its own', () => {
        expect(aforceModes.map((mode) => mainLayer(mode.layers))).toEqual([
            'waves',
            'rings',
            'streaks',
            'terrain',
            'helix',
        ])
    })

    it('finds a mode by its id', () => {
        expect(aforceMode('warp')?.id).toBe('warp')
        expect(aforceMode('aurora')).toBeUndefined()
        expect(aforceMode('gone')).toBeUndefined()
    })

    it('keeps only the look of a mode', () => {
        expect(Object.keys(modeLook(aforceModes[2]!)).sort()).toEqual([
            'layers',
            'motion',
            'reaction',
        ])
    })
})

describe('mixModes', () => {
    it('gives the first mode at 0 and the second at 1', () => {
        expect(mixModes(first, second, 0)).toEqual(first)
        expect(mixModes(first, second, 1)).toEqual(second)
    })

    it('cross-fades the layers and morphs every number', () => {
        const half = mixModes(first, second, 0.5)

        expect(half.layers.waves).toBeCloseTo((first.layers.waves + second.layers.waves) / 2)
        expect(half.layers.rings).toBeCloseTo((first.layers.rings + second.layers.rings) / 2)
        expect(half.motion.speed).toBeCloseTo((first.motion.speed + second.motion.speed) / 2)
        expect(half.reaction.twist).toBeCloseTo((first.reaction.twist + second.reaction.twist) / 2)
    })

    it('clamps the amount', () => {
        expect(mixModes(first, second, -1).motion.speed).toBe(first.motion.speed)
        expect(mixModes(first, second, 2).motion.speed).toBe(second.motion.speed)
        expect(mixModes(first, second, Number.NaN).motion.speed).toBe(second.motion.speed)
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

describe('ModeTransition', () => {
    it('shows a mode at once without blend seconds', () => {
        const transition = new ModeTransition(first)

        transition.show(second, 0, 10)

        expect(transition.look(10)).toBe(second)
        expect(transition.blending(10)).toBe(false)
    })

    it('blends smoothly into the next mode over the blend seconds', () => {
        const transition = new ModeTransition(first)

        transition.show(second, 2, 10)

        expect(transition.look(10).motion.speed).toBeCloseTo(first.motion.speed)
        expect(transition.look(11).layers.rings).toBeCloseTo(
            (first.layers.rings + second.layers.rings) / 2,
        )
        expect(transition.blending(11)).toBe(true)
        expect(transition.look(12)).toBe(second)
        expect(transition.blending(12)).toBe(false)
    })

    it('starts a change during a blend from the look shown at that moment', () => {
        const third = modeLook(aforceModes[2]!)
        const transition = new ModeTransition(first)
        transition.show(second, 2, 0)
        const shown = transition.look(1)

        transition.show(third, 2, 1)

        expect(transition.look(1)).toEqual(shown)
        expect(transition.look(3)).toBe(third)
    })
})
