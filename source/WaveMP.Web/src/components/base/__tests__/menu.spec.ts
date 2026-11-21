import { describe, expect, it } from 'vitest'
import {
    clampMenuPosition,
    firstEnabledIndex,
    isMenuKey,
    isTypeaheadKey,
    lastEnabledIndex,
    layoutRect,
    layoutSize,
    menuAbovePosition,
    nextEnabledIndex,
    roomAbove,
    submenuPosition,
    typeaheadIndex,
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

describe('menuAbovePosition', () => {
    const viewport = { width: 800, height: 600 }
    const button = { left: 100, top: 400, right: 300, bottom: 432 }

    it('puts the menu right above the button, at its start', () => {
        expect(menuAbovePosition(button, { width: 200, height: 150 }, viewport)).toEqual({
            x: 100,
            y: 246,
            maxHeight: 392,
        })
    })

    it('keeps a long menu above the button and lets it scroll within the room', () => {
        expect(menuAbovePosition(button, { width: 200, height: 1000 }, viewport)).toEqual({
            x: 100,
            y: 4,
            maxHeight: 392,
        })
    })

    it('keeps the menu within the viewport sideways', () => {
        const right = { left: 700, top: 400, right: 790, bottom: 432 }
        expect(menuAbovePosition(right, { width: 200, height: 100 }, viewport).x).toBe(596)
        const left = { left: -20, top: 400, right: 80, bottom: 432 }
        expect(menuAbovePosition(left, { width: 200, height: 100 }, viewport).x).toBe(4)
    })

    it('has no room above a button at the top edge', () => {
        expect(roomAbove({ left: 0, top: 6, right: 10, bottom: 20 })).toBe(0)
        expect(roomAbove(button)).toBe(392)
    })
})

describe('typeaheadIndex', () => {
    const presets = [
        { label: 'Aurora' },
        { label: 'Nebula' },
        { label: 'Ember', disabled: true },
        { separator: true },
        { label: 'Eclipse' },
        { label: 'nebula drift' },
    ]

    it('moves to the next entry that starts with the typed letter, ignoring case', () => {
        expect(typeaheadIndex(presets, 0, 'n')).toBe(1)
        expect(typeaheadIndex(presets, 1, 'N')).toBe(5)
        expect(typeaheadIndex(presets, 5, 'n')).toBe(1)
    })

    it('skips disabled entries and separators', () => {
        expect(typeaheadIndex(presets, 0, 'e')).toBe(4)
    })

    it('stays on the current entry while a longer text still fits it', () => {
        expect(typeaheadIndex(presets, 1, 'neb')).toBe(1)
        expect(typeaheadIndex(presets, 1, 'nebula d')).toBe(5)
    })

    it('cycles through the entries with one letter when it is typed again and again', () => {
        expect(typeaheadIndex(presets, 1, 'nn')).toBe(5)
    })

    it('starts at the first entry when none is focused and finds nothing for other text', () => {
        expect(typeaheadIndex(presets, -1, 'a')).toBe(0)
        expect(typeaheadIndex(presets, 0, 'x')).toBe(-1)
        expect(typeaheadIndex(presets, 0, '')).toBe(-1)
        expect(typeaheadIndex([], 0, 'a')).toBe(-1)
    })
})

describe('isTypeaheadKey', () => {
    const key = (value: string, modifiers: Partial<KeyboardEvent> = {}) => ({
        key: value,
        ctrlKey: false,
        metaKey: false,
        altKey: false,
        ...modifiers,
    })

    it('takes printable characters without modifiers', () => {
        expect(isTypeaheadKey(key('a'))).toBe(true)
        expect(isTypeaheadKey(key('7'))).toBe(true)
    })

    it('leaves out special keys, the space and shortcuts', () => {
        expect(isTypeaheadKey(key('ArrowDown'))).toBe(false)
        expect(isTypeaheadKey(key(' '))).toBe(false)
        expect(isTypeaheadKey(key('a', { ctrlKey: true }))).toBe(false)
        expect(isTypeaheadKey(key('a', { metaKey: true }))).toBe(false)
        expect(isTypeaheadKey(key('a', { altKey: true }))).toBe(false)
    })
})
