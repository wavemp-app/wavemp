import { describe, expect, it } from 'vitest'
import {
    applyVisualizationChange,
    defaultMilkdropSpeed,
    defaultVisualizationSettings,
    explicitVisualizationChange,
    toVisualizationSettings,
    type VisualizationSettings,
} from '../settings'
import { mergeChanges } from '@/session/settingSaver'

const settings: VisualizationSettings = {
    module: 'Milkdrop',
    preset: 'Waves',
    autoChange: true,
    autoChangeSeconds: 45,
    quality: 'High',
    colorScheme: null,
    intensity: 'Reduced',
    milkdropSpeed: 75,
}

const aforce: VisualizationSettings = {
    ...settings,
    module: 'AForce',
    preset: 'rings',
    colorScheme: 'ember',
}

describe('toVisualizationSettings', () => {
    it('takes over complete settings', () => {
        expect(toVisualizationSettings(settings)).toEqual(settings)
    })

    it('treats an empty preset as none', () => {
        expect(toVisualizationSettings({ ...settings, preset: '  ' })?.preset).toBeNull()
        expect(toVisualizationSettings({ ...settings, preset: null })?.preset).toBeNull()
    })

    it('takes over A-Force with its mode and color scheme', () => {
        expect(toVisualizationSettings(aforce)).toEqual(aforce)
    })

    it('treats an empty color scheme as none', () => {
        expect(toVisualizationSettings({ ...aforce, colorScheme: ' ' })?.colorScheme).toBeNull()
        expect(toVisualizationSettings({ ...aforce, colorScheme: null })?.colorScheme).toBeNull()
        expect(toVisualizationSettings({ ...aforce, colorScheme: 4 })?.colorScheme).toBeNull()
    })

    it.each([
        ['missing', undefined],
        ['unknown', 'Extreme'],
        ['not a name', 1],
    ])('takes Normal for an intensity that is %s', (_, intensity) => {
        expect(toVisualizationSettings({ ...settings, intensity })?.intensity).toBe('Normal')
    })

    it.each([
        ['missing', undefined],
        ['too slow', 9],
        ['too fast', 101],
        ['fractional', 50.5],
        ['not a number', '50'],
    ])('takes the default speed for a Milkdrop speed that is %s', (_, milkdropSpeed) => {
        expect(toVisualizationSettings({ ...settings, milkdropSpeed })?.milkdropSpeed).toBe(
            defaultMilkdropSpeed,
        )
    })

    it.each([10, 25, 100])('takes over the Milkdrop speed %i', (milkdropSpeed) => {
        expect(toVisualizationSettings({ ...settings, milkdropSpeed })?.milkdropSpeed).toBe(
            milkdropSpeed,
        )
    })

    it('has calm defaults', () => {
        expect(defaultVisualizationSettings).toMatchObject({
            colorScheme: null,
            intensity: 'Normal',
            milkdropSpeed: 50,
        })
    })

    it.each([
        ['missing', undefined],
        ['unknown', 'Ultra'],
        ['not a name', 3],
    ])('takes Auto for a quality that is %s', (_, quality) => {
        expect(toVisualizationSettings({ ...settings, quality })?.quality).toBe('Auto')
    })

    it('falls back to the default module without preset and color scheme for an unknown module', () => {
        expect(toVisualizationSettings({ ...aforce, module: 'Spectrum' })).toEqual({
            ...aforce,
            module: defaultVisualizationSettings.module,
            preset: null,
            colorScheme: null,
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

    it('forgets the color scheme for another module unless the change names one', () => {
        expect(applyVisualizationChange(aforce, { module: 'BlackCap' })).toEqual({
            ...aforce,
            module: 'BlackCap',
            preset: null,
            colorScheme: null,
        })
        expect(
            applyVisualizationChange(aforce, { module: 'BlackCap', colorScheme: 'tide' })
                .colorScheme,
        ).toBe('tide')
    })

    it('keeps the color scheme for the same module and another mode', () => {
        expect(applyVisualizationChange(aforce, { module: 'AForce', preset: 'warp' })).toEqual({
            ...aforce,
            preset: 'warp',
        })
    })

    it('takes over and forgets a color scheme', () => {
        expect(applyVisualizationChange(aforce, { colorScheme: 'prism' }).colorScheme).toBe('prism')
        expect(applyVisualizationChange(aforce, { colorScheme: '' }).colorScheme).toBeNull()
        expect(applyVisualizationChange(aforce, { colorScheme: null }).colorScheme).toBe('ember')
    })

    it('changes the intensity and the Milkdrop speed and keeps the rest', () => {
        expect(
            applyVisualizationChange(settings, { intensity: 'Normal', milkdropSpeed: 25 }),
        ).toEqual({ ...settings, intensity: 'Normal', milkdropSpeed: 25 })
        expect(
            applyVisualizationChange(settings, { intensity: null, milkdropSpeed: null }),
        ).toEqual(settings)
    })
})

describe('explicitVisualizationChange', () => {
    it('forgets the preset for another module unless the change names one', () => {
        expect(explicitVisualizationChange(settings, { module: 'AForce' })).toEqual({
            module: 'AForce',
            preset: '',
            colorScheme: '',
        })
        expect(
            explicitVisualizationChange(settings, {
                module: 'AForce',
                preset: 'rings',
                colorScheme: 'ember',
            }),
        ).toEqual({ module: 'AForce', preset: 'rings', colorScheme: 'ember' })
    })

    it('leaves other changes as they are', () => {
        expect(explicitVisualizationChange(settings, { module: 'Milkdrop' })).toEqual({
            module: 'Milkdrop',
        })
        expect(explicitVisualizationChange(settings, { quality: 'Low' })).toEqual({
            quality: 'Low',
        })
        expect(explicitVisualizationChange(aforce, { colorScheme: 'prism' })).toEqual({
            colorScheme: 'prism',
        })
    })

    it('has the effect of two changes one after the other when merged', () => {
        const first = explicitVisualizationChange(settings, { preset: 'Flames' })
        const between = applyVisualizationChange(settings, first)
        const second = explicitVisualizationChange(between, { module: 'AForce' })

        expect(applyVisualizationChange(settings, mergeChanges(first, second))).toEqual(
            applyVisualizationChange(between, second),
        )
    })

    it('forgets a color scheme chosen before a change of the module when merged', () => {
        const first = explicitVisualizationChange(aforce, { colorScheme: 'prism' })
        const between = applyVisualizationChange(aforce, first)
        const second = explicitVisualizationChange(between, { module: 'BlackCap' })
        const merged = applyVisualizationChange(aforce, mergeChanges(first, second))

        expect(merged).toEqual(applyVisualizationChange(between, second))
        expect(merged.colorScheme).toBeNull()
    })
})
