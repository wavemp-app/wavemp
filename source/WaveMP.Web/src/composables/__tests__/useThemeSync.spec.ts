import { describe, expect, it } from 'vitest'
import { shownTheme } from '../useThemeSync'

describe('shownTheme', () => {
    it('shows the theme of the profile in the variant of its appearance', () => {
        expect(shownTheme({ id: 'accent', appearance: 'dark' }, false, '')).toEqual({
            id: 'accent',
            variant: 'dark',
        })
        expect(shownTheme({ id: 'clear-glass', appearance: 'system' }, true, '')).toEqual({
            id: 'clear-glass',
            variant: 'dark',
        })
    })

    it('shows the default theme for an unknown id', () => {
        expect(shownTheme({ id: 'uploaded-1', appearance: 'light' }, false, '')).toEqual({
            id: 'midnight',
            variant: 'dark',
        })
    })

    it('shows the theme of a preview server with the mode of the device', () => {
        expect(shownTheme({ id: 'midnight', appearance: 'light' }, true, 'accent')).toEqual({
            id: 'accent',
            variant: 'dark',
        })
        expect(shownTheme({ id: 'midnight', appearance: 'dark' }, false, 'clear-glass')).toEqual({
            id: 'clear-glass',
            variant: 'light',
        })
    })
})
