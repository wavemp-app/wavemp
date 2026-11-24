import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import type { ArtistPictureSettings } from '@/api/generated/models'
import { createWaveMpI18n } from '@/i18n'

const api = vi.hoisted(() => ({
    getArtistPictureSettings: vi.fn(),
    updateArtistPictureSettings: vi.fn(),
}))

vi.mock('@/library/artistPictureApi', () => api)

const ArtistPictureSettingsForm = (await import('../ArtistPictureSettingsForm.vue')).default

const settings: ArtistPictureSettings = { onlineLookupEnabled: false, fanartTvApiKeySet: false }

async function mountForm() {
    const wrapper = mount(ArtistPictureSettingsForm, {
        global: { plugins: [createWaveMpI18n('en')] },
        attachTo: document.body,
    })
    await flushPromises()
    return wrapper
}

type Wrapper = Awaited<ReturnType<typeof mountForm>>

function input(wrapper: Wrapper, field: string) {
    return wrapper.get(`[data-part="field"][data-field="${field}"] input`)
}

async function submit(wrapper: Wrapper) {
    await wrapper.get('form').trigger('submit')
    await flushPromises()
}

describe('ArtistPictureSettingsForm', () => {
    beforeEach(() => {
        api.getArtistPictureSettings.mockReset().mockResolvedValue(settings)
        api.updateArtistPictureSettings.mockReset()
    })

    it('shows the lookup switched off, no key and what the lookup sends', async () => {
        const wrapper = await mountForm()

        const enabled = input(wrapper, 'artist-pictures-online')
        expect((enabled.element as HTMLInputElement).checked).toBe(false)
        const key = input(wrapper, 'fanart-tv-key')
        expect(key.attributes('type')).toBe('password')
        expect(key.attributes('autocomplete')).toBe('new-password')
        expect((key.element as HTMLInputElement).value).toBe('')
        expect(wrapper.get('[data-item="fanart-tv-key-state"]').text()).toBe('No key saved.')
        expect(wrapper.find('button[data-item="remove-fanart-tv-key"]').exists()).toBe(false)
        expect(wrapper.get('[data-item="artist-pictures-privacy"]').text()).toContain('MusicBrainz')
        wrapper.unmount()
    })

    it('switches the lookup on and sends only that', async () => {
        api.updateArtistPictureSettings.mockResolvedValue({
            ...settings,
            onlineLookupEnabled: true,
        })
        const wrapper = await mountForm()

        await input(wrapper, 'artist-pictures-online').setValue(true)
        await submit(wrapper)

        expect(api.updateArtistPictureSettings).toHaveBeenCalledWith({ onlineLookupEnabled: true })
        expect(wrapper.get('[data-part="alert"][data-state="success"]').text()).toBe(
            'The settings of the artist pictures were saved.',
        )
        wrapper.unmount()
    })

    it('saves a typed key, never shows it and only tells that one is saved', async () => {
        api.updateArtistPictureSettings.mockResolvedValue({ ...settings, fanartTvApiKeySet: true })
        const wrapper = await mountForm()

        await input(wrapper, 'fanart-tv-key').setValue('  abcDEF123  ')
        await submit(wrapper)

        expect(api.updateArtistPictureSettings).toHaveBeenCalledWith({
            fanartTvApiKey: 'abcDEF123',
        })
        const key = input(wrapper, 'fanart-tv-key')
        expect((key.element as HTMLInputElement).value).toBe('')
        expect(key.attributes('placeholder')).toBe('Key saved')
        expect(wrapper.html()).not.toContain('abcDEF123')
        expect(wrapper.get('[data-item="fanart-tv-key-state"]').text()).toBe('A key is saved.')
        wrapper.unmount()
    })

    it('removes the saved key', async () => {
        api.getArtistPictureSettings.mockResolvedValue({ ...settings, fanartTvApiKeySet: true })
        api.updateArtistPictureSettings.mockResolvedValue(settings)
        const wrapper = await mountForm()

        await wrapper.get('button[data-item="remove-fanart-tv-key"]').trigger('click')
        await flushPromises()

        expect(api.updateArtistPictureSettings).toHaveBeenCalledWith({ fanartTvApiKey: '' })
        expect(wrapper.get('[data-part="alert"][data-state="success"]').text()).toBe(
            'The key was removed.',
        )
        expect(wrapper.get('[data-item="fanart-tv-key-state"]').text()).toBe('No key saved.')
        wrapper.unmount()
    })

    it('checks the format of the key before sending it and shows errors of the server', async () => {
        const wrapper = await mountForm()

        await input(wrapper, 'fanart-tv-key').setValue('not a key!')
        await submit(wrapper)
        expect(api.updateArtistPictureSettings).not.toHaveBeenCalled()
        expect(wrapper.get('[data-field="fanart-tv-key"] [data-part="field-error"]').text()).toBe(
            'Enter the key as 1 to 64 letters (A to Z) or digits.',
        )

        api.updateArtistPictureSettings.mockRejectedValue({
            responseStatusCode: 400,
            code: 'request.invalid',
            errors: [{ field: 'fanartTvApiKey', code: 'invalid_format' }],
        })
        await input(wrapper, 'fanart-tv-key').setValue('abc')
        await submit(wrapper)
        expect(wrapper.get('[data-field="fanart-tv-key"] [data-part="field-error"]').text()).toBe(
            'Enter the key as 1 to 64 letters (A to Z) or digits.',
        )

        api.updateArtistPictureSettings.mockRejectedValue(new Error('offline'))
        await submit(wrapper)
        expect(wrapper.get('[data-part="alert"][data-state="error"]').text()).toBe(
            'The settings could not be saved.',
        )
        wrapper.unmount()
    })

    it('offers to load the settings again after an error', async () => {
        api.getArtistPictureSettings.mockRejectedValueOnce(new Error('offline'))
        const wrapper = await mountForm()

        expect(wrapper.get('[data-part="alert"]').text()).toBe(
            'The settings of the artist pictures could not be loaded.',
        )
        await wrapper.get('button[data-item="retry"]').trigger('click')
        await flushPromises()
        expect(wrapper.find('form').exists()).toBe(true)
        wrapper.unmount()
    })
})
