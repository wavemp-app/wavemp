import { describe, expect, it } from 'vitest'
import { maxPresetNameLength } from '../../../settings'
import {
    blackcapMode,
    blackcapModes,
    maxModeShapes,
    minModeBlendSeconds,
    mixModes,
    modeBlendSeconds,
    modeLook,
    ModeTransition,
    type ModeLook,
} from '../modes'
import { blackcapColorSchemes } from '../schemes'

const first: ModeLook = modeLook(blackcapModes[0]!)
const second: ModeLook = modeLook(blackcapModes[1]!)

describe('blackcapModes', () => {
    it('offers the five modes with unique ids the profile can keep', () => {
        const ids = blackcapModes.map((mode) => mode.id)

        expect(ids).toEqual(['drift', 'orbit', 'cluster', 'ribbon', 'bloom'])
        expect(new Set(ids).size).toBe(ids.length)
        for (const id of ids) {
            expect(id).toMatch(/^[a-z][a-z-]*$/)
            expect(id.length).toBeLessThanOrEqual(maxPresetNameLength)
        }
    })

    it('uses no id of a color scheme, so a stored old scene is never taken for a mode', () => {
        const schemes = new Set(blackcapColorSchemes.map((scheme) => scheme.id))

        for (const mode of blackcapModes) {
            expect(schemes.has(mode.id)).toBe(false)
        }
    })

    it('has message keys and complete forms, motions and reactions for every mode', () => {
        const keys = (record: object) => Object.keys(record).sort()
        for (const mode of blackcapModes) {
            expect(mode.labelKey).toBe(`nowPlaying.blackcapModes.${mode.id}`)
            expect(keys(mode.form)).toEqual(keys(first.form))
            expect(keys(mode.motion)).toEqual(keys(first.motion))
            expect(keys(mode.reaction)).toEqual(keys(first.reaction))
            expect(mode.form.lobes).toHaveLength(4)
            expect(mode.form.stretch).toHaveLength(3)
            expect(mode.motion.shapes).toBeGreaterThanOrEqual(1)
            expect(mode.motion.shapes).toBeLessThanOrEqual(maxModeShapes)
            // Every mode reacts to all of the music.
            for (const gain of Object.values(mode.reaction)) {
                expect(gain).toBeGreaterThan(0)
            }
        }
    })

    it('lifts the surfaces clearly with the spectrum and swells on a beat without jerking', () => {
        for (const mode of blackcapModes) {
            expect(mode.form.spectrum).toBeGreaterThanOrEqual(0.45)
            expect(mode.reaction.swell).toBeGreaterThanOrEqual(0.1)
            expect(mode.reaction.swell).toBeLessThanOrEqual(0.2)
            expect(mode.motion.breath).toBeLessThanOrEqual(0.1)
        }
    })

    it('gives every mode a form and motion of its own', () => {
        const [drift, orbit, cluster, ribbon, bloom] = blackcapModes

        // One shape, a shape with companions, many small shapes.
        expect(drift!.motion.shapes).toBe(1)
        expect(orbit!.motion.shapes).toBeGreaterThan(2)
        expect(cluster!.motion.shapes).toBe(maxModeShapes)
        expect(cluster!.motion.size).toBeLessThan(drift!.motion.size)
        // Companions moving the most with the music: the orbit and the cluster.
        expect(Math.min(orbit!.reaction.spread, cluster!.reaction.spread)).toBeGreaterThan(
            Math.max(drift!.reaction.spread, ribbon!.reaction.spread, bloom!.reaction.spread),
        )
        // Stretched, ring-like forms twisting with the mids.
        expect(ribbon!.form.stretch[0]).toBeGreaterThan(2 * ribbon!.form.stretch[1])
        expect(ribbon!.reaction.twist).toBeGreaterThan(drift!.reaction.twist)
        // Petals that open with the loudness.
        expect(bloom!.form.lobes[2]).toBeGreaterThan(0.25)
        expect(bloom!.reaction.open).toBe(1)
    })

    it('finds a mode by its id', () => {
        expect(blackcapMode('orbit')?.id).toBe('orbit')
        expect(blackcapMode('tide')).toBeUndefined()
        expect(blackcapMode('gone')).toBeUndefined()
    })

    it('keeps only the look of a mode', () => {
        expect(Object.keys(modeLook(blackcapModes[2]!)).sort()).toEqual([
            'form',
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

    it('morphs the forms and blends motion and reaction', () => {
        const half = mixModes(first, second, 0.5)

        expect(half.form.stretch[1]).toBeCloseTo(
            (first.form.stretch[1] + second.form.stretch[1]) / 2,
        )
        expect(half.form.lobes[3]).toBeCloseTo((first.form.lobes[3] + second.form.lobes[3]) / 2)
        expect(half.motion.shapes).toBeCloseTo((first.motion.shapes + second.motion.shapes) / 2)
        expect(half.reaction.spread).toBeCloseTo(
            (first.reaction.spread + second.reaction.spread) / 2,
        )
    })

    it('clamps the amount', () => {
        expect(mixModes(first, second, -1).motion.size).toBe(first.motion.size)
        expect(mixModes(first, second, 2).motion.size).toBe(second.motion.size)
    })
})

describe('modeBlendSeconds', () => {
    it('shows the first mode at once', () => {
        expect(modeBlendSeconds(0)).toBe(0)
        expect(modeBlendSeconds(-1)).toBe(0)
        expect(modeBlendSeconds(Number.NaN)).toBe(0)
    })

    it('blends longer than asked for, at least the shortest blend', () => {
        expect(modeBlendSeconds(0.2)).toBe(minModeBlendSeconds)
        expect(modeBlendSeconds(1)).toBeGreaterThanOrEqual(minModeBlendSeconds)
        expect(modeBlendSeconds(4)).toBeGreaterThan(4)
    })
})

describe('ModeTransition', () => {
    it('morphs into the next mode and starts a change during a blend where it is', () => {
        const transition = new ModeTransition(first)

        transition.show(second, 4, 10)
        const halfway = transition.look(12)
        transition.show(first, 4, 12)

        expect(halfway.motion.size).not.toBe(first.motion.size)
        expect(halfway.motion.size).not.toBe(second.motion.size)
        expect(transition.look(12)).toEqual(halfway)
        expect(transition.look(16)).toBe(first)
    })
})
