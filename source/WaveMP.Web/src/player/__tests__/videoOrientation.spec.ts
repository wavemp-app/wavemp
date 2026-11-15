import { describe, expect, it } from 'vitest'
import { videoAspectRatio, videoOrientation } from '../videoOrientation'

describe('videoOrientation', () => {
    it('tells portrait, landscape and square pictures apart', () => {
        expect(videoOrientation(1080, 1920)).toBe('portrait')
        expect(videoOrientation(1920, 1080)).toBe('landscape')
        expect(videoOrientation(720, 720)).toBe('square')
    })

    it('is unknown while the size is not loaded', () => {
        expect(videoOrientation(0, 0)).toBeUndefined()
        expect(videoOrientation(1920, 0)).toBeUndefined()
        expect(videoOrientation(Number.NaN, 1080)).toBeUndefined()
    })
})

describe('videoAspectRatio', () => {
    it('divides the width by the height', () => {
        expect(videoAspectRatio(1920, 1080)).toBe(1.7778)
        expect(videoAspectRatio(1080, 1920)).toBe(0.5625)
    })

    it('is unknown while the size is not loaded', () => {
        expect(videoAspectRatio(0, 1080)).toBeUndefined()
    })
})
