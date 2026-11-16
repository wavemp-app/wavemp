import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
    autoBlendSeconds,
    drawingSize,
    manualBlendSeconds,
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
