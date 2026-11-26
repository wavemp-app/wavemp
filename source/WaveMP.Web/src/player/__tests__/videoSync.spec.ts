import { describe, expect, it } from 'vitest'
import {
    endMargin,
    hardSyncThreshold,
    hlsHardSyncThreshold,
    hlsMinSeekInterval,
    jumpThreshold,
    maxRateCorrection,
    maxSeekInterval,
    maxSeekLead,
    minSeekInterval,
    missedSeeksAfter,
    nextSeekLatency,
    rateFor,
    rateStartThreshold,
    seekIntervalOf,
    softSyncThreshold,
    stageViewOf,
    syncAction,
    videoTarget,
    type SyncInput,
} from '../videoSync'

function input(extra: Partial<SyncInput> = {}): SyncInput {
    return {
        audioPosition: 30,
        playing: true,
        videoPosition: 30,
        offset: 0,
        videoDuration: 200,
        sinceLastSeek: 10,
        ...extra,
    }
}

describe('video sync', () => {
    it('adds the offset to the position of the audio', () => {
        expect(videoTarget(30, 1.5)).toBe(31.5)
        expect(videoTarget(30, -2)).toBe(28)
    })

    it('leaves a video in sync at its normal speed', () => {
        expect(syncAction(input({ videoPosition: 30 + softSyncThreshold / 2 }))).toEqual({
            kind: 'rate',
            rate: 1,
        })
    })

    it('holds the first picture until the music of the video starts', () => {
        expect(syncAction(input({ audioPosition: 1, offset: -3, videoPosition: 0 }))).toEqual({
            kind: 'hold',
            at: 0,
        })
    })

    it('holds the last picture once the video ended before the audio', () => {
        expect(syncAction(input({ audioPosition: 199, offset: 2 }))).toEqual({
            kind: 'hold',
            at: 200 - endMargin,
        })
    })

    it('seeks from the hard threshold on, with the offset', () => {
        expect(syncAction(input({ videoPosition: 30, offset: hardSyncThreshold + 0.1 }))).toEqual({
            kind: 'seek',
            to: 30 + (hardSyncThreshold + 0.1),
        })
        expect(syncAction(input({ audioPosition: 40, videoPosition: 30, offset: 1.5 }))).toEqual({
            kind: 'seek',
            to: 41.5,
        })
    })

    it('does not seek again within the minimum interval but changes the speed', () => {
        const action = syncAction(input({ videoPosition: 32, sinceLastSeek: minSeekInterval / 2 }))

        expect(action).toEqual({ kind: 'rate', rate: 1 - maxRateCorrection })
    })

    it('slows down a video that is a little ahead and speeds up one that is behind', () => {
        const ahead = syncAction(input({ videoPosition: 30.1 }))
        const behind = syncAction(input({ videoPosition: 29.9 }))

        expect(ahead.kind).toBe('rate')
        expect(ahead.kind === 'rate' && ahead.rate).toBeCloseTo(0.95)
        expect(behind.kind === 'rate' && behind.rate).toBeCloseTo(1.05)
    })

    it('limits the change of the speed', () => {
        const action = syncAction(input({ videoPosition: 29.75 }))

        expect(action).toEqual({ kind: 'rate', rate: 1 + maxRateCorrection })
    })

    it('seeks a paused video to the exact position', () => {
        expect(syncAction(input({ playing: false, videoPosition: 30.1 }))).toEqual({
            kind: 'seek',
            to: 30,
        })
    })

    it('ignores the end while the duration of the video is unknown', () => {
        expect(
            syncAction(input({ videoDuration: 0, audioPosition: 500, videoPosition: 500 })),
        ).toEqual({ kind: 'rate', rate: 1 })
    })

    it('does not seek a playing video while it waits for data or for its last seek', () => {
        expect(syncAction(input({ videoPosition: 25, buffering: true }))).toEqual({ kind: 'wait' })
        expect(syncAction(input({ videoPosition: 25, settling: true }))).toEqual({ kind: 'wait' })
    })

    it('seeks once the video can play again', () => {
        expect(syncAction(input({ videoPosition: 25, buffering: false }))).toEqual({
            kind: 'seek',
            to: 30,
        })
    })

    it('aims a seek ahead by the time seeks took, but not beyond the end', () => {
        expect(syncAction(input({ videoPosition: 25, seekLatency: 0.6 }))).toEqual({
            kind: 'seek',
            to: 30.6,
        })
        expect(syncAction(input({ videoPosition: 25, seekLatency: 10 }))).toEqual({
            kind: 'seek',
            to: 30 + maxSeekLead,
        })
        expect(
            syncAction(input({ audioPosition: 199.5, videoPosition: 190, seekLatency: 1 })),
        ).toEqual({ kind: 'seek', to: 200 - endMargin })
        // A paused video shows the exact picture.
        expect(syncAction(input({ playing: false, videoPosition: 25, seekLatency: 1 }))).toEqual({
            kind: 'seek',
            to: 30,
        })
    })

    it('puts up with a larger difference and seeks more rarely in an HLS stream', () => {
        const drift = (hardSyncThreshold + hlsHardSyncThreshold) / 2
        expect(syncAction(input({ videoPosition: 30 + drift })).kind).toBe('seek')
        expect(syncAction(input({ videoPosition: 30 + drift, hls: true })).kind).toBe('rate')

        const late = input({ videoPosition: 31, hls: true, sinceLastSeek: minSeekInterval + 0.5 })
        expect(syncAction(late)).toEqual({ kind: 'rate', rate: 1 - maxRateCorrection })
        expect(syncAction({ ...late, sinceLastSeek: hlsMinSeekInterval }).kind).toBe('seek')
    })

    it('waits longer after each seek that missed, but follows a jump of the audio at once', () => {
        expect(seekIntervalOf(false, 0)).toBe(minSeekInterval)
        expect(seekIntervalOf(false, 1)).toBe(minSeekInterval * 2)
        expect(seekIntervalOf(false, 3)).toBe(minSeekInterval * 8)
        expect(seekIntervalOf(false, 20)).toBe(maxSeekInterval)
        expect(seekIntervalOf(true, 1)).toBe(hlsMinSeekInterval * 2)
        expect(seekIntervalOf(false, 3, jumpThreshold + 1)).toBe(minSeekInterval)

        const missed = input({ videoPosition: 29, missedSeeks: 2, sinceLastSeek: 3 })
        expect(syncAction(missed).kind).toBe('rate')
        expect(syncAction({ ...missed, sinceLastSeek: 4 }).kind).toBe('seek')
        expect(syncAction({ ...missed, videoPosition: 20 }).kind).toBe('seek')
    })

    it('counts the seeks in a row that missed', () => {
        expect(missedSeeksAfter(0, hardSyncThreshold + 0.1, false)).toBe(1)
        expect(missedSeeksAfter(2, hardSyncThreshold + 0.1, false)).toBe(3)
        expect(missedSeeksAfter(2, hardSyncThreshold + 0.1, true)).toBe(0)
        expect(missedSeeksAfter(3, 0.1, false)).toBe(0)
    })

    it('measures the time of seeks as the mean with the time before', () => {
        expect(nextSeekLatency(null, 0.4)).toBe(0.4)
        expect(nextSeekLatency(0.4, 0.8)).toBeCloseTo(0.6)
        expect(nextSeekLatency(0.4, -1)).toBe(0.2)
        expect(nextSeekLatency(null, 60)).toBe(maxSeekLead * 2)
    })
})

describe('speed of the video', () => {
    it('changes the speed only clearly above the threshold of the sync', () => {
        expect(rateFor(softSyncThreshold / 2, 1)).toBe(1)
        expect(rateFor(-(rateStartThreshold - 0.01), 1)).toBe(1)
        expect(rateFor(-rateStartThreshold, 1)).toBeCloseTo(1.02)
        expect(rateFor(rateStartThreshold, 1)).toBeCloseTo(0.98)
    })

    it('uses a few steps for larger differences', () => {
        expect(rateFor(-0.15, 1)).toBeCloseTo(1.05)
        expect(rateFor(-0.25, 1)).toBeCloseTo(1.08)
        expect(rateFor(-0.15, 1.02)).toBeCloseTo(1.05)
    })

    it('keeps a correction until the video is in sync (hysteresis)', () => {
        // Below the start of a correction, but not yet in sync: the speed stays.
        expect(rateFor(-(rateStartThreshold - 0.02), 1.02)).toBeCloseTo(1.02)
        expect(rateFor(-softSyncThreshold / 2, 1.02)).toBe(1)
    })

    it('takes a smaller step only clearly below the start of the current one', () => {
        expect(rateFor(-0.09, 1.05)).toBeCloseTo(1.05)
        expect(rateFor(-0.06, 1.05)).toBeCloseTo(1.02)
    })

    it('goes back to the normal speed when the video went past the audio', () => {
        expect(rateFor(0.06, 1.05)).toBe(1)
        expect(rateFor(-0.06, 0.95)).toBe(1)
    })

    it('keeps the speed the same over checks with a jittering position', () => {
        const rates = new Set<number>()
        let rate = 1
        for (const drift of [-0.12, -0.11, -0.13, -0.1, -0.09, -0.11, -0.08, -0.1, -0.09]) {
            rate = rateFor(drift, rate)
            rates.add(rate)
        }

        expect([...rates]).toEqual([1.05])
    })
})

describe('stage view', () => {
    it('shows the video only when the track has one and it is ready', () => {
        expect(stageViewOf('Video', true, 'ready')).toBe('video')
        expect(stageViewOf('Video', true, 'loading')).toBe('cover')
        expect(stageViewOf('Video', true, 'failed')).toBe('cover')
        expect(stageViewOf('Video', true, 'idle')).toBe('cover')
        expect(stageViewOf('Video', false, 'ready')).toBe('cover')
    })

    it('shows the cover and the visualization as chosen', () => {
        expect(stageViewOf('Cover', true, 'ready')).toBe('cover')
        expect(stageViewOf('Visualization', false, 'idle')).toBe('visualization')
        expect(stageViewOf('Visualization', true, 'ready')).toBe('visualization')
    })

    it('shows the lyrics only for tracks with lyrics', () => {
        expect(stageViewOf('Lyrics', false, 'idle', true)).toBe('lyrics')
        expect(stageViewOf('Lyrics', true, 'ready', false)).toBe('cover')
        expect(stageViewOf('Lyrics', false, 'idle')).toBe('cover')
        expect(stageViewOf('Cover', false, 'idle', true)).toBe('cover')
    })
})
