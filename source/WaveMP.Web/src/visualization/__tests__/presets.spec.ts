import { describe, expect, it } from 'vitest'
import type { VisualizationPreset } from '../module'
import { adjacentPreset, randomPreset, sortPresets, startPreset } from '../presets'

function presets(...names: string[]): VisualizationPreset[] {
    return names.map((name) => ({ id: name, name }))
}

const list = presets('A', 'B', 'C', 'D')

describe('sortPresets', () => {
    it('sorts by name, ignoring case and with numbers in their natural order', () => {
        expect(sortPresets(presets('b 10', 'B 9', 'a')).map((preset) => preset.name)).toEqual([
            'a',
            'B 9',
            'b 10',
        ])
    })

    it('leaves the given list unchanged', () => {
        const given = presets('b', 'a')

        sortPresets(given)

        expect(given.map((preset) => preset.name)).toEqual(['b', 'a'])
    })
})

describe('startPreset', () => {
    it('starts with the stored preset', () => {
        expect(startPreset(list, 'C', () => 0)?.id).toBe('C')
    })

    it('picks a random preset without a stored one or for an unknown one', () => {
        expect(startPreset(list, null, () => 0.6)?.id).toBe('C')
        expect(startPreset(list, 'Gone', () => 0)?.id).toBe('A')
    })

    it('finds nothing without presets', () => {
        expect(startPreset([], 'A', () => 0)).toBeUndefined()
    })
})

describe('randomPreset', () => {
    it('never repeats the current preset', () => {
        for (const value of [0, 0.3, 0.5, 0.99999]) {
            expect(randomPreset(list, 'A', () => value)?.id).not.toBe('A')
        }
        expect(randomPreset(list, 'A', () => 0)?.id).toBe('B')
        expect(randomPreset(list, 'A', () => 0.99999)?.id).toBe('D')
    })

    it('keeps the only preset', () => {
        expect(randomPreset(presets('A'), 'A', () => 0.5)?.id).toBe('A')
    })

    it('stays within the list for a random value of 1', () => {
        expect(randomPreset(list, null, () => 1)?.id).toBe('D')
    })

    it('finds nothing without presets', () => {
        expect(randomPreset([], null, () => 0)).toBeUndefined()
    })
})

describe('adjacentPreset', () => {
    it('steps through the list and wraps at both ends', () => {
        expect(adjacentPreset(list, 'B', 1)?.id).toBe('C')
        expect(adjacentPreset(list, 'B', -1)?.id).toBe('A')
        expect(adjacentPreset(list, 'D', 1)?.id).toBe('A')
        expect(adjacentPreset(list, 'A', -1)?.id).toBe('D')
    })

    it('starts at the first or last preset without a current one', () => {
        expect(adjacentPreset(list, null, 1)?.id).toBe('A')
        expect(adjacentPreset(list, 'Gone', -1)?.id).toBe('D')
    })

    it('finds nothing without presets', () => {
        expect(adjacentPreset([], 'A', 1)).toBeUndefined()
    })
})
