import { describe, expect, it } from 'vitest'
import { applyVideoSettingsChange, defaultVideoSettings, toVideoSettings } from '../videoSettings'

describe('video settings', () => {
    it('show the ambient light until the settings are loaded, like the server', () => {
        expect(defaultVideoSettings).toEqual({ ambientMode: true })
    })

    it('are read from an answer of the server', () => {
        expect(toVideoSettings({ ambientMode: false })).toEqual({ ambientMode: false })
        expect(toVideoSettings({ ambientMode: true, other: 1 })).toEqual({ ambientMode: true })
    })

    it('are left out when the answer is incomplete', () => {
        expect(toVideoSettings(undefined)).toBeNull()
        expect(toVideoSettings(null)).toBeNull()
        expect(toVideoSettings({})).toBeNull()
        expect(toVideoSettings({ ambientMode: 'false' })).toBeNull()
    })

    it('apply a change like the server: missing values stay', () => {
        expect(applyVideoSettingsChange({ ambientMode: true }, { ambientMode: false })).toEqual({
            ambientMode: false,
        })
        expect(applyVideoSettingsChange({ ambientMode: false }, {})).toEqual({
            ambientMode: false,
        })
        expect(applyVideoSettingsChange({ ambientMode: false }, { ambientMode: null })).toEqual({
            ambientMode: false,
        })
    })
})
