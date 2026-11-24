import { afterEach, describe, expect, it, vi } from 'vitest'
import {
    afterTransition,
    firstVisiblePosition,
    groupOverviewBlocked,
    transitionTime,
} from '../groupJump'

const nothingOpen = { fullPlayerExpanded: false, compactInline: false }

function keyEvent(target: EventTarget): Pick<Event, 'target'> {
    return { target }
}

afterEach(() => {
    document.body.replaceChildren()
})

describe('groupOverviewBlocked', () => {
    it('lets the key open the overview while nothing else is open', () => {
        const button = document.createElement('button')
        document.body.append(button)

        expect(groupOverviewBlocked(keyEvent(button), nothingOpen)).toBe(false)
        expect(groupOverviewBlocked(keyEvent(document.body), nothingOpen)).toBe(false)
    })

    it('leaves the key to the full-screen player and the mini player in place of the app', () => {
        expect(
            groupOverviewBlocked(keyEvent(document.body), {
                ...nothingOpen,
                fullPlayerExpanded: true,
            }),
        ).toBe(true)
        expect(
            groupOverviewBlocked(keyEvent(document.body), { ...nothingOpen, compactInline: true }),
        ).toBe(true)
    })

    it('leaves the key to the open drawer', () => {
        const sidebar = document.createElement('aside')
        sidebar.dataset.part = 'sidebar'
        sidebar.dataset.state = 'closed expanded'
        document.body.append(sidebar)
        expect(groupOverviewBlocked(keyEvent(document.body), nothingOpen)).toBe(false)

        sidebar.dataset.state = 'open expanded'
        expect(groupOverviewBlocked(keyEvent(document.body), nothingOpen)).toBe(true)
    })

    it('leaves the key to an open dialog', () => {
        const dialog = document.createElement('dialog')
        dialog.setAttribute('open', '')
        document.body.append(dialog)

        expect(groupOverviewBlocked(keyEvent(document.body), nothingOpen)).toBe(true)
    })

    it('leaves keys of another document (the floating mini player) alone', () => {
        const other = document.implementation.createHTMLDocument('mini player')
        const button = other.createElement('button')
        other.body.append(button)

        expect(groupOverviewBlocked(keyEvent(button), nothingOpen)).toBe(true)
    })
})

describe('transitionTime', () => {
    it('is the longest transition with its delay', () => {
        const element = document.createElement('div')
        element.style.transitionDuration = '180ms, 0.25s'
        element.style.transitionDelay = '0s, 50ms'
        document.body.append(element)

        expect(transitionTime(element)).toBe(300)
    })

    it('is 0 without transitions', () => {
        const element = document.createElement('div')
        document.body.append(element)

        expect(transitionTime(element)).toBe(0)
    })
})

describe('afterTransition', () => {
    it('resolves at once without transitions', async () => {
        await expect(afterTransition(document.createElement('div'))).resolves.toBeUndefined()
        await expect(afterTransition(null)).resolves.toBeUndefined()
    })

    it('resolves when the transition of the element ends, at the latest after its time', async () => {
        vi.useFakeTimers()
        try {
            const element = document.createElement('div')
            element.style.transitionDuration = '200ms'
            document.body.append(element)

            let done = false
            void afterTransition(element).then(() => (done = true))
            await vi.advanceTimersByTimeAsync(100)
            expect(done).toBe(false)

            element.dispatchEvent(new Event('transitionend'))
            await vi.advanceTimersByTimeAsync(0)
            expect(done).toBe(true)

            let late = false
            void afterTransition(element).then(() => (late = true))
            await vi.advanceTimersByTimeAsync(260)
            expect(late).toBe(true)
        } finally {
            vi.useRealTimers()
        }
    })
})

function groupAt(position: number, top: number, bottom: number): HTMLElement {
    const element = document.createElement('div')
    element.dataset.position = String(position)
    element.getBoundingClientRect = () => ({ top, bottom }) as DOMRect
    return element
}

describe('firstVisiblePosition', () => {
    it('is the position of the first group that reaches below the top of the content area', () => {
        const scroller = document.createElement('main')
        scroller.getBoundingClientRect = () => ({ top: 100, bottom: 900 }) as DOMRect
        const groups = [groupAt(0, -500, 40), groupAt(12, 40, 300), groupAt(30, 300, 700)]

        expect(firstVisiblePosition(groups, scroller)).toBe(12)
    })

    it('is undefined without groups', () => {
        expect(firstVisiblePosition([], null)).toBeUndefined()
    })
})
