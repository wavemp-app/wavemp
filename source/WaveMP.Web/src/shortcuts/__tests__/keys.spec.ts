import { describe, expect, it } from 'vitest'
import { comboKeys, comboOf, isApplePlatform, normalizeCombo, type KeyInput } from '../keys'

function key(value: string, modifiers: Partial<Omit<KeyInput, 'key'>> = {}): KeyInput {
    return {
        key: value,
        ctrlKey: false,
        metaKey: false,
        altKey: false,
        shiftKey: false,
        ...modifiers,
    }
}

describe('comboOf', () => {
    it('names plain keys', () => {
        expect(comboOf(key(' '), false)).toBe('space')
        expect(comboOf(key('ArrowLeft'), false)).toBe('arrowleft')
        expect(comboOf(key('Down'), false)).toBe('arrowdown')
        expect(comboOf(key('m'), false)).toBe('m')
    })

    it('matches letters regardless of Shift and Caps Lock', () => {
        expect(comboOf(key('M'), false)).toBe('m')
        expect(comboOf(key('M', { shiftKey: true }), false)).toBe('m')
    })

    it('takes characters that need Shift by the character', () => {
        // ? is Shift+/ on a US keyboard and Shift+ß on a German one.
        expect(comboOf(key('?', { shiftKey: true }), false)).toBe('?')
    })

    it('keeps Shift for named keys', () => {
        expect(comboOf(key('ArrowLeft', { shiftKey: true }), false)).toBe('shift+arrowleft')
    })

    it('uses Ctrl as main modifier, and Command on Apple devices', () => {
        expect(comboOf(key('ArrowLeft', { ctrlKey: true }), false)).toBe('mod+arrowleft')
        expect(comboOf(key('ArrowLeft', { metaKey: true }), true)).toBe('mod+arrowleft')
        expect(comboOf(key('f', { ctrlKey: true, altKey: true }), false)).toBe('mod+alt+f')
    })

    it('ignores the modifier of the other platform', () => {
        expect(comboOf(key('ArrowLeft', { metaKey: true }), false)).toBeNull()
        expect(comboOf(key('ArrowLeft', { ctrlKey: true }), true)).toBeNull()
    })

    it('ignores modifiers on their own and keys without a name', () => {
        expect(comboOf(key('Control', { ctrlKey: true }), false)).toBeNull()
        expect(comboOf(key('Shift', { shiftKey: true }), false)).toBeNull()
        expect(comboOf(key('Dead'), false)).toBeNull()
        expect(comboOf(key('Unidentified'), false)).toBeNull()
    })
})

describe('normalizeCombo', () => {
    it('writes the modifiers in their order and in lower case', () => {
        expect(normalizeCombo('Shift+Alt+Mod+ArrowLeft')).toBe('mod+alt+shift+arrowleft')
        expect(normalizeCombo('Space')).toBe('space')
    })

    it('drops Shift of a character', () => {
        expect(normalizeCombo('shift+?')).toBe('?')
    })

    it('rejects unknown modifiers and missing keys', () => {
        expect(() => normalizeCombo('ctrl+f')).toThrow(/Unknown modifier/)
        expect(() => normalizeCombo('mod+')).toThrow(/no key/)
    })
})

describe('comboKeys', () => {
    it('names the keys for the overview', () => {
        expect(comboKeys('mod+arrowleft', false)).toEqual([
            { key: 'mod', labelKey: 'shortcuts.keys.ctrl' },
            { key: 'arrowleft', labelKey: 'shortcuts.keys.left' },
        ])
        expect(comboKeys('mod+f', true)).toEqual([
            { key: 'mod', labelKey: 'shortcuts.keys.command' },
            { key: 'f', text: 'F' },
        ])
        expect(comboKeys('?', false)).toEqual([{ key: '?', text: '?' }])
        expect(comboKeys('space', false)).toEqual([
            { key: 'space', labelKey: 'shortcuts.keys.space' },
        ])
        expect(comboKeys('home', false)).toEqual([{ key: 'home', text: 'Home' }])
    })

    it('names the minus key of Ctrl+Minus', () => {
        expect(comboOf(key('-', { ctrlKey: true }), false)).toBe('mod+-')
        expect(comboOf(key('-', { metaKey: true }), true)).toBe('mod+-')
        expect(comboKeys('mod+-', false)).toEqual([
            { key: 'mod', labelKey: 'shortcuts.keys.ctrl' },
            { key: '-', labelKey: 'shortcuts.keys.minus' },
        ])
    })
})

describe('isApplePlatform', () => {
    it('recognizes Macs, iPhones and iPads', () => {
        expect(isApplePlatform({ platform: 'MacIntel' })).toBe(true)
        expect(
            isApplePlatform({ platform: '', userAgent: 'Mozilla/5.0 (iPad; CPU OS 17_0)' }),
        ).toBe(true)
        expect(
            isApplePlatform({ platform: 'Win32', userAgent: 'Mozilla/5.0 (Windows NT 10.0)' }),
        ).toBe(false)
        expect(isApplePlatform({ platform: 'Linux x86_64' })).toBe(false)
    })
})
