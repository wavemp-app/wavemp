// Orientation of the picture of a video, from the size the browser shows it in (videoWidth and
// videoHeight already apply a rotation of the file). The video stage carries it as data-orientation
// and the aspect ratio as --wmp-video-aspect-ratio, so themes can design the free areas beside a
// portrait video or above and below a wide one.

export type VideoOrientation = 'landscape' | 'portrait' | 'square'

/** The orientation of a picture; undefined while its size is unknown (no metadata yet). */
export function videoOrientation(width: number, height: number): VideoOrientation | undefined {
    if (!(width > 0) || !(height > 0)) {
        return undefined
    }

    if (width === height) {
        return 'square'
    }

    return width > height ? 'landscape' : 'portrait'
}

/** Width divided by height, rounded to four places; undefined while the size is unknown. */
export function videoAspectRatio(width: number, height: number): number | undefined {
    if (!(width > 0) || !(height > 0)) {
        return undefined
    }

    return Math.round((width / height) * 10_000) / 10_000
}
