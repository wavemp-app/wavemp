import { describe, expect, it } from 'vitest'
import {
    choose,
    chooseAll,
    chooseForMenu,
    emptySelection,
    prune,
    selectionMode,
} from '../selection'

const order = ['a', 'b', 'c', 'd', 'e']

describe('selectionMode', () => {
    it('reads the modifier keys of a click or key press', () => {
        expect(selectionMode({})).toBe('only')
        expect(selectionMode({ ctrlKey: true })).toBe('toggle')
        expect(selectionMode({ metaKey: true })).toBe('toggle')
        expect(selectionMode({ shiftKey: true, ctrlKey: true })).toBe('range')
    })
})

describe('choose', () => {
    it('selects one row and makes it the anchor', () => {
        expect(choose(emptySelection, order, 'c', 'only')).toEqual({ ids: ['c'], anchor: 'c' })
    })

    it('adds and removes rows with Ctrl and keeps the order of the list', () => {
        let selection = choose(emptySelection, order, 'd', 'only')
        selection = choose(selection, order, 'b', 'toggle')
        expect(selection).toEqual({ ids: ['b', 'd'], anchor: 'b' })

        selection = choose(selection, order, 'd', 'toggle')
        expect(selection).toEqual({ ids: ['b'], anchor: 'd' })
    })

    it('selects the range from the anchor with Shift, in both directions', () => {
        const selection = choose(emptySelection, order, 'b', 'only')

        expect(choose(selection, order, 'd', 'range')).toEqual({
            ids: ['b', 'c', 'd'],
            anchor: 'b',
        })
        const back = choose(choose(selection, order, 'e', 'range'), order, 'a', 'range')
        expect(back).toEqual({ ids: ['a', 'b'], anchor: 'b' })
    })

    it('selects one row with Shift before there is an anchor', () => {
        expect(choose(emptySelection, order, 'c', 'range')).toEqual({ ids: ['c'], anchor: 'c' })
    })

    it('ignores rows that are not in the list', () => {
        const selection = choose(emptySelection, order, 'a', 'only')
        expect(choose(selection, order, 'x', 'toggle')).toBe(selection)
    })
})

describe('chooseAll and chooseForMenu', () => {
    it('selects all rows', () => {
        expect(chooseAll(emptySelection, order)).toEqual({ ids: order, anchor: 'a' })
    })

    it('keeps the selection for the menu of a selected row, else selects the row alone', () => {
        const selection = { ids: ['b', 'c'], anchor: 'b' }

        expect(chooseForMenu(selection, order, 'c')).toBe(selection)
        expect(chooseForMenu(selection, order, 'e')).toEqual({ ids: ['e'], anchor: 'e' })
    })
})

describe('prune', () => {
    it('leaves out rows that left the list', () => {
        const selection = { ids: ['b', 'c', 'd'], anchor: 'd' }

        expect(prune(selection, order)).toBe(selection)
        expect(prune(selection, ['a', 'c', 'e'])).toEqual({ ids: ['c'], anchor: undefined })
    })
})
