import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import BackupSettingsForm from '../BackupSettingsForm.vue'
import { createWaveMpI18n } from '@/i18n'
import { useBackupSettingsStore } from '@/stores/backupSettings'

const loaded = { scheduleTime: '03:00', retentionCount: 7 }

/** Mounts the form while the settings are still loading; the returned function answers. */
function mountLoading() {
    const store = useBackupSettingsStore()
    let answer: () => void = () => {}
    vi.spyOn(store, 'load').mockImplementation(
        () =>
            new Promise<void>((resolve) => {
                store.status = 'loading'
                answer = () => {
                    store.settings = loaded
                    store.status = 'ready'
                    resolve()
                }
            }),
    )
    const update = vi.spyOn(store, 'update')
    const wrapper = mount(BackupSettingsForm, {
        props: { timeZone: 'Europe/Berlin' },
        global: { plugins: [createWaveMpI18n('en')] },
        attachTo: document.body,
    })
    return { wrapper, update, answer: () => answer() }
}

describe('BackupSettingsForm', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
    })

    it('locks the fields until the settings are loaded', async () => {
        const { wrapper, answer } = mountLoading()
        await flushPromises()

        expect(wrapper.get('input').attributes('disabled')).toBeDefined()
        expect(wrapper.get('select').attributes('disabled')).toBeDefined()
        expect(wrapper.get('button[type="submit"]').attributes('disabled')).toBeDefined()

        answer()
        await flushPromises()

        expect((wrapper.get('input').element as HTMLInputElement).value).toBe('03:00')
        expect((wrapper.get('select').element as HTMLSelectElement).value).toBe('7')
        expect(wrapper.get('input').attributes('disabled')).toBeUndefined()
        expect(wrapper.get('select').attributes('disabled')).toBeUndefined()
        expect(wrapper.get('button[type="submit"]').attributes('disabled')).toBeUndefined()
        wrapper.unmount()
    })

    it('saves the time and the number of kept exports', async () => {
        const { wrapper, update, answer } = mountLoading()
        answer()
        await flushPromises()
        update.mockResolvedValue({ scheduleTime: '22:15', retentionCount: 14 })

        await wrapper.get('input').setValue('22:15')
        await wrapper.get('select').setValue('14')
        await wrapper.get('form').trigger('submit')
        await flushPromises()

        expect(update).toHaveBeenCalledWith({ scheduleTime: '22:15', retentionCount: 14 })
        expect(wrapper.get('[data-part="alert"]').text()).toBe('The settings are saved.')
        expect(wrapper.emitted('saved')).toHaveLength(1)
        wrapper.unmount()
    })

    it('sends only the changed value and shows the settings the server answers with', async () => {
        const { wrapper, update, answer } = mountLoading()
        answer()
        await flushPromises()
        // The server has a time another admin saved after the form was loaded.
        update.mockResolvedValue({ scheduleTime: '04:30', retentionCount: 14 })

        await wrapper.get('select').setValue('14')
        await wrapper.get('form').trigger('submit')
        await flushPromises()

        expect(update).toHaveBeenCalledWith({ retentionCount: 14 })
        expect((wrapper.get('input').element as HTMLInputElement).value).toBe('04:30')
        wrapper.unmount()
    })

    it('sends nothing unchanged', async () => {
        const { wrapper, update, answer } = mountLoading()
        answer()
        await flushPromises()
        update.mockResolvedValue(loaded)

        await wrapper.get('form').trigger('submit')
        await flushPromises()

        expect(update).toHaveBeenCalledWith({})
        expect(wrapper.get('[data-part="alert"]').text()).toBe('The settings are saved.')
        wrapper.unmount()
    })
})
