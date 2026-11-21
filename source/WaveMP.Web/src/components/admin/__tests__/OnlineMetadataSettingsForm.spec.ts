import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import type { OnlineMetadataSettings } from '@/api/generated/models'
import { createWaveMpI18n } from '@/i18n'

const api = vi.hoisted(() => ({
    getOnlineMetadataSettings: vi.fn(),
    updateOnlineMetadataSettings: vi.fn(),
}))

vi.mock('@/library/onlineMetadata', () => api)

const OnlineMetadataSettingsForm = (await import('../OnlineMetadataSettingsForm.vue')).default

const settings: OnlineMetadataSettings = {
    enabled: false,
    musicBrainzUrl: 'https://musicbrainz.org',
    coverArtArchiveUrl: 'https://coverartarchive.org',
    musicBrainzRequestIntervalMilliseconds: 1000,
}

async function mountForm() {
    const wrapper = mount(OnlineMetadataSettingsForm, {
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

describe('OnlineMetadataSettingsForm', () => {
    beforeEach(() => {
        api.getOnlineMetadataSettings.mockReset().mockResolvedValue(settings)
        api.updateOnlineMetadataSettings.mockReset()
    })

    it('shows the settings, off by default', async () => {
        const wrapper = await mountForm()

        expect(
            (input(wrapper, 'online-metadata-enabled').element as HTMLInputElement).checked,
        ).toBe(false)
        expect((input(wrapper, 'musicbrainz-url').element as HTMLInputElement).value).toBe(
            'https://musicbrainz.org',
        )
        expect((input(wrapper, 'cover-art-archive-url').element as HTMLInputElement).value).toBe(
            'https://coverartarchive.org',
        )
        expect((input(wrapper, 'musicbrainz-interval').element as HTMLInputElement).value).toBe(
            '1000',
        )
        expect(wrapper.text()).toContain('WaveMP sends no requests to either service')
        wrapper.unmount()
    })

    it('saves the changed settings', async () => {
        api.updateOnlineMetadataSettings.mockImplementation((changes) =>
            Promise.resolve({ ...settings, ...changes }),
        )
        const wrapper = await mountForm()

        await input(wrapper, 'online-metadata-enabled').setValue(true)
        await input(wrapper, 'musicbrainz-url').setValue(' http://mirror.local:5000 ')
        await input(wrapper, 'musicbrainz-interval').setValue('250')
        await submit(wrapper)

        // Only the changed values, so settings changed elsewhere in the meantime stay as they are.
        expect(api.updateOnlineMetadataSettings).toHaveBeenCalledWith({
            enabled: true,
            musicBrainzUrl: 'http://mirror.local:5000',
            musicBrainzRequestIntervalMilliseconds: 250,
        })
        expect(wrapper.get('[data-part="alert"][data-state="success"]').text()).toBe(
            'The settings of the online search were saved.',
        )
        wrapper.unmount()
    })

    it('sends nothing unchanged and shows the settings the server answers with', async () => {
        // The server has an address another admin saved after the form was loaded.
        api.updateOnlineMetadataSettings.mockResolvedValue({
            ...settings,
            coverArtArchiveUrl: 'http://covers.local',
        })
        const wrapper = await mountForm()

        await submit(wrapper)

        expect(api.updateOnlineMetadataSettings).toHaveBeenCalledWith({})
        expect((input(wrapper, 'cover-art-archive-url').element as HTMLInputElement).value).toBe(
            'http://covers.local',
        )
        wrapper.unmount()
    })

    it('compares the next save with the settings of the last answer', async () => {
        api.updateOnlineMetadataSettings.mockImplementation((changes) =>
            Promise.resolve({ ...settings, ...changes }),
        )
        const wrapper = await mountForm()

        await input(wrapper, 'musicbrainz-interval').setValue('250')
        await submit(wrapper)
        await input(wrapper, 'online-metadata-enabled').setValue(true)
        await submit(wrapper)

        expect(api.updateOnlineMetadataSettings).toHaveBeenLastCalledWith({ enabled: true })
        wrapper.unmount()
    })

    it('checks the interval before saving', async () => {
        const wrapper = await mountForm()

        await input(wrapper, 'musicbrainz-interval').setValue('1.5')
        await submit(wrapper)

        expect(api.updateOnlineMetadataSettings).not.toHaveBeenCalled()
        expect(
            wrapper.get('[data-field="musicbrainz-interval"] [data-part="field-error"]').text(),
        ).toBe('Enter a whole number from 0 to 60000.')
        wrapper.unmount()
    })

    it('shows the errors of the server at their fields', async () => {
        api.updateOnlineMetadataSettings.mockRejectedValue({
            code: 'request.invalid',
            errors: [{ field: 'coverArtArchiveUrl', code: 'invalid_format' }],
        })
        const wrapper = await mountForm()

        await input(wrapper, 'cover-art-archive-url').setValue('ftp://covers')
        await submit(wrapper)

        expect(
            wrapper.get('[data-field="cover-art-archive-url"] [data-part="field-error"]').text(),
        ).toBe('Enter an address that starts with http:// or https://.')
        wrapper.unmount()
    })

    it('says when the settings cannot be loaded', async () => {
        api.getOnlineMetadataSettings.mockRejectedValue(new Error('offline'))
        const wrapper = await mountForm()

        expect(wrapper.get('[data-part="alert"]').text()).toBe(
            'The settings of the online search could not be loaded.',
        )
        expect(wrapper.find('form').exists()).toBe(false)
        wrapper.unmount()
    })
})
