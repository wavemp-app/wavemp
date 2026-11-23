// Fakes of a WebGL 2 context and of the output of the player for the tests of the renderers of the
// own visualizations. jsdom has no WebGL, so the tests check what a renderer asks the context to do.

import { vi } from 'vitest'

export type Call = [string, unknown[]]

/**
 * A stand-in for a WebGL 2 context: every method is recorded and succeeds, every constant is its
 * own name. jsdom has no WebGL, so the tests check what the renderer asks the context to do.
 */
export function fakeWebGl() {
    const calls: Call[] = []
    const loseContext = vi.fn()
    let resources = 0
    const results: Record<string, (...args: unknown[]) => unknown> = {
        getShaderParameter: () => true,
        getProgramParameter: () => true,
        isContextLost: () => false,
        getUniformLocation: (_, name) => ({ name }),
        getExtension: (name) => (name === 'WEBGL_lose_context' ? { loseContext } : null),
    }
    const gl = new Proxy(
        {},
        {
            get(_, property: string) {
                if (/^[A-Z0-9_]+$/.test(property)) {
                    return property
                }

                return (...args: unknown[]) => {
                    calls.push([property, args])
                    if (property in results) {
                        return results[property]!(...args)
                    }

                    return property.startsWith('create') ? { id: ++resources } : undefined
                }
            },
        },
    )
    const named = (name: string) => calls.filter(([property]) => property === name)
    return { gl, calls, named, loseContext }
}

export function fakeAudio() {
    const analyser = {
        fftSize: 0,
        smoothingTimeConstant: 0,
        get frequencyBinCount() {
            return this.fftSize / 2
        },
        context: { sampleRate: 48000 },
        /** A steady spectrum in decibels, as getFloatFrequencyData writes it. */
        getFloatFrequencyData: vi.fn((data: Float32Array) => data.fill(-50)),
        getFloatTimeDomainData: vi.fn((data: Float32Array) =>
            data.forEach((_, index) => (data[index] = Math.sin(index / 10))),
        ),
    }
    const audio = {
        context: { createAnalyser: vi.fn(() => analyser) },
        connect: vi.fn(),
        disconnect: vi.fn(),
    }
    return { audio: audio as unknown as AnalyserNode, analyser, raw: audio }
}
