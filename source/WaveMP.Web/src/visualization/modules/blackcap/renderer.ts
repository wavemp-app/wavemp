// The WebGL 2 renderer of BlackCap. Each frame it reads the sound of the player (the spectrum of an
// analyser of its own), derives calm levels of bass, mids, highs, beats and the tempo from it and
// a coarse spectrum, and draws:
// - the scene into a texture: a background gradient with a breathing glow and floating lights,
//   then the organic shapes with depth, lit by two lights, mirroring their surroundings and glowing
//   at their edges;
// - the scene onto the canvas with a soft glow of bright parts and a vignette.
// The shapes breathe with the tempo, swell a little on a beat and their surfaces sway with the
// spectrum. A scene change blends colors, forms and motion over several seconds. The quality level
// sets how finely the surfaces are curved, the number of shapes and lights, the resolution of the
// scene and the glow.

import { bandSpectrum, followLevels } from '../../analysis/bands'
import { AudioFeatureTracker } from '../../analysis/features'
import { lookAt, modelMatrix, multiply, perspective } from '../../matrices'
import type { VisualizationRenderer, VisualizationRendererOptions } from '../../module'
import type { QualityLevel } from '../../quality'
import { createProgram, fullscreenVertex, requireResource, type Program } from '../../webgl'
import { Breath, cameraPose, shapePlacements } from './motion'
import { blackcapProfiles, sceneSize, type BlackCapProfile } from './profiles'
import { blackcapScene, blackcapScenes, sceneBlendSeconds, SceneTransition } from './scenes'
import {
    backgroundFragment,
    compositeFragment,
    shapeFragment,
    shapeVertex,
    spectrumBands,
} from './shaders'
import { sphereGrid, spreadLevels } from './surface'

/** Size of the analyser of BlackCap. */
const fftSize = 2048

/** Longest frame the motion accounts for, so a hiccup does not make the scene jump. */
const maxFrameSeconds = 0.1

/** Seconds the spectrum of the surfaces needs to rise and to fall: slow, so the shapes sway. */
const spectrumAttackSeconds = 0.18
const spectrumReleaseSeconds = 0.9

/** Passes that spread the spectrum over neighbouring bands. */
const spreadPasses = 2

/** How far the gradient of the surfaces moves on by a beat (radians), and how fast it gets there. */
const beatShade = 0.35
const shadeFollowSeconds = 0.8

/**
 * The levels of the music follow slowly and a beat fades out over more than half a second: the
 * shapes react calmly instead of twitching.
 */
const calmFeatures = {
    attackSeconds: 0.12,
    releaseSeconds: 0.6,
    beat: { pulseSeconds: 0.6 },
}

interface SceneTarget {
    readonly texture: WebGLTexture
    readonly depth: WebGLRenderbuffer
    readonly framebuffer: WebGLFramebuffer
}

export class BlackCapRenderer implements VisualizationRenderer {
    private readonly gl: WebGL2RenderingContext
    private readonly canvas: HTMLCanvasElement
    private readonly source: AnalyserNode
    private readonly analyser: AnalyserNode
    private readonly spectrum: Uint8Array<ArrayBuffer>
    private readonly rawBands = new Float32Array(spectrumBands)
    private readonly bands = new Float32Array(spectrumBands)
    /** The calm spectrum spread over neighbouring bands, as the surfaces follow it. */
    private readonly spread = new Float32Array(spectrumBands)
    private readonly features = new AudioFeatureTracker(calmFeatures)
    private readonly breath = new Breath()
    private readonly transition: SceneTransition

    private readonly background: Program
    private readonly shapes: Program
    private readonly composite: Program
    private readonly emptyVertices: WebGLVertexArrayObject
    private readonly meshVertices: WebGLVertexArrayObject
    private readonly directionBuffer: WebGLBuffer
    private readonly indexBuffer: WebGLBuffer

    private readonly projection = new Float32Array(16)
    private readonly view = new Float32Array(16)
    private readonly viewProjection = new Float32Array(16)
    private readonly model = new Float32Array(16)

    private profile: BlackCapProfile
    private target: SceneTarget | null = null
    private indexCount = 0
    private width: number
    private height: number
    private sceneWidth = 1
    private sceneHeight = 1
    private flow = 0
    /** Time of the motion of the shapes: a little faster while the music is loud. */
    private motionTime = 0
    private lobePhase = 0
    private shade = 0
    private shadeTarget = 0
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
        this.profile = blackcapProfiles[options.quality]

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
        this.background = createProgram(gl, fullscreenVertex, backgroundFragment, 'BlackCap')
        this.shapes = createProgram(gl, shapeVertex, shapeFragment, 'BlackCap')
        this.composite = createProgram(gl, fullscreenVertex, compositeFragment, 'BlackCap')

        this.emptyVertices = requireResource(gl.createVertexArray())
        this.meshVertices = requireResource(gl.createVertexArray())
        this.directionBuffer = requireResource(gl.createBuffer())
        this.indexBuffer = requireResource(gl.createBuffer())
        gl.bindVertexArray(this.meshVertices)
        gl.bindBuffer(gl.ARRAY_BUFFER, this.directionBuffer)
        gl.enableVertexAttribArray(0)
        gl.vertexAttribPointer(0, 3, gl.FLOAT, false, 0, 0)
        gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, this.indexBuffer)
        gl.bindVertexArray(null)
        this.prepareMesh()
        this.prepareScene()

        this.transition = new SceneTransition(blackcapScenes[0]!)

        // An analyser of its own, so the spectrum is not smoothed twice by the one of the player.
        this.source = options.audio
        this.analyser = options.audio.context.createAnalyser()
        this.analyser.fftSize = fftSize
        this.analyser.smoothingTimeConstant = 0.6
        this.source.connect(this.analyser)
        this.spectrum = new Uint8Array(this.analyser.frequencyBinCount)
    }

    showPreset(id: string, blendSeconds: number): void {
        const scene = blackcapScene(id)
        if (scene) {
            this.transition.show(scene, sceneBlendSeconds(blendSeconds), this.now() / 1000)
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
        const profile = blackcapProfiles[level]
        if (profile === this.profile) {
            return
        }

        const rebuildMesh =
            profile.columns !== this.profile.columns || profile.rows !== this.profile.rows
        const rebuildScene =
            profile.resolution !== this.profile.resolution || profile.bloom !== this.profile.bloom
        this.profile = profile
        if (rebuildMesh) {
            this.prepareMesh()
        }

        if (rebuildScene) {
            this.prepareScene()
        }
    }

    render(): void {
        const target = this.target
        if (this.disposed || !target) {
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
        const sampleRate = this.analyser.context.sampleRate
        const audio = this.features.update(this.spectrum, sampleRate, time)
        bandSpectrum(this.spectrum, sampleRate, this.rawBands)
        followLevels(
            this.bands,
            this.rawBands,
            seconds,
            spectrumAttackSeconds,
            spectrumReleaseSeconds,
        )
        spreadLevels(this.bands, this.spread, spreadPasses)
        const breath = this.breath.update(seconds, audio.bpm)
        if (audio.beat) {
            this.shadeTarget += beatShade
        }

        this.shade +=
            (this.shadeTarget - this.shade) * (1 - Math.exp(-seconds / shadeFollowSeconds))
        const { colors, form, motion } = this.transition.look(time)
        this.flow += seconds * motion.flow * (0.6 + 0.5 * audio.level + 0.4 * audio.mid)
        this.lobePhase += seconds * form.lobeSpin * (0.7 + 0.6 * audio.mid)
        this.motionTime += seconds * (0.8 + 0.4 * audio.level)

        const pose = cameraPose(time, motion.drift)
        perspective(this.projection, pose.fov, this.width / this.height, 0.1, 40)
        lookAt(this.view, pose.eye, pose.target, pose.up)
        multiply(this.viewProjection, this.projection, this.view)

        // The scene: background, then the shapes with depth.
        gl.bindFramebuffer(gl.FRAMEBUFFER, target.framebuffer)
        gl.viewport(0, 0, this.sceneWidth, this.sceneHeight)
        gl.disable(gl.BLEND)
        gl.disable(gl.DEPTH_TEST)
        gl.disable(gl.CULL_FACE)

        const background = this.background
        gl.useProgram(background.program)
        gl.uniform3fv(background.uniform('uTop'), colors.backgroundTop)
        gl.uniform3fv(background.uniform('uBottom'), colors.backgroundBottom)
        gl.uniform3fv(background.uniform('uLight'), colors.light)
        gl.uniform3fv(background.uniform('uGlow'), colors.glow)
        gl.uniform1f(background.uniform('uTime'), time)
        gl.uniform1f(background.uniform('uAspect'), this.sceneWidth / this.sceneHeight)
        gl.uniform1f(background.uniform('uLights'), motion.lights)
        gl.uniform1i(background.uniform('uLightCount'), this.profile.lights)
        gl.uniform1f(background.uniform('uBass'), audio.bass)
        gl.uniform1f(background.uniform('uMid'), audio.mid)
        gl.uniform1f(background.uniform('uPulse'), audio.pulse)
        gl.bindVertexArray(this.emptyVertices)
        gl.drawArrays(gl.TRIANGLES, 0, 3)

        gl.clear(gl.DEPTH_BUFFER_BIT)
        gl.enable(gl.DEPTH_TEST)
        gl.depthFunc(gl.LESS)
        gl.enable(gl.CULL_FACE)
        gl.cullFace(gl.BACK)

        const shapes = this.shapes
        gl.useProgram(shapes.program)
        gl.uniformMatrix4fv(shapes.uniform('uViewProjection'), false, this.viewProjection)
        gl.uniform4fv(shapes.uniform('uLobes'), form.lobes)
        gl.uniform1f(shapes.uniform('uLobePhase'), this.lobePhase)
        gl.uniform1f(shapes.uniform('uWaves'), form.waves)
        gl.uniform1f(shapes.uniform('uWaveScale'), form.waveScale)
        gl.uniform1f(shapes.uniform('uSpectrumAmount'), form.spectrum)
        gl.uniform1fv(shapes.uniform('uSpectrum'), this.spread)
        gl.uniform3fv(shapes.uniform('uStretch'), form.stretch)
        gl.uniform1f(shapes.uniform('uFlow'), this.flow)
        gl.uniform1i(shapes.uniform('uDetail'), this.profile.detail)
        gl.uniform3fv(shapes.uniform('uEye'), pose.eye)
        gl.uniform3fv(shapes.uniform('uTop'), colors.backgroundTop)
        gl.uniform3fv(shapes.uniform('uBottom'), colors.backgroundBottom)
        gl.uniform3fv(shapes.uniform('uSurfaceA'), colors.surfaceA)
        gl.uniform3fv(shapes.uniform('uSurfaceB'), colors.surfaceB)
        gl.uniform3fv(shapes.uniform('uRim'), colors.rim)
        gl.uniform3fv(shapes.uniform('uHighlight'), colors.highlight)
        gl.uniform3fv(shapes.uniform('uGlow'), colors.glow)
        gl.uniform1f(shapes.uniform('uGloss'), Math.max(1, motion.gloss))
        gl.uniform1f(shapes.uniform('uMirror'), motion.mirror)
        gl.uniform1f(shapes.uniform('uRimStrength'), motion.rim)
        gl.uniform1f(shapes.uniform('uShade'), this.shade + time * 0.05)
        gl.uniform1f(shapes.uniform('uTime'), time)
        gl.uniform1f(shapes.uniform('uMid'), audio.mid)
        gl.uniform1f(shapes.uniform('uTreble'), audio.treble)
        gl.uniform1f(shapes.uniform('uPulse'), audio.pulse)
        gl.bindVertexArray(this.meshVertices)
        for (const shape of shapePlacements(
            motion,
            this.profile.shapes,
            this.motionTime,
            breath,
            audio.pulse,
        )) {
            if (shape.scale <= 0) {
                continue
            }

            modelMatrix(this.model, shape.position, shape.yaw, shape.pitch, shape.scale)
            gl.uniformMatrix4fv(shapes.uniform('uModel'), false, this.model)
            gl.uniform1f(shapes.uniform('uSeed'), shape.seed)
            gl.drawElements(gl.TRIANGLES, this.indexCount, gl.UNSIGNED_SHORT, 0)
        }

        gl.disable(gl.DEPTH_TEST)
        gl.disable(gl.CULL_FACE)

        if (this.profile.bloom) {
            gl.activeTexture(gl.TEXTURE0)
            gl.bindTexture(gl.TEXTURE_2D, target.texture)
            gl.generateMipmap(gl.TEXTURE_2D)
        }

        // Onto the canvas.
        const composite = this.composite
        gl.bindFramebuffer(gl.FRAMEBUFFER, null)
        gl.viewport(0, 0, this.width, this.height)
        gl.useProgram(composite.program)
        gl.activeTexture(gl.TEXTURE0)
        gl.bindTexture(gl.TEXTURE_2D, target.texture)
        gl.uniform1i(composite.uniform('uScene'), 0)
        gl.uniform3fv(composite.uniform('uGlow'), colors.glow)
        gl.uniform1f(composite.uniform('uBloom'), this.profile.bloom ? motion.bloom : 0)
        gl.uniform1f(composite.uniform('uAspect'), this.width / this.height)
        gl.uniform1f(composite.uniform('uPulse'), audio.pulse)
        gl.bindVertexArray(this.emptyVertices)
        gl.drawArrays(gl.TRIANGLES, 0, 3)
        gl.bindVertexArray(null)
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
        gl.deleteBuffer(this.directionBuffer)
        gl.deleteBuffer(this.indexBuffer)
        gl.deleteVertexArray(this.meshVertices)
        gl.deleteVertexArray(this.emptyVertices)
        for (const program of [this.background, this.shapes, this.composite]) {
            gl.deleteProgram(program.program)
        }

        // Frees the context at once instead of waiting for the garbage collector; browsers only
        // keep a few contexts.
        gl.getExtension('WEBGL_lose_context')?.loseContext()
    }

    /** The sphere of the shapes for the segments of the current profile. */
    private prepareMesh(): void {
        const gl = this.gl
        const grid = sphereGrid(this.profile.columns, this.profile.rows)
        this.indexCount = grid.indices.length
        gl.bindVertexArray(this.meshVertices)
        gl.bindBuffer(gl.ARRAY_BUFFER, this.directionBuffer)
        gl.bufferData(gl.ARRAY_BUFFER, grid.directions, gl.STATIC_DRAW)
        gl.bufferData(gl.ELEMENT_ARRAY_BUFFER, grid.indices, gl.STATIC_DRAW)
        gl.bindVertexArray(null)
        gl.bindBuffer(gl.ARRAY_BUFFER, null)
    }

    /** The texture and the depth of the scene at the scene size. */
    private prepareScene(): void {
        const gl = this.gl
        this.releaseScene()
        const size = sceneSize(this.width, this.height, this.profile)
        this.sceneWidth = size.width
        this.sceneHeight = size.height

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
        if (this.profile.bloom) {
            gl.generateMipmap(gl.TEXTURE_2D)
        }

        const depth = requireResource(gl.createRenderbuffer())
        gl.bindRenderbuffer(gl.RENDERBUFFER, depth)
        gl.renderbufferStorage(gl.RENDERBUFFER, gl.DEPTH_COMPONENT24, size.width, size.height)
        gl.bindRenderbuffer(gl.RENDERBUFFER, null)

        const framebuffer = requireResource(gl.createFramebuffer())
        gl.bindFramebuffer(gl.FRAMEBUFFER, framebuffer)
        gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, texture, 0)
        gl.framebufferRenderbuffer(gl.FRAMEBUFFER, gl.DEPTH_ATTACHMENT, gl.RENDERBUFFER, depth)
        gl.clearColor(0, 0, 0, 1)
        gl.clearDepth(1)
        gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT)
        gl.bindFramebuffer(gl.FRAMEBUFFER, null)
        this.target = { texture, depth, framebuffer }
    }

    private releaseScene(): void {
        const target = this.target
        if (!target) {
            return
        }

        this.gl.deleteFramebuffer(target.framebuffer)
        this.gl.deleteRenderbuffer(target.depth)
        this.gl.deleteTexture(target.texture)
        this.target = null
    }
}

/** Creates the renderer; the module loads this file only when BlackCap is shown. */
export function createBlackCapRenderer(
    options: VisualizationRendererOptions,
    now: () => number,
): VisualizationRenderer {
    return new BlackCapRenderer(options, now)
}
