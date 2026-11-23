// Smooth blends between the looks of the own visualizations (the modes and color schemes of
// A-Force, the scenes of BlackCap): a look is a record of numbers and colors, and a change of preset
// moves every value from the look shown at that moment into the new one. Pure.

/** A color with red, green and blue from 0 to 1. */
export type Rgb = readonly [number, number, number]

export function mixNumber(a: number, b: number, t: number): number {
    return a * (1 - t) + b * t
}

export function mixRgb(a: Rgb, b: Rgb, t: number): Rgb {
    return [mixNumber(a[0], b[0], t), mixNumber(a[1], b[1], t), mixNumber(a[2], b[2], t)]
}

/** Mixes every value of two records of the same keys with the mix function. */
export function mixRecord<T extends object>(a: T, b: T, mix: (x: never, y: never) => unknown): T {
    const result: Record<string, unknown> = {}
    for (const key of Object.keys(a) as (keyof T & string)[]) {
        result[key] = mix(a[key] as never, b[key] as never)
    }

    return result as T
}

/** Smooth start and end of a blend (smoothstep). */
export function ease(t: number): number {
    const x = Math.min(1, Math.max(0, t))
    return x * x * (3 - 2 * x)
}

/** The amount of a blend clamped to 0 .. 1; a value that is not finite counts as 1. */
export function blendAmount(t: number): number {
    return Math.min(1, Math.max(0, Number.isFinite(t) ? t : 1))
}

/** Mixes two looks: a at t = 0, b at t = 1. */
export type LookMix<T> = (a: T, b: T, t: number) => T

/**
 * A look over time while the preset changes: from the look shown at the moment of the change into
 * the new one, eased over the blend seconds. A change during a blend starts from the look shown at
 * that moment, so nothing jumps.
 */
export class LookTransition<T> {
    private from: T
    private to: T
    private start = 0
    private seconds = 0

    constructor(
        initial: T,
        private readonly mix: LookMix<T>,
    ) {
        this.from = initial
        this.to = initial
    }

    /** Starts blending into a look at the time (seconds) over the given seconds (0: at once). */
    show(look: T, seconds: number, time: number): void {
        this.from = this.look(time)
        this.to = look
        this.start = time
        this.seconds = Math.max(0, seconds)
    }

    /** The look at the time in seconds. */
    look(time: number): T {
        if (this.seconds <= 0 || time >= this.start + this.seconds) {
            return this.to
        }

        return this.mix(this.from, this.to, ease((time - this.start) / this.seconds))
    }

    /** Whether the blend still runs at the time. */
    blending(time: number): boolean {
        return this.seconds > 0 && time < this.start + this.seconds
    }
}
