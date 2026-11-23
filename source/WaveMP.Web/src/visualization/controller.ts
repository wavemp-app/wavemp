// The controller of the visualization on the Now Playing stage. It loads a module when the
// visualization is shown the first time, creates its renderer once the player has an output (the
// analyser of its first playback) and a canvas, draws one frame per animation frame while the
// visualization is visible and changes the preset automatically. While the visualization is hidden
// (another view of the stage, another page, a hidden browser tab or out of the viewport), nothing
// is drawn and the automatic change waits. It hands the quality level to the renderer: the one of
// the profile, or with Auto the one the measured frame rate allows; likewise the color scheme
// (chosen apart from the presets and never changed automatically), the intensity and the speed of
// the presets. The browser (animation frames, timers, time, randomness) comes in through a clock,
// so tests drive it by hand.

import type {
    LoadedVisualizationModule,
    PresetBlendSeconds,
    VisualizationIntensityLevel,
    VisualizationModule,
    VisualizationPreset,
    VisualizationRenderer,
} from './module'
import type { VisualizationQuality } from '@/api/generated/models'
import { adjacentPreset, randomPreset, startPreset } from './presets'
import { AdaptiveQuality, fixedQualityLevel, type QualityLevel } from './quality'
import { defaultMilkdropSpeed } from './settings'

/**
 * State of the visualization (data-state of the visualization part):
 * - idle: not shown yet, nothing loaded;
 * - loading: the module loads;
 * - waiting: loaded, waits for the music (the player has no output before its first playback);
 * - running: draws;
 * - paused: drawing stops because the visualization is hidden;
 * - unsupported: the browser cannot show the module;
 * - failed: the module or its renderer failed.
 */
export type VisualizationState =
    'idle' | 'loading' | 'waiting' | 'running' | 'paused' | 'unsupported' | 'failed'

/** Seconds a preset chosen by the user blends into the previous one, unless the module says. */
export const manualBlendSeconds = 1

/** Seconds a preset of the automatic change blends into the previous one, unless the module says. */
export const autoBlendSeconds = 2.7

/** Seconds a color scheme chosen by the user blends into the previous one. */
export const colorSchemeBlendSeconds = 1.5

/** How much longer every blend takes at the reduced intensity. */
export const reducedBlendFactor = 2

/**
 * Seconds a change blends over: the seconds of the normal intensity, longer at the reduced one, so
 * nothing changes abruptly.
 */
export function blendSecondsFor(seconds: number, intensity: VisualizationIntensityLevel): number {
    return intensity === 'reduced' ? seconds * reducedBlendFactor : seconds
}

/** The color scheme to start with: the stored one if the module has it, otherwise its first one. */
export function startColorScheme(
    colorSchemes: readonly VisualizationPreset[],
    stored: string | null,
): string | null {
    return (colorSchemes.find((scheme) => scheme.id === stored) ?? colorSchemes[0])?.id ?? null
}

/** Highest ratio of device pixels to CSS pixels the visualization draws with. */
export const maxPixelRatio = 2

/** What the controller needs from the browser; tests replace it. */
export interface VisualizationClock {
    requestFrame(callback: () => void): number
    cancelFrame(handle: number): void
    setTimeout(callback: () => void, delay: number): number
    clearTimeout(handle: number): void
    /** Milliseconds of a steady clock (performance.now). */
    now(): number
    random(): number
}

export const browserVisualizationClock: VisualizationClock = {
    requestFrame: (callback) => window.requestAnimationFrame(() => callback()),
    cancelFrame: (handle) => window.cancelAnimationFrame(handle),
    setTimeout: (callback, delay) => window.setTimeout(callback, delay),
    clearTimeout: (handle) => window.clearTimeout(handle),
    now: () => performance.now(),
    random: () => Math.random(),
}

export interface VisualizationControllerEvents {
    state?(state: VisualizationState): void
    /** The presets of the module, once it is loaded. */
    presets?(presets: readonly VisualizationPreset[]): void
    /** The preset shown, or null before the module is loaded. */
    preset?(id: string | null): void
    /** The color schemes of the module once it is loaded; empty for modules without. */
    colorSchemes?(colorSchemes: readonly VisualizationPreset[]): void
    /** The color scheme shown, or null for modules without color schemes. */
    colorScheme?(id: string | null): void
    /**
     * The renderer released its canvas (its drawing context is gone); the next renderer needs a new
     * canvas element.
     */
    canvasReleased?(): void
    /** The quality level the visualization draws with. */
    quality?(level: QualityLevel): void
}

export interface AutoChange {
    readonly enabled: boolean
    readonly seconds: number
}

/** Size of the drawing buffer in device pixels for an element of the given CSS size. */
export function drawingSize(
    width: number,
    height: number,
    devicePixelRatio: number,
): { width: number; height: number } {
    const ratio = Math.min(maxPixelRatio, Math.max(1, devicePixelRatio || 1))
    return {
        width: Math.max(1, Math.round(width * ratio)),
        height: Math.max(1, Math.round(height * ratio)),
    }
}

export class VisualizationController {
    private state: VisualizationState = 'idle'
    private loaded: LoadedVisualizationModule | null = null
    private loading = false
    private failed = false
    private disposed = false
    private renderer: VisualizationRenderer | null = null
    /** The analyser the renderer listens to. */
    private rendererAudio: AnalyserNode | null = null
    private canvas: HTMLCanvasElement | null = null
    /** Canvases whose drawing context a renderer released; they are never used again. */
    private readonly released = new WeakSet<HTMLCanvasElement>()
    private audio: AnalyserNode | null = null
    private visible = false
    private width = 0
    private height = 0
    private current: string | null = null
    /** The color scheme of the profile; it applies when the module loads. */
    private storedColorScheme: string | null = null
    private currentColorScheme: string | null = null
    private intensity: VisualizationIntensityLevel = 'normal'
    /** Speed of the presets in percent. */
    private speed = defaultMilkdropSpeed
    private frame: number | null = null
    private timer: number | null = null
    /** The quality of the profile; null for Auto. */
    private fixedQuality: QualityLevel | null = null
    private readonly adaptive = new AdaptiveQuality()
    private qualityLevel: QualityLevel = this.adaptive.level
    /** Time of the previous drawn frame, or null when the drawing starts (again). */
    private lastFrame: number | null = null

    constructor(
        private readonly module: VisualizationModule,
        /** The preset to start with (the choice of the profile), or null for a random one. */
        private storedPreset: string | null,
        private autoChange: AutoChange,
        private readonly events: VisualizationControllerEvents = {},
        private readonly clock: VisualizationClock = browserVisualizationClock,
    ) {}

    get currentState(): VisualizationState {
        return this.state
    }

    get presets(): readonly VisualizationPreset[] {
        return this.loaded?.presets ?? []
    }

    get preset(): string | null {
        return this.current
    }

    get colorSchemes(): readonly VisualizationPreset[] {
        return this.loaded?.colorSchemes ?? []
    }

    get colorScheme(): string | null {
        return this.currentColorScheme
    }

    /** How strongly the visualization moves and reacts. */
    get currentIntensity(): VisualizationIntensityLevel {
        return this.intensity
    }

    /** Speed of the presets in percent. */
    get currentSpeed(): number {
        return this.speed
    }

    /** The quality level the visualization draws with. */
    get quality(): QualityLevel {
        return this.qualityLevel
    }

    /** Whether the visualization is shown; the first time it loads the module. */
    setVisible(visible: boolean): void {
        this.visible = visible
        this.update()
    }

    /** The output of the player, or null while it has none. */
    setAudio(audio: AnalyserNode | null): void {
        this.audio = audio
        this.update()
    }

    /** The canvas to draw into, or null while there is none. */
    setCanvas(canvas: HTMLCanvasElement | null): void {
        this.canvas = canvas
        this.update()
    }

    /** Size of the drawing buffer in device pixels. */
    resize(width: number, height: number): void {
        this.width = width
        this.height = height
        if (this.renderer && width > 0 && height > 0) {
            this.guard(() => this.renderer?.resize(width, height))
        }
    }

    /** Changes the automatic change; a running interval starts again. */
    setAutoChange(autoChange: AutoChange): void {
        if (
            autoChange.enabled === this.autoChange.enabled &&
            autoChange.seconds === this.autoChange.seconds
        ) {
            return
        }

        this.autoChange = autoChange
        this.restartTimer()
    }

    /** The quality of the profile: a fixed level, or Auto to follow the frame rate. */
    setQuality(quality: VisualizationQuality): void {
        this.fixedQuality = fixedQualityLevel(quality)
        this.applyQuality(this.fixedQuality ?? this.adaptive.level)
    }

    /**
     * The choice of the profile changed (e.g. on another device). It applies only while no preset
     * is shown yet; a running visualization keeps its preset.
     */
    setStoredPreset(id: string | null): void {
        this.storedPreset = id
    }

    /**
     * The color scheme of the profile changed. Like the stored preset it applies only while the
     * module is not loaded yet; a running visualization keeps its color scheme.
     */
    setStoredColorScheme(id: string | null): void {
        this.storedColorScheme = id
    }

    /** Shows a preset chosen by the user; the automatic change starts its interval again. */
    selectPreset(id: string): void {
        if (!this.presets.some((preset) => preset.id === id)) {
            return
        }

        this.show(id, this.blendSeconds('manual'))
        this.restartTimer()
    }

    /**
     * Shows a color scheme chosen by the user. The preset and the automatic change stay as they
     * are: the automatic change only changes presets.
     */
    selectColorScheme(id: string): void {
        if (
            id === this.currentColorScheme ||
            !this.colorSchemes.some((scheme) => scheme.id === id)
        ) {
            return
        }

        this.currentColorScheme = id
        this.events.colorScheme?.(id)
        if (this.renderer) {
            const seconds = blendSecondsFor(colorSchemeBlendSeconds, this.intensity)
            this.guard(() => this.renderer?.showColorScheme?.(id, seconds))
        }
    }

    /**
     * How strongly the visualization moves and reacts: the setting of the profile, or reduced
     * while the device asks for reduced motion. It also makes every blend longer.
     */
    setIntensity(level: VisualizationIntensityLevel): void {
        if (level === this.intensity) {
            return
        }

        this.intensity = level
        if (this.renderer) {
            this.guard(() => this.renderer?.setIntensity?.(level))
        }
    }

    /** Speed of the presets in percent (Milkdrop). */
    setSpeed(percent: number): void {
        if (percent === this.speed) {
            return
        }

        this.speed = percent
        if (this.renderer) {
            this.guard(() => this.renderer?.setSpeed?.(percent))
        }
    }

    /** Shows the preset before (-1) or after (1) the current one; returns its id. */
    step(offset: -1 | 1): string | null {
        const preset = adjacentPreset(this.presets, this.current, offset)
        if (preset) {
            this.selectPreset(preset.id)
        }

        return preset?.id ?? null
    }

    /** Stops drawing and releases the renderer; the controller is not used again. */
    dispose(): void {
        this.disposed = true
        this.stopLoop()
        this.stopTimer()
        this.releaseRenderer(false)
    }

    private setState(state: VisualizationState): void {
        if (this.state !== state) {
            this.state = state
            this.events.state?.(state)
        }
    }

    /** Brings renderer, drawing and automatic change in line with what is known. */
    private update(): void {
        if (this.disposed) {
            return
        }

        if (!this.module.isSupported()) {
            this.setState('unsupported')
            return
        }

        if (this.failed) {
            this.setState('failed')
            return
        }

        if (!this.loaded) {
            if (this.visible || this.loading) {
                this.load()
            }

            this.setState(this.loading ? 'loading' : 'idle')
            return
        }

        if (this.renderer && this.rendererAudio !== this.audio) {
            // Another output (or none): the renderer belongs to the audio context of the old one.
            this.releaseRenderer(true)
        }

        if (!this.audio) {
            this.stopLoop()
            this.stopTimer()
            this.setState('waiting')
            return
        }

        if (!this.renderer && this.visible) {
            this.createRenderer()
            if (this.failed) {
                this.setState('failed')
                return
            }
        }

        if (this.renderer && this.visible) {
            this.setState('running')
            this.startLoop()
            this.startTimer()
        } else {
            this.stopLoop()
            this.stopTimer()
            this.setState('paused')
        }
    }

    private load(): void {
        if (this.loading) {
            return
        }

        this.loading = true
        this.module
            .load()
            .then((loaded) => {
                this.loading = false
                if (this.disposed) {
                    return
                }

                if (loaded.presets.length === 0) {
                    this.failed = true
                } else {
                    this.loaded = loaded
                    this.events.presets?.(loaded.presets)
                    this.current =
                        startPreset(loaded.presets, this.storedPreset, () => this.clock.random())
                            ?.id ?? null
                    this.events.preset?.(this.current)
                    const colorSchemes = loaded.colorSchemes ?? []
                    this.currentColorScheme = startColorScheme(colorSchemes, this.storedColorScheme)
                    this.events.colorSchemes?.(colorSchemes)
                    this.events.colorScheme?.(this.currentColorScheme)
                }

                this.update()
            })
            .catch(() => {
                this.loading = false
                this.failed = true
                this.update()
            })
    }

    private createRenderer(): void {
        const canvas = this.canvas
        const audio = this.audio
        if (!this.loaded || !canvas || !audio || this.released.has(canvas)) {
            return
        }

        try {
            const renderer = this.loaded.createRenderer({
                canvas,
                audio,
                width: Math.max(1, this.width || canvas.width),
                height: Math.max(1, this.height || canvas.height),
                quality: this.qualityLevel,
                colorScheme: this.currentColorScheme,
                intensity: this.intensity,
                speed: this.speed,
            })
            this.renderer = renderer
            this.rendererAudio = audio
            if (this.current) {
                renderer.showPreset(this.current, 0)
            }
        } catch {
            this.failed = true
            this.renderer = null
            this.rendererAudio = null
        }
    }

    private releaseRenderer(notify: boolean): void {
        const renderer = this.renderer
        if (!renderer) {
            return
        }

        this.stopLoop()
        this.stopTimer()
        this.renderer = null
        this.rendererAudio = null
        if (this.canvas) {
            this.released.add(this.canvas)
        }

        try {
            renderer.dispose()
        } catch {
            // The canvas is not used again anyway.
        }

        if (notify) {
            this.events.canvasReleased?.()
        }
    }

    private applyQuality(level: QualityLevel): void {
        if (level === this.qualityLevel) {
            return
        }

        this.qualityLevel = level
        this.events.quality?.(level)
        if (this.renderer) {
            this.guard(() => this.renderer?.setQuality(level))
        }
    }

    /** Measures the frame rate for Auto. */
    private measureFrame(): void {
        const now = this.clock.now()
        const last = this.lastFrame
        this.lastFrame = now
        if (last !== null && this.fixedQuality === null) {
            this.applyQuality(this.adaptive.frame((now - last) / 1000))
        }
    }

    /** Seconds a new preset blends over: as the module wants it, longer at the reduced intensity. */
    private blendSeconds(kind: keyof PresetBlendSeconds): number {
        const seconds = this.loaded?.blendSeconds ?? {
            manual: manualBlendSeconds,
            automatic: autoBlendSeconds,
        }
        return blendSecondsFor(seconds[kind], this.intensity)
    }

    private show(id: string, blendSeconds: number): void {
        this.current = id
        this.events.preset?.(id)
        if (this.renderer) {
            this.guard(() => this.renderer?.showPreset(id, blendSeconds))
        }
    }

    /** Runs a call of the renderer; an error ends the visualization as failed. */
    private guard(call: () => void): void {
        try {
            call()
        } catch {
            this.failed = true
            this.releaseRenderer(false)
            this.update()
        }
    }

    private startLoop(): void {
        if (this.frame !== null) {
            return
        }

        const tick = (): void => {
            this.frame = null
            if (this.state !== 'running' || !this.renderer) {
                return
            }

            this.measureFrame()
            this.guard(() => this.renderer?.render())
            if (this.state === 'running' && this.frame === null) {
                this.frame = this.clock.requestFrame(tick)
            }
        }

        this.lastFrame = null
        this.adaptive.restart()
        this.frame = this.clock.requestFrame(tick)
    }

    private stopLoop(): void {
        if (this.frame !== null) {
            this.clock.cancelFrame(this.frame)
            this.frame = null
        }
    }

    private startTimer(): void {
        if (
            this.timer !== null ||
            this.state !== 'running' ||
            !this.autoChange.enabled ||
            this.presets.length < 2
        ) {
            return
        }

        this.timer = this.clock.setTimeout(() => {
            this.timer = null
            const next = randomPreset(this.presets, this.current, () => this.clock.random())
            if (next && this.state === 'running') {
                this.show(next.id, this.blendSeconds('automatic'))
            }

            this.startTimer()
        }, this.autoChange.seconds * 1000)
    }

    private stopTimer(): void {
        if (this.timer !== null) {
            this.clock.clearTimeout(this.timer)
            this.timer = null
        }
    }

    private restartTimer(): void {
        this.stopTimer()
        this.startTimer()
    }
}
