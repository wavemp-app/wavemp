import { describe, expect, it } from 'vitest'
import {
    clampMenuPosition,
    firstEnabledIndex,
    isMenuKey,
    lastEnabledIndex,
    layoutRect,
    layoutSize,
    nextEnabledIndex,
    submenuPosition,
} from '../menu'

const entries = [{ disabled: true }, {}, { separator: true }, {}, { disabled: true }]

describe('nextEnabledIndex', () => {
    it('skips disabled entries and separators', () => {
        expect(nextEnabledIndex(1, 1, entries)).toBe(3)
        expect(nextEnabledIndex(3, -1, entries)).toBe(1)
    })

    it('wraps around at the ends', () => {
        expect(nextEnabledIndex(3, 1, entries)).toBe(1)
        expect(nextEnabledIndex(1, -1, entries)).toBe(3)
    })

    it('returns -1 if no entry can be focused', () => {
        expect(nextEnabledIndex(0, 1, [{ disabled: true }, { separator: true }])).toBe(-1)
        expect(nextEnabledIndex(0, 1, [])).toBe(-1)
    })

    it('finds the first and the last focusable entry', () => {
        expect(firstEnabledIndex(entries)).toBe(1)
        expect(lastEnabledIndex(entries)).toBe(3)
    })
})

describe('clampMenuPosition', () => {
    const size = { width: 200, height: 100 }
    const viewport = { width: 800, height: 600 }

    it('keeps the point if the menu fits', () => {
        expect(clampMenuPosition({ x: 100, y: 100 }, size, viewport)).toEqual({ x: 100, y: 100 })
    })

    it('moves the menu back into the viewport', () => {
        expect(clampMenuPosition({ x: 750, y: 580 }, size, viewport)).toEqual({ x: 596, y: 496 })
        expect(clampMenuPosition({ x: -20, y: 2 }, size, viewport)).toEqual({ x: 4, y: 4 })
    })

    it('keeps the margin if the menu is larger than the viewport', () => {
        expect(clampMenuPosition({ x: 50, y: 50 }, { width: 900, height: 700 }, viewport)).toEqual({
            x: 4,
            y: 4,
        })
    })
})

describe('submenuPosition', () => {
    const size = { width: 150, height: 120 }
    const viewport = { width: 800, height: 600 }
    const menu = { left: 100, top: 100, right: 300, bottom: 400 }

    it('opens on the right of the menu at the height of the entry', () => {
        const entry = { left: 104, top: 180, right: 296, bottom: 212 }
        expect(submenuPosition(entry, menu, size, viewport)).toEqual({ x: 300, y: 180 })
    })

    it('opens on the left where the right side has no room', () => {
        const wide = { left: 500, top: 100, right: 700, bottom: 400 }
        const entry = { left: 504, top: 180, right: 696, bottom: 212 }
        expect(submenuPosition(entry, wide, size, viewport)).toEqual({ x: 350, y: 180 })
    })

    it('stays within the viewport', () => {
        const entry = { left: 104, top: 560, right: 296, bottom: 592 }
        expect(submenuPosition(entry, menu, size, viewport)).toEqual({ x: 300, y: 476 })
    })
})

describe('isMenuKey', () => {
    it('knows the menu key and Shift+F10', () => {
        expect(isMenuKey({ key: 'ContextMenu', shiftKey: false })).toBe(true)
        expect(isMenuKey({ key: 'F10', shiftKey: true })).toBe(true)
        expect(isMenuKey({ key: 'F10', shiftKey: false })).toBe(false)
        expect(isMenuKey({ key: 'Enter', shiftKey: true })).toBe(false)
    })
})

describe('layoutSize', () => {
    it('takes the size of the layout, not the scaled box of a menu that is still growing', () => {
        const element = { offsetWidth: 200, offsetHeight: 300 }
        expect(layoutSize(element)).toEqual({ width: 200, height: 300 })
    })
})

describe('layoutRect', () => {
    it('places the size at the top left corner', () => {
        expect(layoutRect({ x: 100, y: 40 }, { width: 200, height: 300 })).toEqual({
            left: 100,
            top: 40,
            right: 300,
            bottom: 340,
        })
    })
})
