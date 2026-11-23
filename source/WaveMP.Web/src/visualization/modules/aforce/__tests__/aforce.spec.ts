import { describe, expect, it, vi } from 'vitest'
import type { VisualizationRendererOptions } from '../../../module'
import { visualizationModule, visualizationModuleChoices, visualizationModules } from '../..'
import {
    aforceColorSchemePresets,
    aforcePresets,
    browserAForceEnvironment,
    createAForceModule,
    type AForceEnvironment,
} from '..'
import { aforceProfiles } from '../profiles'
import { aforceModes } from '../modes'
import { AForceRenderer, spectrumBands } from '../renderer'
import { aforceColorSchemes } from '../schemes'
import { fakeAudio, fakeWebGl, rendererDefaults } from '../../../__tests__/webgl'

function setup(
    quality: VisualizationRendererOptions['quality'] = 'high',
    options: Partial<VisualizationRendererOptions> = {},
) {
    const webgl = fakeWebGl()
    const canvas = document.createElement('canvas')
    canvas.getContext = vi.fn(() => webgl.gl) as unknown as HTMLCanvasElement['getContext']
    const sound = fakeAudio()
    let now = 1000
    const renderer = new AForceRenderer(
        {
            canvas,
            audio: sound.audio,
            width: 800,
            height: 450,
            quality,
            ...rendererDefaults,
            ...options,
        },
        () => now,
    )
    return {
        ...webgl,
        ...sound,
        canvas,
        renderer,
        advance: (ms: number) => (now += ms),
    }
}

describe('the module A-Force', () => {
    function environment(overrides: Partial<AForceEnvironment> = {}): AForceEnvironment {
        return {
            supported: vi.fn(() => true),
            loadRenderer: vi.fn(() => Promise.resolve(vi.fn())),
            now: () => 42,
            ...overrides,
        }
    }

    it('is a module of the visualization with its modes as presets and its color schemes', async () => {
        const module = createAForceModule(environment())

        expect(module.id).toBe('AForce')
        const loaded = await module.load()
        expect(loaded.presets).toBe(aforcePresets)
        expect(loaded.presets.map((preset) => preset.id)).toEqual(
            aforceModes.map((mode) => mode.id),
        )
        expect(loaded.presets[0]).toEqual({
            id: 'flow',
            name: 'Flow',
            labelKey: 'nowPlaying.aforceModes.flow',
        })
        expect(loaded.colorSchemes).toBe(aforceColorSchemePresets)
        expect(loaded.colorSchemes!.map((scheme) => scheme.id)).toEqual(
            aforceColorSchemes.map((scheme) => scheme.id),
        )
        expect(loaded.colorSchemes![0]).toEqual({
            id: 'aurora',
            name: 'Aurora',
            labelKey: 'nowPlaying.aforceColorSchemes.aurora',
        })
        // A stored id is either a mode or a color scheme, never both.
        const schemes = new Set(loaded.colorSchemes!.map((scheme) => scheme.id))
        expect(loaded.presets.filter((preset) => schemes.has(preset.id))).toEqual([])
    })

    it('checks the browser only once', () => {
        const env = environment()
        const module = createAForceModule(env)

        module.isSupported()
        module.isSupported()

        expect(env.supported).toHaveBeenCalledTimes(1)
    })

    it('loads its renderer once for all callers and again after a failed load', async () => {
        const env = environment()
        vi.mocked(env.loadRenderer).mockRejectedValueOnce(new Error('offline'))
        const module = createAForceModule(env)

        await expect(module.load()).rejects.toThrow('offline')
        const [first, second] = await Promise.all([module.load(), module.load()])

        expect(first).toBe(second)
        expect(env.loadRenderer).toHaveBeenCalledTimes(2)
    })

    it('hands its clock to the renderer', async () => {
        const create = vi.fn()
        const module = createAForceModule(environment({ loadRenderer: async () => create }))
        const loaded = await module.load()
        const options = {} as VisualizationRendererOptions

        loaded.createRenderer(options)

        expect(create).toHaveBeenCalledWith(options, expect.any(Function))
        expect(create.mock.calls[0]![1]()).toBe(42)
    })

    it('loads the renderer of the browser as a chunk of its own', async () => {
        const create = await browserAForceEnvironment.loadRenderer()

        expect(typeof create).toBe('function')
    })

    it('is offered after Milkdrop', () => {
        expect(visualizationModule('AForce')).toBe(visualizationModules.AForce)
        expect(visualizationModuleChoices.map((choice) => choice.id).slice(0, 2)).toEqual([
            'Milkdrop',
            'AForce',
        ])
    })
})

type Named = ReturnType<typeof fakeWebGl>['named']

/** The values given to a uniform of the name with the method, in order. */
function uniformValues(named: Named, method: string, name: string): unknown[] {
    return named(method)
        .filter(([, args]) => (args[0] as { name: string } | null)?.name === name)
        .map(([, args]) => (args.length === 2 ? args[1] : args.slice(1)))
}

/** The draws of the guard at the end of every frame: reduction, limit and output. */
const guardDraws = [
    ['TRIANGLES', 0, 3],
    ['TRIANGLES', 0, 3],
    ['TRIANGLES', 0, 3],
]

describe('AForceRenderer', () => {
    it('fails without a WebGL 2 context', () => {
        const canvas = document.createElement('canvas')
        canvas.getContext = vi.fn(() => null) as unknown as HTMLCanvasElement['getContext']

        expect(
            () =>
                new AForceRenderer(
                    {
                        canvas,
                        audio: fakeAudio().audio,
                        width: 10,
                        height: 10,
                        quality: 'low',
                        ...rendererDefaults,
                    },
                    () => 0,
                ),
        ).toThrow()
    })

    it('fails when a shader does not compile', () => {
        const { gl } = fakeWebGl()
        const broken = new Proxy(gl, {
            get(target, property: string) {
                if (property === 'getShaderParameter') {
                    return () => false
                }

                return Reflect.get(target, property)
            },
        })
        const canvas = document.createElement('canvas')
        canvas.getContext = vi.fn(() => broken) as unknown as HTMLCanvasElement['getContext']

        expect(
            () =>
                new AForceRenderer(
                    {
                        canvas,
                        audio: fakeAudio().audio,
                        width: 10,
                        height: 10,
                        quality: 'low',
                        ...rendererDefaults,
                    },
                    () => 0,
                ),
        ).toThrow(/does not compile/)
    })

    it('listens to the player with an analyser of its own', () => {
        const { raw, analyser, canvas } = setup()

        expect(raw.connect).toHaveBeenCalledWith(analyser)
        expect(analyser.fftSize).toBe(2048)
        expect(analyser.smoothingTimeConstant).toBeLessThan(0.8)
        expect([canvas.width, canvas.height]).toEqual([800, 450])
    })

    it('draws the waveforms, the particles and the composite of a frame, then the guard', () => {
        const { renderer, named, analyser } = setup('high')

        renderer.render()

        expect(analyser.getFloatFrequencyData).toHaveBeenCalledTimes(1)
        expect(analyser.getFloatTimeDomainData).toHaveBeenCalledTimes(1)
        const profile = aforceProfiles.high
        expect(named('drawArraysInstanced')).toEqual([
            ['drawArraysInstanced', ['TRIANGLE_STRIP', 0, profile.wavePoints * 2, profile.waves]],
        ])
        expect(named('drawArrays').map(([, args]) => args)).toEqual([
            ['TRIANGLES', 0, 3],
            ['POINTS', 0, profile.particles],
            ['TRIANGLES', 0, 3],
            ...guardDraws,
        ])
        // The scene at full resolution, the composite into the guard at the size of the canvas,
        // the cells of the guard and its output onto the canvas.
        expect(named('viewport').map(([, args]) => args)).toEqual([
            [0, 0, 800, 450],
            [0, 0, 800, 450],
            [0, 0, 12, 4],
            [0, 0, 6, 4],
            [0, 0, 800, 450],
        ])
        expect(named('readPixels')).toHaveLength(0)
    })

    it('ends every frame with the flash guard onto the canvas', () => {
        const { renderer, calls, advance } = setup('medium')

        for (let frame = 0; frame < 3; frame++) {
            renderer.render()
            advance(16)
        }

        const names = calls.map(([name]) => name)
        const composite = calls
            .map(
                ([name, args]) =>
                    name === 'uniform1f' && (args[0] as { name: string }).name === 'uGlowStrength',
            )
            .lastIndexOf(true)
        const lastDraw = names.lastIndexOf('drawArrays')
        // After the composite come its draw and the three passes of the guard, the last one onto
        // the canvas, then only the copy of the output for the next frame.
        expect(names.slice(composite).filter((name) => name === 'drawArrays')).toHaveLength(4)
        const output = calls
            .slice(0, lastDraw)
            .filter(([name]) => name === 'bindFramebuffer')
            .at(-1)
        expect(output![1]).toEqual(['FRAMEBUFFER', null])
        expect(names.slice(lastDraw + 1)).toContain('blitFramebuffer')
        expect(names.slice(lastDraw + 1).filter((name) => name.startsWith('draw'))).toHaveLength(0)
        expect(names.filter((name) => name === 'blitFramebuffer')).toHaveLength(3)
    })

    it.each([
        ['flow', ['waves'], ['TRIANGLES', 'POINTS']],
        ['rings', [], ['TRIANGLES', 'TRIANGLES', 'POINTS']],
        ['warp', ['waves', 'streaks'], ['TRIANGLES']],
        ['ridges', ['terrain'], ['TRIANGLES', 'POINTS']],
        ['helix', ['helix'], ['TRIANGLES', 'POINTS']],
    ])('draws the layers of the mode %s and skips the others', (mode, instanced, scene) => {
        const { renderer, named, calls, advance } = setup('high')
        const profile = aforceProfiles.high
        const instancedDraws: Record<string, unknown[]> = {
            waves: ['TRIANGLE_STRIP', 0, profile.wavePoints * 2, profile.waves],
            streaks: ['TRIANGLE_STRIP', 0, 4, profile.streaks],
            terrain: ['TRIANGLE_STRIP', 0, 96 * 2, profile.terrainRows],
            helix: ['TRIANGLE_STRIP', 0, profile.wavePoints * 2, 2],
        }

        renderer.showPreset(mode, 0)
        calls.length = 0
        advance(16)
        renderer.render()

        expect(named('drawArraysInstanced').map(([, args]) => args)).toEqual(
            instanced.map((layer) => instancedDraws[layer]),
        )
        // The scene (trail and the layers drawn without instances), the composite and the guard.
        expect(named('drawArrays').map(([, args]) => args[0])).toEqual([
            ...scene,
            'TRIANGLES',
            ...guardDraws.map((draw) => draw[0]),
        ])
    })

    it('cross-fades the layers while the mode changes', () => {
        const { renderer, named, calls, advance } = setup('high')
        const weights = () => uniformValues(named, 'uniform1f', 'uWeight') as number[]

        renderer.showPreset('rings', 1)
        advance(500)
        calls.length = 0
        renderer.render()

        // Halfway: the waveforms fading out, the rings fading in, the particles in between.
        expect(named('drawArraysInstanced')).toHaveLength(1)
        expect(named('drawArrays')).toHaveLength(3 + 1 + 3)
        const [rings, waves] = weights()
        expect(rings).toBeCloseTo(0.5)
        expect(waves).toBeCloseTo(0.5)

        advance(600)
        calls.length = 0
        renderer.render()
        expect(named('drawArraysInstanced')).toHaveLength(0)
        expect(weights()).toEqual([1, aforceModes[1]!.layers.particles])
    })

    it('ignores unknown modes and ids of color schemes as modes', () => {
        const { renderer, named, calls, advance } = setup('high')

        renderer.showPreset('gone', 0)
        renderer.showPreset('ember', 0)
        calls.length = 0
        advance(16)
        renderer.render()

        expect(named('drawArraysInstanced')).toHaveLength(1)
        expect(uniformValues(named, 'uniform1f', 'uWeight')).toEqual([1, 1])
    })

    it('starts with the stored color scheme, or with the first one', () => {
        const glow = (scheme: string | null) => {
            const { renderer, named } = setup('low', { colorScheme: scheme })
            renderer.render()
            return uniformValues(named, 'uniform3fv', 'uGlow').at(-1)
        }

        expect(glow('lagoon')).toEqual(aforceColorSchemes[3]!.colors.glow)
        expect(glow(null)).toEqual(aforceColorSchemes[0]!.colors.glow)
        expect(glow('gone')).toEqual(aforceColorSchemes[0]!.colors.glow)
    })

    it('blends into a chosen color scheme and keeps it while the mode changes', () => {
        const { renderer, named, calls, advance } = setup()
        const glow = () => uniformValues(named, 'uniform3fv', 'uGlow').at(-1) as number[]
        const ember = aforceColorSchemes.find((scheme) => scheme.id === 'ember')!

        renderer.showColorScheme('gone', 1.5)
        renderer.showColorScheme('ember', 1.5)
        advance(750)
        renderer.render()
        const halfway = glow()
        calls.length = 0
        advance(800)
        renderer.render()
        const done = glow()
        renderer.showPreset('helix', 1)
        advance(1100)
        calls.length = 0
        renderer.render()

        expect(halfway).not.toEqual(ember.colors.glow)
        expect(halfway).not.toEqual(aforceColorSchemes[0]!.colors.glow)
        expect(done).toEqual(ember.colors.glow)
        expect(glow()).toEqual(ember.colors.glow)
    })

    it('limits flashes further at the reduced intensity', () => {
        const { renderer, named, calls, advance } = setup('low')
        const step = () => (uniformValues(named, 'uniform2f', 'uSteps').at(-1) as number[])[0]!

        renderer.render()
        advance(20)
        calls.length = 0
        renderer.render()
        const normal = step()
        renderer.setIntensity('reduced')
        advance(20)
        calls.length = 0
        renderer.render()

        expect(step()).toBeLessThan(normal)
        expect(step()).toBeGreaterThan(0)
    })

    it('starts at the reduced intensity when asked to', () => {
        const normal = setup('low')
        const reduced = setup('low', { intensity: 'reduced' })
        for (const { renderer, advance } of [normal, reduced]) {
            renderer.render()
            advance(20)
            renderer.render()
        }

        const step = (named: Named) =>
            (uniformValues(named, 'uniform2f', 'uSteps').at(-1) as number[])[0]!
        expect(step(reduced.named)).toBeLessThan(step(normal.named))
    })

    it('writes the spectrum of every frame into its history', () => {
        const { renderer, named, calls, advance } = setup('low')
        const rows = () => named('texSubImage2D').filter(([, args]) => args[4] === spectrumBands)

        renderer.render()
        expect(rows()).toHaveLength(1)
        calls.length = 0
        advance(16)
        renderer.render()

        expect(rows()).toHaveLength(1)
        const levels = rows()[0]![1].at(-1) as Float32Array
        expect(levels).toHaveLength(spectrumBands)
        for (const level of levels) {
            expect(level).toBeGreaterThanOrEqual(0)
            expect(level).toBeLessThanOrEqual(1)
        }
    })

    it('draws less at a lower quality level', () => {
        const { renderer, named, calls } = setup('high')

        renderer.setQuality('low')
        calls.length = 0
        renderer.render()

        const profile = aforceProfiles.low
        expect(named('drawArraysInstanced')[0]![1]).toEqual([
            'TRIANGLE_STRIP',
            0,
            profile.wavePoints * 2,
            profile.waves,
        ])
        expect(named('drawArrays')[1]![1]).toEqual(['POINTS', 0, profile.particles])
        expect(named('viewport')[0]![1]).toEqual([0, 0, 400, 225])
        expect(named('generateMipmap')).toHaveLength(0)
    })

    it('draws fewer rings, streaks and ridges at a lower quality level', () => {
        const { renderer, named, calls, advance } = setup('high')
        const show = (mode: string) => {
            renderer.showPreset(mode, 0)
            advance(16)
            calls.length = 0
            renderer.render()
        }

        renderer.setQuality('low')
        show('ridges')
        expect(named('drawArraysInstanced')[0]![1].at(-1)).toBe(aforceProfiles.low.terrainRows)
        show('warp')
        expect(named('drawArraysInstanced')[1]![1].at(-1)).toBe(aforceProfiles.low.streaks)
        show('rings')
        expect(uniformValues(named, 'uniform1f', 'uCount')).toEqual([aforceProfiles.low.rings])
    })

    it('resizes the canvas, the scene and the guard', () => {
        const { renderer, canvas, named, calls } = setup('medium')

        renderer.resize(1000, 500)
        calls.length = 0
        renderer.render()

        expect([canvas.width, canvas.height]).toEqual([1000, 500])
        expect(named('viewport').map(([, args]) => args)).toEqual([
            [0, 0, 750, 375],
            [0, 0, 1000, 500],
            [0, 0, 12, 4],
            [0, 0, 6, 4],
            [0, 0, 1000, 500],
        ])
    })

    it('disconnects from the player and frees its context when disposed', () => {
        const { renderer, raw, analyser, loseContext, named } = setup()

        renderer.dispose()
        renderer.dispose()
        renderer.render()

        expect(raw.disconnect).toHaveBeenCalledWith(analyser)
        expect(loseContext).toHaveBeenCalledTimes(1)
        expect(named('drawArrays')).toHaveLength(0)
    })
})
