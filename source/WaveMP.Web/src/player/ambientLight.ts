// The ambient light of the video on the Now Playing stage: soft light in the colors of the current
// picture in the free area around it. To use little of the device, the picture is drawn at a tiny
// size (at most 32 pixels on its long side) into one of two canvases a few times per second; the
// base CSS enlarges, blurs and cross-fades the two canvases, so the colors flow smoothly without
// any work per frame. Nothing is drawn while the video is paused (the last picture stays), while the
// view is hidden, out of the viewport or the page is in the background. With reduced motion the
// light is one static color: the average of the picture, read once per video.
//
// The canvases read the picture of the video element. Its signed URLs are paths of the same origin
// as the app (and hls.js plays transcoded videos from blob URLs of the page), so the canvases may
// read its pixels without any CORS rule; if a browser still refuses (a tainted canvas), the light
// keeps drawing and only the static color is left out.

/** Milliseconds between two pictures while the video plays: four pictures per second. */
export const ambientInterval = 250

/** Pixels of the long side of the tiny picture. */
export const ambientLongSide = 32

/** Size of the tiny picture the color of the reduced motion mode is read from. */
export const ambientSampleSize = { width: 16, height: 9 } as const

/** readyState from which the element shows the picture of its position (HAVE_CURRENT_DATA). */
const haveCurrentData = 2

/**
 * What the ambient light does, as data-state of video-ambient:
 * - off: switched off by the profile
 * - waiting: on, but the video has no picture yet
 * - running: draws the picture of the playing video a few times per second
 * - paused: keeps the last picture, because the video is paused or the view is not visible
 * - static: reduced motion; one color of the picture without animation
 */
export type AmbientState = 'off' | 'waiting' | 'running' | 'paused' | 'static'

/** What decides the state of the ambient light. */
export interface AmbientConditions {
    /** Whether the profile shows the ambient light. */
    readonly enabled: boolean
    /** Whether the video has a picture at its position. */
    readonly ready: boolean
    /** Whether the user prefers reduced motion. */
    readonly reducedMotion: boolean
    /** Whether the stage shows the video view. */
    readonly shown: boolean
    /** Whether the stage is in the viewport (or in full screen) and the page in the foreground. */
    readonly visible: boolean
    /** Whether the video plays. */
    readonly playing: boolean
}

/** The state of the ambient light under the conditions. */
export function ambientState(conditions: AmbientConditions): AmbientState {
    if (!conditions.enabled) {
        return 'off'
    }

    if (!conditions.ready) {
        return 'waiting'
    }

    if (conditions.reducedMotion) {
        return 'static'
    }

    return conditions.shown && conditions.visible && conditions.playing ? 'running' : 'paused'
}

/**
 * Size of the tiny picture for a video picture of the size: the aspect ratio of the video with at
 * most ambientLongSide pixels on the long side and at least one pixel on the short one; 16:9 while
 * the size is unknown.
 */
export function ambientCanvasSize(
    width: number,
    height: number,
): { width: number; height: number } {
    if (!(width > 0) || !(height > 0)) {
        return { width: ambientLongSide, height: Math.round((ambientLongSide * 9) / 16) }
    }

    if (width >= height) {
        return {
            width: ambientLongSide,
            height: Math.max(1, Math.round((ambientLongSide * height) / width)),
        }
    }

    return {
        width: Math.max(1, Math.round((ambientLongSide * width) / height)),
        height: ambientLongSide,
    }
}

/** Pixels whose brightest channel is below this count as black bars and are left out. */
const blackThreshold = 16

/**
 * The average color of RGBA pixels as CSS color, for example "rgb(31 92 160)". Black pixels (bars
 * of letterboxed videos) are left out unless the whole picture is black; null without pixels.
 */
export function averageColor(data: ArrayLike<number>): string | null {
    const all = { red: 0, green: 0, blue: 0, count: 0 }
    const bright = { red: 0, green: 0, blue: 0, count: 0 }
    for (let index = 0; index + 3 < data.length; index += 4) {
        const red = data[index] ?? 0
        const green = data[index + 1] ?? 0
        const blue = data[index + 2] ?? 0
        for (const sums of Math.max(red, green, blue) >= blackThreshold ? [all, bright] : [all]) {
            sums.red += red
            sums.green += green
            sums.blue += blue
            sums.count++
        }
    }

    const sums = bright.count > 0 ? bright : all
    if (sums.count === 0) {
        return null
    }

    const channel = (sum: number) => Math.round(sum / sums.count)
    return `rgb(${channel(sums.red)} ${channel(sums.green)} ${channel(sums.blue)})`
}

/** The part of a canvas the ambient light uses; tests replace it. */
export interface AmbientCanvas {
    width: number
    height: number
    getContext(
        type: '2d',
        options?: CanvasRenderingContext2DSettings,
    ): Pick<CanvasRenderingContext2D, 'drawImage' | 'getImageData'> | null
}

/** The part of the video element the ambient light reads. */
export type AmbientVideo = Pick<HTMLVideoElement, 'videoWidth' | 'videoHeight'> & CanvasImageSource

/** What the ambient light needs from the browser; tests replace it. */
export interface AmbientEnvironment {
    setInterval(callback: () => void, delay: number): number
    clearInterval(handle: number): void
    /** A canvas outside the page to read the color from. */
    createCanvas(): AmbientCanvas
}

export const browserAmbientEnvironment: AmbientEnvironment = {
    setInterval: (callback, delay) => window.setInterval(callback, delay),
    clearInterval: (handle) => window.clearInterval(handle),
    createCanvas: () => document.createElement('canvas'),
}

export interface AmbientLightEvents {
    /** A new picture is drawn into the canvas with the index; it is now the current one. */
    frame?(index: number): void
    /** The static color of the picture changed; null without one. */
    color?(color: string | null): void
}

/**
 * Draws the tiny pictures of a video into two canvases by turns, as the state asks for. The
 * canvas with the newest picture is the current one (frame); the base CSS fades it in above the
 * previous one.
 */
export class AmbientLight {
    private timer: number | null = null
    private current = 1
    /** Whether a picture of the current source is drawn. */
    private drawn = false
    /** Whether the static color of the current source is read. */
    private sampled = false
    private sampleCanvas: AmbientCanvas | null = null
    private conditions: AmbientConditions | null = null

    constructor(
        private readonly canvases: readonly [AmbientCanvas, AmbientCanvas],
        private readonly video: AmbientVideo,
        private readonly events: AmbientLightEvents = {},
        private readonly environment: AmbientEnvironment = browserAmbientEnvironment,
    ) {}

    /** The state under the conditions; starts or stops drawing as it needs. */
    update(conditions: AmbientConditions): AmbientState {
        this.conditions = conditions
        const state = ambientState(conditions)
        if (state === 'running') {
            this.start()
            return state
        }

        this.stop()
        if (state === 'paused' && conditions.shown && conditions.visible && !this.drawn) {
            // The paused video shows a picture the light has not drawn yet (a new video, a seek).
            this.draw()
        } else if (state === 'static' && !this.sampled) {
            this.sample()
        }

        return state
    }

    /** The video loads another source: the pictures and the color of the previous one are gone. */
    reset(): void {
        this.drawn = false
        this.sampled = false
        this.events.color?.(null)
    }

    /**
     * The picture of the video changed without playing (a seek): a paused light draws it once and
     * a static one reads its color again.
     */
    refresh(): void {
        this.drawn = false
        if (this.conditions) {
            this.update(this.conditions)
        }
    }

    /** Draws the current picture into the previous canvas, which becomes the current one. */
    draw(): boolean {
        const { width, height } = ambientCanvasSize(this.video.videoWidth, this.video.videoHeight)
        const next = 1 - this.current
        const canvas = this.canvases[next]!
        if (canvas.width !== width || canvas.height !== height) {
            canvas.width = width
            canvas.height = height
        }

        try {
            const context = canvas.getContext('2d', { alpha: false })
            if (!context) {
                return false
            }

            context.drawImage(this.video, 0, 0, width, height)
        } catch {
            // The video has no picture at the moment (e.g. while it switches its source).
            return false
        }

        this.current = next
        this.drawn = true
        this.events.frame?.(next)
        return true
    }

    /** Reads the average color of the current picture for the static light. */
    sample(): string | null {
        this.sampled = true
        const { width, height } = ambientSampleSize
        let color: string | null = null
        try {
            this.sampleCanvas ??= this.environment.createCanvas()
            const canvas = this.sampleCanvas
            canvas.width = width
            canvas.height = height
            const context = canvas.getContext('2d', { willReadFrequently: true })
            if (context) {
                context.drawImage(this.video, 0, 0, width, height)
                color = averageColor(context.getImageData(0, 0, width, height).data)
            }
        } catch {
            // No picture yet, or a canvas the browser does not let us read (tainted).
            color = null
        }

        this.events.color?.(color)
        return color
    }

    /** Stops drawing; the canvases keep their pictures. */
    dispose(): void {
        this.stop()
        this.conditions = null
    }

    private start(): void {
        if (this.timer !== null) {
            return
        }

        this.draw()
        this.timer = this.environment.setInterval(() => this.draw(), ambientInterval)
    }

    private stop(): void {
        if (this.timer !== null) {
            this.environment.clearInterval(this.timer)
            this.timer = null
        }
    }
}

/** Whether a video element shows a picture at its position. */
export function hasPicture(video: Pick<HTMLVideoElement, 'readyState'>): boolean {
    return video.readyState >= haveCurrentData
}
