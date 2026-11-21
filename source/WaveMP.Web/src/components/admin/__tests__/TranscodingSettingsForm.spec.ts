import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import type { TranscodingSettings } from '@/api/generated/models'
import TranscodingSettingsForm from '../TranscodingSettingsForm.vue'
import { createWaveMpI18n } from '@/i18n'
import { useTranscodingStore } from '@/stores/transcoding'

const settings: TranscodingSettings = {
    hardwareAcceleration: 'Auto',
    maxParallelJobs: 2,
    maxVideoHeight: 1080,
    audioBitrateKbps: 192,
}

async function mountForm() {
    const store = useTranscodingStore()
    vi.spyOn(store, 'loadSettings').mockImplementation(async () => {
        store.settings = settings
        store.settingsStatus = 'ready'
    })
    const update = vi.spyOn(store, 'updateSettings')
    const wrapper = mount(TranscodingSettingsForm, {
        global: { plugins: [createWaveMpI18n('en')] },
        attachTo: document.body,
    })
    await flushPromises()
    return { wrapper, update }
}

type Wrapper = Awaited<ReturnType<typeof mountForm>>['wrapper']

/** The selects of the form: parallel transcodes, video height and audio bitrate. */
function selects(wrapper: Wrapper) {
    const [parallel, height, bitrate] = wrapper.findAll('select')
    return { parallel: parallel!, height: height!, bitrate: bitrate! }
}

async function submit(wrapper: Wrapper) {
    await wrapper.get('form').trigger('submit')
    await flushPromises()
}

describe('TranscodingSettingsForm', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
    })

    it('locks the fields until the settings are loaded', async () => {
        const store = useTranscodingStore()
        let answer: () => void = () => {}
        vi.spyOn(store, 'loadSettings').mockImplementation(
            () =>
                new Promise<void>((resolve) => {
                    store.settingsStatus = 'loading'
                    answer = () => {
                        store.settings = { ...settings, hardwareAcceleration: 'Off' }
                        store.settingsStatus = 'ready'
                        resolve()
                    }
                }),
        )
        const wrapper = mount(TranscodingSettingsForm, {
            global: { plugins: [createWaveMpI18n('en')] },
            attachTo: document.body,
        })
        await flushPromises()

        const group = () => wrapper.get('[data-part="segmented-control"]')
        expect(group().attributes('aria-disabled')).toBe('true')
        // A click on a locked segment changes nothing.
        await wrapper.get('[data-part="segment"][data-item="off"]').trigger('click')
        for (const select of wrapper.findAll('select')) {
            expect(select.attributes('disabled')).toBeDefined()
        }
        expect(wrapper.get('button[type="submit"]').attributes('disabled')).toBeDefined()

        answer()
        await flushPromises()

        expect(
            wrapper.get('[data-part="segment"][data-item="off"]').attributes('aria-checked'),
        ).toBe('true')
        expect(group().attributes('aria-disabled')).toBeUndefined()
        expect((selects(wrapper).parallel.element as HTMLSelectElement).value).toBe('2')
        for (const select of wrapper.findAll('select')) {
            expect(select.attributes('disabled')).toBeUndefined()
        }
        expect(wrapper.get('button[type="submit"]').attributes('disabled')).toBeUndefined()
        wrapper.unmount()
    })

    it('shows the saved settings', async () => {
        const { wrapper } = await mountForm()

        const { parallel, height, bitrate } = selects(wrapper)
        expect((parallel.element as HTMLSelectElement).value).toBe('2')
        expect((height.element as HTMLSelectElement).value).toBe('1080')
        expect((bitrate.element as HTMLSelectElement).value).toBe('192')
        expect(
            wrapper.get('[data-part="segment"][data-item="auto"]').attributes('aria-checked'),
        ).toBe('true')
        wrapper.unmount()
    })

    it('sends only the changed values and shows the settings the server answers with', async () => {
        const { wrapper, update } = await mountForm()
        // The server has a bitrate another admin saved after the form was loaded.
        update.mockResolvedValue({ ...settings, maxVideoHeight: 720, audioBitrateKbps: 320 })

        await selects(wrapper).height.setValue('720')
        await submit(wrapper)

        expect(update).toHaveBeenCalledWith({ maxVideoHeight: 720 })
        expect((selects(wrapper).bitrate.element as HTMLSelectElement).value).toBe('320')
        expect(wrapper.emitted('saved')).toHaveLength(1)
        wrapper.unmount()
    })

    it('sends the changed hardware acceleration', async () => {
        const { wrapper, update } = await mountForm()
        update.mockResolvedValue({ ...settings, hardwareAcceleration: 'Off' })

        await wrapper.get('[data-part="segment"][data-item="off"]').trigger('click')
        await submit(wrapper)

        expect(update).toHaveBeenCalledWith({ hardwareAcceleration: 'Off' })
        wrapper.unmount()
    })

    it('sends nothing unchanged', async () => {
        const { wrapper, update } = await mountForm()
        update.mockResolvedValue(settings)

        await submit(wrapper)

        expect(update).toHaveBeenCalledWith({})
        expect(wrapper.find('[data-part="alert"][data-state="success"]').exists()).toBe(true)
        wrapper.unmount()
    })
})
