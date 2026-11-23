import { describe, expect, it, vi } from 'vitest'
import type { VisualizationRendererOptions } from '../../../module'
import { visualizationModule, visualizationModuleChoices, visualizationModules } from '../..'
import {
    blackcapColorSchemePresets,
    blackcapPresets,
    browserBlackCapEnvironment,
    createBlackCapModule,
    type BlackCapEnvironment,
} from '..'
import { blackcapModes, minModeBlendSeconds } from '../modes'
import { blackcapProfiles } from '../profiles'
import { BlackCapRenderer } from '../renderer'
import { blackcapColorSchemes } from '../schemes'
import { spectrumBands } from '../shaders'
import { sphereGrid } from '../surface'
import { fakeAudio, fakeWebGl, rendererDefaults, type Call } from '../../../__tests__/webgl'

function setup(
    quality: VisualizationRendererOptions['quality'] = 'high',
    options: Partial<VisualizationRendererOptions> = {},
) {
    const webgl = fakeWebGl()
    const canvas = document.createElement('canvas')
    canvas.getContext = vi.fn(() => webgl.gl) as unknown as HTMLCanvasElement['getContext']
    const sound = fakeAudio()
    let now = 1000
    const renderer = new BlackCapRenderer(
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
    /** The last call that set the uniform. */
    const uniform = (name: string): Call | undefined =>
        [...webgl.calls]
            .reverse()
            .find(
                ([property, args]: Call) =>
                    property.startsWith('uniform') && (args[0] as { name?: string })?.name === name,
            )
    return {
        ...webgl,
        ...sound,
        canvas,
        renderer,
        uniform,
        advance: (ms: number) => (now += ms),
    }
}

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

/** Index count of the mesh of a quality level. */
function indexCount(quality: VisualizationRendererOptions['quality']): number {
    const profile = blackcapProfiles[quality]
    return sphereGrid(profile.columns, profile.rows).indices.length
}

describe('the module BlackCap', () => {
    function environment(overrides: Partial<BlackCapEnvironment> = {}): BlackCapEnvironment {
        return {
            supported: vi.fn(() => true),
            loadRenderer: vi.fn(() => Promise.resolve(vi.fn())),
            now: () => 42,
            ...overrides,
        }
    }

    it('is a module of the visualization with its modes as presets and its color schemes', async () => {
        const module = createBlackCapModule(environment())

        expect(module.id).toBe('BlackCap')
        const loaded = await module.load()
        expect(loaded.presets).toBe(blackcapPresets)
        expect(loaded.presets.map((preset) => preset.id)).toEqual(
            blackcapModes.map((mode) => mode.id),
        )
        expect(loaded.presets[0]).toEqual({
            id: 'drift',
            name: 'Drift',
            labelKey: 'nowPlaying.blackcapModes.drift',
        })
        expect(loaded.colorSchemes).toBe(blackcapColorSchemePresets)
        expect(loaded.colorSchemes!.map((scheme) => scheme.id)).toEqual(
            blackcapColorSchemes.map((scheme) => scheme.id),
        )
        expect(loaded.colorSchemes![0]).toEqual({
            id: 'lumen',
            name: 'Lumen',
            labelKey: 'nowPlaying.blackcapColorSchemes.lumen',
        })
    })

    it('checks the browser only once and is unsupported without WebGL 2', () => {
        const env = environment({ supported: vi.fn(() => false) })
        const module = createBlackCapModule(env)

        expect(module.isSupported()).toBe(false)
        module.isSupported()

        expect(env.supported).toHaveBeenCalledTimes(1)
    })

    it('loads its renderer once for all callers and again after a failed load', async () => {
        const env = environment()
        vi.mocked(env.loadRenderer).mockRejectedValueOnce(new Error('offline'))
        const module = createBlackCapModule(env)

        await expect(module.load()).rejects.toThrow('offline')
        const [first, second] = await Promise.all([module.load(), module.load()])

        expect(first).toBe(second)
        expect(env.loadRenderer).toHaveBeenCalledTimes(2)
    })

    it('hands its clock to the renderer', async () => {
        const create = vi.fn()
        const module = createBlackCapModule(environment({ loadRenderer: async () => create }))
        const loaded = await module.load()
        const options = {} as VisualizationRendererOptions

        loaded.createRenderer(options)

        expect(create).toHaveBeenCalledWith(options, expect.any(Function))
        expect(create.mock.calls[0]![1]()).toBe(42)
    })

    it('loads the renderer of the browser as a chunk of its own', async () => {
        const create = await browserBlackCapEnvironment.loadRenderer()

        expect(typeof create).toBe('function')
    })

    it('is offered after Milkdrop and A-Force', () => {
        expect(visualizationModule('BlackCap')).toBe(visualizationModules.BlackCap)
        expect(visualizationModuleChoices.map((choice) => choice.id)).toEqual([
            'Milkdrop',
            'AForce',
            'BlackCap',
        ])
        expect(visualizationModuleChoices[2]!.labelKey).toBe(
            'nowPlaying.visualizationModules.blackcap',
        )
    })
})

describe('BlackCapRenderer', () => {
    it('fails without a WebGL 2 context', () => {
        const canvas = document.createElement('canvas')
        canvas.getContext = vi.fn(() => null) as unknown as HTMLCanvasElement['getContext']

        expect(
            () =>
                new BlackCapRenderer(
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
                new BlackCapRenderer(
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
        ).toThrow(/BlackCap does not compile/)
    })

    it('listens to the player with an analyser of its own', () => {
        const { raw, analyser, canvas } = setup()

        expect(raw.connect).toHaveBeenCalledWith(analyser)
        expect(analyser.fftSize).toBe(2048)
        expect(analyser.smoothingTimeConstant).toBeLessThan(0.6)
        expect([canvas.width, canvas.height]).toEqual([800, 450])
    })

    it('draws the background, the shapes with depth and the composite of a frame, then the guard', () => {
        const { renderer, named, analyser } = setup('high')

        renderer.render()

        expect(analyser.getFloatFrequencyData).toHaveBeenCalledTimes(1)
        expect(named('drawArrays').map(([, args]) => args)).toEqual([
            ['TRIANGLES', 0, 3],
            ['TRIANGLES', 0, 3],
            ...guardDraws,
        ])
        // The first mode shows one shape in the middle.
        expect(named('drawElements').map(([, args]) => args)).toEqual([
            ['TRIANGLES', indexCount('high'), 'UNSIGNED_SHORT', 0],
        ])
        expect(named('enable').map(([, args]) => args[0])).toEqual(
            expect.arrayContaining(['DEPTH_TEST', 'CULL_FACE']),
        )
        // A depth buffer of the scene, then the scene at full resolution, the composite into the
        // guard at the size of the canvas, the cells of the guard and its output onto the canvas.
        expect(named('renderbufferStorage')[0]![1]).toEqual([
            'RENDERBUFFER',
            'DEPTH_COMPONENT24',
            800,
            450,
        ])
        expect(named('viewport').map(([, args]) => args)).toEqual([
            [0, 0, 800, 450],
            [0, 0, 800, 450],
            [0, 0, 12, 4],
            [0, 0, 6, 4],
            [0, 0, 800, 450],
        ])
        expect(named('generateMipmap').length).toBeGreaterThan(0)
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
                    name === 'uniform1f' && (args[0] as { name: string }).name === 'uBloom',
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

    it('lets the surfaces follow the spectrum', () => {
        const { renderer, uniform, analyser, advance } = setup()

        // Two seconds of a steady sound with the balance of most music (3 dB less per octave), then
        // the lows up to about 1 kHz get 20 dB louder.
        const pink = (bin: number) => -40 - 3 * Math.log2(Math.max(1, bin))
        analyser.getFloatFrequencyData.mockImplementation((data: Float32Array) => {
            data.forEach((_, bin) => (data[bin] = pink(bin)))
            return data
        })
        for (let frame = 0; frame < 120; frame++) {
            advance(1000 / 60)
            renderer.render()
        }

        const steady = [...(uniform('uSpectrum')![1][1] as Float32Array)]
        analyser.getFloatFrequencyData.mockImplementation((data: Float32Array) => {
            data.forEach((_, bin) => (data[bin] = pink(bin) + (bin < 40 ? 20 : 0)))
            return data
        })
        for (let frame = 0; frame < 30; frame++) {
            advance(1000 / 60)
            renderer.render()
        }

        const louder = [...(uniform('uSpectrum')![1][1] as Float32Array)]
        const low = louder.slice(0, spectrumBands / 4)
        const high = louder.slice((spectrumBands * 3) / 4)

        expect(steady).toHaveLength(spectrumBands)
        expect(Math.min(...low)).toBeGreaterThan(Math.max(...high))
        // The highs did not change, but they are now far below the loudest bands.
        expect(Math.max(...high)).toBeLessThan(Math.min(...steady.slice((spectrumBands * 3) / 4)))
    })

    it('swells the surfaces and lights the edges with every kick of the music', () => {
        const { renderer, analyser, advance, named } = setup()
        const values = (name: string) =>
            (uniformValues(named, 'uniform1f', name) as number[]).slice(-120)

        // Four seconds of a quiet sound with a kick (the bass 25 dB louder for four frames) every
        // half second.
        const quiet = (bin: number) => -60 - 3 * Math.log2(Math.max(1, bin))
        let frame = 0
        analyser.getFloatFrequencyData.mockImplementation((data: Float32Array) => {
            const kick = frame % 30 < 4
            data.forEach((_, bin) => (data[bin] = quiet(bin) + (kick && bin < 12 ? 25 : 0)))
            return data
        })
        for (; frame < 240; frame++) {
            advance(1000 / 60)
            renderer.render()
        }

        const lift = values('uBassLift')
        const rim = values('uRimLight')
        expect(Math.max(...lift) - Math.min(...lift)).toBeGreaterThan(0.15)
        expect(Math.max(...rim)).toBeGreaterThan(Math.min(...rim) * 1.25)
    })

    it('shows more shapes in a mode with more of them, limited by the quality level', () => {
        const high = setup('high')
        high.renderer.showPreset('cluster', 0)
        high.renderer.render()
        const low = setup('low')
        low.renderer.showPreset('cluster', 0)
        low.renderer.render()

        expect(high.named('drawElements')).toHaveLength(blackcapProfiles.high.shapes)
        expect(low.named('drawElements')).toHaveLength(blackcapProfiles.low.shapes)
        expect(low.named('drawElements')[0]![1]).toEqual([
            'TRIANGLES',
            indexCount('low'),
            'UNSIGNED_SHORT',
            0,
        ])
    })

    it('draws less at a lower quality level', () => {
        const { renderer, named, calls, uniform } = setup('high')

        renderer.setQuality('low')
        calls.length = 0
        renderer.render()

        expect(named('drawElements')[0]![1]).toEqual([
            'TRIANGLES',
            indexCount('low'),
            'UNSIGNED_SHORT',
            0,
        ])
        expect(named('viewport')[0]![1]).toEqual([0, 0, 400, 225])
        expect(named('generateMipmap')).toHaveLength(0)
        expect(uniform('uLightCount')![1][1]).toBe(blackcapProfiles.low.lights)
        expect(uniform('uDetail')![1][1]).toBe(blackcapProfiles.low.detail)
        expect(uniform('uBloom')![1][1]).toBe(0)
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

    it('morphs into a chosen mode over several seconds and ignores unknown ones', () => {
        const { renderer, named, advance } = setup()
        const ribbon = blackcapModes.find((mode) => mode.id === 'ribbon')!
        const stretch = () => uniformValues(named, 'uniform3fv', 'uStretch').at(-1) as number[]

        renderer.showPreset('gone', 1)
        // An id of a color scheme is no mode.
        renderer.showPreset('tide', 1)
        renderer.showPreset('ribbon', 1)
        advance(1000)
        renderer.render()
        const during = stretch()
        advance(minModeBlendSeconds * 1000)
        renderer.render()

        expect(during).not.toEqual(ribbon.form.stretch)
        expect(during).not.toEqual(blackcapModes[0]!.form.stretch)
        expect(stretch()).toEqual(ribbon.form.stretch)
    })

    it('starts with the stored color scheme, or with the first one', () => {
        const glow = (scheme: string | null) => {
            const { renderer, named } = setup('low', { colorScheme: scheme })
            renderer.render()
            return uniformValues(named, 'uniform3fv', 'uGlow').at(-1)
        }

        expect(glow('mercury')).toEqual(blackcapColorSchemes[3]!.colors.glow)
        expect(glow(null)).toEqual(blackcapColorSchemes[0]!.colors.glow)
        expect(glow('gone')).toEqual(blackcapColorSchemes[0]!.colors.glow)
    })

    it('blends into a chosen color scheme and keeps it while the mode changes', () => {
        const { renderer, named, calls, advance } = setup()
        const glow = () => uniformValues(named, 'uniform3fv', 'uGlow').at(-1) as number[]
        const mirror = () => uniformValues(named, 'uniform1f', 'uMirror').at(-1) as number
        const halo = blackcapColorSchemes.find((scheme) => scheme.id === 'halo')!

        renderer.showColorScheme('gone', 1.5)
        renderer.showColorScheme('drift', 1.5)
        renderer.showColorScheme('halo', 1.5)
        advance(750)
        renderer.render()
        const halfway = glow()
        advance(800)
        renderer.render()
        const done = glow()
        renderer.showPreset('bloom', 1)
        advance(minModeBlendSeconds * 1000 + 100)
        calls.length = 0
        renderer.render()

        expect(halfway).not.toEqual(halo.colors.glow)
        expect(halfway).not.toEqual(blackcapColorSchemes[0]!.colors.glow)
        expect(done).toEqual(halo.colors.glow)
        expect(glow()).toEqual(halo.colors.glow)
        expect(mirror()).toBe(halo.material.mirror)
    })

    it('limits flashes further and reacts more gently at the reduced intensity', () => {
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

    it('disconnects from the player and frees its context when disposed', () => {
        const { renderer, raw, analyser, loseContext, named } = setup()

        renderer.dispose()
        renderer.dispose()
        renderer.render()

        expect(raw.disconnect).toHaveBeenCalledWith(analyser)
        expect(loseContext).toHaveBeenCalledTimes(1)
        expect(named('drawArrays')).toHaveLength(0)
        expect(named('deleteRenderbuffer')).toHaveLength(1)
    })
})
