import { describe, expect, it } from 'vitest'
import { maxColorSchemeLength } from '../../../settings'
import {
    blackcapColorScheme,
    blackcapColorSchemes,
    mixSchemes,
    schemeLook,
    SchemeTransition,
    type SchemeLook,
} from '../schemes'

const first: SchemeLook = schemeLook(blackcapColorSchemes[0]!)
const second: SchemeLook = schemeLook(blackcapColorSchemes[1]!)

describe('blackcapColorSchemes', () => {
    it('offers the six schemes of the former scenes with unique ids', () => {
        const ids = blackcapColorSchemes.map((scheme) => scheme.id)

        // The ids of the former scenes, so a migrated profile keeps its colors.
        expect(ids).toEqual(['lumen', 'tide', 'orchid', 'mercury', 'halo', 'dusk'])
        expect(new Set(ids).size).toBe(ids.length)
        for (const id of ids) {
            expect(id.length).toBeLessThanOrEqual(maxColorSchemeLength)
        }
    })

    it('has message keys, complete colors and a material for every scheme', () => {
        const keys = (record: object) => Object.keys(record).sort()
        for (const scheme of blackcapColorSchemes) {
            expect(scheme.labelKey).toBe(`nowPlaying.blackcapColorSchemes.${scheme.id}`)
            expect(keys(scheme.colors)).toEqual(keys(first.colors))
            expect(keys(scheme.material)).toEqual(keys(first.material))
            for (const color of Object.values(scheme.colors)) {
                expect(color).toHaveLength(3)
                for (const channel of color) {
                    expect(channel).toBeGreaterThanOrEqual(0)
                    expect(channel).toBeLessThanOrEqual(1)
                }
            }

            expect(scheme.material.gloss).toBeGreaterThan(1)
            expect(scheme.material.mirror).toBeGreaterThanOrEqual(0)
            expect(scheme.material.mirror).toBeLessThanOrEqual(1)
        }
    })

    it('finds a scheme by its id', () => {
        expect(blackcapColorScheme('mercury')?.id).toBe('mercury')
        expect(blackcapColorScheme('drift')).toBeUndefined()
    })
})

describe('mixSchemes', () => {
    it('gives the first scheme at 0 and the second at 1', () => {
        expect(mixSchemes(first, second, 0)).toEqual(first)
        expect(mixSchemes(first, second, 1)).toEqual(second)
    })

    it('blends colors and material', () => {
        const half = mixSchemes(first, second, 0.5)

        expect(half.colors.rim[0]).toBeCloseTo((first.colors.rim[0] + second.colors.rim[0]) / 2)
        expect(half.material.gloss).toBeCloseTo((first.material.gloss + second.material.gloss) / 2)
    })

    it('clamps the amount', () => {
        expect(mixSchemes(first, second, -1).material.mirror).toBe(first.material.mirror)
        expect(mixSchemes(first, second, 2).material.mirror).toBe(second.material.mirror)
    })
})

describe('SchemeTransition', () => {
    it('blends into the next scheme over the blend seconds', () => {
        const transition = new SchemeTransition(first)

        transition.show(second, 1.5, 10)

        expect(transition.look(10.75).colors.glow).not.toEqual(first.colors.glow)
        expect(transition.look(10.75).colors.glow).not.toEqual(second.colors.glow)
        expect(transition.look(11.5)).toBe(second)
    })
})
