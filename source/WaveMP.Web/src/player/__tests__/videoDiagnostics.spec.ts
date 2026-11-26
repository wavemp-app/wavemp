import { describe, expect, it } from 'vitest'
import {
    bufferedAheadOf,
    bufferedRangeAt,
    maxDiagnosticsEvents,
    stutterWindow,
    VideoDiagnosticsRecorder,
    type VideoSample,
} from '../videoDiagnostics'

function sample(extra: Partial<VideoSample> = {}): VideoSample {
    return {
        time: 0,
        audioPosition: 30,
        drift: 0.01,
        rate: 1,
        readyState: 4,
        videoPosition: 30,
        buffered: [[0, 40]],
        droppedFrames: 0,
        totalFrames: 0,
        action: 'rate',
        missedSeeks: 0,
        seekLatency: null,
        ...extra,
    }
}

describe('video diagnostics', () => {
    it('finds the loaded range at the position and the seconds loaded ahead', () => {
        const ranges: [number, number][] = [
            [0, 10],
            [20, 35],
        ]

        expect(bufferedRangeAt(ranges, 25)).toEqual({ start: 20, end: 35 })
        expect(bufferedRangeAt(ranges, 15)).toBeNull()
        expect(bufferedAheadOf(ranges, 25)).toBe(10)
        expect(bufferedAheadOf(ranges, 15)).toBe(0)
    })

    it('shows the values of the last check and counts from the start of the video', () => {
        const recorder = new VideoDiagnosticsRecorder()
        recorder.start('hls', 1000)
        recorder.sample(sample({ time: 1000, droppedFrames: 40, totalFrames: 400 }))
        recorder.sample(
            sample({
                time: 3000,
                drift: -0.2,
                rate: 1.08,
                readyState: 3,
                droppedFrames: 41,
                totalFrames: 460,
                missedSeeks: 1,
                seekLatency: 0.4,
            }),
        )

        expect(recorder.snapshot(3500)).toEqual(
            expect.objectContaining({
                delivery: 'hls',
                elapsed: 2.5,
                drift: -0.2,
                rate: 1.08,
                readyState: 3,
                buffered: { start: 0, end: 40 },
                bufferedAhead: 10,
                droppedFrames: 1,
                totalFrames: 60,
                missedSeeks: 1,
                seekLatency: 0.4,
                stuttering: false,
                stutters: 0,
            }),
        )
    })

    it('tells no dropped pictures where the browser does not count them', () => {
        const recorder = new VideoDiagnosticsRecorder()
        recorder.start('file', 0)
        recorder.sample(sample({ droppedFrames: null, totalFrames: null }))

        expect(recorder.snapshot(0).droppedFrames).toBeNull()
    })

    it('logs seeks and changes of the speed with the values of the last check', () => {
        const recorder = new VideoDiagnosticsRecorder()
        recorder.start('file', 0)
        recorder.sample(sample({ time: 500, drift: -0.5 }))
        recorder.record('seek', 600, 30.5)

        const snapshot = recorder.snapshot(700)
        expect(snapshot.seeks).toBe(1)
        expect(snapshot.events).toEqual([
            {
                at: 0.6,
                kind: 'seek',
                position: 30,
                drift: -0.5,
                rate: 1,
                readyState: 4,
                bufferedAhead: 10,
                action: 'rate',
                value: 30.5,
            },
        ])
    })

    it('records when a stutter starts and how long it lasted', () => {
        const recorder = new VideoDiagnosticsRecorder()
        recorder.start('hls', 0)
        recorder.sample(sample({ time: 0, droppedFrames: 0 }))
        // A single seek is no stutter.
        recorder.record('seek', 1000, 31)
        expect(recorder.snapshot(1000).stuttering).toBe(false)

        // Many dropped pictures soon after: the stutter starts.
        recorder.sample(sample({ time: 2000, droppedFrames: 10 }))
        expect(recorder.snapshot(2000).stuttering).toBe(true)
        recorder.record('waiting', 3000)
        recorder.sample(sample({ time: 4000, droppedFrames: 10 }))
        expect(recorder.snapshot(4000).stuttering).toBe(true)

        // Smooth for the whole window: the stutter ended.
        recorder.sample(sample({ time: 3000 + stutterWindow * 1000, droppedFrames: 11 }))
        const snapshot = recorder.snapshot(9000)
        expect(snapshot.stuttering).toBe(false)
        expect(snapshot.stutters).toBe(1)
        expect(snapshot.events.map((event) => [event.kind, event.value])).toEqual([
            ['seek', 31],
            ['stutter-start', 10],
            ['waiting', null],
            ['stutter-end', 1],
        ])
    })

    it('starts anew with every video', () => {
        const recorder = new VideoDiagnosticsRecorder()
        recorder.start('hls', 0)
        recorder.record('seek', 100, 1)
        recorder.record('seek', 200, 2)

        recorder.start('file', 1000)

        const snapshot = recorder.snapshot(1000)
        expect(snapshot.delivery).toBe('file')
        expect(snapshot.seeks).toBe(0)
        expect(snapshot.stutters).toBe(0)
        expect(snapshot.events).toEqual([])
    })

    it('keeps only the newest entries of the log', () => {
        const recorder = new VideoDiagnosticsRecorder()
        recorder.start('file', 0)
        for (let index = 0; index < maxDiagnosticsEvents + 10; index++) {
            recorder.record('rate', index, index)
        }

        const events = recorder.snapshot(0).events
        expect(events).toHaveLength(maxDiagnosticsEvents)
        expect(events[0]?.value).toBe(10)
    })
})
