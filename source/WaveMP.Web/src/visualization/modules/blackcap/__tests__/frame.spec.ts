import { describe, expect, it } from 'vitest'
import { silentFeatures, type AudioFeatures } from '../../../analysis/features'
import { blackcapFrameInputs, type BlackCapFrameInputs } from '../frame'
import { blackcapModes, modeLook, type BlackCapMode } from '../modes'

// The reaction of BlackCap measured on the numbers that become the uniforms of its shaders and the
// placement of its shapes: every mode has to react clearly to bass, mids, highs, beats, loudness
// and quiet passages. "Clearly" is a change of at least a quarter of the larger value
// (relativeChange), from a calm moment of music. BlackCap used to be a static bubble: its surface
// lift was 0.22 of a spectrum that hardly moved and a beat swelled it by 6 %.

/** A calm moment of music: some of everything, loud enough, no beat. */
const calm: AudioFeatures = {
    bass: 0.2,
    mid: 0.2,
    treble: 0.2,
    level: 0.7,
    quiet: 0,
    onset: 0,
    beat: false,
    pulse: 0,
    bpm: 120,
}

const clearly = 0.25

function relativeChange(a: number, b: number): number {
    const larger = Math.max(Math.abs(a), Math.abs(b))
    return larger === 0 ? 0 : Math.abs(b - a) / larger
}

function inputs(
    mode: BlackCapMode,
    change: Partial<AudioFeatures> = {},
    intensity: 'normal' | 'reduced' = 'normal',
): BlackCapFrameInputs {
    return blackcapFrameInputs(modeLook(mode), { ...calm, ...change }, intensity)
}

/** Size of the lobes: how far the surface is shaped by them. */
function lobeSize(frame: BlackCapFrameInputs): number {
    return frame.lobes.reduce((sum, lobe) => sum + Math.abs(lobe), 0)
}

/** The number of a mode that shows its main form reacting to the loudness. */
const loudnessShape: Record<string, (frame: BlackCapFrameInputs) => number> = {
    drift: (frame) => frame.flowSpeed,
    orbit: (frame) => frame.orbitScale,
    cluster: (frame) => frame.orbitScale,
    ribbon: (frame) => frame.flowSpeed,
    bloom: lobeSize,
}

describe.each(blackcapModes.map((mode) => [mode.id, mode] as const))(
    'the reaction of the mode %s',
    (id, mode) => {
        const rest = inputs(mode)

        it('swells from the bottom, grows and glows with the bass', () => {
            const loud = inputs(mode, { bass: 1 })
            const none = inputs(mode, { bass: 0 })

            expect(none.bassLift).toBe(0)
            expect(loud.bassLift).toBeGreaterThanOrEqual(0.3)
            expect(loud.size).toBeGreaterThan(none.size * 1.1)
            expect(relativeChange(none.glow, loud.glow)).toBeGreaterThanOrEqual(clearly)
        })

        it('lifts the surfaces with the spectrum far more than it used to', () => {
            expect(rest.surfaceLift).toBeGreaterThanOrEqual(0.45)
        })

        it('twists and lights its edges with the mids', () => {
            const loud = inputs(mode, { mid: 1 })
            const none = inputs(mode, { mid: 0 })

            expect(relativeChange(none.lobeSpeed, loud.lobeSpeed)).toBeGreaterThanOrEqual(clearly)
            expect(relativeChange(none.rim, loud.rim)).toBeGreaterThanOrEqual(clearly)
            expect(relativeChange(none.lights, loud.lights)).toBeGreaterThanOrEqual(clearly)
        })

        it('ripples and glints with the highs', () => {
            const loud = inputs(mode, { treble: 1 })
            const none = inputs(mode, { treble: 0 })

            expect(none.ripple).toBe(0)
            expect(loud.ripple).toBeGreaterThan(0.04)
            expect(relativeChange(none.glint, loud.glint)).toBeGreaterThanOrEqual(clearly)
            expect(loud.waves).toBeGreaterThan(none.waves)
        })

        it('swells softly and lights its edges on a beat, fading with the pulse', () => {
            const hit = inputs(mode, { beat: true, pulse: 1 })
            const after = inputs(mode, { pulse: 0.4 })

            expect(rest.swell).toBe(0)
            expect(hit.swell).toBeGreaterThanOrEqual(0.1)
            // Soft: the shapes never jump by more than a fifth of their size.
            expect(hit.swell).toBeLessThanOrEqual(0.2)
            expect(relativeChange(rest.rim, hit.rim)).toBeGreaterThanOrEqual(clearly)
            expect(hit.pulse).toBe(1)
            expect(after.swell).toBeCloseTo(hit.swell * 0.4)
        })

        it('shows the loudness in its form', () => {
            const loud = inputs(mode, { level: 1 })
            const soft = inputs(mode, { level: 0 })
            const shape = loudnessShape[id]!

            expect(relativeChange(shape(soft), shape(loud))).toBeGreaterThanOrEqual(clearly)
            expect(shape(loud)).toBeGreaterThan(shape(soft))
        })

        it('contracts, slows down and dims in quiet passages', () => {
            const quiet = inputs(mode, { quiet: 1, level: 0.3 })

            expect(quiet.size).toBeLessThan(rest.size)
            expect(quiet.orbitScale).toBeLessThan(rest.orbitScale)
            expect(relativeChange(rest.motionSpeed, quiet.motionSpeed)).toBeGreaterThanOrEqual(
                clearly,
            )
            expect(relativeChange(rest.flowSpeed, quiet.flowSpeed)).toBeGreaterThanOrEqual(clearly)
            expect(relativeChange(rest.brightness, quiet.brightness)).toBeGreaterThanOrEqual(
                clearly,
            )
            expect(quiet.brightness).toBeLessThan(rest.brightness)
            expect(quiet.lobeSpeed).toBeLessThan(rest.lobeSpeed)
        })

        it('halves its gains and moves more gently at the reduced intensity', () => {
            const loud = { bass: 1, mid: 1, treble: 1, pulse: 1, beat: true }
            const normal = inputs(mode, loud)
            const reduced = inputs(mode, loud, 'reduced')

            expect(reduced.bassLift).toBeCloseTo(normal.bassLift / 2)
            expect(reduced.swell).toBeCloseTo(normal.swell / 2)
            expect(reduced.ripple).toBeCloseTo(normal.ripple / 2)
            expect(reduced.size - 1).toBeCloseTo((normal.size - 1) / 2)
            expect(reduced.drift).toBeCloseTo(normal.drift / 2)
            expect(reduced.breath).toBeCloseTo(normal.breath / 2)
            expect(reduced.lobeSpeed).toBeLessThan(normal.lobeSpeed)
            expect(reduced.motionSpeed).toBeLessThan(normal.motionSpeed)
            // It still reacts.
            expect(reduced.bassLift).toBeGreaterThan(inputs(mode, {}, 'reduced').bassLift)
        })

        it('stays calm and finite in silence', () => {
            const silent = blackcapFrameInputs(modeLook(mode), silentFeatures, 'normal')

            for (const [key, number] of Object.entries(silent)) {
                if (key !== 'lobes') {
                    expect(Number.isFinite(number), key).toBe(true)
                }
            }

            expect(silent.bassLift).toBe(0)
            expect(silent.swell).toBe(0)
            expect(silent.ripple).toBe(0)
            expect(silent.motionSpeed).toBeGreaterThan(0)
            expect(silent.motionSpeed).toBeLessThan(rest.motionSpeed)
            expect(silent.brightness).toBeLessThan(rest.brightness)
        })
    },
)

describe('blackcapFrameInputs', () => {
    it('clamps features out of range', () => {
        const mode = blackcapModes[0]!
        const wild = blackcapFrameInputs(
            modeLook(mode),
            { ...calm, bass: 5, mid: -1, treble: Number.NaN, pulse: 3, quiet: 2, level: 4 },
            'normal',
        )
        const limited = blackcapFrameInputs(
            modeLook(mode),
            { ...calm, bass: 1, mid: 0, treble: 0, pulse: 1, quiet: 1, level: 1 },
            'normal',
        )

        expect(wild).toEqual(limited)
    })

    it('never lets the companions fall into the middle', () => {
        for (const mode of blackcapModes) {
            const quietest = blackcapFrameInputs(
                modeLook(mode),
                { ...silentFeatures, quiet: 1 },
                'normal',
            )
            expect(quietest.orbitScale).toBeGreaterThanOrEqual(0.4)
        }
    })
})
