import { describe, expect, it } from 'vitest'
import { createChangeTracker } from '../changeTracker'

describe('createChangeTracker', () => {
    it('lets a read apply when nothing changed', () => {
        const tracker = createChangeTracker()

        const mayApply = tracker.read()

        expect(mayApply()).toBe(true)
    })

    it('lets a read apply that started after a change was saved', () => {
        const tracker = createChangeTracker()
        tracker.change()()

        const mayApply = tracker.read()

        expect(mayApply()).toBe(true)
    })

    it('does not let a read apply while a change made after its start is being saved', () => {
        const tracker = createChangeTracker()
        const mayApply = tracker.read()

        tracker.change()

        expect(mayApply()).toBe(false)
    })

    it('does not let a read apply if a change was made and saved after its start', () => {
        const tracker = createChangeTracker()
        const mayApply = tracker.read()

        tracker.change()()

        expect(mayApply()).toBe(false)
    })

    it('does not let a read apply that started while a change was being saved', () => {
        const tracker = createChangeTracker()
        const finish = tracker.change()
        const mayApply = tracker.read()

        finish()

        expect(mayApply()).toBe(false)
    })

    it('counts a finished change only once', () => {
        const tracker = createChangeTracker()
        const first = tracker.change()
        const second = tracker.change()
        first()
        first()

        const mayApply = tracker.read()
        expect(mayApply()).toBe(false)

        second()
        expect(tracker.read()()).toBe(true)
    })

    it('forgets the changes being saved and the reads started before a reset', () => {
        const tracker = createChangeTracker()
        const finish = tracker.change()
        const before = tracker.read()

        tracker.reset()
        const after = tracker.read()
        finish()

        expect(before()).toBe(false)
        expect(after()).toBe(true)
        expect(tracker.read()()).toBe(true)
    })
})
