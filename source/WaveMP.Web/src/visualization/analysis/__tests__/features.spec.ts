import { describe, expect, it } from 'vitest'
import { AudioFeatureTracker } from '../features'

const frame = 1 / 60

/** A spectrum of 1024 bins at 48 kHz with bass, mids and highs at the given bytes. */
function spectrum(bass: number, mid: number, treble: number): Uint8Array {
    const bins = new Uint8Array(1024)
    const width = 48000 / 2 / bins.length
    for (let index = 0; index < bins.length; index++) {
        const frequency = index * width
        bins[index] =
            frequency < 250 ? bass : frequency < 4000 ? mid : frequency < 16000 ? treble : 0
    }

    return bins
}

describe('AudioFeatureTracker', () => {
    it('is silent for silence', () => {
        const tracker = new AudioFeatureTracker()

        let features = tracker.update(spectrum(0, 0, 0), 48000, 0)
        for (let index = 1; index < 60; index++) {
            features = tracker.update(spectrum(0, 0, 0), 48000, index * frame)
        }

        expect(features).toEqual({
            bass: 0,
            mid: 0,
            treble: 0,
            level: 0,
            beat: false,
            pulse: 0,
            bpm: null,
        })
    })

    it('measures each band against its own recent peak', () => {
        const tracker = new AudioFeatureTracker()

        let features = tracker.update(spectrum(200, 100, 60), 48000, 0)
        for (let index = 1; index < 60; index++) {
            features = tracker.update(spectrum(200, 100, 60), 48000, index * frame)
        }

        // Steady sound: every band is at its peak, however loud it is.
        expect(features.bass).toBeCloseTo(1, 2)
        expect(features.mid).toBeCloseTo(1, 2)
        expect(features.treble).toBeCloseTo(1, 2)
        expect(features.level).toBeCloseTo((200 + 100 + 60) / 3 / 255, 2)
    })

    it('keeps near silence low', () => {
        const tracker = new AudioFeatureTracker()

        let features = tracker.update(spectrum(10, 10, 10), 48000, 0)
        for (let index = 1; index < 60; index++) {
            features = tracker.update(spectrum(10, 10, 10), 48000, index * frame)
        }

        expect(features.bass).toBeLessThan(0.25)
    })

    it('follows a rise fast and a fall slowly', () => {
        const tracker = new AudioFeatureTracker()
        tracker.update(spectrum(255, 0, 0), 48000, 0)
        for (let index = 1; index < 30; index++) {
            tracker.update(spectrum(0, 0, 0), 48000, index * frame)
        }

        // A rise: a third of the way in one frame, almost there after a tenth of a second.
        const first = tracker.update(spectrum(255, 0, 0), 48000, 30 * frame).bass
        let risen = first
        for (let index = 31; index < 37; index++) {
            risen = tracker.update(spectrum(255, 0, 0), 48000, index * frame).bass
        }

        // A fall: hardly lower after one frame.
        const fallen = tracker.update(spectrum(0, 0, 0), 48000, 37 * frame).bass

        expect(first).toBeGreaterThan(0.3)
        expect(first).toBeLessThan(0.5)
        expect(risen).toBeGreaterThan(0.9)
        expect(fallen).toBeGreaterThan(0.85)
        expect(fallen).toBeLessThan(risen)
    })

    it('finds the beats of the bass', () => {
        const tracker = new AudioFeatureTracker()
        let beats = 0
        let last = tracker.update(spectrum(40, 80, 40), 48000, 0)
        for (let index = 1; index < 480; index++) {
            const time = index * frame
            const kick = time % 0.5 < 0.05
            last = tracker.update(spectrum(kick ? 240 : 40, 80, 40), 48000, time)
            beats += last.beat ? 1 : 0
        }

        expect(beats).toBeGreaterThanOrEqual(14)
        expect(last.bpm).toBeCloseTo(120, -1)
    })

    it('starts again when the time goes backwards', () => {
        const tracker = new AudioFeatureTracker()
        for (let index = 0; index < 60; index++) {
            tracker.update(spectrum(255, 255, 255), 48000, index * frame)
        }

        const features = tracker.update(spectrum(0, 0, 0), 48000, 0)

        expect(features.bass).toBe(0)
        expect(features.level).toBe(0)
    })
})
