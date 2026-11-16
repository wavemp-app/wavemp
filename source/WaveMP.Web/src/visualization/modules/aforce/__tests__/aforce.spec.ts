import { describe, expect, it, vi } from 'vitest'
import type { VisualizationRendererOptions } from '../../../module'
import { visualizationModule, visualizationModuleChoices, visualizationModules } from '../..'
import {
    aforcePresets,
    browserAForceEnvironment,
    createAForceModule,
    type AForceEnvironment,
} from '..'
import { aforceProfiles } from '../profiles'
import { AForceRenderer } from '../renderer'
import { aforceStyles } from '../styles'

type Call = [string, unknown[]]

/**
 * A stand-in for a WebGL 2 context: every method is recorded and succeeds, every constant is its
 * own name. jsdom has no WebGL, so the tests check what the renderer asks the context to do.
 */
function fakeWebGl() {
    const calls: Call[] = []
    const loseContext = vi.fn()
    let resources = 0
    const results: Record<string, (...args: unknown[]) => unknown> = {
        getShaderParameter: () => true,
        getProgramParameter: () => true,
        isContextLost: () => false,
        getUniformLocation: (_, name) => ({ name }),
        getExtension: (name) => (name === 'WEBGL_lose_context' ? { loseContext } : null),
    }
    const gl = new Proxy(
        {},
        {
            get(_, property: string) {
                if (/^[A-Z0-9_]+$/.test(property)) {
                    return property
                }

                return (...args: unknown[]) => {
                    calls.push([property, args])
                    if (property in results) {
                        return results[property]!(...args)
                    }

                    return property.startsWith('create') ? { id: ++resources } : undefined
                }
            },
        },
    )
    const named = (name: string) => calls.filter(([property]) => property === name)
    return { gl, calls, named, loseContext }
}

function fakeAudio() {
    const analyser = {
        fftSize: 0,
        smoothingTimeConstant: 0,
        get frequencyBinCount() {
            return this.fftSize / 2
        },
        context: { sampleRate: 48000 },
        getByteFrequencyData: vi.fn((data: Uint8Array) => data.fill(120)),
        getFloatTimeDomainData: vi.fn((data: Float32Array) =>
            data.forEach((_, index) => (data[index] = Math.sin(index / 10))),
        ),
    }
    const audio = {
        context: { createAnalyser: vi.fn(() => analyser) },
        connect: vi.fn(),
        disconnect: vi.fn(),
    }
    return { audio: audio as unknown as AnalyserNode, analyser, raw: audio }
}

function setup(quality: VisualizationRendererOptions['quality'] = 'high') {
    const webgl = fakeWebGl()
    const canvas = document.createElement('canvas')
    canvas.getContext = vi.fn(() => webgl.gl) as unknown as HTMLCanvasElement['getContext']
    const sound = fakeAudio()
    let now = 1000
    const renderer = new AForceRenderer(
        { canvas, audio: sound.audio, width: 800, height: 450, quality },
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

    it('is a module of the visualization with its style variants as presets', async () => {
        const module = createAForceModule(environment())

        expect(module.id).toBe('AForce')
        const loaded = await module.load()
        expect(loaded.presets).toBe(aforcePresets)
        expect(loaded.presets.map((preset) => preset.id)).toEqual(
            aforceStyles.map((style) => style.id),
        )
        expect(loaded.presets[0]).toEqual({
            id: 'aurora',
            name: 'Aurora',
            labelKey: 'nowPlaying.aforceStyles.aurora',
        })
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
        expect(visualizationModuleChoices.map((choice) => choice.id)).toEqual([
            'Milkdrop',
            'AForce',
        ])
    })
})

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

    it('draws the waveforms, the particles and the composite of a frame', () => {
        const { renderer, named, analyser } = setup('high')

        renderer.render()

        expect(analyser.getByteFrequencyData).toHaveBeenCalledTimes(1)
        expect(analyser.getFloatTimeDomainData).toHaveBeenCalledTimes(1)
        const profile = aforceProfiles.high
        expect(named('drawArraysInstanced')).toEqual([
            ['drawArraysInstanced', ['TRIANGLE_STRIP', 0, profile.wavePoints * 2, profile.waves]],
        ])
        expect(named('drawArrays').map(([, args]) => args)).toEqual([
            ['TRIANGLES', 0, 3],
            ['POINTS', 0, profile.particles],
            ['TRIANGLES', 0, 3],
        ])
        // The scene at full resolution, then the canvas.
        expect(named('viewport').map(([, args]) => args)).toEqual([
            [0, 0, 800, 450],
            [0, 0, 800, 450],
        ])
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

    it('resizes the canvas and the scene', () => {
        const { renderer, canvas, named, calls } = setup('medium')

        renderer.resize(1000, 500)
        calls.length = 0
        renderer.render()

        expect([canvas.width, canvas.height]).toEqual([1000, 500])
        expect(named('viewport').map(([, args]) => args)).toEqual([
            [0, 0, 750, 375],
            [0, 0, 1000, 500],
        ])
    })

    it('blends into a chosen style and ignores unknown ones', () => {
        const { renderer, named, calls, advance } = setup()
        const glow = () =>
            named('uniform3fv').find(([, args]) => (args[0] as { name: string }).name === 'uGlow')
        const ember = aforceStyles.find((style) => style.id === 'ember')!

        renderer.showPreset('gone', 1)
        renderer.showPreset('ember', 1)
        advance(500)
        renderer.render()
        const halfway = glow()![1][1] as number[]
        calls.length = 0
        advance(600)
        renderer.render()
        const done = glow()![1][1] as number[]

        expect(halfway).not.toEqual(ember.colors.glow)
        expect(done).toEqual(ember.colors.glow)
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
