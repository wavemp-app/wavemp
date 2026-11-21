import { describe, expect, it } from 'vitest'
import {
    SwipeTracker,
    swipeCloseDuration,
    swipeFlickVelocity,
    swipeMaximumCloseDuration,
    swipeMinimumCloseDuration,
    swipeOutcome,
    swipeProgress,
} from '../swipe'

describe('swiping the full-screen player down', () => {
    const height = 800

    it('closes after a quarter of the height, else snaps back', () => {
        expect(swipeOutcome(150, 0, height)).toBe('snap')
        expect(swipeOutcome(199, 0.2, height)).toBe('snap')
        expect(swipeOutcome(200, 0, height)).toBe('close')
        expect(swipeOutcome(500, 0.1, height)).toBe('close')
    })

    it('closes on a fast flick, also after a short distance', () => {
        expect(swipeOutcome(40, swipeFlickVelocity, height)).toBe('close')
        expect(swipeOutcome(20, 2, height)).toBe('close')
    })

    it('snaps back when the finger moves up fast at the end', () => {
        expect(swipeOutcome(500, -swipeFlickVelocity, height)).toBe('snap')
    })

    it('never closes without a height', () => {
        expect(swipeOutcome(100, 0, 0)).toBe('snap')
    })

    it('leaves the screen at the speed of the finger', () => {
        // 600 pixels left at 3 pixels per millisecond.
        expect(swipeCloseDuration(200, 3, height)).toBe(200)
        // A very fast flick still moves visibly, a slow release does not drag on.
        expect(swipeCloseDuration(200, 50, height)).toBe(swipeMinimumCloseDuration)
        expect(swipeCloseDuration(200, 0.1, height)).toBe(swipeMaximumCloseDuration)
        expect(swipeCloseDuration(300, 0, height)).toBe(swipeMaximumCloseDuration)
    })

    it('tells the share of the way', () => {
        expect(swipeProgress(0, height)).toBe(0)
        expect(swipeProgress(200, height)).toBe(0.25)
        expect(swipeProgress(1000, height)).toBe(1)
        expect(swipeProgress(-20, height)).toBe(0)
        expect(swipeProgress(20, 0)).toBe(0)
    })
})

describe('swipe tracker', () => {
    it('follows the finger down, never up', () => {
        const tracker = new SwipeTracker()
        tracker.begin(100, 0)
        expect(tracker.move(160, 16)).toBe(60)
        expect(tracker.move(80, 32)).toBe(0)
    })

    it('measures the speed at the end of a slow drag', () => {
        const tracker = new SwipeTracker()
        tracker.begin(100, 0)
        for (let time = 16; time <= 800; time += 16) {
            tracker.move(100 + time * 0.3, time)
        }
        expect(tracker.velocity(800)).toBeCloseTo(0.3, 5)
    })

    it('measures the speed of a flick over its last moments', () => {
        const tracker = new SwipeTracker()
        tracker.begin(100, 0)
        // Resting first, then a fast flick.
        tracker.move(102, 300)
        tracker.move(132, 316)
        tracker.move(162, 332)
        tracker.move(192, 348)
        expect(tracker.velocity(350)).toBeCloseTo(90 / 48, 5)
    })

    it('uses the moment before the window when only the last one is in it', () => {
        const tracker = new SwipeTracker()
        tracker.begin(100, 0)
        tracker.move(250, 150)
        expect(tracker.velocity(150)).toBe(1)
    })

    it('has no speed when the finger rested before it let go', () => {
        const tracker = new SwipeTracker()
        tracker.begin(100, 0)
        tracker.move(300, 50)
        expect(tracker.velocity(400)).toBe(0)
    })
})
