// The WebGL 2 renderer of A-Force. Each frame it reads the sound of the player (spectrum and
// waveform of an analyser of its own), derives bass, mids, highs, beats and quiet passages from it,
// turns them into the numbers of the frame for the shown mode (frame.ts) and draws:
// - the scene into a texture: the previous frame as fading, growing and turning trails, then the
//   layers of the mode with additive light (rings of the spectrum, ridges of its recent past,
//   waveforms flowing into the depth, a helix of two waveforms, streaks and particles); a layer the
//   mode does not show is not drawn at all;
// - the scene over a background gradient with a glow that breathes with the bass, a soft bloom of
//   bright parts and a vignette into the input of the flash guard,
// - the guarded frame onto the canvas (the last pass), so no change of mode, scheme or music
//   flashes.
// A change of mode blends layers, geometry and reaction, a change of color scheme every color. The
// quality level sets the number of particles, streaks, waveforms, rings and ridges, the resolution
// of the scene and the bloom.

import { AudioFeatureTracker } from '../../analysis/features'
import { SpectrumLevels } from '../../analysis/spectrum'
import type {
    VisualizationIntensityLevel,
    VisualizationRenderer,
    VisualizationRendererOptions,
} from '../../module'
import type { QualityLevel } from '../../quality'
import { FlashGuard } from '../../safety/flashGuard'
import { createProgram, requireResource, type Program } from '../../webgl'
import { cameraPose, lookAt, multiply, perspective } from './camera'
import { aforceFrameInputs } from './frame'
import { aforceModes, aforceMode, modeLook, ModeTransition, type ModeLayers } from './modes'
import {
    aforceProfiles,
    maxParticles,
    maxTerrainRows,
    maxWaves,
    sceneSize,
    type AForceProfile,
} from './profiles'
import { aforceColorScheme, aforceColorSchemes, ColorTransition } from './schemes'
import {
    compositeFragment,
    fullscreenVertex,
    helixVertex,
    lineFragment,
    particleFragment,
    particleVertex,
    ringFragment,
    streakFragment,
    streakVertex,
    terrainVertex,
    trailFragment,
    waveVertex,
} from './shaders'
import { smoothPoints, waveformPoints } from './waveform'

/** Rows of the waveform texture between two waveforms: farther waveforms show older sound. */
const waveLag = 3

/** Rows of the waveform texture: the history of the sound the farthest waveform reaches. */
const waveRows = maxWaves * waveLag

/** Width of the waveforms in the units of the scene. */
const waveWidth = 9

/** Size of the analyser of A-Force; its waveform has as many samples. */
const fftSize = 2048

/** Samples a waveform shows (about 21 ms at 48 kHz). */
const sampleSpan = 1024

/** Bands of the spectrum the rings and the ridges show. */
export const spectrumBands = 48

/** Points across a ridge. */
const terrainPoints = 96

/** Longest frame the motion accounts for, so a hiccup does not make the scene jump. */
const maxFrameSeconds = 0.1

/** Longest time between two frames the flash guard accounts for. */
const maxGuardSeconds = 1

/** A layer with less weight than this is not drawn. */
export const minLayerWeight = 0.01

/** Half the width of a streak in the units of the picture (a share of its height). */
const streakWidth = 0.0035

interface SceneTarget {
    readonly texture: WebGLTexture
    readonly framebuffer: WebGLFramebuffer
}

/** A small seeded random generator (mulberry32), so the particles look the same every time. */
export function seededRandom(seed: number): () => number {
    let state = seed >>> 0
    return () => {
        state = (state + 0x6d2b79f5) >>> 0
        let t = state
        t = Math.imul(t ^ (t >>> 15), t | 1)
        t ^= t + Math.imul(t ^ (t >>> 7), t | 61)
        return ((t ^ (t >>> 14)) >>> 0) / 4294967296
    }
}

/** Four random values per particle: phase of its flight, angle, radius and size. */
export function particleSeeds(count: number, random: () => number): Float32Array {
    const seeds = new Float32Array(count * 4)
    for (let index = 0; index < seeds.length; index++) {
        seeds[index] = random()
    }

    return seeds
}

/** The vertices of a strip of lines: per point its position (0 to 1) and both edges (-1, 1). */
export function waveStrip(points: number): Float32Array {
    const vertices = new Float32Array(points * 4)
    for (let point = 0; point < points; point++) {
        const u = points > 1 ? point / (points - 1) : 0
        vertices.set([u, -1, u, 1], point * 4)
    }

    return vertices
}

/** The layers of a frame that are drawn: those with a weight of at least minLayerWeight. */
export function drawnLayers(layers: ModeLayers): (keyof ModeLayers)[] {
    return (Object.keys(layers) as (keyof ModeLayers)[]).filter(
        (layer) => layers[layer] >= minLayerWeight,
    )
}

/**
 * Rows of the spectrum history that start in a frame and the phase towards the next row: the
 * clock counts rows (a fraction is the way to the next one); at most three rows start per frame.
 */
export function terrainRows(clock: number): { rows: number; phase: number } {
    const whole = Math.floor(Math.max(0, clock))
    return { rows: Math.min(3, whole), phase: Math.max(0, clock) - whole }
}

export class AForceRenderer implements VisualizationRenderer {
    private readonly gl: WebGL2RenderingContext
    private readonly canvas: HTMLCanvasElement
    private readonly source: AnalyserNode
    private readonly analyser: AnalyserNode
    /** The spectrum in decibels (getFloatFrequencyData): unlike bytes it never saturates. */
    private readonly spectrum: Float32Array<ArrayBuffer>
    private readonly samples: Float32Array<ArrayBuffer>
    private readonly features = new AudioFeatureTracker()
    private readonly spectrumLevels = new SpectrumLevels(spectrumBands)
    private readonly bandLevels = new Float32Array(spectrumBands)
    private readonly modes: ModeTransition
    private readonly colors: ColorTransition
    private readonly guard: FlashGuard

    private readonly trail: Program
    private readonly particles: Program
    private readonly waves: Program
    private readonly rings: Program
    private readonly streaks: Program
    private readonly terrain: Program
    private readonly helix: Program
    private readonly composite: Program
    private readonly emptyVertices: WebGLVertexArrayObject
    private readonly particleVertices: WebGLVertexArrayObject
    private readonly streakVertices: WebGLVertexArrayObject
    private readonly particleBuffer: WebGLBuffer
    private readonly waveVertices: WebGLVertexArrayObject
    private readonly waveBuffer: WebGLBuffer
    private readonly terrainVertices: WebGLVertexArrayObject
    private readonly terrainBuffer: WebGLBuffer
    private readonly waveTexture: WebGLTexture
    private readonly spectrumTexture: WebGLTexture

    private readonly projection = new Float32Array(16)
    private readonly view = new Float32Array(16)
    private readonly viewProjection = new Float32Array(16)

    private profile: AForceProfile
    private intensity: VisualizationIntensityLevel
    private targets: [SceneTarget, SceneTarget] | null = null
    private width: number
    private height: number
    private sceneWidth = 1
    private sceneHeight = 1
    private wavePoints: Float32Array<ArrayBuffer> = new Float32Array(0)
    private waveTarget: Float32Array<ArrayBuffer> = new Float32Array(0)
    private head = 0
    private spectrumHead = 0
    private terrainClock = 0
    private flow = 0
    private spin = 0
    private lastTime: number | null = null
    private disposed = false

    constructor(
        options: VisualizationRendererOptions,
        private readonly now: () => number,
    ) {
        this.canvas = options.canvas
        this.width = Math.max(1, Math.round(options.width))
        this.height = Math.max(1, Math.round(options.height))
        this.canvas.width = this.width
        this.canvas.height = this.height
        this.profile = aforceProfiles[options.quality]
        this.intensity = options.intensity

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
        this.trail = createProgram(gl, fullscreenVertex, trailFragment, 'A-Force')
        this.particles = createProgram(gl, particleVertex, particleFragment, 'A-Force')
        this.waves = createProgram(gl, waveVertex, lineFragment, 'A-Force')
        this.rings = createProgram(gl, fullscreenVertex, ringFragment, 'A-Force')
        this.streaks = createProgram(gl, streakVertex, streakFragment, 'A-Force')
        this.terrain = createProgram(gl, terrainVertex, lineFragment, 'A-Force')
        this.helix = createProgram(gl, helixVertex, lineFragment, 'A-Force')
        this.composite = createProgram(gl, fullscreenVertex, compositeFragment, 'A-Force')

        this.emptyVertices = requireResource(gl.createVertexArray())

        // The seeds of the particles; the streaks use them per instance.
        this.particleBuffer = requireResource(gl.createBuffer())
        gl.bindBuffer(gl.ARRAY_BUFFER, this.particleBuffer)
        gl.bufferData(
            gl.ARRAY_BUFFER,
            particleSeeds(maxParticles, seededRandom(0x5eed)),
            gl.STATIC_DRAW,
        )
        this.particleVertices = requireResource(gl.createVertexArray())
        gl.bindVertexArray(this.particleVertices)
        gl.enableVertexAttribArray(0)
        gl.vertexAttribPointer(0, 4, gl.FLOAT, false, 0, 0)
        this.streakVertices = requireResource(gl.createVertexArray())
        gl.bindVertexArray(this.streakVertices)
        gl.enableVertexAttribArray(0)
        gl.vertexAttribPointer(0, 4, gl.FLOAT, false, 0, 0)
        gl.vertexAttribDivisor(0, 1)

        this.waveVertices = requireResource(gl.createVertexArray())
        this.waveBuffer = requireResource(gl.createBuffer())
        gl.bindVertexArray(this.waveVertices)
        gl.bindBuffer(gl.ARRAY_BUFFER, this.waveBuffer)
        gl.enableVertexAttribArray(0)
        gl.vertexAttribPointer(0, 2, gl.FLOAT, false, 0, 0)

        this.terrainVertices = requireResource(gl.createVertexArray())
        this.terrainBuffer = requireResource(gl.createBuffer())
        gl.bindVertexArray(this.terrainVertices)
        gl.bindBuffer(gl.ARRAY_BUFFER, this.terrainBuffer)
        gl.bufferData(gl.ARRAY_BUFFER, waveStrip(terrainPoints), gl.STATIC_DRAW)
        gl.enableVertexAttribArray(0)
        gl.vertexAttribPointer(0, 2, gl.FLOAT, false, 0, 0)
        gl.bindVertexArray(null)
        gl.bindBuffer(gl.ARRAY_BUFFER, null)

        this.waveTexture = requireResource(gl.createTexture())
        this.spectrumTexture = this.createFloatTexture(spectrumBands, maxTerrainRows)
        this.prepareWaves()
        this.prepareScene()
        this.guard = new FlashGuard(gl, this.width, this.height, options.intensity)

        this.modes = new ModeTransition(modeLook(aforceModes[0]!))
        const scheme =
            (options.colorScheme === null ? undefined : aforceColorScheme(options.colorScheme)) ??
            aforceColorSchemes[0]!
        this.colors = new ColorTransition(scheme.colors)

        // An analyser of its own: less smoothing than the one of the player, so beats stay sharp.
        this.source = options.audio
        this.analyser = options.audio.context.createAnalyser()
        this.analyser.fftSize = fftSize
        this.analyser.smoothingTimeConstant = 0.5
        this.source.connect(this.analyser)
        this.spectrum = new Float32Array(this.analyser.frequencyBinCount)
        this.samples = new Float32Array(this.analyser.fftSize)
    }

    showPreset(id: string, blendSeconds: number): void {
        const mode = aforceMode(id)
        if (mode) {
            this.modes.show(modeLook(mode), blendSeconds, this.now() / 1000)
        }
    }

    showColorScheme(id: string, blendSeconds: number): void {
        const scheme = aforceColorScheme(id)
        if (scheme) {
            this.colors.show(scheme.colors, blendSeconds, this.now() / 1000)
        }
    }

    setIntensity(level: VisualizationIntensityLevel): void {
        this.intensity = level
        this.guard.setIntensity(level)
    }

    resize(width: number, height: number): void {
        this.width = Math.max(1, Math.round(width))
        this.height = Math.max(1, Math.round(height))
        this.canvas.width = this.width
        this.canvas.height = this.height
        this.prepareScene()
        this.guard.resize(this.width, this.height)
    }

    setQuality(level: QualityLevel): void {
        const profile = aforceProfiles[level]
        if (profile === this.profile) {
            return
        }

        const rebuildWaves = profile.wavePoints !== this.profile.wavePoints
        const rebuildScene =
            profile.resolution !== this.profile.resolution || profile.bloom !== this.profile.bloom
        this.profile = profile
        if (rebuildWaves) {
            this.prepareWaves()
        }

        if (rebuildScene) {
            this.prepareScene()
        }
    }

    render(): void {
        const targets = this.targets
        if (this.disposed || !targets) {
            return
        }

        const gl = this.gl
        const time = this.now() / 1000
        const elapsed = this.lastTime === null ? 1 / 60 : Math.max(0, time - this.lastTime)
        const seconds = Math.min(maxFrameSeconds, elapsed)
        this.lastTime = time

        this.analyser.getFloatFrequencyData(this.spectrum)
        this.analyser.getFloatTimeDomainData(this.samples)
        const sampleRate = this.analyser.context.sampleRate
        const audio = this.features.update(this.spectrum, sampleRate, time)
        const colors = this.colors.look(time)
        const look = this.modes.look(time)
        const motion = look.motion
        const frame = aforceFrameInputs(look, audio, this.intensity)
        const layers = frame.layers
        this.flow += seconds * frame.flowSpeed
        this.spin += seconds * frame.spin

        // The newest row of the waveform texture.
        waveformPoints(this.samples, this.waveTarget, sampleSpan)
        smoothPoints(this.wavePoints, this.waveTarget, 1 - Math.exp(-seconds / 0.03))
        this.head = (this.head + 1) % waveRows
        gl.bindTexture(gl.TEXTURE_2D, this.waveTexture)
        gl.texSubImage2D(
            gl.TEXTURE_2D,
            0,
            0,
            this.head,
            this.profile.wavePoints,
            1,
            gl.RED,
            gl.FLOAT,
            this.wavePoints,
        )

        // The spectrum: the newest row follows the music every frame; new rows start at a pace
        // that follows the speed of the scene, so the ridges glide towards the viewer.
        this.spectrumLevels.update(this.spectrum, sampleRate, seconds, this.bandLevels)
        this.terrainClock += seconds * (10 + 12 * Math.min(2, frame.flowSpeed))
        const pace = terrainRows(this.terrainClock)
        this.terrainClock = pace.phase
        gl.bindTexture(gl.TEXTURE_2D, this.spectrumTexture)
        for (let row = 0; row < Math.max(1, pace.rows); row++) {
            if (row < pace.rows) {
                this.spectrumHead = (this.spectrumHead + 1) % maxTerrainRows
            }

            gl.texSubImage2D(
                gl.TEXTURE_2D,
                0,
                0,
                this.spectrumHead,
                spectrumBands,
                1,
                gl.RED,
                gl.FLOAT,
                this.bandLevels,
            )
        }

        const pose = cameraPose(
            time,
            {
                orbit: frame.cameraOrbit,
                sway: frame.cameraSway,
                punch: frame.punch,
                pitch: frame.cameraPitch,
            },
            { mid: frame.cameraMid, pulse: 1 },
        )
        perspective(this.projection, pose.fov, this.width / this.height, 0.1, 60)
        lookAt(this.view, pose.eye, pose.target, pose.up)
        multiply(this.viewProjection, this.projection, this.view)

        const [previous, next] = targets
        const frames = seconds * 60
        const aspect = this.sceneWidth / this.sceneHeight

        // The scene: trails of the previous frame, then the layers as light.
        gl.bindFramebuffer(gl.FRAMEBUFFER, next.framebuffer)
        gl.viewport(0, 0, this.sceneWidth, this.sceneHeight)
        gl.disable(gl.BLEND)
        gl.useProgram(this.trail.program)
        gl.activeTexture(gl.TEXTURE0)
        gl.bindTexture(gl.TEXTURE_2D, previous.texture)
        gl.uniform1i(this.trail.uniform('uPrevious'), 0)
        gl.uniform1f(this.trail.uniform('uKeep'), Math.pow(frame.trail, frames))
        gl.uniform1f(this.trail.uniform('uZoom'), 1 + frame.trailZoom * frames)
        gl.uniform1f(this.trail.uniform('uTurn'), frame.trailTurn * frames)
        gl.uniform1f(this.trail.uniform('uAspect'), aspect)
        gl.bindVertexArray(this.emptyVertices)
        gl.drawArrays(gl.TRIANGLES, 0, 3)

        gl.enable(gl.BLEND)
        gl.blendFunc(gl.ONE, gl.ONE)
        gl.activeTexture(gl.TEXTURE1)
        gl.bindTexture(gl.TEXTURE_2D, this.waveTexture)
        gl.activeTexture(gl.TEXTURE2)
        gl.bindTexture(gl.TEXTURE_2D, this.spectrumTexture)
        const drawn = drawnLayers(layers)
        const shows = (layer: keyof ModeLayers) => drawn.includes(layer)

        if (shows('rings')) {
            const rings = this.use(this.rings, time, layers.rings, frame.brightness)
            gl.uniform1i(rings.uniform('uSpectrum'), 2)
            gl.uniform1i(rings.uniform('uBands'), spectrumBands)
            gl.uniform1i(rings.uniform('uHead'), this.spectrumHead)
            gl.uniform1f(rings.uniform('uCount'), this.profile.rings)
            gl.uniform1f(rings.uniform('uRadius'), motion.ringRadius)
            gl.uniform1f(rings.uniform('uAspect'), aspect)
            gl.uniform1f(rings.uniform('uSpin'), this.spin)
            gl.uniform1f(rings.uniform('uSwell'), frame.ringSwell)
            gl.uniform1f(rings.uniform('uSparkle'), frame.sparkle)
            gl.uniform1f(rings.uniform('uBeat'), frame.beat)
            gl.uniform3fv(rings.uniform('uInner'), colors.waveStart)
            gl.uniform3fv(rings.uniform('uOuter'), colors.waveFar)
            gl.uniform3fv(rings.uniform('uSpark'), colors.spark)
            gl.bindVertexArray(this.emptyVertices)
            gl.drawArrays(gl.TRIANGLES, 0, 3)
        }

        if (shows('terrain')) {
            const terrain = this.use(this.terrain, time, layers.terrain, frame.brightness)
            gl.uniform1i(terrain.uniform('uSpectrum'), 2)
            gl.uniform1i(terrain.uniform('uBands'), spectrumBands)
            gl.uniform1i(terrain.uniform('uRows'), maxTerrainRows)
            gl.uniform1i(terrain.uniform('uHead'), this.spectrumHead)
            gl.uniform1i(terrain.uniform('uCount'), this.profile.terrainRows)
            gl.uniformMatrix4fv(terrain.uniform('uViewProjection'), false, this.viewProjection)
            gl.uniform1f(terrain.uniform('uPhase'), pace.phase)
            gl.uniform1f(terrain.uniform('uLift'), frame.terrainLift)
            gl.uniform1f(terrain.uniform('uFront'), frame.terrainFront)
            gl.uniform1f(terrain.uniform('uSparkle'), frame.sparkle)
            gl.uniform1f(terrain.uniform('uSway'), frame.sway)
            gl.uniform1f(terrain.uniform('uThickness'), 0.035 / Math.sqrt(this.profile.resolution))
            gl.uniform3fv(terrain.uniform('uNear'), colors.waveStart)
            gl.uniform3fv(terrain.uniform('uFar'), colors.waveFar)
            gl.uniform3fv(terrain.uniform('uSpark'), colors.spark)
            gl.bindVertexArray(this.terrainVertices)
            gl.drawArraysInstanced(
                gl.TRIANGLE_STRIP,
                0,
                terrainPoints * 2,
                this.profile.terrainRows,
            )
        }

        if (shows('waves')) {
            const waves = this.use(this.waves, time, layers.waves, frame.brightness)
            this.bindWaves(waves)
            gl.uniform1i(waves.uniform('uCount'), this.profile.waves)
            gl.uniformMatrix4fv(waves.uniform('uViewProjection'), false, this.viewProjection)
            gl.uniform1f(waves.uniform('uFlow'), this.flow)
            gl.uniform1f(waves.uniform('uAmplitude'), frame.waveAmplitude)
            gl.uniform1f(waves.uniform('uSwing'), frame.waveSwing)
            gl.uniform1f(waves.uniform('uSpread'), motion.waveSpread)
            // A scene of lower resolution draws the waveforms a little thicker, so thin lines do
            // not break up.
            gl.uniform1f(
                waves.uniform('uThickness'),
                frame.waveThickness / Math.sqrt(this.profile.resolution),
            )
            gl.uniform1f(waves.uniform('uWaveFlow'), motion.waveFlow)
            gl.uniform1f(waves.uniform('uWidth'), waveWidth)
            gl.uniform3fv(waves.uniform('uColorStart'), colors.waveStart)
            gl.uniform3fv(waves.uniform('uColorEnd'), colors.waveEnd)
            gl.uniform3fv(waves.uniform('uColorFar'), colors.waveFar)
            gl.bindVertexArray(this.waveVertices)
            gl.drawArraysInstanced(
                gl.TRIANGLE_STRIP,
                0,
                this.profile.wavePoints * 2,
                this.profile.waves,
            )
        }

        if (shows('helix')) {
            const helix = this.use(this.helix, time, layers.helix, frame.brightness)
            this.bindWaves(helix)
            gl.uniformMatrix4fv(helix.uniform('uViewProjection'), false, this.viewProjection)
            gl.uniform1f(helix.uniform('uSpin'), this.spin)
            gl.uniform1f(helix.uniform('uTurns'), frame.helixTurns)
            gl.uniform1f(helix.uniform('uRadius'), motion.helixRadius * frame.helixScale)
            gl.uniform1f(helix.uniform('uAmplitude'), frame.waveAmplitude)
            gl.uniform1f(
                helix.uniform('uThickness'),
                (frame.waveThickness * 1.2) / Math.sqrt(this.profile.resolution),
            )
            gl.uniform1f(helix.uniform('uSparkle'), frame.sparkle)
            gl.uniform1f(helix.uniform('uFlow'), this.flow)
            gl.uniform3fv(helix.uniform('uColorStart'), colors.waveStart)
            gl.uniform3fv(helix.uniform('uColorEnd'), colors.waveEnd)
            gl.uniform3fv(helix.uniform('uSpark'), colors.spark)
            gl.bindVertexArray(this.waveVertices)
            gl.drawArraysInstanced(gl.TRIANGLE_STRIP, 0, this.profile.wavePoints * 2, 2)
        }

        if (shows('streaks')) {
            const streaks = this.use(this.streaks, time, layers.streaks, frame.brightness)
            gl.uniformMatrix4fv(streaks.uniform('uViewProjection'), false, this.viewProjection)
            gl.uniform1f(streaks.uniform('uFlow'), this.flow)
            gl.uniform1f(streaks.uniform('uLength'), frame.streakLength)
            gl.uniform1f(streaks.uniform('uSpread'), motion.streakSpread * frame.particleScale)
            gl.uniform1f(streaks.uniform('uSparkle'), frame.sparkle)
            gl.uniform1f(streaks.uniform('uBeat'), frame.beat)
            gl.uniform1f(streaks.uniform('uPixelWidth'), streakWidth)
            gl.uniform1f(streaks.uniform('uAspect'), aspect)
            gl.uniform3fv(streaks.uniform('uColorA'), colors.particleA)
            gl.uniform3fv(streaks.uniform('uColorB'), colors.particleB)
            gl.uniform3fv(streaks.uniform('uSpark'), colors.spark)
            gl.bindVertexArray(this.streakVertices)
            gl.drawArraysInstanced(gl.TRIANGLE_STRIP, 0, 4, this.profile.streaks)
        }

        if (shows('particles')) {
            const particles = this.use(this.particles, time, layers.particles, frame.brightness)
            gl.uniformMatrix4fv(particles.uniform('uViewProjection'), false, this.viewProjection)
            gl.uniform1f(particles.uniform('uFlow'), this.flow)
            gl.uniform1f(particles.uniform('uScale'), frame.particleScale)
            gl.uniform1f(particles.uniform('uSway'), frame.sway)
            gl.uniform1f(particles.uniform('uSparkle'), frame.sparkle)
            gl.uniform1f(particles.uniform('uBeat'), frame.beat)
            gl.uniform1f(particles.uniform('uSpread'), motion.particleSpread)
            gl.uniform1f(particles.uniform('uSwirl'), motion.particleSwirl)
            gl.uniform1f(particles.uniform('uSize'), motion.particleSize)
            gl.uniform1f(particles.uniform('uPixelScale'), this.sceneHeight / 60)
            gl.uniform3fv(particles.uniform('uColorA'), colors.particleA)
            gl.uniform3fv(particles.uniform('uColorB'), colors.particleB)
            gl.uniform3fv(particles.uniform('uSpark'), colors.spark)
            gl.bindVertexArray(this.particleVertices)
            gl.drawArrays(gl.POINTS, 0, this.profile.particles)
        }

        gl.disable(gl.BLEND)

        if (this.profile.bloom) {
            gl.activeTexture(gl.TEXTURE0)
            gl.bindTexture(gl.TEXTURE_2D, next.texture)
            gl.generateMipmap(gl.TEXTURE_2D)
        }

        // The finished frame into the guard.
        const composite = this.composite
        this.guard.bindInput()
        gl.useProgram(composite.program)
        gl.activeTexture(gl.TEXTURE0)
        gl.bindTexture(gl.TEXTURE_2D, next.texture)
        gl.uniform1i(composite.uniform('uScene'), 0)
        gl.uniform3fv(composite.uniform('uTop'), colors.backgroundTop)
        gl.uniform3fv(composite.uniform('uBottom'), colors.backgroundBottom)
        gl.uniform3fv(composite.uniform('uGlow'), colors.glow)
        gl.uniform1f(composite.uniform('uTime'), time)
        gl.uniform1f(composite.uniform('uMass'), frame.mass)
        gl.uniform1f(composite.uniform('uBeat'), frame.beat)
        gl.uniform1f(composite.uniform('uGlowStrength'), frame.glow)
        gl.uniform1f(composite.uniform('uBrightness'), frame.brightness)
        gl.uniform1f(composite.uniform('uBloom'), this.profile.bloom ? frame.bloom : 0)
        gl.uniform1f(composite.uniform('uAspect'), this.width / this.height)
        gl.bindVertexArray(this.emptyVertices)
        gl.drawArrays(gl.TRIANGLES, 0, 3)
        gl.bindVertexArray(null)

        // The last pass: the guarded frame onto the canvas.
        this.guard.present(Math.min(maxGuardSeconds, elapsed))

        this.targets = [next, previous]
    }

    dispose(): void {
        if (this.disposed) {
            return
        }

        this.disposed = true
        try {
            this.source.disconnect(this.analyser)
        } catch {
            // Already disconnected.
        }

        const gl = this.gl
        try {
            this.releaseScene()
            this.guard.dispose()
            gl.deleteTexture(this.waveTexture)
            gl.deleteTexture(this.spectrumTexture)
            for (const buffer of [this.particleBuffer, this.waveBuffer, this.terrainBuffer]) {
                gl.deleteBuffer(buffer)
            }

            for (const vertices of [
                this.particleVertices,
                this.streakVertices,
                this.waveVertices,
                this.terrainVertices,
                this.emptyVertices,
            ]) {
                gl.deleteVertexArray(vertices)
            }

            for (const program of [
                this.trail,
                this.particles,
                this.waves,
                this.rings,
                this.streaks,
                this.terrain,
                this.helix,
                this.composite,
            ]) {
                gl.deleteProgram(program.program)
            }
        } catch {
            // The context is lost anyway.
        }

        // Frees the context at once instead of waiting for the garbage collector; browsers only
        // keep a few contexts.
        gl.getExtension('WEBGL_lose_context')?.loseContext()
    }

    /** Uses a program of a layer and sets what all layers share: time, weight and brightness. */
    private use(program: Program, time: number, weight: number, brightness: number): Program {
        const gl = this.gl
        gl.useProgram(program.program)
        gl.uniform1f(program.uniform('uTime'), time)
        gl.uniform1f(program.uniform('uWeight'), Math.min(1, weight))
        gl.uniform1f(program.uniform('uBrightness'), brightness)
        return program
    }

    /** The waveform texture for a layer that reads it (waveforms, helix). */
    private bindWaves(program: Program): void {
        const gl = this.gl
        gl.uniform1i(program.uniform('uWaves'), 1)
        gl.uniform1i(program.uniform('uPoints'), this.profile.wavePoints)
        gl.uniform1i(program.uniform('uRows'), waveRows)
        gl.uniform1i(program.uniform('uHead'), this.head)
        gl.uniform1i(program.uniform('uLag'), waveLag)
    }

    /** A texture of one float per texel, read with texelFetch. */
    private createFloatTexture(width: number, height: number): WebGLTexture {
        const gl = this.gl
        const texture = requireResource(gl.createTexture())
        gl.bindTexture(gl.TEXTURE_2D, texture)
        gl.pixelStorei(gl.UNPACK_ALIGNMENT, 1)
        gl.texImage2D(
            gl.TEXTURE_2D,
            0,
            gl.R32F,
            width,
            height,
            0,
            gl.RED,
            gl.FLOAT,
            new Float32Array(width * height),
        )
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST)
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST)
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE)
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE)
        return texture
    }

    /** The strip and the texture of the waveforms for the points of the current profile. */
    private prepareWaves(): void {
        const gl = this.gl
        const points = this.profile.wavePoints
        this.wavePoints = new Float32Array(points)
        this.waveTarget = new Float32Array(points)
        this.head = 0

        gl.bindBuffer(gl.ARRAY_BUFFER, this.waveBuffer)
        gl.bufferData(gl.ARRAY_BUFFER, waveStrip(points), gl.STATIC_DRAW)
        gl.bindBuffer(gl.ARRAY_BUFFER, null)

        gl.bindTexture(gl.TEXTURE_2D, this.waveTexture)
        gl.pixelStorei(gl.UNPACK_ALIGNMENT, 1)
        gl.texImage2D(
            gl.TEXTURE_2D,
            0,
            gl.R32F,
            points,
            waveRows,
            0,
            gl.RED,
            gl.FLOAT,
            new Float32Array(points * waveRows),
        )
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST)
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST)
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE)
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE)
    }

    /** Two textures for the scene (this frame and the previous one) at the scene size. */
    private prepareScene(): void {
        const gl = this.gl
        this.releaseScene()
        const size = sceneSize(this.width, this.height, this.profile)
        this.sceneWidth = size.width
        this.sceneHeight = size.height
        const create = (): SceneTarget => {
            const texture = requireResource(gl.createTexture())
            gl.bindTexture(gl.TEXTURE_2D, texture)
            gl.texImage2D(
                gl.TEXTURE_2D,
                0,
                gl.RGBA8,
                size.width,
                size.height,
                0,
                gl.RGBA,
                gl.UNSIGNED_BYTE,
                null,
            )
            gl.texParameteri(
                gl.TEXTURE_2D,
                gl.TEXTURE_MIN_FILTER,
                this.profile.bloom ? gl.LINEAR_MIPMAP_LINEAR : gl.LINEAR,
            )
            gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR)
            gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE)
            gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE)
            const framebuffer = requireResource(gl.createFramebuffer())
            gl.bindFramebuffer(gl.FRAMEBUFFER, framebuffer)
            gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, texture, 0)
            gl.clearColor(0, 0, 0, 1)
            gl.clear(gl.COLOR_BUFFER_BIT)
            if (this.profile.bloom) {
                gl.generateMipmap(gl.TEXTURE_2D)
            }

            return { texture, framebuffer }
        }

        this.targets = [create(), create()]
        gl.bindFramebuffer(gl.FRAMEBUFFER, null)
    }

    private releaseScene(): void {
        if (!this.targets) {
            return
        }

        for (const target of this.targets) {
            this.gl.deleteFramebuffer(target.framebuffer)
            this.gl.deleteTexture(target.texture)
        }

        this.targets = null
    }
}

/** Creates the renderer; the module loads this file only when A-Force is shown. */
export function createAForceRenderer(
    options: VisualizationRendererOptions,
    now: () => number,
): VisualizationRenderer {
    return new AForceRenderer(options, now)
}
