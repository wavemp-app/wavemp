import { beforeAll, describe, expect, it } from 'vitest'
import { bassBand } from '../bands'
import { AudioFeatureTracker, type AudioFeatureOptions, type AudioFeatures } from '../features'
import { TestAnalyser } from './analyser'
import {
    beatSeconds,
    bridgeEnd,
    grooveEnd,
    grooves,
    makeMusic,
    musicSampleRate,
    type Music,
} from './music'

// The features of a piece of synthetic music (kick, snare, hi-hats, bass line, a quiet bridge)
// heard through an analyser like the one of the browser at 60 frames per second: what the
// visualizations react to must follow the drums, the loudness and the quiet bridge, at any volume.

const frameSeconds = 1 / 60

interface Frame {
    readonly time: number
    readonly features: AudioFeatures
    /** Share of the bins of the bass at the top of the byte spectrum (255). */
    readonly saturatedBass: number
}

function listen(
    music: Music,
    gain = 1,
    smoothing = 0.5,
    options: AudioFeatureOptions = {},
): Frame[] {
    const analyser = new TestAnalyser(2048, smoothing)
    const decibels = new Float32Array(analyser.frequencyBinCount)
    const bytes = new Uint8Array(analyser.frequencyBinCount)
    const tracker = new AudioFeatureTracker(options)
    const binWidth = musicSampleRate / 2 / analyser.frequencyBinCount
    const firstBass = Math.floor(bassBand.low / binWidth)
    const endBass = Math.ceil(bassBand.high / binWidth)
    const frames: Frame[] = []
    const seconds = music.samples.length / musicSampleRate
    for (let frame = 0; frame * frameSeconds < seconds; frame++) {
        const time = frame * frameSeconds
        analyser.analyse(music.samples, Math.round(time * musicSampleRate), gain)
        analyser.getFloatFrequencyData(decibels)
        analyser.getByteFrequencyData(bytes)
        let saturated = 0
        for (let bin = firstBass; bin < endBass; bin++) {
            saturated += bytes[bin] === 255 ? 1 : 0
        }

        frames.push({
            time,
            features: tracker.update(decibels, musicSampleRate, time),
            saturatedBass: saturated / (endBass - firstBass),
        })
    }

    return frames
}

function between(frames: readonly Frame[], from: number, to: number): Frame[] {
    return frames.filter((frame) => frame.time >= from && frame.time < to)
}

function mean(values: readonly number[]): number {
    return values.reduce((sum, value) => sum + value, 0) / Math.max(1, values.length)
}

function median(values: readonly number[]): number {
    const sorted = [...values].sort((a, b) => a - b)
    return sorted[Math.floor(sorted.length / 2)] ?? 0
}

type Level = 'bass' | 'mid' | 'treble'

/**
 * The mean swing of a level between hits and the gaps before them: the highest value shortly
 * after each hit minus the lowest value shortly before it. Hits of the first two seconds, while
 * the analysis learns the music, do not count.
 */
function swing(
    frames: readonly Frame[],
    hits: readonly number[],
    level: Level,
    gapSeconds: number,
): number {
    const swings = hits
        .filter((hit) => hit >= 2 && grooves(hit - gapSeconds))
        .map((hit) => {
            const after = between(frames, hit, hit + 0.12).map((frame) => frame.features[level])
            const before = between(frames, hit - gapSeconds, hit).map(
                (frame) => frame.features[level],
            )
            return Math.max(...after) - Math.min(...before)
        })
    return mean(swings)
}

describe('the features of music', () => {
    let music: Music
    let frames: Frame[]

    beforeAll(() => {
        music = makeMusic()
        frames = listen(music)
    })

    it('finds the kicks as beats and none in the quiet bridge', () => {
        const beats = frames.filter((frame) => frame.features.beat).map((frame) => frame.time)
        const counted = music.kicks.filter((kick) => kick >= 1)
        const found = counted.filter((kick) =>
            beats.some((beat) => beat >= kick && beat < kick + 0.1),
        )

        expect(found.length / counted.length).toBeGreaterThanOrEqual(0.9)
        expect(beats.filter((beat) => beat >= grooveEnd && beat < bridgeEnd)).toEqual([])
        // Every beat belongs to a kick: the snares and the bass line between them are no beats.
        expect(
            beats.every((beat) => music.kicks.some((kick) => beat >= kick && beat < kick + 0.1)),
        ).toBe(true)
        expect(frames.at(-1)!.features.bpm).toBeCloseTo(60 / beatSeconds, -1)
    })

    it('lets bass, mids and highs swing between the hits and the gaps', () => {
        // Kicks for the bass, snares for the mids, hi-hats for the highs.
        expect(swing(frames, music.kicks, 'bass', 0.12)).toBeGreaterThanOrEqual(0.5)
        expect(swing(frames, music.snares, 'mid', 0.12)).toBeGreaterThanOrEqual(0.5)
        expect(swing(frames, music.hats, 'treble', 0.1)).toBeGreaterThanOrEqual(0.5)
    })

    it('tells the loud groove from the quiet bridge', () => {
        const groove = between(frames, 3, grooveEnd)
        const bridge = between(frames, grooveEnd + 1.5, bridgeEnd)

        expect(mean(groove.map((frame) => frame.features.quiet))).toBeLessThanOrEqual(0.1)
        expect(mean(bridge.map((frame) => frame.features.quiet))).toBeGreaterThanOrEqual(0.8)
        expect(mean(groove.map((frame) => frame.features.level))).toBeGreaterThanOrEqual(0.7)
        expect(mean(bridge.map((frame) => frame.features.level))).toBeLessThanOrEqual(0.3)
        expect(Math.max(...bridge.map((frame) => frame.features.pulse))).toBeLessThan(0.1)
    })

    it('reacts the same at a volume 20 dB lower', () => {
        const quiet = listen(music, 0.1)
        const keys = ['bass', 'mid', 'treble', 'level', 'quiet'] as const

        for (const key of keys) {
            const differences = frames
                .slice(120)
                .map((frame, index) =>
                    Math.abs(frame.features[key] - quiet[index + 120]!.features[key]),
                )
            expect(median(differences), key).toBeLessThanOrEqual(0.1)
        }

        expect(quiet.filter((frame) => frame.features.beat)).toHaveLength(
            frames.filter((frame) => frame.features.beat).length,
        )
    })

    it('still swings with music so loud that the byte spectrum saturates', () => {
        const loud = listen(music, 4)
        const kickFrames = music.kicks
            .filter((kick) => kick >= 1)
            .map((kick) => between(loud, kick + 0.03, kick + 0.06)[0]!)

        // The case of the old byte spectrum: most bins of the bass stuck at 255 on every kick.
        expect(mean(kickFrames.map((frame) => frame.saturatedBass))).toBeGreaterThan(0.5)
        expect(swing(loud, music.kicks, 'bass', 0.12)).toBeGreaterThanOrEqual(0.5)
    })
})
