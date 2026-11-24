import { afterEach, describe, expect, it, vi } from 'vitest'
import { captureScrollAnchor } from '../scrollAnchor'

/**
 * A content area at the top 50 with rows of 40 pixels, whose positions follow the scroll offset of
 * the area and the height of what was inserted above them.
 */
function setup(rowCount: number) {
    const state = { scrollTop: 0, inserted: 0, scrolls: true }
    const scroller = document.createElement('div')
    Object.defineProperty(scroller, 'scrollTop', {
        get: () => state.scrollTop,
        set: (value: number) => {
            if (state.scrolls) {
                state.scrollTop = value
            }
        },
    })
    scroller.getBoundingClientRect = () => ({ top: 50, bottom: 650 }) as DOMRect

    const container = document.createElement('div')
    const rows = Array.from({ length: rowCount }, (_, index) => {
        const row = document.createElement('div')
        row.className = 'row'
        row.id = `row-${index}`
        row.getBoundingClientRect = () => {
            const top = 50 + index * 40 + state.inserted - state.scrollTop
            return { top, bottom: top + 40 } as DOMRect
        }
        container.append(row)
        return row
    })
    scroller.append(container)
    document.body.append(scroller)
    return { state, scroller, container, rows }
}

afterEach(() => {
    document.body.replaceChildren()
})

describe('captureScrollAnchor', () => {
    it('keeps the first visible entry in place when entries are inserted above it', () => {
        const { state, scroller, container } = setup(10)
        state.scrollTop = 130

        const restore = captureScrollAnchor(container, '.row', scroller)
        // 100 rows of 40 pixels arrive above.
        state.inserted = 4000
        restore()

        expect(state.scrollTop).toBe(4130)
    })

    it('finds an entry that was rendered anew by its id', () => {
        const { state, scroller, container, rows } = setup(5)
        state.scrollTop = 0

        const restore = captureScrollAnchor(container, '.row', scroller)
        const replacement = document.createElement('div')
        replacement.id = 'row-0'
        replacement.getBoundingClientRect = () => {
            const top = 50 + 400 - state.scrollTop
            return { top, bottom: top + 40 } as DOMRect
        }
        rows[0]?.replaceWith(replacement)
        restore()

        expect(state.scrollTop).toBe(400)
    })

    it('lets the window take what the content area cannot', () => {
        const { state, scroller, container } = setup(3)
        state.scrolls = false
        const scrollBy = vi.spyOn(window, 'scrollBy').mockImplementation(() => {})

        const restore = captureScrollAnchor(container, '.row', scroller)
        state.inserted = 120
        restore()

        expect(scrollBy).toHaveBeenCalledWith(0, 120)
    })

    it('does nothing without an entry or a container', () => {
        const { scroller, container } = setup(0)

        expect(() => captureScrollAnchor(container, '.row', scroller)()).not.toThrow()
        expect(() => captureScrollAnchor(null, '.row', scroller)()).not.toThrow()
    })
})
