// Fakes of a visualization module and of the clock of the controller for the tests.

import { vi } from 'vitest'
import type { VisualizationClock } from '../controller'
import type {
    LoadedVisualizationModule,
    PresetBlendSeconds,
    VisualizationModule,
    VisualizationPreset,
    VisualizationRenderer,
    VisualizationRendererOptions,
} from '../module'

export class FakeRenderer implements VisualizationRenderer {
    readonly showPreset = vi.fn()
    readonly resize = vi.fn()
    readonly setQuality = vi.fn()
    readonly showColorScheme = vi.fn()
    readonly setIntensity = vi.fn()
    readonly setSpeed = vi.fn()
    readonly render = vi.fn()
    readonly dispose = vi.fn()

    constructor(readonly options: VisualizationRendererOptions) {}
}

export class FakeModule implements VisualizationModule {
    readonly id = 'Milkdrop' as const
    supported = true
    presets: VisualizationPreset[] = ['A', 'B', 'C'].map((name) => ({ id: name, name }))
    /** The color schemes of the module; none unless a test gives some. */
    colorSchemes: VisualizationPreset[] | undefined = undefined
    /** The blend seconds of the module; the default of the controller unless a test gives some. */
    blendSeconds: PresetBlendSeconds | undefined = undefined
    readonly renderers: FakeRenderer[] = []
    failCreate = false
    private pending: { resolve: () => void; reject: (error: Error) => void } | null = null
    loads = 0

    isSupported(): boolean {
        return this.supported
    }

    load(): Promise<LoadedVisualizationModule> {
        this.loads++
        return new Promise((resolve, reject) => {
            this.pending = {
                resolve: () =>
                    resolve({
                        presets: this.presets,
                        colorSchemes: this.colorSchemes,
                        blendSeconds: this.blendSeconds,
                        createRenderer: (options) => {
                            if (this.failCreate) {
                                throw new Error('No WebGL context.')
                            }

                            const renderer = new FakeRenderer(options)
                            this.renderers.push(renderer)
                            return renderer
                        },
                    }),
                reject,
            }
        })
    }

    /** Finishes the pending load and lets its promise callbacks run. */
    async finishLoad(): Promise<void> {
        this.pending?.resolve()
        this.pending = null
        await flush()
    }

    async failLoad(): Promise<void> {
        this.pending?.reject(new Error('Network error.'))
        this.pending = null
        await flush()
    }

    get renderer(): FakeRenderer | undefined {
        return this.renderers.at(-1)
    }
}

/** A clock whose frames and timers run only when the test says so. */
export class FakeClock implements VisualizationClock {
    private next = 1
    readonly frames = new Map<number, () => void>()
    readonly timers = new Map<number, { callback: () => void; delay: number }>()
    randomValue = 0
    /** Milliseconds of the clock; frame() advances it by frameMs. */
    time = 0
    frameMs = 1000 / 60

    requestFrame(callback: () => void): number {
        const handle = this.next++
        this.frames.set(handle, callback)
        return handle
    }

    cancelFrame(handle: number): void {
        this.frames.delete(handle)
    }

    setTimeout(callback: () => void, delay: number): number {
        const handle = this.next++
        this.timers.set(handle, { callback, delay })
        return handle
    }

    clearTimeout(handle: number): void {
        this.timers.delete(handle)
    }

    random(): number {
        return this.randomValue
    }

    now(): number {
        return this.time
    }

    /** Advances the time by frameMs and runs the requested animation frames once. */
    frame(): void {
        this.time += this.frameMs
        const callbacks = [...this.frames.values()]
        this.frames.clear()
        callbacks.forEach((callback) => callback())
    }

    /** Runs the pending timers once; returns their delays. */
    elapse(): number[] {
        const timers = [...this.timers.values()]
        this.timers.clear()
        timers.forEach((timer) => timer.callback())
        return timers.map((timer) => timer.delay)
    }
}

export async function flush(): Promise<void> {
    for (let index = 0; index < 5; index++) {
        await Promise.resolve()
    }
}

/** An analyser stand-in; the fake module never touches it. */
export function fakeAnalyser(): AnalyserNode {
    return { context: {} } as unknown as AnalyserNode
}
