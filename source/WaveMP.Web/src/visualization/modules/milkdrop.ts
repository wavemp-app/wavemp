// The module Milkdrop: Milkdrop presets drawn with WebGL 2 by butterchurn (MIT), with the presets of
// the package butterchurn-presets (MIT). Both are loaded on demand in a chunk of their own, so the
// app only loads them when the visualization is shown.
//
// Butterchurn advances its presets by one step per call of render, so on its own the presets run
// faster on a display with more frames per second. The renderer therefore runs a fixed number of
// steps per second (60 at the speed of 100 %), scaled by the speed of the profile, whatever the
// display does. Butterchurn draws into a canvas outside the page; the canvas of the stage has a
// WebGL context of its own that takes over every step as a texture, cross-fades the last two steps
// by the time between them (smooth at low speeds) and shows the result through the flash guard.
// Two contexts keep butterchurn, which changes the state of its context freely, apart from the
// guard.
//
// The presets react to the music on their own, but butterchurn measures it against its own recent
// past, so a quiet passage moves as much as a loud one. The renderer therefore also listens to the
// music (the shared analysis of the own visualizations) and lets the presets run slower in quiet
// passages and push ahead a little on a beat (milkdropMusicPace), and dims quiet passages
// (milkdropMusicBrightness).

import type { Butterchurn, ButterchurnRendererOptions } from 'butterchurn'
import type { ButterchurnPresetPack } from 'butterchurn-presets'
import { VisualizationModuleObject } from '@/api/generated/models'
import { AudioFeatureTracker, type AudioFeatures } from '../analysis/features'
import type {
    LoadedVisualizationModule,
    PresetBlendSeconds,
    VisualizationIntensityLevel,
    VisualizationModule,
    VisualizationPreset,
    VisualizationRenderer,
    VisualizationRendererOptions,
} from '../module'
import { sortPresets } from '../presets'
import type { QualityLevel } from '../quality'
import { FlashGuard } from '../safety/flashGuard'
import { maxMilkdropSpeed, maxPresetNameLength, minMilkdropSpeed } from '../settings'
import { supportsWebGl2Visualization } from '../support'
import { requireResource } from '../webgl'

/** Steps of the presets per second at the speed of 100 %: their original speed at 60 Hz. */
export const milkdropStepsPerSecond = 60

/** Most steps per frame of the display; after a hiccup the rest is dropped instead of caught up. */
export const maxMilkdropStepsPerFrame = 2

/** Highest speed of the presets in percent at the reduced intensity. */
export const reducedMilkdropSpeed = 35

/**
 * Seconds the Milkdrop presets blend into each other at the normal intensity: longer than the
 * default of the controller, because a sudden change of a preset is a flash of its own.
 */
export const milkdropBlendSeconds: PresetBlendSeconds = { manual: 2, automatic: 5 }

/** Share of their speed the presets lose in a quiet passage. */
export const milkdropQuietSlowdown = 0.6

/** Share of their speed a beat adds for a moment (fading with the pulse of the beat). */
export const milkdropBeatPush = 0.4

/** Share of their brightness the presets lose in a quiet passage. */
export const milkdropQuietDimming = 0.45

/** Size of the analyser the renderer listens to the music with. */
const fftSize = 2048

/**
 * How fast the presets run for the music at the moment, as a factor of their speed: 1 in loud
 * passages, down to 1 - milkdropQuietSlowdown in quiet ones, a little faster for a moment after a
 * beat; at the reduced intensity beats push nothing.
 */
export function milkdropMusicPace(
    features: Pick<AudioFeatures, 'quiet' | 'pulse'>,
    intensity: VisualizationIntensityLevel,
): number {
    const clamp = (value: number) => Math.min(1, Math.max(0, Number.isFinite(value) ? value : 0))
    const push = intensity === 'reduced' ? 0 : milkdropBeatPush * clamp(features.pulse)
    return (1 - milkdropQuietSlowdown * clamp(features.quiet)) * (1 + push)
}

/**
 * How bright the presets are shown for the music at the moment: 1 in loud passages, down to
 * 1 - milkdropQuietDimming in quiet ones. The quiet value of the analysis changes slowly, so this
 * never flashes.
 */
export function milkdropMusicBrightness(features: Pick<AudioFeatures, 'quiet'>): number {
    const quiet = Number.isFinite(features.quiet) ? Math.min(1, Math.max(0, features.quiet)) : 0
    return 1 - milkdropQuietDimming * quiet
}

/** Part of a step the rounding of the times of the frames may leave missing. */
const stepTolerance = 1e-6

/** Longest time between two frames the flash guard accounts for. */
const maxGuardSeconds = 1

/** What the module needs from the browser and the packages; tests replace it. */
export interface MilkdropEnvironment {
    /** Whether WebGL 2 and Web Audio exist. */
    supported(): boolean
    loadButterchurn(): Promise<Butterchurn>
    loadPresets(): Promise<Record<string, object>>
    /** A canvas outside the page for butterchurn to draw into. */
    createCanvas(): HTMLCanvasElement
    /** Milliseconds of a steady clock. */
    now(): number
}

/**
 * The export of a CommonJS package as an ES module: depending on the bundler the object itself or
 * its default, possibly twice wrapped.
 */
function exported<T>(module: unknown, has: (value: Record<string, unknown>) => boolean): T {
    let value = module as Record<string, unknown> | undefined
    for (let depth = 0; depth < 3 && value; depth++) {
        if (has(value)) {
            return value as T
        }

        value = value.default as Record<string, unknown> | undefined
    }

    throw new Error('The package has an unexpected export.')
}

export const browserMilkdropEnvironment: MilkdropEnvironment = {
    supported: supportsWebGl2Visualization,
    loadButterchurn: async () =>
        exported<Butterchurn>(
            await import('butterchurn'),
            (value) => typeof value.createVisualizer === 'function',
        ),
    loadPresets: async () =>
        exported<ButterchurnPresetPack>(
            await import('butterchurn-presets'),
            (value) => typeof value.getPresets === 'function',
        ).getPresets(),
    createCanvas: () => document.createElement('canvas'),
    now: () => performance.now(),
}

/** The presets of a pack, sorted by name; names the API cannot keep are left out. */
export function milkdropPresets(pack: Record<string, object>): VisualizationPreset[] {
    return sortPresets(
        Object.keys(pack)
            .filter((name) => name.trim() !== '' && name.length <= maxPresetNameLength)
            .map((name) => ({ id: name, name })),
    )
}

/**
 * What the quality levels change: the resolution of the textures of butterchurn (the drawing
 * buffer keeps its size) and the mesh its per-vertex equations run on. High is the default of
 * butterchurn.
 */
export const milkdropQuality: Readonly<
    Record<
        QualityLevel,
        Required<Pick<ButterchurnRendererOptions, 'textureRatio' | 'meshWidth' | 'meshHeight'>>
    >
> = {
    low: { textureRatio: 0.5, meshWidth: 32, meshHeight: 24 },
    medium: { textureRatio: 0.75, meshWidth: 48, meshHeight: 36 },
    high: { textureRatio: 1, meshWidth: 48, meshHeight: 36 },
}

/**
 * The speed the presets run at in percent: the speed of the profile within the range of the API,
 * at most reducedMilkdropSpeed at the reduced intensity.
 */
export function effectiveMilkdropSpeed(
    percent: number,
    intensity: VisualizationIntensityLevel,
): number {
    const speed = Math.max(minMilkdropSpeed, Math.min(maxMilkdropSpeed, percent || 0))
    return intensity === 'reduced' ? Math.min(speed, reducedMilkdropSpeed) : speed
}

/**
 * Counts the steps of the presets: per frame of the display as many as the time and the speed
 * give, independent of the frame rate. What is left of a step is the phase between the last step
 * and the next one.
 */
export class MilkdropStepper {
    private pending = 0

    /** The steps to run for a frame shown seconds after the previous one, at percent speed. */
    advance(seconds: number, percent: number): number {
        this.pending += (Math.max(0, seconds) * milkdropStepsPerSecond * percent) / 100
        // A step that is due up to the rounding of the times of the frames counts.
        const steps = Math.floor(this.pending + stepTolerance)
        this.pending = Math.max(0, this.pending - steps)
        return Math.min(steps, maxMilkdropStepsPerFrame)
    }

    /** How far the time has come from the last step towards the next one (0 to 1). */
    get phase(): number {
        return Math.min(1, this.pending)
    }

    reset(): void {
        this.pending = 0
    }
}

class MilkdropRenderer implements VisualizationRenderer {
    private readonly visualizer
    private readonly audio: AnalyserNode
    /** An analyser of its own and the features of the music it hears. */
    private readonly analyser: AnalyserNode
    private readonly spectrum: Float32Array<ArrayBuffer>
    private readonly features = new AudioFeatureTracker()
    private readonly canvas: HTMLCanvasElement
    /** The canvas butterchurn draws into; outside the page. */
    private readonly source: HTMLCanvasElement
    private readonly gl: WebGL2RenderingContext
    private readonly guard: FlashGuard
    /** The two newest steps of butterchurn: the earlier one, then the latest one. */
    private steps: [WebGLTexture, WebGLTexture]
    /** Steps taken over since the start or the last change of the size (at most 2 matter). */
    private stepCount = 0
    private readonly stepper = new MilkdropStepper()
    private quality: QualityLevel
    private intensity: VisualizationIntensityLevel
    private speed: number
    private lastTime: number | null = null
    private disposed = false

    constructor(
        butterchurn: Butterchurn,
        private readonly pack: Record<string, object>,
        options: VisualizationRendererOptions,
        environment: MilkdropEnvironment,
        private readonly now: () => number,
    ) {
        this.canvas = options.canvas
        this.audio = options.audio
        this.quality = options.quality
        this.intensity = options.intensity
        this.speed = options.speed
        this.canvas.width = options.width
        this.canvas.height = options.height
        this.source = environment.createCanvas()
        this.source.width = options.width
        this.source.height = options.height

        const gl = this.canvas.getContext('webgl2', {
            alpha: false,
            antialias: false,
            depth: false,
            stencil: false,
            premultipliedAlpha: false,
            preserveDrawingBuffer: false,
        })
        if (!gl) {
            throw new Error('The browser gives no WebGL 2 context.')
        }

        this.gl = gl
        this.guard = new FlashGuard(gl, options.width, options.height, options.intensity)
        this.steps = [this.createStepTexture(), this.createStepTexture()]

        // The size is given in device pixels; butterchurn should not multiply it once more.
        this.visualizer = butterchurn.createVisualizer(options.audio.context, this.source, {
            width: options.width,
            height: options.height,
            pixelRatio: 1,
            ...milkdropQuality[options.quality],
        })
        this.visualizer.connectAudio(options.audio)

        this.analyser = options.audio.context.createAnalyser()
        this.analyser.fftSize = fftSize
        this.analyser.smoothingTimeConstant = 0.5
        options.audio.connect(this.analyser)
        this.spectrum = new Float32Array(this.analyser.frequencyBinCount)
    }

    /** The speed the presets run at, as a fraction of their original speed. */
    private get rate(): number {
        return effectiveMilkdropSpeed(this.speed, this.intensity) / 100
    }

    showPreset(id: string, blendSeconds: number): void {
        const preset = this.pack[id]
        if (preset) {
            // Butterchurn blends in the time of its presets, which runs at the speed.
            this.visualizer.loadPreset(preset, blendSeconds * this.rate)
        }
    }

    resize(width: number, height: number): void {
        this.canvas.width = width
        this.canvas.height = height
        this.source.width = width
        this.source.height = height
        this.visualizer.setRendererSize(width, height, {
            pixelRatio: 1,
            ...milkdropQuality[this.quality],
        })
        this.guard.resize(width, height)
        this.stepCount = 0
    }

    setQuality(level: QualityLevel): void {
        if (level !== this.quality) {
            this.quality = level
            this.resize(this.canvas.width, this.canvas.height)
        }
    }

    setIntensity(level: VisualizationIntensityLevel): void {
        this.intensity = level
        this.guard.setIntensity(level)
    }

    setSpeed(percent: number): void {
        this.speed = percent
    }

    render(): void {
        if (this.disposed) {
            return
        }

        const time = this.now() / 1000
        const seconds = this.lastTime === null ? 0 : Math.max(0, time - this.lastTime)
        this.lastTime = time

        this.analyser.getFloatFrequencyData(this.spectrum)
        const music = this.features.update(this.spectrum, this.analyser.context.sampleRate, time)
        const pace = milkdropMusicPace(music, this.intensity)

        // At least one step before the first frame, so there is a picture to show.
        const steps = Math.max(
            this.stepCount === 0 ? 1 : 0,
            this.stepper.advance(seconds, this.rate * 100 * pace),
        )
        for (let step = 0; step < steps; step++) {
            this.visualizer.render({ elapsedTime: 1 / milkdropStepsPerSecond })
            this.takeStep()
        }

        const [earlier, latest] = this.steps
        this.guard.present(Math.min(maxGuardSeconds, seconds), {
            texture: latest,
            earlier,
            // From the earlier step towards the latest one as the time moves on to the next step.
            earlierWeight: this.stepCount > 1 ? 1 - this.stepper.phase : 0,
            brightness: milkdropMusicBrightness(music),
        })
    }

    dispose(): void {
        if (this.disposed) {
            return
        }

        this.disposed = true
        try {
            this.visualizer.disconnectAudio(this.audio)
        } catch {
            // Already disconnected.
        }

        try {
            this.audio.disconnect(this.analyser)
        } catch {
            // Already disconnected.
        }

        try {
            this.guard.dispose()
            this.steps.forEach((texture) => this.gl.deleteTexture(texture))
        } catch {
            // The context is lost anyway.
        }

        // Frees both WebGL contexts at once instead of waiting for the garbage collector; browsers
        // only keep a few contexts.
        this.gl.getExtension('WEBGL_lose_context')?.loseContext()
        this.source.getContext('webgl2')?.getExtension('WEBGL_lose_context')?.loseContext()
    }

    /** Takes over the picture butterchurn just drew as the latest step. */
    private takeStep(): void {
        const gl = this.gl
        this.steps.reverse()
        gl.bindTexture(gl.TEXTURE_2D, this.steps[1])
        // The rows of a canvas start at its top, those of a texture at its bottom.
        gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, true)
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA8, gl.RGBA, gl.UNSIGNED_BYTE, this.source)
        gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, false)
        this.stepCount = Math.min(this.stepCount + 1, 2)
    }

    private createStepTexture(): WebGLTexture {
        const gl = this.gl
        const texture = requireResource(gl.createTexture())
        gl.bindTexture(gl.TEXTURE_2D, texture)
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA8, 1, 1, 0, gl.RGBA, gl.UNSIGNED_BYTE, null)
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST)
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST)
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE)
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE)
        return texture
    }
}

/** Creates the module Milkdrop; the app uses milkdropModule, tests pass their own environment. */
export function createMilkdropModule(
    environment: MilkdropEnvironment = browserMilkdropEnvironment,
): VisualizationModule {
    let supported: boolean | undefined
    let loading: Promise<LoadedVisualizationModule> | undefined

    async function load(): Promise<LoadedVisualizationModule> {
        const [butterchurn, pack] = await Promise.all([
            environment.loadButterchurn(),
            environment.loadPresets(),
        ])
        const presets = milkdropPresets(pack)
        return {
            presets,
            blendSeconds: milkdropBlendSeconds,
            createRenderer: (options) =>
                new MilkdropRenderer(butterchurn, pack, options, environment, () =>
                    environment.now(),
                ),
        }
    }

    return {
        id: VisualizationModuleObject.Milkdrop,
        isSupported: () => (supported ??= environment.supported()),
        load: () => {
            loading ??= load().catch((error: unknown) => {
                // A later attempt (e.g. after a network error) loads again.
                loading = undefined
                throw error
            })
            return loading
        },
    }
}

export const milkdropModule = createMilkdropModule()
