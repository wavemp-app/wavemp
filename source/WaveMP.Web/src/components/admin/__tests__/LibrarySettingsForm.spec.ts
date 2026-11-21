import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import LibrarySettingsForm from '../LibrarySettingsForm.vue'
import { createWaveMpI18n } from '@/i18n'
import { useCoversStore } from '@/stores/covers'
import { useLibrarySettingsStore } from '@/stores/librarySettings'

const settings = {
    scanIntervalMinutes: 360,
    fileWatcherEnabled: true,
    audioExtensions: ['mp3', 'flac'],
    videoExtensions: ['mkv'],
    imageExtensions: ['jpg'],
    albumCoverNames: ['cover', 'folder'],
    coverPriority: 'AlbumFirst' as const,
    downloadLyrics: false,
    subsonicDownloads: false,
}

async function mountForm(current = settings) {
    const store = useLibrarySettingsStore()
    const load = vi.spyOn(store, 'load').mockImplementation(async () => {
        store.settings = current
        store.status = 'ready'
    })
    const update = vi.spyOn(store, 'update')
    const wrapper = mount(LibrarySettingsForm, {
        global: { plugins: [createWaveMpI18n('en')] },
        attachTo: document.body,
    })
    await flushPromises()
    return { wrapper, store, load, update }
}

describe('LibrarySettingsForm', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
    })

    it('locks the fields until the settings are loaded', async () => {
        const store = useLibrarySettingsStore()
        let answer: () => void = () => {}
        vi.spyOn(store, 'load').mockImplementation(
            () =>
                new Promise<void>((resolve) => {
                    store.status = 'loading'
                    answer = () => {
                        store.settings = settings
                        store.status = 'ready'
                        resolve()
                    }
                }),
        )
        const wrapper = mount(LibrarySettingsForm, {
            global: { plugins: [createWaveMpI18n('en')] },
            attachTo: document.body,
        })
        await flushPromises()

        const fields = () => wrapper.findAll('select, input')
        expect(fields().length).toBe(8)
        for (const field of fields()) {
            expect(field.attributes('disabled')).toBeDefined()
        }
        expect(wrapper.get('[data-part="segmented-control"]').attributes('aria-disabled')).toBe(
            'true',
        )
        expect(wrapper.get('button[type="submit"]').attributes('disabled')).toBeDefined()

        answer()
        await flushPromises()

        for (const field of fields()) {
            expect(field.attributes('disabled')).toBeUndefined()
        }
        expect(
            wrapper.get('[data-part="segmented-control"]').attributes('aria-disabled'),
        ).toBeUndefined()
        expect(wrapper.findAll('input[data-part="input"]')[0]!.element).toHaveProperty(
            'value',
            'mp3, flac',
        )
        expect(wrapper.get('button[type="submit"]').attributes('disabled')).toBeUndefined()
        wrapper.unmount()
    })

    it('shows the saved settings', async () => {
        const { wrapper } = await mountForm()

        expect((wrapper.get('select').element as HTMLSelectElement).value).toBe('360')
        expect((wrapper.get('input[type="checkbox"]').element as HTMLInputElement).checked).toBe(
            true,
        )
        const inputs = wrapper.findAll('input[data-part="input"]')
        expect(inputs.map((input) => (input.element as HTMLInputElement).value)).toEqual([
            'mp3, flac',
            'mkv',
            'jpg',
            'cover, folder',
        ])
        wrapper.unmount()
    })

    it('offers an interval of the server that is not in the list', async () => {
        const { wrapper } = await mountForm({ ...settings, scanIntervalMinutes: 45 })

        const options = wrapper.findAll('option').map((option) => option.text())
        expect(options).toContain('Every 45 minutes')
        wrapper.unmount()
    })

    it('saves the changes as lists and confirms', async () => {
        const { wrapper, update } = await mountForm()
        update.mockResolvedValue({
            ...settings,
            scanIntervalMinutes: 720,
            fileWatcherEnabled: false,
        })

        await wrapper.get('select').setValue('720')
        await wrapper.get('input[type="checkbox"]').setValue(false)
        await wrapper.findAll('input[data-part="input"]')[0]!.setValue(' .MP3 , ogg,')
        await wrapper.get('form').trigger('submit')
        await flushPromises()

        // Only the changed values, so settings changed elsewhere in the meantime stay as they are.
        expect(update).toHaveBeenCalledWith({
            scanIntervalMinutes: 720,
            fileWatcherEnabled: false,
            audioExtensions: ['.MP3', 'ogg'],
        })
        expect(wrapper.get('[data-part="alert"][data-state="success"]').text()).toBe(
            'The settings are saved.',
        )
        wrapper.unmount()
    })

    it('shows the global cover rule as segmented control and saves a change', async () => {
        const { wrapper, update } = await mountForm()
        const covers = useCoversStore()
        const invalidate = vi.spyOn(covers, 'invalidate')
        update.mockResolvedValue({ ...settings, coverPriority: 'TrackFirst' })

        const field = wrapper.get('[data-field="cover-priority"]')
        const segments = field.findAll('[data-part="segment"]')
        expect(segments.map((segment) => segment.attributes('data-item'))).toEqual([
            'album-first',
            'track-first',
        ])
        expect(segments.map((segment) => segment.text())).toEqual(['Album cover', 'Song cover'])
        expect(segments[0]!.attributes('aria-checked')).toBe('true')

        await segments[1]!.trigger('click')
        await wrapper.get('form').trigger('submit')
        await flushPromises()

        expect(update).toHaveBeenCalledWith({ coverPriority: 'TrackFirst' })
        expect(invalidate).toHaveBeenCalledTimes(1)
        wrapper.unmount()
    })

    it('switches the download of lyrics on', async () => {
        const { wrapper, update } = await mountForm()
        update.mockResolvedValue({ ...settings, downloadLyrics: true })

        const field = wrapper.get('[data-field="download-lyrics"]')
        expect(field.text()).toContain('Download lyrics from LRCLIB')
        const checkbox = field.get('input[type="checkbox"]')
        expect((checkbox.element as HTMLInputElement).checked).toBe(false)

        await checkbox.setValue(true)
        await wrapper.get('form').trigger('submit')
        await flushPromises()

        expect(update).toHaveBeenCalledWith(expect.objectContaining({ downloadLyrics: true }))
        wrapper.unmount()
    })

    it('allows Subsonic apps to download songs', async () => {
        const { wrapper, update } = await mountForm()
        update.mockResolvedValue({ ...settings, subsonicDownloads: true })

        const field = wrapper.get('[data-field="subsonic-downloads"]')
        expect(field.text()).toContain('Subsonic apps may download songs')
        const checkbox = field.get('input[type="checkbox"]')
        expect((checkbox.element as HTMLInputElement).checked).toBe(false)

        await checkbox.setValue(true)
        await wrapper.get('form').trigger('submit')
        await flushPromises()

        expect(update).toHaveBeenCalledWith(expect.objectContaining({ subsonicDownloads: true }))
        wrapper.unmount()
    })

    it('keeps the loaded covers while the cover rule stays the same', async () => {
        const { wrapper, update } = await mountForm()
        const invalidate = vi.spyOn(useCoversStore(), 'invalidate')
        update.mockResolvedValue({ ...settings, scanIntervalMinutes: 720 })

        await wrapper.get('form').trigger('submit')
        await flushPromises()

        expect(invalidate).not.toHaveBeenCalled()
        wrapper.unmount()
    })

    it('sends nothing unchanged and shows the settings the server answers with', async () => {
        const { wrapper, update } = await mountForm()
        // The server has a value another admin saved after the form was loaded.
        update.mockResolvedValue({ ...settings, subsonicDownloads: true })

        await wrapper.get('form').trigger('submit')
        await flushPromises()

        expect(update).toHaveBeenCalledWith({})
        const downloads = wrapper.get('[data-field="subsonic-downloads"] input[type="checkbox"]')
        expect((downloads.element as HTMLInputElement).checked).toBe(true)
        expect(wrapper.get('[data-part="alert"][data-state="success"]').text()).toBe(
            'The settings are saved.',
        )
        wrapper.unmount()
    })

    it('shows the errors of the server at their fields', async () => {
        const { wrapper, update } = await mountForm()
        update.mockRejectedValue({
            responseStatusCode: 400,
            code: 'request.invalid',
            errors: [
                { field: 'scanIntervalMinutes', code: 'out_of_range', min: 15, max: 1440 },
                { field: 'videoExtensions', code: 'duplicate' },
                { field: 'albumCoverNames', code: 'max_items', max: 50 },
                { field: '', code: 'required' },
            ],
        })

        await wrapper.get('form').trigger('submit')
        await flushPromises()

        const errors = wrapper.findAll('[data-part="field-error"]').map((error) => error.text())
        expect(errors).toEqual([
            'Choose an interval between 15 and 1,440 minutes.',
            'An extension may only be in one list, and lrc is reserved for lyrics files.',
            'This list has too many entries (maximum: 50).',
        ])
        expect(wrapper.find('[data-part="alert"]').exists()).toBe(false)
        wrapper.unmount()
    })

    it('reports other errors', async () => {
        const { wrapper, update } = await mountForm()
        update.mockRejectedValue(new Error('offline'))

        await wrapper.get('form').trigger('submit')
        await flushPromises()

        expect(wrapper.get('[data-part="alert"][data-state="error"]').text()).toBe(
            'The settings could not be saved.',
        )
        wrapper.unmount()
    })
})
