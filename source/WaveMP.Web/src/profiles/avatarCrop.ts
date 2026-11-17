// Cropping of an uploaded avatar: the user moves and zooms the picture behind a square frame, and
// the web app uploads exactly the square inside the frame. Pure functions only; the component
// AvatarCropper shows the crop, avatarImage.ts draws it into a picture.

/** Picture types the API accepts as avatar. */
export const avatarUploadTypes = ['image/jpeg', 'image/png', 'image/webp'] as const

/** Largest picture the API accepts by default (10 MiB); larger ones are refused before reading. */
export const maxAvatarUploadBytes = 10 * 1024 * 1024

/** Largest zoom: the frame shows a quarter of the shorter side of the picture. */
export const maxAvatarZoom = 4

/**
 * Largest side of the uploaded square in pixels: the largest size the API stores avatars in, so
 * nothing is uploaded that the server would scale down anyway.
 */
export const maxAvatarOutputSize = 1000

export type AvatarUploadProblem = 'type' | 'size'

/** Why a file cannot become an avatar, or null if it can. */
export function avatarUploadProblem(file: {
    readonly type: string
    readonly size: number
}): AvatarUploadProblem | null {
    if (!(avatarUploadTypes as readonly string[]).includes(file.type)) {
        return 'type'
    }

    return file.size > maxAvatarUploadBytes ? 'size' : null
}

/** Width and height of the picture in pixels. */
export interface PictureSize {
    readonly width: number
    readonly height: number
}

/**
 * The square inside the frame: its top left corner in pixels of the picture and the zoom (1 shows
 * the whole shorter side, maxAvatarZoom a quarter of it).
 */
export interface AvatarCrop {
    readonly x: number
    readonly y: number
    readonly zoom: number
}

function clamp(value: number, min: number, max: number): number {
    return Math.min(Math.max(value, min), max)
}

function isValid(picture: PictureSize): boolean {
    return (
        Number.isFinite(picture.width) &&
        Number.isFinite(picture.height) &&
        picture.width > 0 &&
        picture.height > 0
    )
}

/** Side of the square inside the frame in pixels of the picture. */
export function cropSide(picture: PictureSize, zoom: number): number {
    return Math.min(picture.width, picture.height) / clamp(zoom, 1, maxAvatarZoom)
}

/** The crop kept inside the picture: zoom from 1 to maxAvatarZoom, the square never outside. */
export function clampCrop(crop: AvatarCrop, picture: PictureSize): AvatarCrop {
    if (!isValid(picture)) {
        return { x: 0, y: 0, zoom: 1 }
    }

    const zoom = clamp(Number.isFinite(crop.zoom) ? crop.zoom : 1, 1, maxAvatarZoom)
    const side = cropSide(picture, zoom)
    return {
        x: clamp(Number.isFinite(crop.x) ? crop.x : 0, 0, picture.width - side),
        y: clamp(Number.isFinite(crop.y) ? crop.y : 0, 0, picture.height - side),
        zoom,
    }
}

/** The largest square in the middle of the picture, like the server crops pictures itself. */
export function initialCrop(picture: PictureSize): AvatarCrop {
    if (!isValid(picture)) {
        return { x: 0, y: 0, zoom: 1 }
    }

    const side = cropSide(picture, 1)
    return { x: (picture.width - side) / 2, y: (picture.height - side) / 2, zoom: 1 }
}

/** Zooms around the middle of the frame. */
export function zoomCrop(crop: AvatarCrop, picture: PictureSize, zoom: number): AvatarCrop {
    const before = cropSide(picture, crop.zoom)
    const after = cropSide(picture, zoom)
    return clampCrop(
        {
            x: crop.x + (before - after) / 2,
            y: crop.y + (before - after) / 2,
            zoom,
        },
        picture,
    )
}

/**
 * Moves the picture by a distance in pixels of the frame on the screen (frameSize wide): dragging
 * to the right shows more of the left side of the picture.
 */
export function panCrop(
    crop: AvatarCrop,
    picture: PictureSize,
    deltaX: number,
    deltaY: number,
    frameSize: number,
): AvatarCrop {
    if (!(frameSize > 0)) {
        return clampCrop(crop, picture)
    }

    const scale = cropSide(picture, crop.zoom) / frameSize
    return clampCrop({ ...crop, x: crop.x - deltaX * scale, y: crop.y - deltaY * scale }, picture)
}

/** Distance in pixels of the frame that one press of an arrow key moves the picture. */
export const avatarKeyStep = 10

/** The crop after an arrow key on the frame, or null for other keys. */
export function cropForKey(
    key: string,
    crop: AvatarCrop,
    picture: PictureSize,
    frameSize: number,
): AvatarCrop | null {
    switch (key) {
        case 'ArrowLeft':
            return panCrop(crop, picture, avatarKeyStep, 0, frameSize)
        case 'ArrowRight':
            return panCrop(crop, picture, -avatarKeyStep, 0, frameSize)
        case 'ArrowUp':
            return panCrop(crop, picture, 0, avatarKeyStep, frameSize)
        case 'ArrowDown':
            return panCrop(crop, picture, 0, -avatarKeyStep, frameSize)
        default:
            return null
    }
}

/**
 * Runtime variables that place the picture behind the square frame, in sizes of the frame: its
 * width (--wmp-crop-scale) and the offset of its top left corner (--wmp-crop-x, --wmp-crop-y).
 * The base CSS turns them into the size and position of the picture.
 */
export function cropVariables(
    crop: AvatarCrop,
    picture: PictureSize,
): { scale: number; x: number; y: number } {
    if (!isValid(picture)) {
        return { scale: 1, x: 0, y: 0 }
    }

    const side = cropSide(picture, crop.zoom)
    return { scale: picture.width / side, x: crop.x / side, y: crop.y / side }
}

/** The square to draw: source rectangle in pixels of the picture and the side of the result. */
export interface CropRectangle {
    readonly x: number
    readonly y: number
    readonly side: number
    readonly outputSize: number
}

/** The rectangle of the picture to upload, at most maxAvatarOutputSize pixels, never enlarged. */
export function cropRectangle(crop: AvatarCrop, picture: PictureSize): CropRectangle {
    const clamped = clampCrop(crop, picture)
    const side = isValid(picture) ? cropSide(picture, clamped.zoom) : 1
    return {
        x: clamped.x,
        y: clamped.y,
        side,
        outputSize: Math.max(1, Math.min(maxAvatarOutputSize, Math.round(side))),
    }
}
