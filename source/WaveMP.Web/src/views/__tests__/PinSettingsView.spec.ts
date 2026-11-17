import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createWaveMpI18n } from '@/i18n'

const api = vi.hoisted(() => ({
    get: vi.fn(),
    put: vi.fn(),
    remove: vi.fn(),
}))

vi.mock('@/api/client', () => ({
    apiClient: {
        api: {
            v1: {
                me: {
                    pin: {
                        get: () => api.get(),
                        put: (body: unknown) => api.put(body),
                        delete: () => api.remove(),
                    },
                },
            },
        },
    },
}))

const { default: PinSettingsView } = await import('../PinSettingsView.vue')
const { useSessionStore } = await import('@/stores/session')

async function mountView() {
    const session = useSessionStore()
    const refresh = vi.spyOn(session, 'refresh').mockResolvedValue()
    const wrapper = mount(PinSettingsView, {
        global: { plugins: [createWaveMpI18n('en')] },
        attachTo: document.body,
    })
    await flushPromises()
    return { wrapper, refresh }
}

async function fill(
    wrapper: Awaited<ReturnType<typeof mountView>>['wrapper'],
    pin: string,
    confirmation = pin,
) {
    await wrapper.get('[data-field="pin"] input').setValue(pin)
    await wrapper.get('[data-field="pin-confirmation"] input').setValue(confirmation)
    await wrapper.get('[data-part="form"]').trigger('submit')
    await flushPromises()
}

describe('PinSettingsView', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
        for (const mock of Object.values(api)) {
            mock.mockReset()
        }
    })

    it('sets a PIN for a profile without one', async () => {
        api.get.mockResolvedValue({ isSet: false, isManagedByEnvironment: false })
        api.put.mockResolvedValue({ isSet: true, isManagedByEnvironment: false })
        const { wrapper, refresh } = await mountView()

        const view = wrapper.get('[data-view="settings-pin"]')
        expect(view.attributes('data-state')).toBe('off')
        expect(wrapper.get('[data-field="pin-status"]').text()).toBe(
            'This profile has no PIN. Anyone can select it.',
        )
        expect(wrapper.find('[data-item="remove-pin"]').exists()).toBe(false)
        expect(wrapper.get('[data-item="save-pin"]').text()).toBe('Set PIN')

        await fill(wrapper, '4826')

        expect(api.put).toHaveBeenCalledWith({ pin: '4826' })
        expect(view.attributes('data-state')).toBe('on')
        expect(wrapper.get('[data-part="alert"]').text()).toBe('The PIN was saved.')
        expect((wrapper.get('[data-field="pin"] input').element as HTMLInputElement).value).toBe('')
        expect(refresh).toHaveBeenCalledOnce()
        wrapper.unmount()
    })

    it('checks the PIN before saving it', async () => {
        api.get.mockResolvedValue({ isSet: false, isManagedByEnvironment: false })
        const { wrapper } = await mountView()

        await fill(wrapper, '12')
        expect(wrapper.get('[data-field="pin"]').text()).toContain('Please enter 4 to 8 digits.')

        await fill(wrapper, '1234', '1243')
        expect(wrapper.get('[data-field="pin-confirmation"]').text()).toContain(
            'The PINs do not match.',
        )

        await fill(wrapper, '12ab')
        expect(api.put).not.toHaveBeenCalled()
        wrapper.unmount()
    })

    it('removes the PIN after the confirmation', async () => {
        api.get.mockResolvedValue({ isSet: true, isManagedByEnvironment: false })
        api.remove.mockResolvedValue(undefined)
        const { wrapper, refresh } = await mountView()
        expect(wrapper.get('[data-item="save-pin"]').text()).toBe('Change PIN')

        await wrapper.get('[data-item="remove-pin"]').trigger('click')
        await flushPromises()
        await wrapper.get('[data-item="confirm-remove-pin"]').trigger('click')
        await flushPromises()

        expect(api.remove).toHaveBeenCalledOnce()
        expect(wrapper.get('[data-view="settings-pin"]').attributes('data-state')).toBe('off')
        expect(wrapper.get('[data-part="alert"]').text()).toBe('The PIN was removed.')
        expect(refresh).toHaveBeenCalledOnce()
        wrapper.unmount()
    })

    it('only shows a PIN that ADMIN_PIN sets', async () => {
        api.get.mockResolvedValue({ isSet: true, isManagedByEnvironment: true })
        const { wrapper } = await mountView()

        expect(wrapper.get('[data-view="settings-pin"]').attributes('data-state')).toBe('managed')
        expect(wrapper.get('[data-field="pin-status"]').text()).toContain('(ADMIN_PIN)')
        expect(wrapper.find('[data-part="form"]').exists()).toBe(false)
        wrapper.unmount()
    })

    it('reports a failed save', async () => {
        api.get.mockResolvedValue({ isSet: false, isManagedByEnvironment: false })
        api.put.mockRejectedValue(new TypeError('Failed to fetch'))
        const { wrapper } = await mountView()

        await fill(wrapper, '4826')

        expect(wrapper.get('[data-part="alert"]').text()).toBe('The PIN could not be saved.')
        expect(wrapper.get('[data-view="settings-pin"]').attributes('data-state')).toBe('off')
        wrapper.unmount()
    })

    it('offers to try again if the state cannot be loaded', async () => {
        api.get.mockRejectedValueOnce(new TypeError('Failed to fetch'))
        api.get.mockResolvedValueOnce({ isSet: false, isManagedByEnvironment: false })
        const { wrapper } = await mountView()

        expect(wrapper.get('[data-view="settings-pin"]').attributes('data-state')).toBe('error')
        await wrapper.get('[data-item="retry"]').trigger('click')
        await flushPromises()

        expect(wrapper.get('[data-view="settings-pin"]').attributes('data-state')).toBe('off')
        wrapper.unmount()
    })
})
