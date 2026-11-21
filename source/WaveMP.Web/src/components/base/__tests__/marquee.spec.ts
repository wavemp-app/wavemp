import { describe, expect, it } from 'vitest'
import {
    MarqueeCycle,
    marqueeDistance,
    marqueeEndPause,
    marqueeMinimumDuration,
    marqueeScrollDuration,
    marqueeStartDelay,
    marqueeStateTokens,
    type MarqueePhase,
    type MarqueeScheduler,
} from '../marquee'

/** Timers that run only when the test moves the time on. */
function manualScheduler() {
    let now = 0
    let next = 1
    const timers = new Map<number, { at: number; callback: () => void }>()
    const scheduler: MarqueeScheduler = {
        set(callback, milliseconds) {
            const id = next++
            timers.set(id, { at: now + milliseconds, callback })
            return id
        },
        clear(handle) {
            timers.delete(handle as number)
        },
    }

    function advance(milliseconds: number): void {
        const end = now + milliseconds
        for (;;) {
            const due = [...timers.entries()]
                .filter(([, timer]) => timer.at <= end)
                .sort(([, a], [, b]) => a.at - b.at)[0]
            if (!due) {
                break
            }
            timers.delete(due[0])
            now = due[1].at
            due[1].callback()
        }
        now = end
    }

    return { scheduler, advance, pending: () => timers.size }
}

describe('marquee', () => {
    it('moves a text only when it is wider than its place', () => {
        expect(marqueeDistance(120, 200)).toBe(0)
        expect(marqueeDistance(200, 200)).toBe(0)
        // Rounding of the layout does not count.
        expect(marqueeDistance(200.8, 200)).toBe(0)
        expect(marqueeDistance(260.2, 200)).toBe(61)
    })

    it('scrolls slowly, but never in a jerk', () => {
        expect(marqueeScrollDuration(90)).toBe(3000)
        expect(marqueeScrollDuration(300)).toBe(10000)
        expect(marqueeScrollDuration(5)).toBe(marqueeMinimumDuration)
    })

    it('tells the phase in its state', () => {
        expect(marqueeStateTokens(0, null)).toBe('fit')
        expect(marqueeStateTokens(0, 'scrolling')).toBe('fit')
        expect(marqueeStateTokens(40, null)).toBe('overflow')
        expect(marqueeStateTokens(40, 'waiting')).toBe('overflow waiting')
        expect(marqueeStateTokens(40, 'scrolling')).toBe('overflow scrolling')
        expect(marqueeStateTokens(40, 'end')).toBe('overflow end')
    })

    it('waits, scrolls, rests at the end and starts again', () => {
        const time = manualScheduler()
        const phases: (MarqueePhase | null)[] = []
        const cycle = new MarqueeCycle((phase) => phases.push(phase), time.scheduler)

        cycle.start(90)
        expect(cycle.running).toBe(true)
        expect(phases).toEqual(['waiting'])

        time.advance(marqueeStartDelay - 1)
        expect(phases).toEqual(['waiting'])
        time.advance(1)
        expect(phases).toEqual(['waiting', 'scrolling'])

        time.advance(marqueeScrollDuration(90))
        expect(phases).toEqual(['waiting', 'scrolling', 'end'])

        time.advance(marqueeEndPause)
        expect(phases).toEqual(['waiting', 'scrolling', 'end', 'waiting'])

        time.advance(marqueeStartDelay)
        expect(phases.at(-1)).toBe('scrolling')
    })

    it('starts from the beginning and stops', () => {
        const time = manualScheduler()
        const phases: (MarqueePhase | null)[] = []
        const cycle = new MarqueeCycle((phase) => phases.push(phase), time.scheduler)

        cycle.start(40)
        time.advance(marqueeStartDelay + 100)
        expect(phases.at(-1)).toBe('scrolling')

        cycle.start(40)
        expect(phases.at(-1)).toBe('waiting')
        expect(time.pending()).toBe(1)

        cycle.stop()
        expect(phases.at(-1)).toBeNull()
        expect(cycle.running).toBe(false)
        expect(time.pending()).toBe(0)

        time.advance(60_000)
        expect(phases.at(-1)).toBeNull()
    })

    it('does not run for a text that fits', () => {
        const time = manualScheduler()
        const phases: (MarqueePhase | null)[] = []
        const cycle = new MarqueeCycle((phase) => phases.push(phase), time.scheduler)

        cycle.start(0)
        expect(cycle.running).toBe(false)
        expect(phases).toEqual([null])
        expect(time.pending()).toBe(0)
    })
})
