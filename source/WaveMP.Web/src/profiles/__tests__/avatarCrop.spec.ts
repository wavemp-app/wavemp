import { describe, expect, it } from 'vitest'
import {
    avatarUploadProblem,
    clampCrop,
    cropForKey,
    cropRectangle,
    cropSide,
    cropVariables,
    initialCrop,
    maxAvatarUploadBytes,
    maxAvatarZoom,
    panCrop,
    zoomCrop,
} from '../avatarCrop'

const wide = { width: 1600, height: 800 }
const tall = { width: 600, height: 900 }

describe('avatarUploadProblem', () => {
    it('accepts JPEG, PNG and WebP up to the limit', () => {
        expect(avatarUploadProblem({ type: 'image/jpeg', size: 1 })).toBeNull()
        expect(avatarUploadProblem({ type: 'image/png', size: maxAvatarUploadBytes })).toBeNull()
        expect(avatarUploadProblem({ type: 'image/webp', size: 1000 })).toBeNull()
    })

    it('refuses other types and larger files', () => {
        expect(avatarUploadProblem({ type: 'image/gif', size: 1 })).toBe('type')
        expect(avatarUploadProblem({ type: '', size: 1 })).toBe('type')
        expect(avatarUploadProblem({ type: 'image/png', size: maxAvatarUploadBytes + 1 })).toBe(
            'size',
        )
    })
})

describe('initialCrop', () => {
    it('takes the largest square in the middle', () => {
        expect(initialCrop(wide)).toEqual({ x: 400, y: 0, zoom: 1 })
        expect(initialCrop(tall)).toEqual({ x: 0, y: 150, zoom: 1 })
    })

    it('falls back to the origin for pictures without a size', () => {
        expect(initialCrop({ width: 0, height: 0 })).toEqual({ x: 0, y: 0, zoom: 1 })
    })
})

describe('clampCrop', () => {
    it('keeps the zoom between 1 and the maximum', () => {
        expect(clampCrop({ x: 0, y: 0, zoom: 0.5 }, wide).zoom).toBe(1)
        expect(clampCrop({ x: 0, y: 0, zoom: 10 }, wide).zoom).toBe(maxAvatarZoom)
    })

    it('keeps the square inside the picture', () => {
        expect(clampCrop({ x: -50, y: 30, zoom: 1 }, wide)).toEqual({ x: 0, y: 0, zoom: 1 })
        expect(clampCrop({ x: 5000, y: 5000, zoom: 2 }, wide)).toEqual({
            x: 1200,
            y: 400,
            zoom: 2,
        })
    })

    it('replaces values that are no numbers', () => {
        expect(clampCrop({ x: Number.NaN, y: Number.NaN, zoom: Number.NaN }, wide)).toEqual({
            x: 0,
            y: 0,
            zoom: 1,
        })
    })
})

describe('zoomCrop', () => {
    it('zooms around the middle of the frame', () => {
        const zoomed = zoomCrop(initialCrop(wide), wide, 2)

        expect(cropSide(wide, zoomed.zoom)).toBe(400)
        expect(zoomed).toEqual({ x: 600, y: 200, zoom: 2 })
    })

    it('stays inside the picture when zooming out at an edge', () => {
        expect(zoomCrop({ x: 1200, y: 400, zoom: 2 }, wide, 1)).toEqual({ x: 800, y: 0, zoom: 1 })
    })
})

describe('panCrop', () => {
    it('moves the square against the drag in pixels of the picture', () => {
        // The frame is 200 pixels wide and shows 800 pixels of the picture: 1 to 4.
        expect(panCrop({ x: 400, y: 0, zoom: 1 }, wide, 50, 0, 200)).toEqual({
            x: 200,
            y: 0,
            zoom: 1,
        })
        expect(panCrop({ x: 400, y: 0, zoom: 1 }, wide, -500, 0, 200)).toEqual({
            x: 800,
            y: 0,
            zoom: 1,
        })
    })

    it('changes nothing without a frame size', () => {
        expect(panCrop({ x: 400, y: 0, zoom: 1 }, wide, 50, 50, 0)).toEqual({
            x: 400,
            y: 0,
            zoom: 1,
        })
    })
})

describe('cropForKey', () => {
    it('moves with the arrow keys and ignores other keys', () => {
        const crop = { x: 0, y: 150, zoom: 1 }

        expect(cropForKey('ArrowUp', crop, tall, 600)?.y).toBe(140)
        expect(cropForKey('ArrowDown', crop, tall, 600)?.y).toBe(160)
        expect(cropForKey('ArrowLeft', crop, tall, 600)?.x).toBe(0)
        expect(cropForKey('Enter', crop, tall, 600)).toBeNull()
    })
})

describe('cropVariables', () => {
    it('places the picture in sizes of the frame', () => {
        expect(cropVariables({ x: 400, y: 0, zoom: 1 }, wide)).toEqual({ scale: 2, x: 0.5, y: 0 })
        expect(cropVariables({ x: 600, y: 200, zoom: 2 }, wide)).toEqual({
            scale: 4,
            x: 1.5,
            y: 0.5,
        })
    })
})

describe('cropRectangle', () => {
    it('draws the square of the frame, at most 1000 pixels and never enlarged', () => {
        expect(cropRectangle({ x: 400, y: 0, zoom: 1 }, wide)).toEqual({
            x: 400,
            y: 0,
            side: 800,
            outputSize: 800,
        })
        expect(cropRectangle({ x: 0, y: 0, zoom: 1 }, { width: 3000, height: 2000 })).toEqual({
            x: 0,
            y: 0,
            side: 2000,
            outputSize: 1000,
        })
    })
})
