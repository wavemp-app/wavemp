import { describe, expect, it } from 'vitest'
import { AudioFeatureTracker, silentFeatures } from '../features'

const frame = 1 / 60

/**
 * A spectrum in decibels (as getFloatFrequencyData writes it) of 1024 bins at 48 kHz with bass,
 * mids and highs at the given decibels; -Infinity is silence.
 */
function spectrum(bass: number, mid: number, treble: number): Float32Array {
    const bins = new Float32Array(1024)
    const width = 48000 / 2 / bins.length
    for (let index = 0; index < bins.length; index++) {
        const frequency = index * width
        bins[index] =
            frequency < 250 ? bass : frequency < 4000 ? mid : frequency < 16000 ? treble : -Infinity
    }

    return bins
}

const silence = spectrum(-Infinity, -Infinity, -Infinity)

describe('AudioFeatureTracker', () => {
    it('is silent for silence', () => {
        const tracker = new AudioFeatureTracker()

        let features = tracker.update(silence, 48000, 0)
        for (let index = 1; index < 60; index++) {
            features = tracker.update(silence, 48000, index * frame)
        }

        expect(features).toEqual(silentFeatures)
        expect(silentFeatures).toEqual({
            bass: 0,
            mid: 0,
            treble: 0,
            level: 0,
            quiet: 1,
            onset: 0,
            beat: false,
            pulse: 0,
            bpm: null,
        })
    })

    it('reads a steady sound as loud, however loud it is', () => {
        for (const decibels of [-20, -70]) {
            const tracker = new AudioFeatureTracker()

            let features = tracker.update(
                spectrum(decibels, decibels - 10, decibels - 20),
                48000,
                0,
            )
            for (let index = 1; index < 60; index++) {
                features = tracker.update(
                    spectrum(decibels, decibels - 10, decibels - 20),
                    48000,
                    index * frame,
                )
            }

            expect(features.bass).toBeCloseTo(1, 2)
            expect(features.mid).toBeCloseTo(1, 2)
            expect(features.treble).toBeCloseTo(1, 2)
            expect(features.level).toBeCloseTo(1, 2)
            expect(features.quiet).toBe(0)
        }
    })

    it('measures a band between the quiet moments and the hits of its recent past', () => {
        const tracker = new AudioFeatureTracker()
        const levels: number[] = []
        for (let index = 0; index < 240; index++) {
            const time = index * frame
            const hit = time % 0.5 < 0.1
            levels.push(tracker.update(spectrum(hit ? -20 : -44, -40, -50), 48000, time).bass)
        }

        // After two seconds: the hits reach the top, the gaps between them the bottom.
        const last = levels.slice(120)
        expect(Math.max(...last)).toBeGreaterThan(0.95)
        expect(Math.min(...last)).toBeLessThan(0.1)
    })

    it('keeps near silence silent', () => {
        const tracker = new AudioFeatureTracker()

        let features = tracker.update(spectrum(-130, -130, -130), 48000, 0)
        for (let index = 1; index < 60; index++) {
            features = tracker.update(spectrum(-130, -130, -130), 48000, index * frame)
        }

        expect(features.bass).toBe(0)
        expect(features.level).toBe(0)
        expect(features.quiet).toBe(1)
    })

    it('follows a rise within a frame or two and a fall more slowly', () => {
        const tracker = new AudioFeatureTracker()
        tracker.update(spectrum(-20, -160, -160), 48000, 0)
        for (let index = 1; index < 30; index++) {
            tracker.update(silence, 48000, index * frame)
        }

        // A rise: more than half of the way in one frame, there after a tenth of a second.
        const first = tracker.update(spectrum(-20, -160, -160), 48000, 30 * frame).bass
        let risen = first
        for (let index = 31; index < 37; index++) {
            risen = tracker.update(spectrum(-20, -160, -160), 48000, index * frame).bass
        }

        // A fall: hardly lower after one frame.
        const fallen = tracker.update(silence, 48000, 37 * frame).bass

        expect(first).toBeGreaterThan(0.5)
        expect(first).toBeLessThan(0.7)
        expect(risen).toBeGreaterThan(0.95)
        expect(fallen).toBeGreaterThan(0.85)
        expect(fallen).toBeLessThan(risen)
    })

    it('finds the beats of the bass at their onsets', () => {
        const tracker = new AudioFeatureTracker()
        let beats = 0
        let last = tracker.update(spectrum(-50, -40, -50), 48000, 0)
        for (let index = 1; index < 480; index++) {
            const time = index * frame
            const kick = time % 0.5 < 0.05
            last = tracker.update(spectrum(kick ? -15 : -50, -40, -50), 48000, time)
            beats += last.beat ? 1 : 0
        }

        expect(beats).toBeGreaterThanOrEqual(14)
        expect(last.bpm).toBeCloseTo(120, -1)
    })

    it('finds no beats in a held bass note', () => {
        const tracker = new AudioFeatureTracker()
        let beats = 0
        for (let index = 0; index < 480; index++) {
            // A loud bass note that starts after a second and then holds, with a slight wobble.
            const time = index * frame
            const bass = time < 1 ? -60 : -15 + Math.sin(time * 6) * 0.5
            beats += tracker.update(spectrum(bass, -40, -50), 48000, time).beat ? 1 : 0
        }

        expect(beats).toBeLessThanOrEqual(1)
    })

    it('notices a quiet passage and calms its level', () => {
        const tracker = new AudioFeatureTracker()
        let loud = tracker.update(spectrum(-20, -25, -35), 48000, 0)
        for (let index = 1; index < 600; index++) {
            loud = tracker.update(spectrum(-20, -25, -35), 48000, index * frame)
        }

        // 15 decibels quieter for three seconds.
        let quiet = loud
        for (let index = 600; index < 780; index++) {
            quiet = tracker.update(spectrum(-35, -40, -50), 48000, index * frame)
        }

        expect(loud.quiet).toBe(0)
        expect(loud.level).toBeCloseTo(1, 2)
        expect(quiet.quiet).toBeGreaterThan(0.9)
        expect(quiet.level).toBeLessThan(0.3)
    })

    it('starts again when the time goes backwards', () => {
        const tracker = new AudioFeatureTracker()
        for (let index = 0; index < 60; index++) {
            tracker.update(spectrum(-10, -10, -10), 48000, index * frame)
        }

        const features = tracker.update(silence, 48000, 0)

        expect(features.bass).toBe(0)
        expect(features.level).toBe(0)
    })
})
