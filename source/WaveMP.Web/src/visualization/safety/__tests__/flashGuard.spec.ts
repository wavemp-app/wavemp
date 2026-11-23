import { describe, expect, it } from 'vitest'
import { fakeWebGl, type Call } from '../../__tests__/webgl'
import {
    FlashGuard,
    flashGuardUniforms,
    flashSamplesPerCell,
    limitFragment,
    outputFragment,
    reduceFragment,
} from '../flashGuard'
import { flashGridColumns, flashGridRows, flashLimits } from '../flashLimit'

// The flash guard on the GPU with a fake WebGL context: jsdom has no WebGL, so the tests check the
// passes the guard asks for. What the passes compute is the limiter of flashLimit.ts, whose tests
// run strobes through it.

function setup(width = 320, height = 180) {
    const webgl = fakeWebGl()
    const gl = webgl.gl as WebGL2RenderingContext
    const guard = new FlashGuard(gl, width, height)
    webgl.calls.length = 0
    /** The arguments of the last call that set the uniform. */
    const uniform = (name: string): unknown[] | undefined =>
        [...webgl.calls]
            .reverse()
            .find(
                ([property, args]) =>
                    property.startsWith('uniform') &&
                    (args[0] as { name?: string } | null)?.name === name,
            )?.[1]
    return { ...webgl, gl, guard, uniform }
}

/**
 * The passes among the recorded calls: each draw call with the framebuffer, viewport and program
 * it drew with.
 */
function passes(calls: readonly Call[]) {
    const result: { framebuffer: unknown; viewport: unknown[]; program: unknown }[] = []
    let framebuffer: unknown = null
    let viewport: unknown[] = []
    let program: unknown = null
    for (const [property, args] of calls) {
        if (property === 'bindFramebuffer' && args[0] === 'FRAMEBUFFER') {
            framebuffer = args[1]
        } else if (property === 'viewport') {
            viewport = args
        } else if (property === 'useProgram') {
            program = args[0]
        } else if (property === 'drawArrays') {
            result.push({ framebuffer, viewport, program })
        }
    }

    return result
}

describe('FlashGuard', () => {
    it('reduces, limits and mixes each frame onto the canvas without reading pixels back', () => {
        const { guard, calls, named } = setup()

        guard.present(1 / 60)

        const [reduce, limit, output] = passes(calls)
        expect(reduce!.viewport).toEqual([0, 0, flashGridColumns * 2, flashGridRows])
        expect(limit!.viewport).toEqual([0, 0, flashGridColumns, flashGridRows])
        expect(output!.framebuffer).toBeNull()
        expect(output!.viewport).toEqual([0, 0, 320, 180])
        expect(new Set([reduce!.program, limit!.program, output!.program]).size).toBe(3)
        // The output is copied as the previous output of the next frame.
        expect(named('blitFramebuffer')).toEqual([
            ['blitFramebuffer', [0, 0, 320, 180, 0, 0, 320, 180, 'COLOR_BUFFER_BIT', 'NEAREST']],
        ])
        expect(named('readPixels')).toHaveLength(0)
        expect(named('getError')).toHaveLength(0)
    })

    it('lets the first frame pass and limits the following ones', () => {
        const { guard, uniform } = setup()

        guard.present(0)
        expect(uniform('uStarted')?.[1]).toBe(0)

        guard.present(1 / 60)
        expect(uniform('uStarted')?.[1]).toBe(1)

        guard.reset()
        guard.present(1 / 60)
        expect(uniform('uStarted')?.[1]).toBe(0)
    })

    it('moves the centers of the cells at the rate of the intensity', () => {
        const { guard, uniform } = setup()

        guard.present(0.1)
        expect(uniform('uSteps')?.slice(1)).toEqual([
            flashLimits.normal.luminanceRate * 0.1,
            flashLimits.normal.rednessRate * 0.1,
        ])
        expect(uniform('uBands')?.slice(1)).toEqual([
            flashLimits.normal.luminanceBand,
            flashLimits.normal.rednessBand,
        ])

        guard.setIntensity('reduced')
        guard.present(0.1)
        expect(uniform('uSteps')?.slice(1)).toEqual([
            flashLimits.reduced.luminanceRate * 0.1,
            flashLimits.reduced.rednessRate * 0.1,
        ])
    })

    it('reads the centers of the previous frame and draws the new ones (ping-pong)', () => {
        const { guard, calls } = setup()
        const centerTargets = () => passes(calls).filter((pass) => pass.viewport[2] === 6)

        guard.present(1 / 60)
        guard.present(1 / 60)

        const [first, second] = centerTargets()
        expect(first!.framebuffer).not.toBe(second!.framebuffer)
    })

    it('draws the centers and the mix factor of the cells at once', () => {
        const { named } = setup()
        const webgl = fakeWebGl()
        new FlashGuard(webgl.gl as WebGL2RenderingContext, 10, 10)

        expect(webgl.named('drawBuffers')).toEqual([
            ['drawBuffers', [['COLOR_ATTACHMENT0', 'COLOR_ATTACHMENT1']]],
            ['drawBuffers', [['COLOR_ATTACHMENT0', 'COLOR_ATTACHMENT1']]],
        ])
        expect(named('drawBuffers')).toHaveLength(0)
    })

    it('cross-fades an earlier frame into the source when asked', () => {
        const { guard, uniform } = setup()
        const latest = { id: 'latest' } as unknown as WebGLTexture
        const earlier = { id: 'earlier' } as unknown as WebGLTexture

        guard.present(1 / 60, { texture: latest, earlier, earlierWeight: 0.25 })
        expect(uniform('uEarlierWeight')?.[1]).toBe(0.25)

        guard.present(1 / 60, { texture: latest })
        expect(uniform('uEarlierWeight')?.[1]).toBe(0)

        guard.present(1 / 60, { texture: latest, earlier, earlierWeight: 3 })
        expect(uniform('uEarlierWeight')?.[1]).toBe(1)
    })

    it('shows the source at full brightness unless asked to dim it', () => {
        const { guard, uniform } = setup()
        const latest = { id: 'latest' } as unknown as WebGLTexture

        guard.present(1 / 60)
        expect(uniform('uBrightness')?.[1]).toBe(1)

        guard.present(1 / 60, { texture: latest, brightness: 0.6 })
        expect(uniform('uBrightness')?.[1]).toBe(0.6)

        guard.present(1 / 60, { texture: latest, brightness: -2 })
        expect(uniform('uBrightness')?.[1]).toBe(0)
    })

    it('takes new frames at a new size and lets the next frame pass', () => {
        const { guard, uniform, named } = setup()
        guard.present(1 / 60)

        guard.resize(640, 360)
        guard.resize(640, 360)

        const frames = named('texImage2D').filter(([, args]) => args[3] === 640)
        expect(frames).toHaveLength(2)
        expect(named('deleteTexture')).toHaveLength(2)
        guard.present(1 / 60)
        expect(uniform('uStarted')?.[1]).toBe(0)
    })

    it('draws into its input when the renderer asks for it', () => {
        const { guard, named } = setup()

        guard.bindInput()

        const [framebuffer] = named('bindFramebuffer')
        expect(framebuffer![1][1]).not.toBeNull()
        expect(named('viewport')).toEqual([['viewport', [0, 0, 320, 180]]])
    })

    it('releases everything it created', () => {
        const { guard, named } = setup()

        guard.dispose()

        expect(named('deleteProgram')).toHaveLength(3)
        expect(named('deleteVertexArray')).toHaveLength(1)
        // Input, previous output, cells, two targets of the centers and the mix.
        expect(named('deleteTexture')).toHaveLength(6)
        expect(named('deleteFramebuffer')).toHaveLength(5)
    })
})

describe('flashGuardUniforms', () => {
    it('gives the bands and the steps of a frame', () => {
        expect(flashGuardUniforms(flashLimits.normal, 0.5)).toEqual({
            bands: [flashLimits.normal.luminanceBand, flashLimits.normal.rednessBand],
            steps: [flashLimits.normal.luminanceRate * 0.5, flashLimits.normal.rednessRate * 0.5],
        })
        expect(flashGuardUniforms(flashLimits.normal, -1).steps).toEqual([0, 0])
    })
})

describe('the shaders of the flash guard', () => {
    it('measure the grid of the limiter in linear light', () => {
        expect(reduceFragment).toContain(`const int columns = ${flashGridColumns};`)
        expect(reduceFragment).toContain(`const int rows = ${flashGridRows};`)
        expect(reduceFragment).toContain(`const int samples = ${flashSamplesPerCell};`)
        expect(reduceFragment).toContain('vec3(0.2126, 0.7152, 0.0722)')
        expect(reduceFragment).toContain('max(0.0, color.r - color.g - color.b)')
    })

    it('spread the mix factor to the neighbours and round it up', () => {
        expect(limitFragment).toContain('limitCell(neighbour)')
        expect(limitFragment).toContain('ceil(largest * 255.0) / 255.0')
    })

    it('mix in linear light', () => {
        expect(outputFragment).toContain(
            'encodeSrgb(mix(decodeSrgb(now), decodeSrgb(before), amount))',
        )
        for (const source of [reduceFragment, limitFragment, outputFragment]) {
            expect(source.startsWith('#version 300 es')).toBe(true)
        }
    })
})
