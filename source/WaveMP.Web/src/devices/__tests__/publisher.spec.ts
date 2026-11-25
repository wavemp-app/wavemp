import { describe, expect, it, vi } from 'vitest'
import {
    heartbeatMilliseconds,
    PlaybackPublisher,
    publishDelayMilliseconds,
    volumeIntervalMilliseconds,
} from '../publisher'
import { FakeTimers } from './fakes'

function setup() {
    const timers = new FakeTimers()
    const publish = vi.fn()
    return { timers, publish, publisher: new PlaybackPublisher(publish, timers) }
}

describe('PlaybackPublisher', () => {
    it('publishes at once, with claim when asked', () => {
        const { publish, publisher } = setup()
        publisher.now()
        publisher.now(true)

        expect(publish.mock.calls).toEqual([[false], [true]])
    })

    it('publishes changes of the queue once, shortly after the last one', () => {
        const { timers, publish, publisher } = setup()
        publisher.soon()
        publisher.soon()
        publisher.soon()

        expect(publish).not.toHaveBeenCalled()
        expect([...timers.timeouts.values()].map((timeout) => timeout.milliseconds)).toEqual([
            publishDelayMilliseconds,
        ])
        timers.runTimeouts()
        expect(publish).toHaveBeenCalledOnce()
    })

    it('includes a waiting change in a publication at once', () => {
        const { timers, publish, publisher } = setup()
        publisher.soon()
        publisher.now()
        timers.runTimeouts()

        expect(publish).toHaveBeenCalledOnce()
    })

    it('publishes a moving volume at once and then at most every 250 ms', () => {
        const { timers, publish, publisher } = setup()
        publisher.throttled()
        publisher.throttled()
        publisher.throttled()

        expect(publish).toHaveBeenCalledOnce()
        expect([...timers.timeouts.values()][0]?.milliseconds).toBe(volumeIntervalMilliseconds)
        timers.runTimeouts()
        expect(publish).toHaveBeenCalledTimes(2)
        timers.runTimeouts()
        expect(publish).toHaveBeenCalledTimes(2)
    })

    it('publishes every 10 seconds while the heartbeat is on', () => {
        const { timers, publish, publisher } = setup()
        publisher.heartbeat(true)
        publisher.heartbeat(true)

        expect([...timers.intervals.values()].map((interval) => interval.milliseconds)).toEqual([
            heartbeatMilliseconds,
        ])
        timers.tickIntervals()
        expect(publish).toHaveBeenCalledOnce()
        publisher.heartbeat(false)
        expect(timers.intervals.size).toBe(0)
    })

    it('stops every timer when disposed', () => {
        const { timers, publish, publisher } = setup()
        publisher.soon()
        publisher.throttled()
        publisher.throttled()
        publisher.heartbeat(true)
        publisher.dispose()
        timers.runTimeouts()
        timers.tickIntervals()

        expect(publish).toHaveBeenCalledOnce()
        expect(timers.timeouts.size).toBe(0)
        expect(timers.intervals.size).toBe(0)
    })
})
