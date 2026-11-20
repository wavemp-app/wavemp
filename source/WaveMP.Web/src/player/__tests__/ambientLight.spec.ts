import { describe, expect, it, vi } from 'vitest'
import {
    AmbientLight,
    ambientCanvasSize,
    ambientInterval,
    ambientState,
    averageColor,
    hasPicture,
    type AmbientCanvas,
    type AmbientConditions,
    type AmbientEnvironment,
    type AmbientVideo,
} from '../ambientLight'

const playing: AmbientConditions = {
    enabled: true,
    ready: true,
    reducedMotion: false,
    shown: true,
    visible: true,
    playing: true,
}

describe('ambientState', () => {
    it('draws while the shown and visible video plays', () => {
        expect(ambientState(playing)).toBe('running')
    })

    it('is off when the profile switched it off, whatever else holds', () => {
        expect(ambientState({ ...playing, enabled: false })).toBe('off')
        expect(ambientState({ ...playing, enabled: false, reducedMotion: true })).toBe('off')
    })

    it('waits until the video has a picture', () => {
        expect(ambientState({ ...playing, ready: false })).toBe('waiting')
        expect(ambientState({ ...playing, ready: false, reducedMotion: true })).toBe('waiting')
    })

    it('pauses while the video is paused, the view hidden or the stage not visible', () => {
        expect(ambientState({ ...playing, playing: false })).toBe('paused')
        expect(ambientState({ ...playing, shown: false })).toBe('paused')
        expect(ambientState({ ...playing, visible: false })).toBe('paused')
    })

    it('is a static color for reduced motion', () => {
        expect(ambientState({ ...playing, reducedMotion: true })).toBe('static')
        expect(ambientState({ ...playing, reducedMotion: true, playing: false })).toBe('static')
    })
})

describe('ambientCanvasSize', () => {
    it('keeps the aspect ratio with 32 pixels on the long side', () => {
        expect(ambientCanvasSize(1920, 1080)).toEqual({ width: 32, height: 18 })
        expect(ambientCanvasSize(1080, 1920)).toEqual({ width: 18, height: 32 })
        expect(ambientCanvasSize(240, 320)).toEqual({ width: 24, height: 32 })
        expect(ambientCanvasSize(720, 720)).toEqual({ width: 32, height: 32 })
    })

    it('keeps at least one pixel on the short side', () => {
        expect(ambientCanvasSize(4000, 10)).toEqual({ width: 32, height: 1 })
        expect(ambientCanvasSize(10, 4000)).toEqual({ width: 1, height: 32 })
    })

    it('is 16:9 while the size is unknown', () => {
        expect(ambientCanvasSize(0, 0)).toEqual({ width: 32, height: 18 })
        expect(ambientCanvasSize(Number.NaN, 1080)).toEqual({ width: 32, height: 18 })
    })
})

/** RGBA pixels from [red, green, blue] triples. */
function pixels(...colors: [number, number, number][]): Uint8ClampedArray {
    return new Uint8ClampedArray(colors.flatMap(([red, green, blue]) => [red, green, blue, 255]))
}

describe('averageColor', () => {
    it('averages the pixels', () => {
        expect(averageColor(pixels([200, 0, 100], [100, 50, 0]))).toBe('rgb(150 25 50)')
    })

    it('leaves out the black bars of a letterboxed picture', () => {
        expect(averageColor(pixels([0, 0, 0], [40, 80, 160], [2, 3, 1], [60, 100, 200]))).toBe(
            'rgb(50 90 180)',
        )
    })

    it('is black for a black picture and nothing without pixels', () => {
        expect(averageColor(pixels([0, 0, 0], [4, 4, 4]))).toBe('rgb(2 2 2)')
        expect(averageColor(new Uint8ClampedArray())).toBeNull()
    })
})

describe('hasPicture', () => {
    it('needs the data of the current position', () => {
        expect(hasPicture({ readyState: 1 })).toBe(false)
        expect(hasPicture({ readyState: 2 })).toBe(true)
        expect(hasPicture({ readyState: 4 })).toBe(true)
    })
})

class FakeCanvas implements AmbientCanvas {
    width = 300
    height = 150
    readonly draws: [number, number][] = []
    failDraw = false
    tainted = false
    data = pixels([10, 20, 30])

    getContext() {
        return {
            drawImage: (_: CanvasImageSource, _x: number, _y: number, w: number, h: number) => {
                if (this.failDraw) {
                    throw new DOMException('No picture', 'InvalidStateError')
                }
                this.draws.push([w, h])
            },
            getImageData: () => {
                if (this.tainted) {
                    throw new DOMException('Tainted', 'SecurityError')
                }
                return { data: this.data } as ImageData
            },
        } as unknown as CanvasRenderingContext2D
    }
}

class FakeTimers {
    private next = 1
    readonly callbacks = new Map<number, { callback: () => void; delay: number }>()

    readonly environment: AmbientEnvironment

    constructor(readonly sampleCanvas = new FakeCanvas()) {
        this.environment = {
            setInterval: (callback, delay) => {
                const handle = this.next++
                this.callbacks.set(handle, { callback, delay })
                return handle
            },
            clearInterval: (handle) => {
                this.callbacks.delete(handle)
            },
            createCanvas: () => this.sampleCanvas,
        }
    }

    tick(): void {
        for (const { callback } of Array.from(this.callbacks.values())) {
            callback()
        }
    }
}

function setup(video: Partial<AmbientVideo> = {}) {
    const canvases = [new FakeCanvas(), new FakeCanvas()] as const
    const timers = new FakeTimers()
    const frames: number[] = []
    const colors: (string | null)[] = []
    const light = new AmbientLight(
        canvases,
        { videoWidth: 1920, videoHeight: 1080, ...video } as AmbientVideo,
        { frame: (index) => frames.push(index), color: (color) => colors.push(color) },
        timers.environment,
    )
    return { canvases, timers, frames, colors, light }
}

describe('AmbientLight', () => {
    it('draws tiny pictures into the two canvases by turns a few times per second', () => {
        const { canvases, timers, frames, light } = setup()

        expect(light.update(playing)).toBe('running')
        expect([...timers.callbacks.values()].map((entry) => entry.delay)).toEqual([
            ambientInterval,
        ])
        timers.tick()
        timers.tick()

        expect(frames).toEqual([0, 1, 0])
        expect(canvases[0].draws).toEqual([
            [32, 18],
            [32, 18],
        ])
        expect(canvases[1].draws).toEqual([[32, 18]])
        expect(canvases[0]).toMatchObject({ width: 32, height: 18 })
    })

    it('draws portrait videos in their aspect ratio', () => {
        const { canvases, light } = setup({ videoWidth: 1080, videoHeight: 1920 })

        light.update(playing)

        expect(canvases[0]).toMatchObject({ width: 18, height: 32 })
    })

    it('starts only one timer and stops it when the video pauses or is not visible', () => {
        const { timers, frames, light } = setup()
        light.update(playing)
        light.update(playing)
        expect(timers.callbacks.size).toBe(1)

        expect(light.update({ ...playing, playing: false })).toBe('paused')
        expect(timers.callbacks.size).toBe(0)
        // The paused video keeps its last picture; nothing new is drawn.
        expect(frames).toEqual([0])

        light.update(playing)
        expect(light.update({ ...playing, visible: false })).toBe('paused')
        light.update({ ...playing, shown: false })
        light.update({ ...playing, enabled: false })
        expect(timers.callbacks.size).toBe(0)
        timers.tick()
        expect(frames).toEqual([0, 1])
    })

    it('draws the picture of a paused video once when it becomes visible or seeks', () => {
        const { frames, light } = setup()
        const paused = { ...playing, playing: false }

        light.update({ ...paused, visible: false })
        expect(frames).toEqual([])

        light.update(paused)
        light.update(paused)
        expect(frames).toEqual([0])

        light.refresh()
        expect(frames).toEqual([0, 1])
    })

    it('draws again after a new source', () => {
        const { frames, colors, light } = setup()
        const paused = { ...playing, playing: false }
        light.update(paused)

        light.reset()
        expect(colors).toEqual([null])
        light.update({ ...paused, ready: false })
        light.update(paused)

        expect(frames).toEqual([0, 1])
    })

    it('keeps going when the video has no picture for a moment', () => {
        const { canvases, timers, frames, light } = setup()
        canvases[0].failDraw = true

        light.update(playing)
        expect(frames).toEqual([])
        expect(timers.callbacks.size).toBe(1)

        canvases[0].failDraw = false
        timers.tick()
        expect(frames).toEqual([0])
    })

    it('reads one static color for reduced motion and draws nothing', () => {
        const { timers, frames, colors, light } = setup()
        const reduced = { ...playing, reducedMotion: true }

        expect(light.update(reduced)).toBe('static')
        light.update({ ...reduced, playing: false })

        expect(timers.callbacks.size).toBe(0)
        expect(frames).toEqual([])
        expect(colors).toEqual(['rgb(10 20 30)'])
        expect(timers.sampleCanvas.draws).toEqual([[16, 9]])
    })

    it('reads the color of a new video again', () => {
        const { timers, colors, light } = setup()
        const reduced = { ...playing, reducedMotion: true }
        light.update(reduced)

        light.reset()
        timers.sampleCanvas.data = pixels([200, 100, 50])
        light.update({ ...reduced, ready: false })
        light.update(reduced)

        expect(colors).toEqual(['rgb(10 20 30)', null, 'rgb(200 100 50)'])
    })

    it('leaves the color out when the browser does not let it read the picture', () => {
        const { timers, colors, light } = setup()
        timers.sampleCanvas.tainted = true

        expect(light.update({ ...playing, reducedMotion: true })).toBe('static')

        expect(colors).toEqual([null])
    })

    it('stops drawing when disposed', () => {
        const { timers, light } = setup()
        light.update(playing)

        light.dispose()
        light.refresh()

        expect(timers.callbacks.size).toBe(0)
    })

    it('uses the timers of the browser by default', () => {
        const setIntervalSpy = vi.spyOn(window, 'setInterval')
        const canvases = [new FakeCanvas(), new FakeCanvas()] as const
        const light = new AmbientLight(canvases, {
            videoWidth: 640,
            videoHeight: 360,
        } as AmbientVideo)

        light.update(playing)
        light.dispose()

        expect(setIntervalSpy).toHaveBeenCalledWith(expect.any(Function), ambientInterval)
        setIntervalSpy.mockRestore()
    })
})
