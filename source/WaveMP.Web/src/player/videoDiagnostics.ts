// Diagnostics of the video of the Now Playing stage, a developer setting of the device: what the
// video does while it follows the audio. Per video it records the values of the last check (drift,
// speed, seeks, readyState, buffer, dropped pictures, file or HLS) and a log of what happened:
// seeks and changes of the speed of the follower, waits of the stream and, above all, when a
// stutter starts and ends, with what the follower and the stream did at that time. The recorder
// gets its values from the follower and holds no browser objects, so it is tested without one.

/** How the video is delivered: the file as it is or a transcoded HLS stream. */
export type VideoDelivery = 'file' | 'hls'

/** What the log of a video records. */
export type VideoDiagnosticsEventKind =
    /** The follower moved the video (value: the position it moved to). */
    | 'seek'
    /** The follower changed the speed of the video (value: the new speed). */
    | 'rate'
    /** The video waits for data (event waiting of the element). */
    | 'waiting'
    /** The browser gets no data of the video for a while (event stalled of the element). */
    | 'stalled'
    /** The video started to stutter (value: pictures dropped since the last check). */
    | 'stutter-start'
    /** The video plays smoothly again (value: length of the stutter in seconds). */
    | 'stutter-end'
    /** The video could not be played. */
    | 'error'

/** An entry of the log of a video. */
export interface VideoDiagnosticsEvent {
    /** Seconds since the video was loaded. */
    readonly at: number
    readonly kind: VideoDiagnosticsEventKind
    /** Position of the audio in seconds. */
    readonly position: number
    /** Seconds the video is ahead (positive) or behind (negative) of the audio; null if unknown. */
    readonly drift: number | null
    /** Speed of the video at that time. */
    readonly rate: number
    /** readyState of the element at that time. */
    readonly readyState: number
    /** Seconds of the video loaded ahead of its position at that time. */
    readonly bufferedAhead: number
    /** What the follower did with the last check: hold, seek, rate or wait. */
    readonly action: string | null
    /** Value of the entry, see the kinds. */
    readonly value: number | null
}

/** What the follower reads from the video with every check. */
export interface VideoSample {
    /** Milliseconds of a monotonic clock. */
    readonly time: number
    /** Position of the audio in seconds. */
    readonly audioPosition: number
    /** Seconds the video is ahead of the audio; null while the video has no metadata. */
    readonly drift: number | null
    readonly rate: number
    readonly readyState: number
    /** Position of the video in seconds. */
    readonly videoPosition: number
    /** The loaded ranges of the video in seconds. */
    readonly buffered: readonly (readonly [number, number])[]
    /** Pictures dropped and shown in all, if the browser tells (getVideoPlaybackQuality). */
    readonly droppedFrames: number | null
    readonly totalFrames: number | null
    /** What the follower did with this check. */
    readonly action: string | null
    /** Seeks in a row that did not bring the video into sync. */
    readonly missedSeeks: number
    /** Seconds the seeks of the video took so far; null before the first one. */
    readonly seekLatency: number | null
}

/** The diagnostics of the current video, as the overlay shows them. */
export interface VideoDiagnosticsSnapshot {
    readonly delivery: VideoDelivery
    /** Seconds since the video was loaded. */
    readonly elapsed: number
    readonly drift: number | null
    readonly rate: number
    readonly readyState: number
    readonly action: string | null
    /** The loaded range around the position of the video; null if there is none. */
    readonly buffered: { readonly start: number; readonly end: number } | null
    readonly bufferedAhead: number
    /** Seeks of the follower since the video was loaded. */
    readonly seeks: number
    readonly missedSeeks: number
    readonly seekLatency: number | null
    /** Pictures dropped and shown since the video was loaded; null if the browser does not tell. */
    readonly droppedFrames: number | null
    readonly totalFrames: number | null
    /** Whether the video stutters right now. */
    readonly stuttering: boolean
    /** How often the video started to stutter since it was loaded. */
    readonly stutters: number
    /** The log, oldest entry first. */
    readonly events: readonly VideoDiagnosticsEvent[]
}

/** Most entries of the log of a video; older ones are dropped. */
export const maxDiagnosticsEvents = 200

/** Pictures dropped between two checks from which this counts as a sign of a stutter. */
export const droppedFramesSignal = 3

/** Signs of a stutter within this many seconds that start a stutter. */
export const stutterSignals = 2

/** Seconds without signs after which a stutter counts as ended; also the window of the signs. */
export const stutterWindow = 5

/** The loaded range that holds the position, or null. */
export function bufferedRangeAt(
    ranges: readonly (readonly [number, number])[],
    position: number,
): { start: number; end: number } | null {
    // A little tolerance: the position of a playing video may be a hair outside its range.
    const range = ranges.find(([start, end]) => position >= start - 0.1 && position <= end + 0.1)
    return range ? { start: range[0], end: range[1] } : null
}

/** Seconds loaded ahead of the position; 0 outside the loaded ranges. */
export function bufferedAheadOf(
    ranges: readonly (readonly [number, number])[],
    position: number,
): number {
    const range = bufferedRangeAt(ranges, position)
    return range ? Math.max(0, range.end - position) : 0
}

/** Records the diagnostics of one video at a time. */
export class VideoDiagnosticsRecorder {
    private delivery: VideoDelivery = 'file'
    private loadedAt = 0
    private last: VideoSample | null = null
    private droppedAtStart: number | null = null
    private totalAtStart: number | null = null
    private seeks = 0
    private stutters = 0
    private signals: number[] = []
    private stutterStartedAt: number | null = null
    private log: VideoDiagnosticsEvent[] = []

    /** Starts the record of a new video. */
    start(delivery: VideoDelivery, time: number): void {
        this.delivery = delivery
        this.loadedAt = time
        this.last = null
        this.droppedAtStart = null
        this.totalAtStart = null
        this.seeks = 0
        this.stutters = 0
        this.signals = []
        this.stutterStartedAt = null
        this.log = []
    }

    /** The values of a check; detects the start and the end of a stutter. */
    sample(sample: VideoSample): void {
        const previous = this.last
        this.last = sample
        if (sample.droppedFrames !== null && this.droppedAtStart === null) {
            this.droppedAtStart = sample.droppedFrames
            this.totalAtStart = sample.totalFrames
        }

        const dropped =
            previous?.droppedFrames != null && sample.droppedFrames !== null
                ? sample.droppedFrames - previous.droppedFrames
                : 0
        if (dropped >= droppedFramesSignal) {
            this.signal(sample.time, dropped)
        }

        this.checkEnd(sample.time)
    }

    /** An entry of the log at the time, with the values of the last check. */
    record(kind: VideoDiagnosticsEventKind, time: number, value: number | null = null): void {
        if (kind === 'seek') {
            this.seeks++
        }

        this.push(kind, time, value)
        // A seek or a wait for data is a picture that stands still: a sign of a stutter.
        if (kind === 'seek' || kind === 'waiting' || kind === 'stalled') {
            this.signal(time, null)
        }
    }

    /** The diagnostics at the time. */
    snapshot(time: number): VideoDiagnosticsSnapshot {
        const last = this.last
        const ranges = last?.buffered ?? []
        const position = last?.videoPosition ?? 0
        return {
            delivery: this.delivery,
            elapsed: Math.max(0, (time - this.loadedAt) / 1000),
            drift: last?.drift ?? null,
            rate: last?.rate ?? 1,
            readyState: last?.readyState ?? 0,
            action: last?.action ?? null,
            buffered: bufferedRangeAt(ranges, position),
            bufferedAhead: bufferedAheadOf(ranges, position),
            seeks: this.seeks,
            missedSeeks: last?.missedSeeks ?? 0,
            seekLatency: last?.seekLatency ?? null,
            droppedFrames: difference(last?.droppedFrames ?? null, this.droppedAtStart),
            totalFrames: difference(last?.totalFrames ?? null, this.totalAtStart),
            stuttering: this.stutterStartedAt !== null,
            stutters: this.stutters,
            events: [...this.log],
        }
    }

    private signal(time: number, dropped: number | null): void {
        const window = stutterWindow * 1000
        this.signals = [...this.signals.filter((at) => time - at < window), time]
        if (this.stutterStartedAt === null && this.signals.length >= stutterSignals) {
            this.stutterStartedAt = time
            this.stutters++
            this.push('stutter-start', time, dropped)
        }
    }

    private checkEnd(time: number): void {
        const latest = this.signals.at(-1)
        if (this.stutterStartedAt === null || latest === undefined) {
            return
        }

        if (time - latest >= stutterWindow * 1000) {
            const length = (latest - this.stutterStartedAt) / 1000
            this.stutterStartedAt = null
            this.signals = []
            this.push('stutter-end', time, Math.round(length * 100) / 100)
        }
    }

    private push(kind: VideoDiagnosticsEventKind, time: number, value: number | null): void {
        const last = this.last
        const ranges = last?.buffered ?? []
        this.log.push({
            at: Math.max(0, (time - this.loadedAt) / 1000),
            kind,
            position: last?.audioPosition ?? 0,
            drift: last?.drift ?? null,
            rate: last?.rate ?? 1,
            readyState: last?.readyState ?? 0,
            bufferedAhead: bufferedAheadOf(ranges, last?.videoPosition ?? 0),
            action: last?.action ?? null,
            value,
        })
        if (this.log.length > maxDiagnosticsEvents) {
            this.log.splice(0, this.log.length - maxDiagnosticsEvents)
        }
    }
}

/** Key in localStorage: the diagnostics are a setting of the device, not of the profile. */
export const videoDiagnosticsStorageKey = 'wavemp.player.video-diagnostics.v1'

function storage(): Storage | null {
    try {
        return window.localStorage
    } catch {
        return null
    }
}

/** Whether this device shows the video diagnostics; off by default. */
export function loadVideoDiagnostics(store: Storage | null = storage()): boolean {
    try {
        return store?.getItem(videoDiagnosticsStorageKey) === 'true'
    } catch {
        // Blocked storage: the diagnostics stay off.
        return false
    }
}

export function saveVideoDiagnostics(enabled: boolean, store: Storage | null = storage()): void {
    try {
        if (enabled) {
            store?.setItem(videoDiagnosticsStorageKey, 'true')
        } else {
            store?.removeItem(videoDiagnosticsStorageKey)
        }
    } catch {
        // Blocked storage: the setting applies until the page is closed.
    }
}

function difference(value: number | null, start: number | null): number | null {
    return value === null || start === null ? null : Math.max(0, value - start)
}
