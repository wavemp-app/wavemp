import { describe, expect, it } from 'vitest'
import { maxPresetNameLength } from '../../../settings'
import {
    blackcapScene,
    blackcapScenes,
    maxSceneShapes,
    minSceneBlendSeconds,
    mixScenes,
    sceneBlendSeconds,
    SceneTransition,
    type SceneLook,
} from '../scenes'

const first: SceneLook = blackcapScenes[0]!
const second: SceneLook = blackcapScenes[1]!

describe('blackcapScenes', () => {
    it('offers several scenes with unique ids the profile can keep', () => {
        const ids = blackcapScenes.map((scene) => scene.id)

        expect(blackcapScenes.length).toBeGreaterThanOrEqual(5)
        expect(new Set(ids).size).toBe(ids.length)
        for (const id of ids) {
            expect(id).toMatch(/^[a-z][a-z-]*$/)
            expect(id.length).toBeLessThanOrEqual(maxPresetNameLength)
        }
    })

    it('has message keys and complete colors, forms and motions for every scene', () => {
        const keys = (record: object) => Object.keys(record).sort()
        for (const scene of blackcapScenes) {
            expect(scene.labelKey).toBe(`nowPlaying.blackcapScenes.${scene.id}`)
            expect(keys(scene.colors)).toEqual(keys(first.colors))
            expect(keys(scene.form)).toEqual(keys(first.form))
            expect(keys(scene.motion)).toEqual(keys(first.motion))
            for (const color of Object.values(scene.colors)) {
                expect(color).toHaveLength(3)
                for (const channel of color) {
                    expect(channel).toBeGreaterThanOrEqual(0)
                    expect(channel).toBeLessThanOrEqual(1)
                }
            }

            expect(scene.form.lobes).toHaveLength(4)
            expect(scene.form.stretch).toHaveLength(3)
            expect(scene.motion.shapes).toBeGreaterThanOrEqual(1)
            expect(scene.motion.shapes).toBeLessThanOrEqual(maxSceneShapes)
            expect(scene.motion.gloss).toBeGreaterThan(1)
            expect(scene.motion.mirror).toBeLessThanOrEqual(1)
        }
    })

    it('keeps the reaction to beats and the breathing gentle', () => {
        for (const scene of blackcapScenes) {
            expect(scene.motion.swell).toBeLessThanOrEqual(0.1)
            expect(scene.motion.breath).toBeLessThanOrEqual(0.1)
        }
    })

    it('finds a scene by its id', () => {
        expect(blackcapScene('tide')?.id).toBe('tide')
        expect(blackcapScene('gone')).toBeUndefined()
    })
})

describe('mixScenes', () => {
    it('gives the first scene at 0 and the second at 1', () => {
        expect(mixScenes(first, second, 0)).toEqual({
            colors: first.colors,
            form: first.form,
            motion: first.motion,
        })
        expect(mixScenes(first, second, 1)).toEqual({
            colors: second.colors,
            form: second.form,
            motion: second.motion,
        })
    })

    it('morphs the forms and blends colors and motion', () => {
        const half = mixScenes(first, second, 0.5)

        expect(half.form.stretch[1]).toBeCloseTo(
            (first.form.stretch[1] + second.form.stretch[1]) / 2,
        )
        expect(half.form.lobes[3]).toBeCloseTo((first.form.lobes[3] + second.form.lobes[3]) / 2)
        expect(half.form.waves).toBeCloseTo((first.form.waves + second.form.waves) / 2)
        expect(half.motion.shapes).toBeCloseTo((first.motion.shapes + second.motion.shapes) / 2)
        expect(half.colors.rim[0]).toBeCloseTo((first.colors.rim[0] + second.colors.rim[0]) / 2)
    })

    it('clamps the amount', () => {
        expect(mixScenes(first, second, -1).motion.size).toBe(first.motion.size)
        expect(mixScenes(first, second, 2).motion.size).toBe(second.motion.size)
    })
})

describe('sceneBlendSeconds', () => {
    it('shows the first scene at once', () => {
        expect(sceneBlendSeconds(0)).toBe(0)
        expect(sceneBlendSeconds(-1)).toBe(0)
        expect(sceneBlendSeconds(Number.NaN)).toBe(0)
    })

    it('blends longer than asked for, at least the shortest blend', () => {
        expect(sceneBlendSeconds(0.2)).toBe(minSceneBlendSeconds)
        expect(sceneBlendSeconds(1)).toBeGreaterThanOrEqual(minSceneBlendSeconds)
        expect(sceneBlendSeconds(4)).toBeGreaterThan(4)
    })
})

describe('SceneTransition', () => {
    it('morphs into the next scene and starts a change during a blend where it is', () => {
        const transition = new SceneTransition(first)

        transition.show(second, 4, 10)
        const halfway = transition.look(12)
        transition.show(first, 4, 12)

        expect(halfway.form.stretch[0]).not.toBe(first.form.stretch[0])
        expect(halfway.form.stretch[0]).not.toBe(second.form.stretch[0])
        expect(transition.look(12)).toEqual(halfway)
        expect(transition.look(16)).toBe(first)
    })
})
