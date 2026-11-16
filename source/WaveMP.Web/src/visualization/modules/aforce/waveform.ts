// The waveform A-Force draws: a stable section of the samples of the analyser, reduced to the
// points of a waveform. Pure.

/**
 * Index of the first rising zero crossing in the first half of the samples, or 0 without one.
 * Starting there keeps the picture of a steady tone still instead of jumping from frame to frame.
 */
export function risingZeroCrossing(samples: ArrayLike<number>): number {
    const limit = Math.floor(samples.length / 2)
    for (let index = 1; index < limit; index++) {
        if ((samples[index - 1] ?? 0) < 0 && (samples[index] ?? 0) >= 0) {
            return index
        }
    }

    return 0
}

/**
 * Fills out with the samples from the first rising zero crossing on, span samples long (at most
 * up to the end), each point the average of the samples it covers. Values are clamped to -1 .. 1.
 */
export function waveformPoints(samples: ArrayLike<number>, out: Float32Array, span: number): void {
    const points = out.length
    if (points === 0) {
        return
    }

    const start = risingZeroCrossing(samples)
    const length = Math.max(1, Math.min(span, samples.length - start))
    for (let point = 0; point < points; point++) {
        const from = start + Math.floor((point * length) / points)
        const to = Math.max(from + 1, start + Math.floor(((point + 1) * length) / points))
        let sum = 0
        let count = 0
        for (let index = from; index < to && index < samples.length; index++) {
            sum += samples[index] ?? 0
            count++
        }

        const value = count > 0 ? sum / count : 0
        out[point] = Math.min(1, Math.max(-1, Number.isFinite(value) ? value : 0))
    }
}

/** Moves every point of current a share towards target (1 takes target over). */
export function smoothPoints(current: Float32Array, target: Float32Array, share: number): void {
    const amount = Math.min(1, Math.max(0, share))
    const count = Math.min(current.length, target.length)
    for (let index = 0; index < count; index++) {
        const value = current[index] ?? 0
        current[index] = value + ((target[index] ?? 0) - value) * amount
    }
}
