import { describe, expect, it } from 'vitest'
import { applyVideoSettingsChange, defaultVideoSettings, toVideoSettings } from '../videoSettings'

describe('video settings', () => {
    it('show the ambient light and skip intro and outro until loaded, like the server', () => {
        expect(defaultVideoSettings).toEqual({ ambientMode: true, skipIntroAndOutro: true })
    })

    it('are read from an answer of the server', () => {
        expect(toVideoSettings({ ambientMode: false, skipIntroAndOutro: false })).toEqual({
            ambientMode: false,
            skipIntroAndOutro: false,
        })
        expect(toVideoSettings({ ambientMode: true, other: 1 })).toEqual({
            ambientMode: true,
            skipIntroAndOutro: true,
        })
    })

    it('are left out when the answer is incomplete', () => {
        expect(toVideoSettings(undefined)).toBeNull()
        expect(toVideoSettings(null)).toBeNull()
        expect(toVideoSettings({})).toBeNull()
        expect(toVideoSettings({ ambientMode: 'false' })).toBeNull()
    })

    it('apply a change like the server: missing values stay', () => {
        const settings = { ambientMode: true, skipIntroAndOutro: true }
        expect(applyVideoSettingsChange(settings, { ambientMode: false })).toEqual({
            ambientMode: false,
            skipIntroAndOutro: true,
        })
        expect(applyVideoSettingsChange(settings, { skipIntroAndOutro: false })).toEqual({
            ambientMode: true,
            skipIntroAndOutro: false,
        })
        expect(
            applyVideoSettingsChange(
                { ambientMode: false, skipIntroAndOutro: false },
                { ambientMode: null, skipIntroAndOutro: null },
            ),
        ).toEqual({ ambientMode: false, skipIntroAndOutro: false })
    })
})
