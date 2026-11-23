import { describe, expect, it, vi } from 'vitest'
import type { Butterchurn, ButterchurnVisualizer } from 'butterchurn'
import type { VisualizationRendererOptions } from '../module'
import {
    browserMilkdropEnvironment,
    createMilkdropModule,
    effectiveMilkdropSpeed,
    maxMilkdropStepsPerFrame,
    milkdropBlendSeconds,
    milkdropPresets,
    milkdropQuality,
    milkdropStepsPerSecond,
    MilkdropStepper,
    reducedMilkdropSpeed,
    type MilkdropEnvironment,
} from '../modules/milkdrop'
import { visualizationModule, visualizationModules } from '../modules'
import { fakeWebGl, rendererDefaults } from './webgl'

function fakeVisualizer(): ButterchurnVisualizer {
    return {
        connectAudio: vi.fn(),
        disconnectAudio: vi.fn(),
        loadPreset: vi.fn(),
        setRendererSize: vi.fn(),
        render: vi.fn(),
    }
}

const pack: Record<string, object> = {
    'Waves 10': { id: 'waves-10' },
    'waves 9': { id: 'waves-9' },
    Aurora: { id: 'aurora' },
}

/** A canvas whose WebGL 2 context is a fake that records its calls. */
function fakeCanvas() {
    const webgl = fakeWebGl()
    const canvas = document.createElement('canvas')
    canvas.getContext = vi.fn(() => webgl.gl) as unknown as HTMLCanvasElement['getContext']
    return { canvas, ...webgl }
}

function environment(overrides: Partial<MilkdropEnvironment> = {}) {
    const visualizer = fakeVisualizer()
    const butterchurn: Butterchurn = { createVisualizer: vi.fn(() => visualizer) }
    const source = fakeCanvas()
    const clock = { time: 1000 }
    const env: MilkdropEnvironment = {
        supported: vi.fn(() => true),
        loadButterchurn: vi.fn(() => Promise.resolve(butterchurn)),
        loadPresets: vi.fn(() => Promise.resolve(pack)),
        createCanvas: vi.fn(() => source.canvas),
        now: () => clock.time,
        ...overrides,
    }
    return { env, butterchurn, visualizer, source, clock }
}

function analyser() {
    const context = { sampleRate: 48000 }
    return { context } as unknown as AnalyserNode
}

/** A renderer of a loaded module drawing into a fake stage canvas. */
async function renderer(options: Partial<VisualizationRendererOptions> = {}) {
    const setup = environment()
    const loaded = await createMilkdropModule(setup.env).load()
    const stage = fakeCanvas()
    const audio = analyser()
    const created = loaded.createRenderer({
        canvas: stage.canvas,
        audio,
        width: 960,
        height: 540,
        quality: 'high',
        ...rendererDefaults,
        ...options,
    })
    /** Draws frames at the given rate for the given seconds. */
    const play = (framesPerSecond: number, seconds: number) => {
        for (let frame = 0; frame < Math.round(framesPerSecond * seconds); frame++) {
            setup.clock.time += 1000 / framesPerSecond
            created.render()
        }
    }
    return { ...setup, loaded, stage, audio, renderer: created, play }
}

describe('milkdropPresets', () => {
    it('lists the presets of a pack sorted by name', () => {
        expect(milkdropPresets(pack)).toEqual([
            { id: 'Aurora', name: 'Aurora' },
            { id: 'waves 9', name: 'waves 9' },
            { id: 'Waves 10', name: 'Waves 10' },
        ])
    })

    it('leaves out names the settings of a profile cannot keep', () => {
        const presets = milkdropPresets({ [' ']: {}, ['x'.repeat(201)]: {}, ['y'.repeat(200)]: {} })

        expect(presets.map((preset) => preset.name.length)).toEqual([200])
    })
})

describe('the module Milkdrop', () => {
    it('checks the browser only once', () => {
        const { env } = environment()
        const module = createMilkdropModule(env)

        expect(module.id).toBe('Milkdrop')
        expect(module.isSupported()).toBe(true)
        expect(module.isSupported()).toBe(true)
        expect(env.supported).toHaveBeenCalledTimes(1)
    })

    it('loads butterchurn and the presets once for all callers', async () => {
        const { env } = environment()
        const module = createMilkdropModule(env)

        const [first, second] = await Promise.all([module.load(), module.load()])

        expect(first).toBe(second)
        expect(env.loadButterchurn).toHaveBeenCalledTimes(1)
        expect(first.presets.map((preset) => preset.id)).toEqual(['Aurora', 'waves 9', 'Waves 10'])
        expect(first.colorSchemes ?? []).toEqual([])
        expect(first.blendSeconds).toEqual(milkdropBlendSeconds)
    })

    it('loads again after a failed load', async () => {
        const { env } = environment()
        vi.mocked(env.loadPresets).mockRejectedValueOnce(new Error('offline'))
        const module = createMilkdropModule(env)

        await expect(module.load()).rejects.toThrow('offline')
        await expect(module.load()).resolves.toBeDefined()
        expect(env.loadPresets).toHaveBeenCalledTimes(2)
    })

    it('blends its presets slower than the other modules', () => {
        expect(milkdropBlendSeconds.manual).toBeGreaterThanOrEqual(2)
        expect(milkdropBlendSeconds.automatic).toBeGreaterThanOrEqual(5)
    })

    it('lets butterchurn draw the output of the player into a canvas of its own', async () => {
        const { butterchurn, visualizer, source, stage, audio } = await renderer()

        expect(butterchurn.createVisualizer).toHaveBeenCalledWith(audio.context, source.canvas, {
            width: 960,
            height: 540,
            pixelRatio: 1,
            textureRatio: 1,
            meshWidth: 48,
            meshHeight: 36,
        })
        expect(visualizer.connectAudio).toHaveBeenCalledWith(audio)
        expect([source.canvas.width, source.canvas.height]).toEqual([960, 540])
        expect([stage.canvas.width, stage.canvas.height]).toEqual([960, 540])
        expect(stage.canvas.getContext).toHaveBeenCalledWith(
            'webgl2',
            expect.objectContaining({ alpha: false, preserveDrawingBuffer: false }),
        )
    })

    it('fails without a WebGL 2 context on the stage', async () => {
        const { env } = environment()
        const loaded = await createMilkdropModule(env).load()
        const canvas = document.createElement('canvas')
        canvas.getContext = vi.fn(() => null) as unknown as HTMLCanvasElement['getContext']

        expect(() =>
            loaded.createRenderer({
                canvas,
                audio: analyser(),
                width: 10,
                height: 10,
                quality: 'low',
                ...rendererDefaults,
            }),
        ).toThrow()
    })

    it('blends a preset in the time of the presets, which runs at the speed', async () => {
        const { renderer: shown, visualizer } = await renderer({ speed: 50 })

        shown.showPreset('Aurora', 2)
        shown.showPreset('Gone', 2)

        expect(visualizer.loadPreset).toHaveBeenCalledTimes(1)
        expect(visualizer.loadPreset).toHaveBeenCalledWith(pack.Aurora, 1)

        shown.setSpeed?.(100)
        shown.showPreset('Aurora', 2)
        expect(visualizer.loadPreset).toHaveBeenLastCalledWith(pack.Aurora, 2)
    })

    it('resizes both canvases, butterchurn and the guard', async () => {
        const { renderer: shown, visualizer, source, stage } = await renderer()

        shown.resize(320, 180)

        expect(visualizer.setRendererSize).toHaveBeenCalledWith(320, 180, {
            pixelRatio: 1,
            textureRatio: 1,
            meshWidth: 48,
            meshHeight: 36,
        })
        expect([stage.canvas.width, stage.canvas.height]).toEqual([320, 180])
        expect([source.canvas.width, source.canvas.height]).toEqual([320, 180])
        // The guard takes new frames of the new size.
        expect(
            stage.named('texImage2D').some(([, args]) => args[3] === 320 && args[4] === 180),
        ).toBe(true)
    })

    it('draws its textures and mesh at the quality level', async () => {
        const {
            butterchurn,
            visualizer,
            renderer: shown,
            stage,
        } = await renderer({ quality: 'low' })

        expect(butterchurn.createVisualizer).toHaveBeenCalledWith(
            expect.anything(),
            expect.anything(),
            expect.objectContaining(milkdropQuality.low),
        )

        shown.setQuality('low')
        expect(visualizer.setRendererSize).not.toHaveBeenCalled()

        shown.setQuality('medium')
        expect(visualizer.setRendererSize).toHaveBeenCalledWith(
            stage.canvas.width,
            stage.canvas.height,
            { pixelRatio: 1, ...milkdropQuality.medium },
        )
        expect(milkdropQuality.low.textureRatio).toBeLessThan(milkdropQuality.medium.textureRatio)
        expect(milkdropQuality.medium.textureRatio).toBeLessThan(milkdropQuality.high.textureRatio)
    })

    it('disconnects from the player and frees both drawing contexts when disposed', async () => {
        const { renderer: shown, visualizer, audio, stage, source } = await renderer()

        shown.dispose()
        shown.dispose()
        shown.render()

        expect(visualizer.disconnectAudio).toHaveBeenCalledTimes(1)
        expect(visualizer.disconnectAudio).toHaveBeenCalledWith(audio)
        expect(stage.loseContext).toHaveBeenCalledTimes(1)
        expect(source.loseContext).toHaveBeenCalledTimes(1)
        expect(visualizer.render).not.toHaveBeenCalled()
    })
})

describe('the speed of Milkdrop', () => {
    it('runs 60 steps a second at 100 % and half of them at 50 %, each a 60th of a second', async () => {
        const fast = await renderer({ speed: 100 })
        fast.renderer.render()
        fast.play(60, 1)
        // One step before the first frame, then 60 in the second.
        expect(fast.visualizer.render).toHaveBeenCalledTimes(1 + milkdropStepsPerSecond)

        const half = await renderer({ speed: 50 })
        half.renderer.render()
        half.play(60, 1)
        expect(half.visualizer.render).toHaveBeenCalledTimes(1 + milkdropStepsPerSecond / 2)
        for (const call of vi.mocked(half.visualizer.render).mock.calls) {
            expect(call[0]).toEqual({ elapsedTime: 1 / milkdropStepsPerSecond })
        }
    })

    it('runs as many steps on a display of 144 Hz as on one of 60 Hz', async () => {
        const at60 = await renderer({ speed: 50 })
        at60.renderer.render()
        at60.play(60, 4)
        const at144 = await renderer({ speed: 50 })
        at144.renderer.render()
        at144.play(144, 4)

        const steps60 = vi.mocked(at60.visualizer.render).mock.calls.length
        const steps144 = vi.mocked(at144.visualizer.render).mock.calls.length
        expect(steps60).toBe(1 + 120)
        expect(steps144).toBe(steps60)
    })

    it('runs slower at the reduced intensity', async () => {
        const reduced = await renderer({ speed: 100, intensity: 'reduced' })
        reduced.renderer.render()
        reduced.play(60, 1)
        expect(reduced.visualizer.render).toHaveBeenCalledTimes(
            1 + (milkdropStepsPerSecond * reducedMilkdropSpeed) / 100,
        )

        reduced.renderer.setIntensity?.('normal')
        vi.mocked(reduced.visualizer.render).mockClear()
        reduced.play(60, 1)
        expect(reduced.visualizer.render).toHaveBeenCalledTimes(milkdropStepsPerSecond)
    })

    it('follows a new speed', async () => {
        const { renderer: shown, visualizer, play } = await renderer({ speed: 100 })
        shown.render()
        shown.setSpeed?.(25)
        vi.mocked(visualizer.render).mockClear()

        play(60, 2)

        expect(visualizer.render).toHaveBeenCalledTimes(30)
    })

    it('drops the steps of a long hiccup instead of catching up', async () => {
        const { renderer: shown, visualizer, clock } = await renderer({ speed: 100 })
        shown.render()
        clock.time += 1000
        shown.render()

        expect(visualizer.render).toHaveBeenCalledTimes(1 + maxMilkdropStepsPerFrame)
    })

    it('keeps the speed within the range of the profile', () => {
        expect(effectiveMilkdropSpeed(50, 'normal')).toBe(50)
        expect(effectiveMilkdropSpeed(100, 'reduced')).toBe(reducedMilkdropSpeed)
        expect(effectiveMilkdropSpeed(25, 'reduced')).toBe(25)
        expect(effectiveMilkdropSpeed(500, 'normal')).toBe(100)
        expect(effectiveMilkdropSpeed(0, 'normal')).toBe(10)
    })

    it('counts the steps and the phase between them', () => {
        const stepper = new MilkdropStepper()

        expect(stepper.advance(1 / 60, 50)).toBe(0)
        expect(stepper.phase).toBeCloseTo(0.5)
        expect(stepper.advance(1 / 60, 50)).toBe(1)
        expect(stepper.phase).toBeCloseTo(0)
        expect(stepper.advance(0.025, 100)).toBe(1)
        expect(stepper.phase).toBeCloseTo(0.5)
        expect(stepper.advance(-1, 100)).toBe(0)

        stepper.reset()
        expect(stepper.phase).toBe(0)
    })
})

describe('the picture of Milkdrop', () => {
    it('takes over every step of butterchurn and shows it through the flash guard', async () => {
        const { renderer: shown, stage, source, play } = await renderer({ speed: 50 })
        shown.render()
        play(60, 0.5)

        const uploads = stage
            .named('texImage2D')
            .filter(([, args]) => args[args.length - 1] === source.canvas)
        // One step before the first frame and 15 in half a second.
        expect(uploads).toHaveLength(16)
        // Every frame ends with the output pass onto the canvas and the copy of it.
        expect(stage.named('blitFramebuffer')).toHaveLength(31)
        expect(stage.named('readPixels')).toHaveLength(0)
    })

    it('cross-fades the last two steps by the time between them', async () => {
        const { renderer: shown, stage, play } = await renderer({ speed: 25 })
        const weights = () =>
            stage
                .named('uniform1f')
                .filter(([, args]) => (args[0] as { name: string }).name === 'uEarlierWeight')
                .map(([, args]) => args[1] as number)
                // The reduction and the output pass both read it.
                .filter((_, index) => index % 2 === 0)

        shown.render()
        // The first steps: nothing to cross-fade yet.
        expect(weights().at(-1)).toBe(0)

        // At 25 % a step takes 4 frames of 60 Hz: the earlier step fades out over them.
        play(60, 8 / 60)
        const last = weights().slice(-4)
        expect(last[0]).toBeCloseTo(0.75)
        expect(last[1]).toBeCloseTo(0.5)
        expect(last[2]).toBeCloseTo(0.25)
        expect(last[3]).toBeCloseTo(1)
    })

    it('limits flashes further at the reduced intensity', async () => {
        const { renderer: shown, stage, clock } = await renderer({ speed: 50 })
        const steps = () =>
            stage
                .named('uniform2f')
                .filter(([, args]) => (args[0] as { name: string }).name === 'uSteps')
                .map(([, args]) => args[1] as number)
        shown.render()
        clock.time += 100
        shown.render()
        shown.setIntensity?.('reduced')
        clock.time += 100
        shown.render()

        const [, normal, reduced] = steps()
        expect(reduced).toBeLessThan(normal!)
    })
})

describe('the packages', () => {
    it('load as modules with butterchurn and at least a hundred presets', async () => {
        const butterchurn = await browserMilkdropEnvironment.loadButterchurn()
        const presets = milkdropPresets(await browserMilkdropEnvironment.loadPresets())

        expect(typeof butterchurn.createVisualizer).toBe('function')
        expect(presets.length).toBeGreaterThanOrEqual(100)
    })
})

describe('visualizationModule', () => {
    it('finds the module of the profile and falls back to the default', () => {
        expect(visualizationModule('Milkdrop')).toBe(visualizationModules.Milkdrop)
        expect(visualizationModule('AForce')).toBe(visualizationModules.AForce)
        expect(visualizationModule(null)).toBe(visualizationModules.Milkdrop)
        expect(visualizationModule('Spectrum' as 'Milkdrop')).toBe(visualizationModules.Milkdrop)
    })
})
