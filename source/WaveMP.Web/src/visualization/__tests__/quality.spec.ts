import { describe, expect, it } from 'vitest'
import { AdaptiveQuality, fixedQualityLevel, qualityLevels } from '../quality'

/** Counts frames of the given rate for the given seconds; returns the level after them. */
function run(quality: AdaptiveQuality, fps: number, seconds: number): string {
    let level = quality.level
    for (let index = 0; index < fps * seconds; index++) {
        level = quality.frame(1 / fps)
    }

    return level
}

describe('fixedQualityLevel', () => {
    it('maps the settings of the profile to levels and Auto to null', () => {
        expect(fixedQualityLevel('Low')).toBe('low')
        expect(fixedQualityLevel('Medium')).toBe('medium')
        expect(fixedQualityLevel('High')).toBe('high')
        expect(fixedQualityLevel('Auto')).toBeNull()
    })

    it('orders the levels from least to most detail', () => {
        expect(qualityLevels).toEqual(['low', 'medium', 'high'])
    })
})

describe('AdaptiveQuality', () => {
    it('starts with the given level', () => {
        expect(new AdaptiveQuality().level).toBe('high')
        expect(new AdaptiveQuality('low').level).toBe('low')
    })

    it('stays while the device draws 60 frames per second', () => {
        const quality = new AdaptiveQuality()

        expect(run(quality, 60, 60)).toBe('high')
    })

    it('steps down one level per measurement while the frame rate is too low', () => {
        const quality = new AdaptiveQuality()

        expect(run(quality, 30, 2.1)).toBe('medium')
        expect(run(quality, 30, 2)).toBe('low')
        expect(run(quality, 30, 10)).toBe('low')
    })

    it('steps up again after a while of smooth drawing', () => {
        const quality = new AdaptiveQuality()
        run(quality, 30, 2.1)

        // The first step down doubles the wait for a step up from 10 to 20 seconds.
        expect(run(quality, 60, 18)).toBe('medium')
        expect(run(quality, 60, 4)).toBe('high')
    })

    it('waits longer after every step down, so a device on the edge settles', () => {
        const quality = new AdaptiveQuality('high', { upgradeSeconds: 4, maxUpgradeSeconds: 16 })

        run(quality, 30, 2) // high -> medium, wait 8 s
        expect(run(quality, 60, 8.1)).toBe('high')
        run(quality, 30, 2) // high -> medium, wait 16 s
        expect(run(quality, 60, 12)).toBe('medium')
        expect(run(quality, 60, 4.1)).toBe('high')
    })

    it('does not count frames between 50 and 57 per second as smooth', () => {
        const quality = new AdaptiveQuality('medium')

        expect(run(quality, 54, 60)).toBe('medium')
    })

    it('ignores very long frames such as a pause', () => {
        const quality = new AdaptiveQuality()

        for (let index = 0; index < 10; index++) {
            quality.frame(5)
        }

        expect(run(quality, 60, 10)).toBe('high')
    })

    it('steps down on a device that draws only a few frames per second', () => {
        const quality = new AdaptiveQuality()

        expect(run(quality, 4, 2.5)).toBe('medium')
        expect(run(quality, 4, 2.5)).toBe('low')
    })

    it('forgets a started measurement on restart', () => {
        const quality = new AdaptiveQuality()
        run(quality, 20, 1.9)

        quality.restart()

        expect(run(quality, 60, 2)).toBe('high')
    })
})
