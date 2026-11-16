import { describe, expect, it, vi } from 'vitest'
import type { Butterchurn, ButterchurnVisualizer } from 'butterchurn'
import {
    browserMilkdropEnvironment,
    createMilkdropModule,
    milkdropPresets,
    type MilkdropEnvironment,
} from '../modules/milkdrop'
import { visualizationModule, visualizationModules } from '../modules'

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

function environment(overrides: Partial<MilkdropEnvironment> = {}) {
    const visualizer = fakeVisualizer()
    const butterchurn: Butterchurn = { createVisualizer: vi.fn(() => visualizer) }
    const env: MilkdropEnvironment = {
        supported: vi.fn(() => true),
        loadButterchurn: vi.fn(() => Promise.resolve(butterchurn)),
        loadPresets: vi.fn(() => Promise.resolve(pack)),
        ...overrides,
    }
    return { env, butterchurn, visualizer }
}

function analyser() {
    const context = { sampleRate: 48000 }
    return { context } as unknown as AnalyserNode
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
    })

    it('loads again after a failed load', async () => {
        const { env } = environment()
        vi.mocked(env.loadPresets).mockRejectedValueOnce(new Error('offline'))
        const module = createMilkdropModule(env)

        await expect(module.load()).rejects.toThrow('offline')
        await expect(module.load()).resolves.toBeDefined()
        expect(env.loadPresets).toHaveBeenCalledTimes(2)
    })

    it('draws the output of the player in its audio context at the size in device pixels', async () => {
        const { env, butterchurn, visualizer } = environment()
        const loaded = await createMilkdropModule(env).load()
        const canvas = document.createElement('canvas')
        const audio = analyser()

        const renderer = loaded.createRenderer({ canvas, audio, width: 960, height: 540 })

        expect(butterchurn.createVisualizer).toHaveBeenCalledWith(audio.context, canvas, {
            width: 960,
            height: 540,
            pixelRatio: 1,
            textureRatio: 1,
        })
        expect(visualizer.connectAudio).toHaveBeenCalledWith(audio)
        expect([canvas.width, canvas.height]).toEqual([960, 540])

        renderer.showPreset('Aurora', 2)
        renderer.showPreset('Gone', 2)
        expect(visualizer.loadPreset).toHaveBeenCalledTimes(1)
        expect(visualizer.loadPreset).toHaveBeenCalledWith(pack.Aurora, 2)

        renderer.resize(320, 180)
        expect(visualizer.setRendererSize).toHaveBeenCalledWith(320, 180, {
            pixelRatio: 1,
            textureRatio: 1,
        })
        expect([canvas.width, canvas.height]).toEqual([320, 180])

        renderer.render()
        expect(visualizer.render).toHaveBeenCalledTimes(1)
    })

    it('disconnects from the player and frees the drawing context when disposed', async () => {
        const { env, visualizer } = environment()
        const loaded = await createMilkdropModule(env).load()
        const canvas = document.createElement('canvas')
        const loseContext = vi.fn()
        canvas.getContext = vi.fn(() => ({
            getExtension: () => ({ loseContext }),
        })) as unknown as HTMLCanvasElement['getContext']
        const audio = analyser()
        const renderer = loaded.createRenderer({ canvas, audio, width: 10, height: 10 })

        renderer.dispose()

        expect(visualizer.disconnectAudio).toHaveBeenCalledWith(audio)
        expect(loseContext).toHaveBeenCalled()
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
        expect(visualizationModule(null)).toBe(visualizationModules.Milkdrop)
        expect(visualizationModule('Spectrum' as 'Milkdrop')).toBe(visualizationModules.Milkdrop)
    })
})
