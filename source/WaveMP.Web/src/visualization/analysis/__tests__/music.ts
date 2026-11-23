// A short piece of synthetic music for the tests of the analysis, made like a real groove instead
// of steady sine tones: a kick drum on every beat at 120 beats per minute, a snare on beats two and
// four, closed hi-hats on every eighth note, a bass line between the kicks and a soft chord pad
// throughout. In the middle the drums and the bass stop for a quiet bridge in which only the pad
// plays. Deterministic (its noise comes from a fixed seed), so the tests always hear the same.

export const musicSampleRate = 48000

/** Seconds of one beat (120 beats per minute). */
export const beatSeconds = 0.5

/** The parts of the piece in seconds: groove, quiet bridge, groove. */
export const grooveEnd = 8
export const bridgeEnd = 12
export const musicSeconds = 20

export interface Music {
    readonly samples: Float32Array
    /** Times in seconds at which a kick starts. */
    readonly kicks: readonly number[]
    /** Times in seconds at which a snare starts. */
    readonly snares: readonly number[]
    /** Times in seconds at which a hi-hat starts. */
    readonly hats: readonly number[]
}

/** Whether the drums and the bass play at a time (outside the bridge). */
export function grooves(time: number): boolean {
    return time < grooveEnd || time >= bridgeEnd
}

/** A small pseudo random generator with a fixed seed (mulberry32), values from -1 to 1. */
function noise(seed: number): () => number {
    let state = seed >>> 0
    return () => {
        state = (state + 0x6d2b79f5) >>> 0
        let value = state
        value = Math.imul(value ^ (value >>> 15), value | 1)
        value ^= value + Math.imul(value ^ (value >>> 7), value | 61)
        return (((value ^ (value >>> 14)) >>> 0) / 4294967296) * 2 - 1
    }
}

export function makeMusic(): Music {
    const rate = musicSampleRate
    const samples = new Float32Array(Math.round(musicSeconds * rate))
    const kicks: number[] = []
    const snares: number[] = []
    const hats: number[] = []
    const random = noise(121)

    for (let beat = 0; beat * beatSeconds < musicSeconds; beat++) {
        const start = beat * beatSeconds
        if (!grooves(start)) {
            continue
        }

        // Kick: a sine falling from 150 Hz to 45 Hz within a few hundredths of a second.
        kicks.push(start)
        let phase = 0
        for (let index = 0; index < 0.35 * rate; index++) {
            const time = index / rate
            const frequency = 45 + 105 * Math.exp(-time / 0.03)
            phase += (2 * Math.PI * frequency) / rate
            add(samples, start, index, 0.9 * Math.sin(phase) * Math.exp(-time / 0.12))
        }

        // Snare on beats two and four: a short tone of its body and a burst of noise.
        if (beat % 2 === 1) {
            snares.push(start)
            let previous = 0
            for (let index = 0; index < 0.25 * rate; index++) {
                const time = index / rate
                const white = random()
                const bright = white - previous
                previous = white
                const body = Math.sin(2 * Math.PI * 330 * time) * Math.exp(-time / 0.05)
                add(samples, start, index, 0.3 * body + 0.25 * bright * Math.exp(-time / 0.07))
            }
        }

        // Bass line on the second eighth note of the beat, between the kicks.
        const note = [55, 55, 65.4, 49][beat % 4]!
        const bassStart = start + beatSeconds / 2
        for (let index = 0; index < 0.18 * rate; index++) {
            const time = index / rate
            const envelope = Math.min(1, time / 0.01) * Math.exp(-time / 0.08)
            add(samples, bassStart, index, 0.25 * envelope * Math.sin(2 * Math.PI * note * time))
        }

        // Closed hi-hats on both eighth notes: very bright noise that fades within milliseconds.
        for (const hat of [start, bassStart]) {
            hats.push(hat)
            let first = 0
            let second = 0
            for (let index = 0; index < 0.06 * rate; index++) {
                const white = random()
                const bright = white - 2 * first + second
                second = first
                first = white
                add(samples, hat, index, 0.15 * bright * Math.exp(-index / rate / 0.015))
            }
        }
    }

    // The pad: a soft chord through the whole piece, the only sound of the bridge.
    for (let index = 0; index < samples.length; index++) {
        const time = index / rate
        const chord =
            Math.sin(2 * Math.PI * 220 * time) +
            Math.sin(2 * Math.PI * 277.2 * time) +
            Math.sin(2 * Math.PI * 329.6 * time)
        samples[index] = samples[index]! + 0.012 * chord
    }

    return { samples, kicks, snares, hats }
}

function add(samples: Float32Array, start: number, index: number, value: number): void {
    const position = Math.round(start * musicSampleRate) + index
    if (position < samples.length) {
        samples[position] = samples[position]! + value
    }
}
