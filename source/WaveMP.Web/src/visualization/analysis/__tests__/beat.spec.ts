import { describe, expect, it } from 'vitest'
import { BeatDetector, type BeatState } from '../beat'

const frame = 1 / 60

/**
 * Feeds the detector frames at 60 per second for the given seconds: a kick (energy 0.9 for 50 ms)
 * at every beat of the tempo on a floor of 0.2, with a little deterministic jitter.
 */
function kicks(
    detector: BeatDetector,
    bpm: number,
    seconds: number,
    start = 0,
): { states: BeatState[]; times: number[] } {
    const states: BeatState[] = []
    const times: number[] = []
    const interval = 60 / bpm
    for (let index = 0; index * frame < seconds; index++) {
        const time = start + index * frame
        const phase = (time - start) % interval
        const jitter = ((index * 7919) % 13) / 1000
        const energy = phase < 0.05 ? 0.9 : 0.2 + jitter
        const state = detector.update(energy, time)
        states.push(state)
        if (state.beat) {
            times.push(time)
        }
    }

    return { states, times }
}

describe('BeatDetector', () => {
    it('finds no beats in silence or a steady sound', () => {
        const silence = new BeatDetector()
        const steady = new BeatDetector()

        for (let index = 0; index < 240; index++) {
            expect(silence.update(0, index * frame).beat).toBe(false)
            expect(steady.update(0.5, index * frame).beat).toBe(false)
        }
    })

    it('takes a sudden rise of the sound as one beat', () => {
        const detector = new BeatDetector()
        let beats = 0

        for (let index = 0; index < 240; index++) {
            beats += detector.update(index < 120 ? 0.1 : 0.6, index * frame).beat ? 1 : 0
        }

        expect(beats).toBe(1)
    })

    it('finds no beats in faint noise', () => {
        const detector = new BeatDetector()
        let beats = 0
        for (let index = 0; index < 600; index++) {
            const noise = ((index * 7919) % 17) / 1000
            beats += detector.update(0.3 + noise, index * frame).beat ? 1 : 0
        }

        expect(beats).toBe(0)
    })

    it('finds the kicks of a steady tempo', () => {
        const detector = new BeatDetector()

        const { times } = kicks(detector, 120, 8)

        // The first kick comes before the detector has heard enough; every later one is found.
        expect(times.length).toBeGreaterThanOrEqual(15)
        expect(times.length).toBeLessThanOrEqual(16)
        for (let index = 1; index < times.length; index++) {
            expect(times[index]! - times[index - 1]!).toBeCloseTo(0.5, 1)
        }
    })

    it('estimates the tempo from the intervals between beats', () => {
        const detector = new BeatDetector()

        const { states } = kicks(detector, 128, 10)

        expect(states.at(-1)?.bpm).toBeGreaterThan(124)
        expect(states.at(-1)?.bpm).toBeLessThan(132)
    })

    it('has no tempo before a few beats', () => {
        const detector = new BeatDetector()

        const { states } = kicks(detector, 120, 1.2)

        expect(states.at(-1)?.bpm).toBeNull()
    })

    it('never reports two beats closer than the shortest interval', () => {
        const detector = new BeatDetector({ minIntervalSeconds: 0.3 })

        // Kicks at 400 beats per minute are too fast; the detector takes at most every 0.3 s.
        const { times } = kicks(detector, 400, 6)

        expect(times.length).toBeGreaterThan(0)
        for (let index = 1; index < times.length; index++) {
            expect(times[index]! - times[index - 1]!).toBeGreaterThanOrEqual(0.3 - 1e-9)
        }
    })

    it('lets the pulse of a beat fade out', () => {
        const detector = new BeatDetector()
        const { states } = kicks(detector, 60, 2.8)
        const beat = states.map((state) => state.beat).lastIndexOf(true)

        const pulse = states[beat]!.pulse
        expect(pulse).toBeGreaterThan(0.5)
        expect(pulse).toBe(states[beat]!.strength)
        expect(states[beat + 6]!.pulse).toBeLessThan(pulse)
        expect(states[beat + 30]!.pulse).toBeLessThan(0.15 * pulse)
        expect(states[beat + 30]!.sinceBeat).toBeCloseTo(0.5, 5)
    })

    it('reacts to softer music once its threshold has adapted', () => {
        const detector = new BeatDetector()
        kicks(detector, 120, 4)

        // The same rhythm, much quieter: kicks of 0.35 on a floor of 0.1.
        let beats = 0
        for (let index = 0; index < 360; index++) {
            const time = 4 + index * frame
            const energy = (time - 4) % 0.5 < 0.05 ? 0.35 : 0.1
            beats += detector.update(energy, time).beat ? 1 : 0
        }

        expect(beats).toBeGreaterThanOrEqual(10)
    })

    it('starts again when the time goes backwards', () => {
        const detector = new BeatDetector()
        kicks(detector, 120, 6)

        const state = detector.update(0.9, 0)

        expect(state.beat).toBe(false)
        expect(state.bpm).toBeNull()
        expect(state.sinceBeat).toBeNull()
    })

    it('forgets everything on reset', () => {
        const detector = new BeatDetector()
        kicks(detector, 120, 6)

        detector.reset()
        const state = detector.update(0.2, 6)

        expect(state).toEqual({ beat: false, strength: 0, pulse: 0, sinceBeat: null, bpm: null })
    })

    it('treats invalid energies as silence', () => {
        const detector = new BeatDetector()

        for (let index = 0; index < 30; index++) {
            const state = detector.update(index % 2 === 0 ? Number.NaN : -1, index * frame)
            expect(state.beat).toBe(false)
        }
    })
})
