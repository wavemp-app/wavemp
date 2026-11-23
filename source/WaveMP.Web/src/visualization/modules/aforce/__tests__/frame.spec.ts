import { describe, expect, it } from 'vitest'
import { silentFeatures, type AudioFeatures } from '../../../analysis/features'
import { aforceFrameInputs, type AForceFrameInputs } from '../frame'
import { aforceModes, modeLook, type AForceMode } from '../modes'

// The reaction of A-Force measured on the numbers that become the uniforms of its shaders: every
// mode has to react clearly to bass, mids, highs, beats and quiet passages. "Clearly" is a change
// of at least a quarter of the larger value (relativeChange), from a calm moment of music.

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
    mode: AForceMode,
    change: Partial<AudioFeatures> = {},
    intensity: 'normal' | 'reduced' = 'normal',
): AForceFrameInputs {
    return aforceFrameInputs(modeLook(mode), { ...calm, ...change }, intensity)
}

/** The number of a mode that shows its main shape reacting to the bass. */
const bassShape: Record<string, keyof AForceFrameInputs> = {
    flow: 'waveAmplitude',
    rings: 'ringSwell',
    warp: 'flowSpeed',
    ridges: 'terrainLift',
    helix: 'helixScale',
}

/** The number of a mode that shows its main shape reacting to a beat. */
const beatShape: Record<string, keyof AForceFrameInputs> = {
    flow: 'waveAmplitude',
    rings: 'ringSwell',
    warp: 'streakLength',
    ridges: 'terrainFront',
    helix: 'helixTurns',
}

function value(frame: AForceFrameInputs, key: keyof AForceFrameInputs): number {
    return frame[key] as number
}

describe.each(aforceModes.map((mode) => [mode.id, mode] as const))(
    'the reaction of the mode %s',
    (id, mode) => {
        const rest = inputs(mode)

        it('gains mass and speed with the bass', () => {
            const loud = inputs(mode, { bass: 1 })
            const none = inputs(mode, { bass: 0 })

            expect(relativeChange(none.mass, loud.mass)).toBeGreaterThanOrEqual(clearly)
            expect(relativeChange(none.flowSpeed, loud.flowSpeed)).toBeGreaterThanOrEqual(clearly)
            expect(
                relativeChange(value(none, bassShape[id]!), value(loud, bassShape[id]!)),
            ).toBeGreaterThanOrEqual(clearly)
            expect(loud.glow).toBeGreaterThan(none.glow)
        })

        it('sways and twists with the mids', () => {
            const loud = inputs(mode, { mid: 1 })
            const none = inputs(mode, { mid: 0 })

            expect(relativeChange(none.sway, loud.sway)).toBeGreaterThanOrEqual(clearly)
            expect(relativeChange(none.spin, loud.spin)).toBeGreaterThanOrEqual(clearly)
            expect(relativeChange(none.waveSwing, loud.waveSwing)).toBeGreaterThanOrEqual(clearly)
        })

        it('sparkles with the highs', () => {
            const loud = inputs(mode, { treble: 1 })
            const none = inputs(mode, { treble: 0 })

            expect(none.sparkle).toBe(0)
            expect(loud.sparkle).toBeGreaterThanOrEqual(1)
            expect(relativeChange(none.waveThickness, loud.waveThickness)).toBeGreaterThanOrEqual(
                clearly,
            )
        })

        it('punches on a beat and glows after it as the pulse fades', () => {
            const hit = inputs(mode, { beat: true, pulse: 1 })
            const after = inputs(mode, { pulse: 0.4 })

            expect(rest.punch).toBe(0)
            expect(rest.beat).toBe(0)
            expect(hit.punch).toBeGreaterThanOrEqual(0.5)
            expect(hit.beat).toBe(1)
            expect(
                relativeChange(value(rest, beatShape[id]!), value(hit, beatShape[id]!)),
            ).toBeGreaterThanOrEqual(clearly)
            // The afterglow: the punch follows the fading pulse down.
            expect(after.punch).toBeCloseTo(hit.punch * 0.4)
            expect(after.beat).toBeCloseTo(0.4)
        })

        it('slows down, dims and widens its trails in quiet passages', () => {
            const quiet = inputs(mode, { quiet: 1, level: 0.3 })

            expect(relativeChange(rest.flowSpeed, quiet.flowSpeed)).toBeGreaterThanOrEqual(clearly)
            expect(quiet.flowSpeed).toBeLessThan(rest.flowSpeed)
            expect(relativeChange(rest.brightness, quiet.brightness)).toBeGreaterThanOrEqual(
                clearly,
            )
            expect(quiet.brightness).toBeLessThan(rest.brightness)
            expect(quiet.spin).toBeLessThan(rest.spin)
            expect(quiet.trail).toBeGreaterThan(rest.trail)
            expect(quiet.trail).toBeLessThan(1)
        })

        it('halves its gains and does not punch at the reduced intensity', () => {
            const loud = { bass: 1, mid: 1, treble: 1, pulse: 1, beat: true }
            const normal = inputs(mode, loud)
            const reduced = inputs(mode, loud, 'reduced')

            expect(reduced.mass - 1).toBeCloseTo((normal.mass - 1) / 2)
            expect(reduced.sway).toBeCloseTo(normal.sway / 2)
            expect(reduced.sparkle).toBeCloseTo(normal.sparkle / 2)
            expect(reduced.beat).toBeCloseTo(normal.beat / 2)
            expect(reduced.punch).toBe(0)
            expect(reduced.flowSpeed).toBeLessThan(normal.flowSpeed)
            expect(reduced.cameraOrbit).toBeCloseTo(normal.cameraOrbit / 2)
            // It still reacts.
            expect(reduced.mass).toBeGreaterThan(inputs(mode, {}, 'reduced').mass)
        })

        it('stays calm and finite in silence', () => {
            const silent = aforceFrameInputs(modeLook(mode), silentFeatures, 'normal')

            for (const [key, number] of Object.entries(silent)) {
                if (key !== 'layers') {
                    expect(Number.isFinite(number), key).toBe(true)
                }
            }

            expect(silent.mass).toBe(1)
            expect(silent.punch).toBe(0)
            expect(silent.sparkle).toBe(0)
            expect(silent.flowSpeed).toBeGreaterThan(0)
            expect(silent.flowSpeed).toBeLessThan(rest.flowSpeed)
            expect(silent.brightness).toBeLessThan(rest.brightness)
        })

        it('hands the layers of the mode to the renderer', () => {
            expect(rest.layers).toEqual(mode.layers)
        })
    },
)

describe('aforceFrameInputs', () => {
    it('clamps features out of range', () => {
        const mode = aforceModes[0]!
        const wild = aforceFrameInputs(
            modeLook(mode),
            { ...calm, bass: 5, mid: -1, treble: Number.NaN, pulse: 3, quiet: 2 },
            'normal',
        )
        const limited = aforceFrameInputs(
            modeLook(mode),
            { ...calm, bass: 1, mid: 0, treble: 0, pulse: 1, quiet: 1 },
            'normal',
        )

        expect(wild).toEqual(limited)
    })
})
