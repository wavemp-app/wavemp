import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
    autoBlendSeconds,
    blendSecondsFor,
    colorSchemeBlendSeconds,
    drawingSize,
    manualBlendSeconds,
    reducedBlendFactor,
    startColorScheme,
    VisualizationController,
    type AutoChange,
    type VisualizationControllerEvents,
} from '../controller'
import { FakeClock, FakeModule, fakeAnalyser } from './fakes'

function canvas(): HTMLCanvasElement {
    return document.createElement('canvas')
}

describe('VisualizationController', () => {
    let module: FakeModule
    let clock: FakeClock
    let events: Required<VisualizationControllerEvents>

    function create(
        preset: string | null = null,
        autoChange: AutoChange = { enabled: true, seconds: 30 },
    ): VisualizationController {
        return new VisualizationController(module, preset, autoChange, events, clock)
    }

    /** A controller that draws: loaded, with audio and canvas, visible. */
    async function running(
        preset: string | null = 'A',
        autoChange?: AutoChange,
    ): Promise<VisualizationController> {
        const controller = create(preset, autoChange)
        controller.setCanvas(canvas())
        controller.setAudio(fakeAnalyser())
        controller.setVisible(true)
        await module.finishLoad()
        return controller
    }

    beforeEach(() => {
        module = new FakeModule()
        clock = new FakeClock()
        events = {
            state: vi.fn(),
            presets: vi.fn(),
            preset: vi.fn(),
            canvasReleased: vi.fn(),
            quality: vi.fn(),
            colorSchemes: vi.fn(),
            colorScheme: vi.fn(),
        }
    })

    it('loads nothing until the visualization is shown', () => {
        const controller = create()
        controller.setAudio(fakeAnalyser())
        controller.setCanvas(canvas())

        expect(controller.currentState).toBe('idle')
        expect(module.loads).toBe(0)
    })

    it('loads the module once when it is shown and starts with the stored preset', async () => {
        const controller = create('B')
        controller.setVisible(true)
        controller.setVisible(false)
        controller.setVisible(true)

        expect(controller.currentState).toBe('loading')
        await module.finishLoad()

        expect(module.loads).toBe(1)
        expect(events.presets).toHaveBeenCalledWith(module.presets)
        expect(controller.preset).toBe('B')
        expect(events.preset).toHaveBeenLastCalledWith('B')
    })

    it('starts with a random preset when the stored one is unknown', async () => {
        clock.randomValue = 0.99
        const controller = create('Gone')
        controller.setVisible(true)

        await module.finishLoad()

        expect(controller.preset).toBe('C')
    })

    it('waits for the music while the player has no output', async () => {
        const controller = create('A')
        controller.setCanvas(canvas())
        controller.setVisible(true)

        await module.finishLoad()

        expect(controller.currentState).toBe('waiting')
        expect(module.renderers).toHaveLength(0)
        expect(clock.frames.size).toBe(0)
    })

    it('draws one frame per animation frame with the output of the player', async () => {
        const audio = fakeAnalyser()
        const element = canvas()
        const controller = create('A')
        controller.resize(640, 360)
        controller.setCanvas(element)
        controller.setAudio(audio)
        controller.setVisible(true)
        await module.finishLoad()

        expect(controller.currentState).toBe('running')
        expect(module.renderer?.options).toEqual({
            canvas: element,
            audio,
            width: 640,
            height: 360,
            quality: 'high',
            colorScheme: null,
            intensity: 'normal',
            speed: 50,
        })
        expect(module.renderer?.showPreset).toHaveBeenCalledWith('A', 0)

        clock.frame()
        clock.frame()
        expect(module.renderer?.render).toHaveBeenCalledTimes(2)
        expect(clock.frames.size).toBe(1)
    })

    it('stops drawing and the automatic change while it is hidden', async () => {
        const controller = await running()
        expect(clock.frames.size).toBe(1)
        expect(clock.timers.size).toBe(1)

        controller.setVisible(false)

        expect(controller.currentState).toBe('paused')
        expect(clock.frames.size).toBe(0)
        expect(clock.timers.size).toBe(0)
        expect(module.renderer?.dispose).not.toHaveBeenCalled()

        controller.setVisible(true)
        expect(controller.currentState).toBe('running')
        expect(clock.frames.size).toBe(1)
        expect(module.renderers).toHaveLength(1)
    })

    it('creates the renderer only once it is shown', async () => {
        const controller = create('A')
        controller.setCanvas(canvas())
        controller.setAudio(fakeAnalyser())
        controller.setVisible(true)
        controller.setVisible(false)
        await module.finishLoad()

        expect(controller.currentState).toBe('paused')
        expect(module.renderers).toHaveLength(0)

        controller.setVisible(true)
        expect(module.renderers).toHaveLength(1)
    })

    it('changes to a random other preset after the interval', async () => {
        clock.randomValue = 0.99
        const controller = await running('A', { enabled: true, seconds: 20 })

        expect(clock.elapse()).toEqual([20000])

        expect(controller.preset).toBe('C')
        expect(module.renderer?.showPreset).toHaveBeenLastCalledWith('C', autoBlendSeconds)
        expect(clock.timers.size).toBe(1)
    })

    it('does not change automatically while the automatic change is off', async () => {
        const controller = await running('A', { enabled: false, seconds: 20 })
        expect(clock.timers.size).toBe(0)

        controller.setAutoChange({ enabled: true, seconds: 10 })
        expect([...clock.timers.values()].map((timer) => timer.delay)).toEqual([10000])

        controller.setAutoChange({ enabled: false, seconds: 10 })
        expect(clock.timers.size).toBe(0)
    })

    it('shows a chosen preset and starts the interval again', async () => {
        const controller = await running('A')
        const [before] = clock.timers.keys()

        controller.selectPreset('B')

        expect(controller.preset).toBe('B')
        expect(module.renderer?.showPreset).toHaveBeenLastCalledWith('B', manualBlendSeconds)
        expect(clock.timers.size).toBe(1)
        expect(clock.timers.has(before!)).toBe(false)
    })

    it('ignores an unknown preset', async () => {
        const controller = await running('A')

        controller.selectPreset('Gone')

        expect(controller.preset).toBe('A')
    })

    it('steps to the previous and the next preset', async () => {
        const controller = await running('A')

        expect(controller.step(1)).toBe('B')
        expect(controller.step(-1)).toBe('A')
        expect(controller.step(-1)).toBe('C')
        expect(controller.preset).toBe('C')
    })

    it('hands a new size to the renderer', async () => {
        const controller = await running()

        controller.resize(800, 600)

        expect(module.renderer?.resize).toHaveBeenCalledWith(800, 600)
    })

    it('needs a new canvas when the output of the player changes', async () => {
        const controller = await running()
        const first = module.renderer

        controller.setAudio(fakeAnalyser())

        expect(first?.dispose).toHaveBeenCalled()
        expect(events.canvasReleased).toHaveBeenCalled()
        expect(module.renderers).toHaveLength(1)

        const next = canvas()
        controller.setCanvas(next)
        expect(module.renderers).toHaveLength(2)
        expect(module.renderer?.options.canvas).toBe(next)
        expect(controller.currentState).toBe('running')
    })

    it('draws at the quality of the profile', async () => {
        const controller = create('A')
        controller.setQuality('Low')
        controller.setCanvas(canvas())
        controller.setAudio(fakeAnalyser())
        controller.setVisible(true)
        await module.finishLoad()

        expect(module.renderer?.options.quality).toBe('low')
        expect(controller.quality).toBe('low')
        expect(events.quality).toHaveBeenCalledWith('low')

        controller.setQuality('Medium')
        expect(module.renderer?.setQuality).toHaveBeenCalledWith('medium')

        // A fixed level stays, however slowly the frames come.
        clock.frameMs = 50
        for (let index = 0; index < 120; index++) {
            clock.frame()
        }
        expect(controller.quality).toBe('medium')
    })

    it('lowers the level with Auto while the frames come slowly', async () => {
        const controller = await running()
        expect(controller.quality).toBe('high')

        clock.frameMs = 1000 / 30
        for (let index = 0; index < 70; index++) {
            clock.frame()
        }

        expect(controller.quality).toBe('medium')
        expect(module.renderer?.setQuality).toHaveBeenLastCalledWith('medium')
        expect(events.quality).toHaveBeenLastCalledWith('medium')
    })

    it('keeps the level with Auto while the frames come at 60 per second', async () => {
        const controller = await running()

        for (let index = 0; index < 600; index++) {
            clock.frame()
        }

        expect(controller.quality).toBe('high')
        expect(module.renderer?.setQuality).not.toHaveBeenCalled()
    })

    it('does not count a pause as a slow frame', async () => {
        const controller = await running()
        clock.frame()

        controller.setVisible(false)
        clock.time += 5000
        controller.setVisible(true)
        for (let index = 0; index < 200; index++) {
            clock.frame()
        }

        expect(controller.quality).toBe('high')
    })

    it('reports a browser that cannot show the module', () => {
        module.supported = false
        const controller = create()

        controller.setVisible(true)

        expect(controller.currentState).toBe('unsupported')
        expect(module.loads).toBe(0)
    })

    it('fails when the module cannot be loaded', async () => {
        const controller = create()
        controller.setVisible(true)

        await module.failLoad()

        expect(controller.currentState).toBe('failed')
        expect(events.state).toHaveBeenLastCalledWith('failed')
    })

    it('fails when the module has no presets', async () => {
        module.presets = []
        const controller = create()
        controller.setVisible(true)

        await module.finishLoad()

        expect(controller.currentState).toBe('failed')
    })

    it('fails when the renderer cannot be created', async () => {
        module.failCreate = true

        const controller = await running()

        expect(controller.currentState).toBe('failed')
        expect(clock.frames.size).toBe(0)
    })

    it('fails and stops drawing when a frame throws', async () => {
        const controller = await running()
        module.renderer!.render.mockImplementation(() => {
            throw new Error('Context lost.')
        })

        clock.frame()

        expect(controller.currentState).toBe('failed')
        expect(module.renderer?.dispose).toHaveBeenCalled()
        expect(clock.frames.size).toBe(0)
        expect(clock.timers.size).toBe(0)
    })

    it('releases the renderer and stops everything when disposed', async () => {
        const controller = await running()

        controller.dispose()
        controller.setVisible(true)

        expect(module.renderer?.dispose).toHaveBeenCalled()
        expect(clock.frames.size).toBe(0)
        expect(clock.timers.size).toBe(0)
        expect(events.canvasReleased).not.toHaveBeenCalled()
    })

    it('ignores a load that finishes after it was disposed', async () => {
        const controller = create()
        controller.setAudio(fakeAnalyser())
        controller.setCanvas(canvas())
        controller.setVisible(true)

        controller.dispose()
        await module.finishLoad()

        expect(module.renderers).toHaveLength(0)
        expect(events.presets).not.toHaveBeenCalled()
    })

    describe('with color schemes', () => {
        const schemes = ['red', 'green', 'blue'].map((name) => ({ id: name, name }))

        beforeEach(() => {
            module.colorSchemes = schemes
        })

        it('starts with the stored color scheme and hands it to the renderer', async () => {
            const controller = create('A')
            controller.setStoredColorScheme('green')
            controller.setCanvas(canvas())
            controller.setAudio(fakeAnalyser())
            controller.setVisible(true)
            await module.finishLoad()

            expect(controller.colorSchemes).toEqual(schemes)
            expect(controller.colorScheme).toBe('green')
            expect(events.colorSchemes).toHaveBeenCalledWith(schemes)
            expect(events.colorScheme).toHaveBeenLastCalledWith('green')
            expect(module.renderer?.options.colorScheme).toBe('green')
        })

        it('starts with the first color scheme when the stored one is unknown or missing', async () => {
            const controller = create('A')
            controller.setStoredColorScheme('violet')
            controller.setVisible(true)
            await module.finishLoad()

            expect(controller.colorScheme).toBe('red')
            expect(startColorScheme(schemes, null)).toBe('red')
            expect(startColorScheme([], 'red')).toBeNull()
        })

        it('keeps the color scheme of a running visualization when the stored one changes', async () => {
            const controller = await running('A')

            controller.setStoredColorScheme('blue')

            expect(controller.colorScheme).toBe('red')
        })

        it('shows a chosen color scheme apart from the preset and the automatic change', async () => {
            const controller = await running('A')
            const timers = [...clock.timers.keys()]

            controller.selectColorScheme('blue')

            expect(controller.colorScheme).toBe('blue')
            expect(controller.preset).toBe('A')
            expect(events.colorScheme).toHaveBeenLastCalledWith('blue')
            expect(module.renderer?.showColorScheme).toHaveBeenCalledWith(
                'blue',
                colorSchemeBlendSeconds,
            )
            expect(module.renderer?.showPreset).toHaveBeenCalledTimes(1)
            expect([...clock.timers.keys()]).toEqual(timers)
        })

        it('ignores an unknown color scheme and the shown one', async () => {
            const controller = await running('A')

            controller.selectColorScheme('violet')
            controller.selectColorScheme('red')

            expect(controller.colorScheme).toBe('red')
            expect(module.renderer?.showColorScheme).not.toHaveBeenCalled()
        })

        it('changes only the preset automatically and keeps the color scheme', async () => {
            clock.randomValue = 0.99
            const controller = await running('A', { enabled: true, seconds: 5 })
            controller.selectColorScheme('green')

            clock.elapse()
            clock.elapse()

            expect(module.renderer?.showPreset).toHaveBeenCalledTimes(3)
            expect(controller.colorScheme).toBe('green')
            expect(module.renderer?.showColorScheme).toHaveBeenCalledTimes(1)
        })
    })

    it('has no color scheme for modules without color schemes', async () => {
        const controller = await running('A')

        expect(controller.colorSchemes).toEqual([])
        expect(controller.colorScheme).toBeNull()
        expect(events.colorSchemes).toHaveBeenCalledWith([])
        expect(events.colorScheme).toHaveBeenLastCalledWith(null)

        controller.selectColorScheme('red')
        expect(module.renderer?.showColorScheme).not.toHaveBeenCalled()
    })

    it('draws at the intensity and the speed it is given', async () => {
        const controller = create('A')
        controller.setIntensity('reduced')
        controller.setSpeed(25)
        controller.setCanvas(canvas())
        controller.setAudio(fakeAnalyser())
        controller.setVisible(true)
        await module.finishLoad()

        expect(controller.currentIntensity).toBe('reduced')
        expect(controller.currentSpeed).toBe(25)
        expect(module.renderer?.options).toMatchObject({ intensity: 'reduced', speed: 25 })

        controller.setIntensity('normal')
        controller.setIntensity('normal')
        controller.setSpeed(75)
        expect(module.renderer?.setIntensity).toHaveBeenCalledTimes(1)
        expect(module.renderer?.setIntensity).toHaveBeenCalledWith('normal')
        expect(module.renderer?.setSpeed).toHaveBeenCalledWith(75)
    })

    it('blends every change longer at the reduced intensity', async () => {
        module.colorSchemes = ['red', 'green'].map((name) => ({ id: name, name }))
        clock.randomValue = 0
        const controller = await running('A', { enabled: true, seconds: 10 })
        controller.setIntensity('reduced')

        controller.selectPreset('B')
        expect(module.renderer?.showPreset).toHaveBeenLastCalledWith(
            'B',
            manualBlendSeconds * reducedBlendFactor,
        )

        clock.elapse()
        expect(module.renderer?.showPreset).toHaveBeenLastCalledWith(
            'A',
            autoBlendSeconds * reducedBlendFactor,
        )

        controller.selectColorScheme('green')
        expect(module.renderer?.showColorScheme).toHaveBeenLastCalledWith(
            'green',
            colorSchemeBlendSeconds * reducedBlendFactor,
        )
        expect(blendSecondsFor(3, 'normal')).toBe(3)
        expect(blendSecondsFor(3, 'reduced')).toBe(6)
    })

    it('blends the presets as long as the module wants', async () => {
        module.blendSeconds = { manual: 2, automatic: 5 }
        clock.randomValue = 0
        const controller = await running('A', { enabled: true, seconds: 10 })

        controller.selectPreset('C')
        expect(module.renderer?.showPreset).toHaveBeenLastCalledWith('C', 2)

        clock.elapse()
        expect(module.renderer?.showPreset).toHaveBeenLastCalledWith('A', 5)
    })
})

describe('drawingSize', () => {
    it('multiplies the size with the pixel ratio of the device', () => {
        expect(drawingSize(400, 225, 1.5)).toEqual({ width: 600, height: 338 })
    })

    it('draws with at most two device pixels per CSS pixel', () => {
        expect(drawingSize(400, 225, 3)).toEqual({ width: 800, height: 450 })
    })

    it('never gets smaller than one pixel', () => {
        expect(drawingSize(0, 0, 0)).toEqual({ width: 1, height: 1 })
    })
})
