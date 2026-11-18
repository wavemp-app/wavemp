import { describe, expect, it } from 'vitest'
import {
    applyThemeChange,
    defaultThemeChoice,
    fromApiAppearance,
    isValidThemeId,
    parseThemeChoice,
    toApiAppearance,
    toThemeChoice,
    toUpdateThemeSettings,
} from '../choice'

describe('theme choice', () => {
    it('starts with Midnight and the mode of the device', () => {
        expect(defaultThemeChoice).toEqual({ id: 'midnight', appearance: 'system' })
    })

    it('accepts the ids the API accepts', () => {
        for (const id of ['midnight', 'clear-glass', 'uploaded-1']) {
            expect(isValidThemeId(id), id).toBe(true)
        }
        for (const id of [
            '',
            'Clear Glass',
            'clear--glass',
            '-a',
            'a-',
            '../x',
            'a'.repeat(65),
            7,
        ]) {
            expect(isValidThemeId(id), String(id)).toBe(false)
        }
    })

    it('maps the appearance of the API', () => {
        expect(fromApiAppearance('System')).toBe('system')
        expect(fromApiAppearance('Light')).toBe('light')
        expect(fromApiAppearance('Dark')).toBe('dark')
        expect(fromApiAppearance('Sepia')).toBeNull()
        expect(fromApiAppearance(undefined)).toBeNull()
        expect(toApiAppearance('system')).toBe('System')
        expect(toApiAppearance('light')).toBe('Light')
        expect(toApiAppearance('dark')).toBe('Dark')
    })

    it('reads the theme of the settings, also an id the web app does not know', () => {
        expect(toThemeChoice({ id: 'accent', appearance: 'Dark' })).toEqual({
            id: 'accent',
            appearance: 'dark',
        })
        expect(toThemeChoice({ id: 'uploaded-1', appearance: 'System' })?.id).toBe('uploaded-1')
        expect(toThemeChoice({ id: 'accent' })).toBeNull()
        expect(toThemeChoice({ id: 'Bad Id', appearance: 'Dark' })).toBeNull()
        expect(toThemeChoice(null)).toBeNull()
    })

    it('reads a remembered theme', () => {
        expect(parseThemeChoice({ id: 'meadow', appearance: 'light' })).toEqual({
            id: 'meadow',
            appearance: 'light',
        })
        expect(parseThemeChoice({ id: 'meadow', appearance: 'Light' })).toBeNull()
        expect(parseThemeChoice('meadow')).toBeNull()
        expect(parseThemeChoice(null)).toBeNull()
    })

    it('applies a partial change', () => {
        const choice = { id: 'accent', appearance: 'dark' } as const

        expect(applyThemeChange(choice, { id: 'meadow' })).toEqual({
            id: 'meadow',
            appearance: 'dark',
        })
        expect(applyThemeChange(choice, { appearance: 'system' })).toEqual({
            id: 'accent',
            appearance: 'system',
        })
    })

    it('sends only the changed members', () => {
        expect(toUpdateThemeSettings({ id: 'meadow' })).toEqual({ id: 'meadow' })
        expect(toUpdateThemeSettings({ appearance: 'dark' })).toEqual({ appearance: 'Dark' })
        expect(toUpdateThemeSettings({ id: 'accent', appearance: 'light' })).toEqual({
            id: 'accent',
            appearance: 'Light',
        })
    })
})
