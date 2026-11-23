// The WebGL 2 renderer of BlackCap. Each frame it reads the sound of the player (the spectrum of an
// analyser of its own), derives the levels of bass, mids, highs, the loudness, quiet passages, the
// beats and the tempo from it and a coarse spectrum, turns them with the mode into the numbers of
// the frame (blackcapFrameInputs) and draws:
// - the scene into a texture: a background gradient with a glow and floating lights, then the
//   organic shapes with depth, lit by two lights, mirroring their surroundings and glowing at their
//   edges;
// - the scene with a soft glow of bright parts and a vignette into the input of the flash guard,
// - the guarded frame onto the canvas (the last pass), so no change of mode, scheme or music
//   flashes more than three times a second.
// A mode change blends forms and motion over several seconds, a color scheme change colors and
// material. The quality level sets how finely the surfaces are curved, the number of shapes and
// lights, the resolution of the scene and the glow.

import { followLevels } from '../../analysis/bands'
import { AudioFeatureTracker } from '../../analysis/features'
import { SpectrumLevels } from '../../analysis/spectrum'
import { lookAt, modelMatrix, multiply, perspective } from '../../matrices'
import type {
    VisualizationIntensityLevel,
    VisualizationRenderer,
    VisualizationRendererOptions,
} from '../../module'
import type { QualityLevel } from '../../quality'
import { FlashGuard } from '../../safety/flashGuard'
import { createProgram, fullscreenVertex, requireResource, type Program } from '../../webgl'
import { blackcapFrameInputs } from './frame'
import { blackcapMode, blackcapModes, modeBlendSeconds, modeLook, ModeTransition } from './modes'
import { Breath, cameraPose, shapePlacements } from './motion'
import { blackcapProfiles, sceneSize, type BlackCapProfile } from './profiles'
import { blackcapColorScheme, blackcapColorSchemes, schemeLook, SchemeTransition } from './schemes'
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

/** Longest time between two frames the flash guard accounts for. */
const maxGuardSeconds = 1

/**
 * Seconds the spectrum of the surfaces needs to rise and to fall: it follows a hit at once but
 * sinks back softly, so the shapes sway instead of twitching.
 */
const spectrumAttackSeconds = 0.08
const spectrumReleaseSeconds = 0.5

/** Passes that spread the spectrum over neighbouring bands. */
const spreadPasses = 2

/** Seconds the gradient of the surfaces needs to move on by a beat. */
const shadeFollowSeconds = 0.8

/**
 * The levels of the music follow a rise at once and fall back softly, and a beat fades out over
 * half a second: the shapes react clearly, but smoothly.
 */
export const blackcapFeatureOptions = {
    attackSeconds: 0.08,
    releaseSeconds: 0.5,
    beat: { pulseSeconds: 0.5 },
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
    /** The spectrum in decibels (getFloatFrequencyData): unlike bytes it never saturates. */
    private readonly spectrum: Float32Array<ArrayBuffer>
    private readonly spectrumLevels = new SpectrumLevels(spectrumBands)
    private readonly rawBands = new Float32Array(spectrumBands)
    private readonly bands = new Float32Array(spectrumBands)
    /** The calm spectrum spread over neighbouring bands, as the surfaces follow it. */
    private readonly spread = new Float32Array(spectrumBands)
    private readonly features = new AudioFeatureTracker(blackcapFeatureOptions)
    private readonly breath = new Breath()
    private readonly modes: ModeTransition
    private readonly schemes: SchemeTransition
    private readonly guard: FlashGuard

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
    private intensity: VisualizationIntensityLevel
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
        this.guard = new FlashGuard(gl, this.width, this.height, options.intensity)

        this.modes = new ModeTransition(modeLook(blackcapModes[0]!))
        const scheme =
            (options.colorScheme === null ? undefined : blackcapColorScheme(options.colorScheme)) ??
            blackcapColorSchemes[0]!
        this.schemes = new SchemeTransition(schemeLook(scheme))

        // An analyser of its own with little smoothing, so the onsets of beats stay sharp; the levels
        // and the spectrum are smoothed by the followers instead.
        this.source = options.audio
        this.analyser = options.audio.context.createAnalyser()
        this.analyser.fftSize = fftSize
        this.analyser.smoothingTimeConstant = 0.5
        this.source.connect(this.analyser)
        this.spectrum = new Float32Array(this.analyser.frequencyBinCount)
    }

    showPreset(id: string, blendSeconds: number): void {
        const mode = blackcapMode(id)
        if (mode) {
            this.modes.show(modeLook(mode), modeBlendSeconds(blendSeconds), this.now() / 1000)
        }
    }

    showColorScheme(id: string, blendSeconds: number): void {
        const scheme = blackcapColorScheme(id)
        if (scheme) {
            this.schemes.show(schemeLook(scheme), blendSeconds, this.now() / 1000)
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
        const elapsed = this.lastTime === null ? 1 / 60 : Math.max(0, time - this.lastTime)
        const seconds = Math.min(maxFrameSeconds, elapsed)
        this.lastTime = time

        this.analyser.getFloatFrequencyData(this.spectrum)
        const sampleRate = this.analyser.context.sampleRate
        const audio = this.features.update(this.spectrum, sampleRate, time)
        this.spectrumLevels.update(this.spectrum, sampleRate, seconds, this.rawBands)
        followLevels(
            this.bands,
            this.rawBands,
            seconds,
            spectrumAttackSeconds,
            spectrumReleaseSeconds,
        )
        spreadLevels(this.bands, this.spread, spreadPasses)
        const look = this.modes.look(time)
        const { colors, material } = this.schemes.look(time)
        const frame = blackcapFrameInputs(look, audio, this.intensity)
        const breath = this.breath.update(seconds, audio.bpm)
        if (audio.beat) {
            this.shadeTarget += frame.shadeStep
        }

        this.shade +=
            (this.shadeTarget - this.shade) * (1 - Math.exp(-seconds / shadeFollowSeconds))
        this.flow += seconds * frame.flowSpeed
        this.lobePhase += seconds * frame.lobeSpeed
        this.motionTime += seconds * frame.motionSpeed

        const pose = cameraPose(time, frame.drift)
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
        gl.uniform1f(background.uniform('uLights'), material.lights)
        gl.uniform1i(background.uniform('uLightCount'), this.profile.lights)
        gl.uniform1f(background.uniform('uGlowStrength'), frame.glow)
        gl.uniform1f(background.uniform('uLightLevel'), frame.lights)
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
        gl.uniform4fv(shapes.uniform('uLobes'), frame.lobes)
        gl.uniform1f(shapes.uniform('uLobePhase'), this.lobePhase)
        gl.uniform1f(shapes.uniform('uWaves'), frame.waves)
        gl.uniform1f(shapes.uniform('uWaveScale'), look.form.waveScale)
        gl.uniform1f(shapes.uniform('uSpectrumAmount'), frame.surfaceLift)
        gl.uniform1fv(shapes.uniform('uSpectrum'), this.spread)
        gl.uniform1f(shapes.uniform('uBassLift'), frame.bassLift)
        gl.uniform1f(shapes.uniform('uRipple'), frame.ripple)
        gl.uniform3fv(shapes.uniform('uStretch'), look.form.stretch)
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
        gl.uniform1f(shapes.uniform('uGloss'), Math.max(1, material.gloss))
        gl.uniform1f(shapes.uniform('uMirror'), material.mirror)
        gl.uniform1f(shapes.uniform('uRimStrength'), material.rim)
        gl.uniform1f(shapes.uniform('uShade'), this.shade + time * 0.05)
        gl.uniform1f(shapes.uniform('uTime'), time)
        gl.uniform1f(shapes.uniform('uRimLight'), frame.rim)
        gl.uniform1f(shapes.uniform('uGlint'), frame.glint)
        gl.uniform1f(shapes.uniform('uPulse'), frame.pulse)
        gl.bindVertexArray(this.meshVertices)
        for (const shape of shapePlacements(
            look.motion,
            frame,
            this.profile.shapes,
            this.motionTime,
            breath,
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

        // The finished frame into the guard.
        const composite = this.composite
        this.guard.bindInput()
        gl.useProgram(composite.program)
        gl.activeTexture(gl.TEXTURE0)
        gl.bindTexture(gl.TEXTURE_2D, target.texture)
        gl.uniform1i(composite.uniform('uScene'), 0)
        gl.uniform3fv(composite.uniform('uGlow'), colors.glow)
        gl.uniform1f(composite.uniform('uBloom'), this.profile.bloom ? material.bloom : 0)
        gl.uniform1f(composite.uniform('uAspect'), this.width / this.height)
        gl.uniform1f(composite.uniform('uPulse'), frame.pulse)
        gl.uniform1f(composite.uniform('uBrightness'), frame.brightness)
        gl.bindVertexArray(this.emptyVertices)
        gl.drawArrays(gl.TRIANGLES, 0, 3)
        gl.bindVertexArray(null)

        // The last pass: the guarded frame onto the canvas.
        this.guard.present(Math.min(maxGuardSeconds, elapsed))
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
        this.guard.dispose()
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
