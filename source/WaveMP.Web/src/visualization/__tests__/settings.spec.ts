import { describe, expect, it } from 'vitest'
import {
    applyVisualizationChange,
    defaultVisualizationSettings,
    toVisualizationSettings,
    type VisualizationSettings,
} from '../settings'

const settings: VisualizationSettings = {
    module: 'Milkdrop',
    preset: 'Waves',
    autoChange: true,
    autoChangeSeconds: 45,
    quality: 'High',
}

describe('toVisualizationSettings', () => {
    it('takes over complete settings', () => {
        expect(toVisualizationSettings(settings)).toEqual(settings)
    })

    it('treats an empty preset as none', () => {
        expect(toVisualizationSettings({ ...settings, preset: '  ' })?.preset).toBeNull()
        expect(toVisualizationSettings({ ...settings, preset: null })?.preset).toBeNull()
    })

    it('takes over A-Force with its style', () => {
        const aforce = { ...settings, module: 'AForce', preset: 'aurora' } as const

        expect(toVisualizationSettings(aforce)).toEqual(aforce)
    })

    it.each([
        ['missing', undefined],
        ['unknown', 'Ultra'],
        ['not a name', 3],
    ])('takes Auto for a quality that is %s', (_, quality) => {
        expect(toVisualizationSettings({ ...settings, quality })?.quality).toBe('Auto')
    })

    it('falls back to the default module without a preset for an unknown module', () => {
        expect(toVisualizationSettings({ ...settings, module: 'Spectrum' })).toEqual({
            ...settings,
            module: defaultVisualizationSettings.module,
            preset: null,
        })
    })

    it.each([
        ['missing', undefined],
        ['without the automatic change', { ...settings, autoChange: undefined }],
        ['with too short an interval', { ...settings, autoChangeSeconds: 4 }],
        ['with too long an interval', { ...settings, autoChangeSeconds: 601 }],
        ['with a fractional interval', { ...settings, autoChangeSeconds: 30.5 }],
    ])('rejects settings %s', (_, value) => {
        expect(toVisualizationSettings(value)).toBeNull()
    })
})

describe('applyVisualizationChange', () => {
    it('changes only the given values', () => {
        expect(applyVisualizationChange(settings, { autoChange: false })).toEqual({
            ...settings,
            autoChange: false,
        })
        expect(applyVisualizationChange(settings, { autoChangeSeconds: null })).toEqual(settings)
    })

    it('forgets the preset for an empty name', () => {
        expect(applyVisualizationChange(settings, { preset: '' }).preset).toBeNull()
    })

    it('takes over a new preset', () => {
        expect(applyVisualizationChange(settings, { preset: 'Flames' }).preset).toBe('Flames')
    })

    it('changes the quality and keeps the rest', () => {
        expect(applyVisualizationChange(settings, { quality: 'Low' })).toEqual({
            ...settings,
            quality: 'Low',
        })
        expect(applyVisualizationChange(settings, { quality: null }).quality).toBe('High')
    })

    it('forgets the preset for another module unless the change names one', () => {
        expect(applyVisualizationChange(settings, { module: 'AForce' })).toEqual({
            ...settings,
            module: 'AForce',
            preset: null,
        })
        expect(
            applyVisualizationChange(settings, { module: 'AForce', preset: 'ember' }).preset,
        ).toBe('ember')
    })

    it('keeps the preset when the same module is named', () => {
        expect(applyVisualizationChange(settings, { module: 'Milkdrop' }).preset).toBe('Waves')
    })
})
