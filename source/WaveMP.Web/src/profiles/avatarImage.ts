// Reading and drawing of an avatar picture in the browser: the chosen file is shown from an object
// URL, and the square inside the crop frame is drawn into a PNG for the upload. Needs the canvas of
// the browser, so components and tests can replace it as a whole (vi.mock('@/profiles/avatarImage')).

import type { CropRectangle, PictureSize } from './avatarCrop'

/** A chosen picture: its object URL (revoke it with release) and its size in pixels. */
export interface LoadedPicture extends PictureSize {
    readonly url: string
    readonly image: HTMLImageElement
}

/** Loads a picture file; rejects if the browser cannot read it. */
export function loadPicture(file: Blob): Promise<LoadedPicture> {
    const url = URL.createObjectURL(file)
    return new Promise((resolve, reject) => {
        const image = new Image()
        image.onload = () => {
            resolve({ url, image, width: image.naturalWidth, height: image.naturalHeight })
        }
        image.onerror = () => {
            URL.revokeObjectURL(url)
            reject(new Error('The picture cannot be read.'))
        }
        image.src = url
    })
}

/** Frees the object URL of a loaded picture. */
export function releasePicture(picture: LoadedPicture | null): void {
    if (picture) {
        URL.revokeObjectURL(picture.url)
    }
}

/** Draws the square of the crop into a PNG of its output size. */
export function drawCrop(picture: LoadedPicture, rectangle: CropRectangle): Promise<Blob> {
    const canvas = document.createElement('canvas')
    canvas.width = rectangle.outputSize
    canvas.height = rectangle.outputSize
    const context = canvas.getContext('2d')
    if (!context) {
        return Promise.reject(new Error('The browser cannot draw the picture.'))
    }

    context.imageSmoothingQuality = 'high'
    context.drawImage(
        picture.image,
        rectangle.x,
        rectangle.y,
        rectangle.side,
        rectangle.side,
        0,
        0,
        rectangle.outputSize,
        rectangle.outputSize,
    )
    return new Promise((resolve, reject) => {
        canvas.toBlob((blob) => {
            if (blob) {
                resolve(blob)
            } else {
                reject(new Error('The browser cannot encode the picture.'))
            }
        }, 'image/png')
    })
}
