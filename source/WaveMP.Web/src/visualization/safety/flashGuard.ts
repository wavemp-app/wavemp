// The flash guard on the GPU (WebGL 2): the last pass of every renderer, so no visualization shows
// more than three general or red flashes in any one second (WCAG 2.3.1). It does on the GPU what
// flashLimit.ts describes and tests on the CPU, without reading anything back:
// 1. reduce: the new frame and the previous output frame become the mean relative luminance and
//    redness (linear light) of the cells of the grid (6 x 4), in one target of 12 x 4 texels;
// 2. limit: per cell the center of the band moves towards the new value by at most the rate of the
//    intensity times the seconds of the frame (ping-pong targets of 6 x 4); the factor that keeps
//    the value within the band is spread to the neighbours (largest of 3 x 3) into a target of
//    6 x 4 that the next pass reads with linear filtering, as mixAt interpolates it;
// 3. output: every pixel of the new frame is mixed back towards the previous output by that factor,
//    in linear light, onto the canvas; the result is copied as the previous output of the next
//    frame.
// Values are kept with 16 bits in two channels of RGBA8 targets, which every WebGL 2 context can
// draw into: at 144 frames per second a frame moves a center by less than 1/255. Small moving
// details hardly change the mean of a cell and pass; flashes of large areas turn into fades.

import { createProgram, fullscreenVertex, requireResource, type Program } from '../webgl'
import {
    flashGridColumns,
    flashGridRows,
    flashLimits,
    type FlashIntensity,
    type FlashLimits,
} from './flashLimit'

/** Samples per cell in each direction the reduction averages (16 x 16 per cell). */
export const flashSamplesPerCell = 16

const decode = `
vec3 decodeSrgb(vec3 color) {
    return mix(color / 12.92, pow((color + 0.055) / 1.055, vec3(2.4)), step(0.04045, color));
}
`

const packing = `
vec2 pack16(float value) {
    float scaled = floor(clamp(value, 0.0, 1.0) * 65535.0 + 0.5);
    float high = floor(scaled / 256.0);
    return vec2(high, scaled - high * 256.0) / 255.0;
}

float unpack16(vec2 packed) {
    vec2 bytes = floor(packed * 255.0 + 0.5);
    return (bytes.x * 256.0 + bytes.y) / 65535.0;
}
`

/**
 * The input of the guard: the frame, and optionally an earlier frame that is cross-faded in by a
 * weight (Milkdrop shows the time between two of its steps that way), at a brightness (Milkdrop
 * dims quiet passages that way), enlarged around its center by a zoom (Milkdrop swells on a beat
 * that way; the reduction sees the enlarged frame, so the guard limits it like any other change).
 */
const input = `
uniform highp sampler2D uCurrent;
uniform highp sampler2D uEarlier;
uniform float uEarlierWeight;
uniform float uBrightness;
uniform float uZoom;

vec3 currentColor(ivec2 texel) {
    if (uZoom > 0.0) {
        vec2 size = vec2(textureSize(uCurrent, 0));
        vec2 at = (vec2(texel) + 0.5 - size * 0.5) / (1.0 + uZoom) + size * 0.5;
        texel = clamp(ivec2(floor(at)), ivec2(0), ivec2(size) - 1);
    }
    vec3 color = texelFetch(uCurrent, texel, 0).rgb;
    if (uEarlierWeight > 0.0) {
        color = mix(color, texelFetch(uEarlier, texel, 0).rgb, uEarlierWeight);
    }
    return color * uBrightness;
}
`

export const reduceFragment = `#version 300 es
precision highp float;
precision highp int;
${input}
uniform highp sampler2D uPrevious;
out vec4 outColor;
const int columns = ${flashGridColumns};
const int rows = ${flashGridRows};
const int samples = ${flashSamplesPerCell};
${decode}
${packing}
vec3 cellMean(ivec2 cell, bool previous) {
    ivec2 size = textureSize(uCurrent, 0);
    vec3 sum = vec3(0.0);
    for (int y = 0; y < samples; y++) {
        for (int x = 0; x < samples; x++) {
            vec2 at = (vec2(cell) + (vec2(x, y) + 0.5) / float(samples)) / vec2(columns, rows);
            ivec2 texel = min(ivec2(at * vec2(size)), size - 1);
            vec3 color = previous ? texelFetch(uPrevious, texel, 0).rgb : currentColor(texel);
            sum += decodeSrgb(color);
        }
    }
    return sum / float(samples * samples);
}

void main() {
    ivec2 at = ivec2(gl_FragCoord.xy);
    bool previous = at.x >= columns;
    vec3 color = cellMean(ivec2(previous ? at.x - columns : at.x, at.y), previous);
    float luminance = dot(color, vec3(0.2126, 0.7152, 0.0722));
    float redness = max(0.0, color.r - color.g - color.b);
    outColor = vec4(pack16(luminance), pack16(redness));
}
`

export const limitFragment = `#version 300 es
precision highp float;
precision highp int;
uniform highp sampler2D uCells;
uniform highp sampler2D uCenters;
uniform bool uStarted;
uniform vec2 uBands;
uniform vec2 uSteps;
layout(location = 0) out vec4 outCenter;
layout(location = 1) out vec4 outMix;
const int columns = ${flashGridColumns};
const int rows = ${flashGridRows};
${packing}
vec2 measured(ivec2 cell, int offset) {
    vec4 texel = texelFetch(uCells, ivec2(cell.x + offset, cell.y), 0);
    return vec2(unpack16(texel.rg), unpack16(texel.ba));
}

// The moved center and the mix towards the previous output (FlashLimiter.limit).
vec2 limitValue(float center, float value, float previous, float band, float step) {
    float moved = center + clamp(value - center, -step, step);
    float target = clamp(value, moved - band * 0.5, moved + band * 0.5);
    float change = value - previous;
    if (target == value || abs(change) < 1e-9) {
        return vec2(moved, 0.0);
    }
    return vec2(moved, clamp((value - target) / change, 0.0, 1.0));
}

// The new centers (luminance, redness) of a cell and its mix factor.
vec3 limitCell(ivec2 cell) {
    vec2 now = measured(cell, 0);
    if (!uStarted) {
        return vec3(now, 0.0);
    }
    vec2 before = measured(cell, columns);
    vec4 stored = texelFetch(uCenters, cell, 0);
    vec2 luminance = limitValue(unpack16(stored.rg), now.x, before.x, uBands.x, uSteps.x);
    vec2 redness = limitValue(unpack16(stored.ba), now.y, before.y, uBands.y, uSteps.y);
    return vec3(luminance.x, redness.x, max(luminance.y, redness.y));
}

void main() {
    ivec2 cell = ivec2(gl_FragCoord.xy);
    vec3 own = limitCell(cell);
    float largest = own.z;
    for (int y = -1; y <= 1; y++) {
        for (int x = -1; x <= 1; x++) {
            ivec2 neighbour = cell + ivec2(x, y);
            if ((x != 0 || y != 0) && neighbour.x >= 0 && neighbour.y >= 0 &&
                neighbour.x < columns && neighbour.y < rows) {
                largest = max(largest, limitCell(neighbour).z);
            }
        }
    }
    outCenter = vec4(pack16(own.x), pack16(own.y));
    // Rounded up, so the 8 bits of the target never weaken the guard.
    outMix = vec4(ceil(largest * 255.0) / 255.0, 0.0, 0.0, 1.0);
}
`

export const outputFragment = `#version 300 es
precision highp float;
precision highp int;
${input}
uniform highp sampler2D uPrevious;
uniform highp sampler2D uMix;
in vec2 vUv;
out vec4 outColor;
${decode}
vec3 encodeSrgb(vec3 color) {
    return mix(color * 12.92, 1.055 * pow(color, vec3(1.0 / 2.4)) - 0.055,
        step(0.0031308, color));
}

void main() {
    ivec2 texel = ivec2(gl_FragCoord.xy);
    vec3 now = currentColor(texel);
    float amount = texture(uMix, vUv).r;
    if (amount <= 0.0) {
        outColor = vec4(now, 1.0);
        return;
    }
    vec3 before = texelFetch(uPrevious, texel, 0).rgb;
    outColor = vec4(encodeSrgb(mix(decodeSrgb(now), decodeSrgb(before), amount)), 1.0);
}
`

/** The uniforms of the limit pass for a frame: bands and the steps the centers may move. */
export function flashGuardUniforms(
    limits: FlashLimits,
    seconds: number,
): { bands: [number, number]; steps: [number, number] } {
    const time = Math.max(0, seconds)
    return {
        bands: [limits.luminanceBand, limits.rednessBand],
        steps: [limits.luminanceRate * time, limits.rednessRate * time],
    }
}

/** What the guard shows: a texture of the size of the canvas, optionally cross-faded. */
export interface FlashGuardSource {
    readonly texture: WebGLTexture
    /** An earlier frame mixed in by earlierWeight (0: none, 1: only the earlier frame). */
    readonly earlier?: WebGLTexture
    readonly earlierWeight?: number
    /** A factor of the brightness of the frame, 0 to 1 (1 when left out). */
    readonly brightness?: number
    /**
     * How much the frame is enlarged around its center: 0 (when left out) shows it as it is, 0.1
     * a tenth larger; at most maxFlashGuardZoom.
     */
    readonly zoom?: number
}

/** Largest zoom of a source the guard shows. */
export const maxFlashGuardZoom = 0.25

interface Target {
    readonly texture: WebGLTexture
    readonly framebuffer: WebGLFramebuffer
}

export class FlashGuard {
    private readonly reduce: Program
    private readonly limit: Program
    private readonly output: Program
    private readonly vertices: WebGLVertexArrayObject
    /** The frame of the renderer (when it draws into the guard). */
    private input: Target | null = null
    /** The previous output frame. */
    private previous: Target | null = null
    private readonly cells: Target
    private readonly mix: WebGLTexture
    /** Ping-pong targets of the centers, each drawn together with the mix (two attachments). */
    private readonly centers: [Target, Target]
    private limits: FlashLimits
    private started = false
    private width: number
    private height: number

    constructor(
        private readonly gl: WebGL2RenderingContext,
        width: number,
        height: number,
        intensity: FlashIntensity = 'normal',
    ) {
        this.width = Math.max(1, Math.round(width))
        this.height = Math.max(1, Math.round(height))
        this.limits = flashLimits[intensity]
        this.reduce = createProgram(gl, fullscreenVertex, reduceFragment, 'the flash guard')
        this.limit = createProgram(gl, fullscreenVertex, limitFragment, 'the flash guard')
        this.output = createProgram(gl, fullscreenVertex, outputFragment, 'the flash guard')
        this.vertices = requireResource(gl.createVertexArray())

        this.cells = this.createTarget(flashGridColumns * 2, flashGridRows, gl.NEAREST)
        this.mix = this.createTexture(flashGridColumns, flashGridRows, gl.LINEAR)
        this.centers = [this.createCenters(), this.createCenters()]
        this.prepareFrames()
    }

    /** The intensity: how fast a cell may change (see flashLimits). */
    setIntensity(level: FlashIntensity): void {
        this.limits = flashLimits[level]
    }

    /** Changes the size of the canvas; the next frame passes as it is. */
    resize(width: number, height: number): void {
        const nextWidth = Math.max(1, Math.round(width))
        const nextHeight = Math.max(1, Math.round(height))
        if (nextWidth === this.width && nextHeight === this.height) {
            return
        }

        this.width = nextWidth
        this.height = nextHeight
        this.prepareFrames()
    }

    /** Starts again: the next frame passes as it is (e.g. after a pause). */
    reset(): void {
        this.started = false
    }

    /** Binds the framebuffer a renderer draws its finished frame into, at the size of the canvas. */
    bindInput(): void {
        const gl = this.gl
        gl.bindFramebuffer(gl.FRAMEBUFFER, this.input!.framebuffer)
        gl.viewport(0, 0, this.width, this.height)
    }

    /**
     * Guards a frame onto the canvas: the frame drawn into the input (bindInput) or the given
     * source, seconds after the previous frame.
     */
    present(seconds: number, source?: FlashGuardSource): void {
        const gl = this.gl
        const current = source?.texture ?? this.input!.texture
        const earlierWeight = source?.earlier
            ? Math.max(0, Math.min(1, source.earlierWeight ?? 0))
            : 0
        const earlier = source?.earlier ?? current
        const brightness = Math.max(0, Math.min(1, source?.brightness ?? 1))
        const zoom = Math.max(0, Math.min(maxFlashGuardZoom, source?.zoom || 0))
        const previous = this.previous!
        const [stored, next] = this.centers
        const uniforms = flashGuardUniforms(this.limits, seconds)

        gl.disable(gl.BLEND)
        gl.bindVertexArray(this.vertices)

        // 1. The cells of the new frame and of the previous output.
        gl.bindFramebuffer(gl.FRAMEBUFFER, this.cells.framebuffer)
        gl.viewport(0, 0, flashGridColumns * 2, flashGridRows)
        gl.useProgram(this.reduce.program)
        this.bindInputs(this.reduce, current, earlier, earlierWeight, brightness, zoom)
        gl.activeTexture(gl.TEXTURE2)
        gl.bindTexture(gl.TEXTURE_2D, previous.texture)
        gl.uniform1i(this.reduce.uniform('uPrevious'), 2)
        gl.drawArrays(gl.TRIANGLES, 0, 3)

        // 2. The centers and the mix factor of the cells.
        gl.bindFramebuffer(gl.FRAMEBUFFER, next.framebuffer)
        gl.viewport(0, 0, flashGridColumns, flashGridRows)
        gl.useProgram(this.limit.program)
        gl.activeTexture(gl.TEXTURE0)
        gl.bindTexture(gl.TEXTURE_2D, this.cells.texture)
        gl.uniform1i(this.limit.uniform('uCells'), 0)
        gl.activeTexture(gl.TEXTURE1)
        gl.bindTexture(gl.TEXTURE_2D, stored.texture)
        gl.uniform1i(this.limit.uniform('uCenters'), 1)
        gl.uniform1i(this.limit.uniform('uStarted'), this.started ? 1 : 0)
        gl.uniform2f(this.limit.uniform('uBands'), ...uniforms.bands)
        gl.uniform2f(this.limit.uniform('uSteps'), ...uniforms.steps)
        gl.drawArrays(gl.TRIANGLES, 0, 3)

        // 3. The output onto the canvas, kept as the previous output of the next frame.
        gl.bindFramebuffer(gl.FRAMEBUFFER, null)
        gl.viewport(0, 0, this.width, this.height)
        gl.useProgram(this.output.program)
        this.bindInputs(this.output, current, earlier, earlierWeight, brightness, zoom)
        gl.activeTexture(gl.TEXTURE2)
        gl.bindTexture(gl.TEXTURE_2D, previous.texture)
        gl.uniform1i(this.output.uniform('uPrevious'), 2)
        gl.activeTexture(gl.TEXTURE3)
        gl.bindTexture(gl.TEXTURE_2D, this.mix)
        gl.uniform1i(this.output.uniform('uMix'), 3)
        gl.drawArrays(gl.TRIANGLES, 0, 3)

        gl.bindFramebuffer(gl.READ_FRAMEBUFFER, null)
        gl.bindFramebuffer(gl.DRAW_FRAMEBUFFER, previous.framebuffer)
        gl.blitFramebuffer(
            0,
            0,
            this.width,
            this.height,
            0,
            0,
            this.width,
            this.height,
            gl.COLOR_BUFFER_BIT,
            gl.NEAREST,
        )
        gl.bindFramebuffer(gl.FRAMEBUFFER, null)
        gl.bindVertexArray(null)

        this.centers.reverse()
        this.started = true
    }

    dispose(): void {
        const gl = this.gl
        this.releaseFrames()
        for (const target of [this.cells, ...this.centers]) {
            gl.deleteFramebuffer(target.framebuffer)
            gl.deleteTexture(target.texture)
        }

        gl.deleteTexture(this.mix)
        gl.deleteVertexArray(this.vertices)
        for (const program of [this.reduce, this.limit, this.output]) {
            gl.deleteProgram(program.program)
        }
    }

    private bindInputs(
        program: Program,
        current: WebGLTexture,
        earlier: WebGLTexture,
        earlierWeight: number,
        brightness: number,
        zoom: number,
    ): void {
        const gl = this.gl
        gl.activeTexture(gl.TEXTURE0)
        gl.bindTexture(gl.TEXTURE_2D, current)
        gl.uniform1i(program.uniform('uCurrent'), 0)
        gl.activeTexture(gl.TEXTURE1)
        gl.bindTexture(gl.TEXTURE_2D, earlier)
        gl.uniform1i(program.uniform('uEarlier'), 1)
        gl.uniform1f(program.uniform('uEarlierWeight'), earlierWeight)
        gl.uniform1f(program.uniform('uBrightness'), brightness)
        gl.uniform1f(program.uniform('uZoom'), zoom)
    }

    private createTexture(width: number, height: number, filter: number): WebGLTexture {
        const gl = this.gl
        const texture = requireResource(gl.createTexture())
        gl.bindTexture(gl.TEXTURE_2D, texture)
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA8, width, height, 0, gl.RGBA, gl.UNSIGNED_BYTE, null)
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, filter)
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, filter)
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE)
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE)
        return texture
    }

    private createTarget(width: number, height: number, filter: number): Target {
        const gl = this.gl
        const texture = this.createTexture(width, height, filter)
        const framebuffer = requireResource(gl.createFramebuffer())
        gl.bindFramebuffer(gl.FRAMEBUFFER, framebuffer)
        gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, texture, 0)
        gl.clearColor(0, 0, 0, 1)
        gl.clear(gl.COLOR_BUFFER_BIT)
        gl.bindFramebuffer(gl.FRAMEBUFFER, null)
        return { texture, framebuffer }
    }

    /** A target of the centers that also draws the mix factor (second attachment). */
    private createCenters(): Target {
        const gl = this.gl
        const target = this.createTarget(flashGridColumns, flashGridRows, gl.NEAREST)
        gl.bindFramebuffer(gl.FRAMEBUFFER, target.framebuffer)
        gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT1, gl.TEXTURE_2D, this.mix, 0)
        gl.drawBuffers([gl.COLOR_ATTACHMENT0, gl.COLOR_ATTACHMENT1])
        gl.bindFramebuffer(gl.FRAMEBUFFER, null)
        return target
    }

    /** The input and the previous output at the size of the canvas. */
    private prepareFrames(): void {
        this.releaseFrames()
        this.input = this.createTarget(this.width, this.height, this.gl.NEAREST)
        this.previous = this.createTarget(this.width, this.height, this.gl.NEAREST)
        this.started = false
    }

    private releaseFrames(): void {
        for (const target of [this.input, this.previous]) {
            if (target) {
                this.gl.deleteFramebuffer(target.framebuffer)
                this.gl.deleteTexture(target.texture)
            }
        }

        this.input = null
        this.previous = null
    }
}
