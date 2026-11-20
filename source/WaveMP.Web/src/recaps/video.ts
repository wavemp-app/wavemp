// The video of a recap: the sections of the story drawn one after another on a canvas and recorded
// with MediaRecorder, together with a soft pad of chords the browser synthesizes (no music of the
// library, nothing is played aloud). Browsers that record MP4 with H.264 and AAC deliver the file
// directly; the others record WebM, which the server converts into MP4.

import { drawRecapFrame, pictureSizes, type RecapFrame } from './canvas'
import type { RecapSlide } from './slides'

/** Seconds each section is shown in the video. */
export const videoSlideSeconds = 3.5

const framesPerSecond = 30

/** Recording formats in order of preference; MP4 with H.264 and AAC needs no conversion. */
export const recordingTypes = [
    'video/mp4;codecs=avc1.42E01F,mp4a.40.2',
    'video/mp4;codecs=avc1,mp4a.40.2',
    'video/webm;codecs=vp9,opus',
    'video/webm;codecs=vp8,opus',
    'video/webm',
] as const

export interface RecordingType {
    readonly mimeType: string
    /** Whether the recording is MP4 already and needs no conversion. */
    readonly mp4: boolean
}

/** The first recording format the browser supports, or null if it records none of them. */
export function chooseRecordingType(isSupported: (type: string) => boolean): RecordingType | null {
    const mimeType = recordingTypes.find((type) => {
        try {
            return isSupported(type)
        } catch {
            return false
        }
    })
    return mimeType ? { mimeType, mp4: mimeType.startsWith('video/mp4') } : null
}

/** Whether the browser can record the video of a recap at all. */
export function canRecordVideo(): boolean {
    return (
        typeof window !== 'undefined' &&
        typeof window.MediaRecorder === 'function' &&
        typeof HTMLCanvasElement.prototype.captureStream === 'function' &&
        typeof window.AudioContext === 'function' &&
        chooseRecordingType((type) => MediaRecorder.isTypeSupported(type)) !== null
    )
}

/** The section shown at a time of the video and how far it is (0 to 1). */
export function videoPosition(
    seconds: number,
    count: number,
): { readonly index: number; readonly progress: number } {
    const clamped = Math.max(0, Math.min(seconds, count * videoSlideSeconds - 0.001))
    const index = Math.min(count - 1, Math.floor(clamped / videoSlideSeconds))
    return { index, progress: (clamped - index * videoSlideSeconds) / videoSlideSeconds }
}

// Chords of the pad (frequencies in Hz), one per section, repeating: Cmaj9, Am9, Fmaj9, G6/9.
const chords = [
    [130.81, 196.0, 246.94, 293.66, 329.63],
    [110.0, 164.81, 196.0, 246.94, 261.63],
    [87.31, 130.81, 164.81, 196.0, 220.0],
    [98.0, 146.83, 196.0, 220.0, 246.94],
]

/** Schedules the pad of the whole video on an audio context and returns its stream. */
function synthesizePad(audio: AudioContext, count: number): MediaStream {
    const destination = audio.createMediaStreamDestination()
    const master = audio.createGain()
    master.gain.value = 0.16
    const filter = audio.createBiquadFilter()
    filter.type = 'lowpass'
    filter.frequency.value = 1400
    filter.connect(master)
    master.connect(destination)

    const start = audio.currentTime + 0.05
    for (let index = 0; index < count; index++) {
        const chord = chords[index % chords.length]!
        const at = start + index * videoSlideSeconds
        const end = at + videoSlideSeconds + 0.6
        for (const frequency of chord) {
            const oscillator = audio.createOscillator()
            oscillator.type = 'triangle'
            oscillator.frequency.value = frequency
            oscillator.detune.value = (Math.random() - 0.5) * 8
            const gain = audio.createGain()
            gain.gain.setValueAtTime(0, at)
            gain.gain.linearRampToValueAtTime(0.18, at + 0.6)
            gain.gain.setValueAtTime(0.18, end - 0.9)
            gain.gain.linearRampToValueAtTime(0, end)
            oscillator.connect(gain)
            gain.connect(filter)
            oscillator.start(at)
            oscillator.stop(end + 0.05)
        }
    }

    return destination.stream
}

export interface RecapVideoOptions {
    readonly slides: readonly RecapSlide[]
    readonly frame: Omit<RecapFrame, 'slide' | 'format' | 'progress' | 'story'>
    /** Share of the recording done (0 to 1). */
    readonly onProgress?: (share: number) => void
    readonly signal?: AbortSignal
}

export interface RecordedVideo {
    readonly blob: Blob
    readonly mp4: boolean
}

/** Records the story as a video in the story format, in real time. */
export async function recordRecapVideo(options: RecapVideoOptions): Promise<RecordedVideo> {
    const type = chooseRecordingType((candidate) => MediaRecorder.isTypeSupported(candidate))
    if (!type || options.slides.length === 0) {
        throw new Error('The browser cannot record videos.')
    }

    const { width, height } = pictureSizes.story
    const canvas = document.createElement('canvas')
    canvas.width = width
    canvas.height = height
    const context = canvas.getContext('2d')
    if (!context) {
        throw new Error('The browser cannot draw on a canvas.')
    }

    const count = options.slides.length
    const draw = (seconds: number) => {
        const position = videoPosition(seconds, count)
        drawRecapFrame(context, {
            ...options.frame,
            slide: options.slides[position.index]!,
            format: 'story',
            progress: position.progress,
            story: { index: position.index, count },
        })
    }
    draw(0)

    const audio = new AudioContext()
    await audio.resume()
    const stream = new MediaStream([
        ...canvas.captureStream(framesPerSecond).getVideoTracks(),
        ...synthesizePad(audio, count).getAudioTracks(),
    ])
    const recorder = new MediaRecorder(stream, {
        mimeType: type.mimeType,
        videoBitsPerSecond: 6_000_000,
        audioBitsPerSecond: 160_000,
    })
    const chunks: Blob[] = []
    recorder.addEventListener('dataavailable', (event) => {
        if (event.data.size > 0) {
            chunks.push(event.data)
        }
    })
    const stopped = new Promise<void>((resolve) => {
        recorder.addEventListener('stop', () => resolve(), { once: true })
    })

    const total = count * videoSlideSeconds
    const startedAt = performance.now()
    recorder.start(1000)
    try {
        await new Promise<void>((resolve, reject) => {
            const tick = () => {
                if (options.signal?.aborted) {
                    reject(new DOMException('The recording was cancelled.', 'AbortError'))
                    return
                }
                const seconds = (performance.now() - startedAt) / 1000
                draw(seconds)
                options.onProgress?.(Math.min(1, seconds / total))
                if (seconds >= total) {
                    resolve()
                } else {
                    window.setTimeout(tick, 1000 / framesPerSecond)
                }
            }
            tick()
        })
    } finally {
        if (recorder.state !== 'inactive') {
            recorder.stop()
        }
        await stopped
        stream.getTracks().forEach((track) => track.stop())
        await audio.close()
    }

    return { blob: new Blob(chunks, { type: type.mimeType.split(';')[0] }), mp4: type.mp4 }
}
