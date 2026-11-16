// The WebGL 2 renderer of A-Force. Each frame it reads the sound of the player (spectrum and
// waveform of an analyser of its own), derives bass, mids, highs and beats from it and draws:
// - the scene into a texture: the previous frame as fading, slowly growing and turning trails, the
//   waveforms of the last moments flowing into the depth and particles flying through a swirling
//   tunnel, all with additive light;
// - the scene onto the canvas over a background gradient with a glow that breathes with the bass,
//   a soft bloom of bright parts and a vignette.
// The camera orbits slowly, sways with the mids and jumps towards the scene on a beat. A style
// change blends all colors and motions over the blend seconds. The quality level sets the number
// of particles and waveforms, the resolution of the scene and the bloom.

import { AudioFeatureTracker } from '../../analysis/features'
import type { VisualizationRenderer, VisualizationRendererOptions } from '../../module'
import type { QualityLevel } from '../../quality'
import { createProgram, requireResource, type Program } from '../../webgl'
import { cameraPose, lookAt, multiply, perspective } from './camera'
import { aforceProfiles, maxParticles, maxWaves, sceneSize, type AForceProfile } from './profiles'
import {
    compositeFragment,
    fullscreenVertex,
    particleFragment,
    particleVertex,
    trailFragment,
    waveFragment,
    waveVertex,
} from './shaders'
import { aforceStyle, aforceStyles, StyleTransition } from './styles'
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

/** Longest frame the motion accounts for, so a hiccup does not make the scene jump. */
const maxFrameSeconds = 0.1

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

/** The vertices of a waveform strip: per point its position (0 to 1) and both edges (-1, 1). */
export function waveStrip(points: number): Float32Array {
    const vertices = new Float32Array(points * 4)
    for (let point = 0; point < points; point++) {
        const u = points > 1 ? point / (points - 1) : 0
        vertices.set([u, -1, u, 1], point * 4)
    }

    return vertices
}

export class AForceRenderer implements VisualizationRenderer {
    private readonly gl: WebGL2RenderingContext
    private readonly canvas: HTMLCanvasElement
    private readonly source: AnalyserNode
    private readonly analyser: AnalyserNode
    private readonly spectrum: Uint8Array<ArrayBuffer>
    private readonly samples: Float32Array<ArrayBuffer>
    private readonly features = new AudioFeatureTracker()
    private readonly transition: StyleTransition

    private readonly trail: Program
    private readonly particles: Program
    private readonly waves: Program
    private readonly composite: Program
    private readonly emptyVertices: WebGLVertexArrayObject
    private readonly particleVertices: WebGLVertexArrayObject
    private readonly particleBuffer: WebGLBuffer
    private readonly waveVertices: WebGLVertexArrayObject
    private readonly waveBuffer: WebGLBuffer
    private readonly waveTexture: WebGLTexture

    private readonly projection = new Float32Array(16)
    private readonly view = new Float32Array(16)
    private readonly viewProjection = new Float32Array(16)

    private profile: AForceProfile
    private targets: [SceneTarget, SceneTarget] | null = null
    private width: number
    private height: number
    private sceneWidth = 1
    private sceneHeight = 1
    private wavePoints: Float32Array<ArrayBuffer> = new Float32Array(0)
    private waveTarget: Float32Array<ArrayBuffer> = new Float32Array(0)
    private head = 0
    private flow = 0
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
        this.waves = createProgram(gl, waveVertex, waveFragment, 'A-Force')
        this.composite = createProgram(gl, fullscreenVertex, compositeFragment, 'A-Force')

        this.emptyVertices = requireResource(gl.createVertexArray())

        this.particleVertices = requireResource(gl.createVertexArray())
        this.particleBuffer = requireResource(gl.createBuffer())
        gl.bindVertexArray(this.particleVertices)
        gl.bindBuffer(gl.ARRAY_BUFFER, this.particleBuffer)
        gl.bufferData(
            gl.ARRAY_BUFFER,
            particleSeeds(maxParticles, seededRandom(0x5eed)),
            gl.STATIC_DRAW,
        )
        gl.enableVertexAttribArray(0)
        gl.vertexAttribPointer(0, 4, gl.FLOAT, false, 0, 0)

        this.waveVertices = requireResource(gl.createVertexArray())
        this.waveBuffer = requireResource(gl.createBuffer())
        gl.bindVertexArray(this.waveVertices)
        gl.bindBuffer(gl.ARRAY_BUFFER, this.waveBuffer)
        gl.enableVertexAttribArray(0)
        gl.vertexAttribPointer(0, 2, gl.FLOAT, false, 0, 0)
        gl.bindVertexArray(null)

        this.waveTexture = requireResource(gl.createTexture())
        this.prepareWaves()
        this.prepareScene()

        const first = aforceStyles[0]!
        this.transition = new StyleTransition(first)

        // An analyser of its own: less smoothing than the one of the player, so beats stay sharp.
        this.source = options.audio
        this.analyser = options.audio.context.createAnalyser()
        this.analyser.fftSize = fftSize
        this.analyser.smoothingTimeConstant = 0.5
        this.source.connect(this.analyser)
        this.spectrum = new Uint8Array(this.analyser.frequencyBinCount)
        this.samples = new Float32Array(this.analyser.fftSize)
    }

    showPreset(id: string, blendSeconds: number): void {
        const style = aforceStyle(id)
        if (style) {
            this.transition.show(style, blendSeconds, this.now() / 1000)
        }
    }

    resize(width: number, height: number): void {
        this.width = Math.max(1, Math.round(width))
        this.height = Math.max(1, Math.round(height))
        this.canvas.width = this.width
        this.canvas.height = this.height
        this.prepareScene()
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
        const seconds =
            this.lastTime === null
                ? 1 / 60
                : Math.min(maxFrameSeconds, Math.max(0, time - this.lastTime))
        this.lastTime = time

        this.analyser.getByteFrequencyData(this.spectrum)
        this.analyser.getFloatTimeDomainData(this.samples)
        const audio = this.features.update(this.spectrum, this.analyser.context.sampleRate, time)
        const { colors, motion } = this.transition.look(time)
        this.flow += seconds * motion.speed * (0.6 + audio.bass * 1.2 + audio.pulse * 0.8)

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

        const pose = cameraPose(time, motion, audio)
        perspective(this.projection, pose.fov, this.width / this.height, 0.1, 60)
        lookAt(this.view, pose.eye, pose.target, pose.up)
        multiply(this.viewProjection, this.projection, this.view)

        const [previous, next] = targets
        const frames = seconds * 60
        const aspect = this.sceneWidth / this.sceneHeight

        // The scene: trails of the previous frame, then waveforms and particles as light.
        gl.bindFramebuffer(gl.FRAMEBUFFER, next.framebuffer)
        gl.viewport(0, 0, this.sceneWidth, this.sceneHeight)
        gl.disable(gl.BLEND)
        gl.useProgram(this.trail.program)
        gl.activeTexture(gl.TEXTURE0)
        gl.bindTexture(gl.TEXTURE_2D, previous.texture)
        gl.uniform1i(this.trail.uniform('uPrevious'), 0)
        gl.uniform1f(this.trail.uniform('uKeep'), Math.pow(motion.trail, frames))
        gl.uniform1f(
            this.trail.uniform('uZoom'),
            1 + (motion.trailZoom + audio.bass * 0.004 + audio.pulse * 0.006) * frames,
        )
        gl.uniform1f(this.trail.uniform('uTurn'), motion.trailTurn * frames)
        gl.uniform1f(this.trail.uniform('uAspect'), aspect)
        gl.bindVertexArray(this.emptyVertices)
        gl.drawArrays(gl.TRIANGLES, 0, 3)

        gl.enable(gl.BLEND)
        gl.blendFunc(gl.ONE, gl.ONE)

        const waves = this.waves
        gl.useProgram(waves.program)
        gl.activeTexture(gl.TEXTURE1)
        gl.bindTexture(gl.TEXTURE_2D, this.waveTexture)
        gl.uniform1i(waves.uniform('uWaves'), 1)
        gl.uniform1i(waves.uniform('uPoints'), this.profile.wavePoints)
        gl.uniform1i(waves.uniform('uRows'), waveRows)
        gl.uniform1i(waves.uniform('uHead'), this.head)
        gl.uniform1i(waves.uniform('uLag'), waveLag)
        gl.uniform1i(waves.uniform('uCount'), this.profile.waves)
        gl.uniformMatrix4fv(waves.uniform('uViewProjection'), false, this.viewProjection)
        gl.uniform1f(waves.uniform('uTime'), time)
        gl.uniform1f(waves.uniform('uFlow'), this.flow)
        gl.uniform1f(waves.uniform('uBass'), audio.bass)
        gl.uniform1f(waves.uniform('uMid'), audio.mid)
        gl.uniform1f(waves.uniform('uTreble'), audio.treble)
        gl.uniform1f(waves.uniform('uPulse'), audio.pulse)
        gl.uniform1f(waves.uniform('uAmplitude'), motion.waveAmplitude)
        gl.uniform1f(waves.uniform('uSpread'), motion.waveSpread)
        // A scene of lower resolution draws the waveforms a little thicker, so thin lines do not break up.
        gl.uniform1f(
            waves.uniform('uThickness'),
            motion.waveThickness / Math.sqrt(this.profile.resolution),
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

        const particles = this.particles
        gl.useProgram(particles.program)
        gl.uniformMatrix4fv(particles.uniform('uViewProjection'), false, this.viewProjection)
        gl.uniform1f(particles.uniform('uTime'), time)
        gl.uniform1f(particles.uniform('uFlow'), this.flow)
        gl.uniform1f(particles.uniform('uBass'), audio.bass)
        gl.uniform1f(particles.uniform('uMid'), audio.mid)
        gl.uniform1f(particles.uniform('uTreble'), audio.treble)
        gl.uniform1f(particles.uniform('uPulse'), audio.pulse)
        gl.uniform1f(particles.uniform('uSpread'), motion.particleSpread)
        gl.uniform1f(particles.uniform('uSwirl'), motion.particleSwirl)
        gl.uniform1f(particles.uniform('uSize'), motion.particleSize)
        gl.uniform1f(particles.uniform('uPixelScale'), this.sceneHeight / 60)
        gl.uniform3fv(particles.uniform('uColorA'), colors.particleA)
        gl.uniform3fv(particles.uniform('uColorB'), colors.particleB)
        gl.uniform3fv(particles.uniform('uSpark'), colors.spark)
        gl.bindVertexArray(this.particleVertices)
        gl.drawArrays(gl.POINTS, 0, this.profile.particles)
        gl.disable(gl.BLEND)

        if (this.profile.bloom) {
            gl.activeTexture(gl.TEXTURE0)
            gl.bindTexture(gl.TEXTURE_2D, next.texture)
            gl.generateMipmap(gl.TEXTURE_2D)
        }

        // Onto the canvas.
        const composite = this.composite
        gl.bindFramebuffer(gl.FRAMEBUFFER, null)
        gl.viewport(0, 0, this.width, this.height)
        gl.useProgram(composite.program)
        gl.activeTexture(gl.TEXTURE0)
        gl.bindTexture(gl.TEXTURE_2D, next.texture)
        gl.uniform1i(composite.uniform('uScene'), 0)
        gl.uniform3fv(composite.uniform('uTop'), colors.backgroundTop)
        gl.uniform3fv(composite.uniform('uBottom'), colors.backgroundBottom)
        gl.uniform3fv(composite.uniform('uGlow'), colors.glow)
        gl.uniform1f(composite.uniform('uTime'), time)
        gl.uniform1f(composite.uniform('uBass'), audio.bass)
        gl.uniform1f(composite.uniform('uPulse'), audio.pulse)
        gl.uniform1f(composite.uniform('uBloom'), this.profile.bloom ? motion.bloom : 0)
        gl.uniform1f(composite.uniform('uAspect'), this.width / this.height)
        gl.bindVertexArray(this.emptyVertices)
        gl.drawArrays(gl.TRIANGLES, 0, 3)
        gl.bindVertexArray(null)

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
        this.releaseScene()
        gl.deleteTexture(this.waveTexture)
        gl.deleteBuffer(this.particleBuffer)
        gl.deleteBuffer(this.waveBuffer)
        gl.deleteVertexArray(this.particleVertices)
        gl.deleteVertexArray(this.waveVertices)
        gl.deleteVertexArray(this.emptyVertices)
        for (const program of [this.trail, this.particles, this.waves, this.composite]) {
            gl.deleteProgram(program.program)
        }

        // Frees the context at once instead of waiting for the garbage collector; browsers only
        // keep a few contexts.
        gl.getExtension('WEBGL_lose_context')?.loseContext()
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
