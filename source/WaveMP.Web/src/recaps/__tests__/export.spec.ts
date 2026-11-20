import { afterEach, describe, expect, it } from 'vitest'
import { RecapPeriodObject } from '@/api/generated/models'
import {
    defaultRecapDesign,
    findRecapDesign,
    loadRecapDesign,
    readRecapPalette,
    recapDesigns,
    recapDesignStorageKey,
    saveRecapDesign,
    fallbackPalette,
} from '../designs'
import { periodItem, periodLabelKey } from '../labels'
import { recapFileName, sharesThroughSystem } from '../share'
import { chooseRecordingType, recordingTypes, videoPosition, videoSlideSeconds } from '../video'

describe('recap designs', () => {
    afterEach(() => {
        localStorage.clear()
        document.body.innerHTML = ''
    })

    it('ships at least three designs with their own decoration', () => {
        expect(recapDesigns.map((design) => design.id)).toEqual(['aurora', 'frost', 'prism'])
        expect(new Set(recapDesigns.map((design) => design.decoration)).size).toBe(3)
        expect(
            recapDesigns.every((design) => design.labelKey === `recap.designs.${design.id}`),
        ).toBe(true)
    })

    it('shows the default design for unknown ids', () => {
        expect(findRecapDesign('frost').id).toBe('frost')
        expect(findRecapDesign('future').id).toBe(defaultRecapDesign)
        expect(findRecapDesign(null).id).toBe(defaultRecapDesign)
    })

    it('remembers the design on the device', () => {
        expect(loadRecapDesign()).toBe(defaultRecapDesign)
        saveRecapDesign('prism')
        expect(localStorage.getItem(recapDesignStorageKey)).toBe('prism')
        expect(loadRecapDesign()).toBe('prism')
        localStorage.setItem(recapDesignStorageKey, 'gone')
        expect(loadRecapDesign()).toBe(defaultRecapDesign)
        expect(loadRecapDesign(null)).toBe(defaultRecapDesign)
    })

    it('reads the colors from the variables of the element', () => {
        const element = document.createElement('div')
        element.style.setProperty('--wmp-recap-accent', '#ff0000')
        document.body.append(element)
        const palette = readRecapPalette(element)
        expect(palette.accent).toBe('#ff0000')
        expect(palette.text).toBe(fallbackPalette.text)
        expect(readRecapPalette(null)).toEqual(fallbackPalette)
    })
})

describe('recap labels', () => {
    it('names the periods', () => {
        expect(periodLabelKey(RecapPeriodObject.LastThreeMonths)).toBe(
            'recap.periods.lastThreeMonths',
        )
        expect(periodLabelKey(null)).toBe('recap.periods.custom')
        expect(periodItem(RecapPeriodObject.LastThreeMonths)).toBe('last-three-months')
        expect(periodItem(RecapPeriodObject.AllTime)).toBe('all-time')
    })
})

describe('recap files', () => {
    it('names the pictures and videos', () => {
        expect(recapFileName('LastMonth', 'top-artists', 'png')).toBe(
            'wavemp-recap-last-month-top-artists.png',
        )
        expect(recapFileName('CurrentYear', '', 'mp4')).toBe('wavemp-recap-current-year.mp4')
    })

    it('shares through the system only on touch devices that can share files', () => {
        const original = window.matchMedia
        const file = new File(['x'], 'a.png', { type: 'image/png' })
        const touch = (coarse: boolean) => {
            window.matchMedia = ((query: string) => ({
                matches: coarse && query.includes('coarse'),
                media: query,
            })) as unknown as typeof window.matchMedia
        }
        const sharing = {
            share: () => Promise.resolve(),
            canShare: () => true,
        } as unknown as Navigator

        touch(true)
        expect(sharesThroughSystem(file, sharing)).toBe(true)
        expect(sharesThroughSystem(file, {} as Navigator)).toBe(false)
        touch(false)
        expect(sharesThroughSystem(file, sharing)).toBe(false)
        window.matchMedia = original
    })
})

describe('recap video', () => {
    it('prefers MP4 with H.264 and AAC and falls back to WebM', () => {
        expect(chooseRecordingType(() => true)).toEqual({ mimeType: recordingTypes[0], mp4: true })
        expect(chooseRecordingType((type) => type.startsWith('video/webm'))).toEqual({
            mimeType: 'video/webm;codecs=vp9,opus',
            mp4: false,
        })
        expect(
            chooseRecordingType((type) => {
                if (type.includes('vp9')) {
                    throw new Error('unknown')
                }
                return type.includes('vp8')
            })?.mimeType,
        ).toBe('video/webm;codecs=vp8,opus')
        expect(chooseRecordingType(() => false)).toBeNull()
    })

    it('shows each section for the same time', () => {
        expect(videoPosition(0, 4)).toEqual({ index: 0, progress: 0 })
        expect(videoPosition(videoSlideSeconds * 1.5, 4)).toEqual({ index: 1, progress: 0.5 })
        const end = videoPosition(videoSlideSeconds * 10, 4)
        expect(end.index).toBe(3)
        expect(end.progress).toBeCloseTo(1, 3)
        expect(videoPosition(-1, 4)).toEqual({ index: 0, progress: 0 })
    })
})
