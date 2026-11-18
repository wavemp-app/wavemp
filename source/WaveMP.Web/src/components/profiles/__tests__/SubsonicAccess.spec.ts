import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createWaveMpI18n } from '@/i18n'
import { useSessionStore } from '@/stores/session'

const api = vi.hoisted(() => ({
    get: vi.fn(),
    patch: vi.fn(),
    create: vi.fn(),
    revoke: vi.fn(),
}))

vi.mock('@/api/client', () => ({
    apiClient: {
        api: {
            v1: {
                me: {
                    subsonic: {
                        get: () => api.get(),
                        patch: (body: unknown) => api.patch(body),
                        password: {
                            post: () => api.create(),
                            delete: () => api.revoke(),
                        },
                    },
                },
            },
        },
    },
}))

const { default: SubsonicAccess } = await import('../SubsonicAccess.vue')

const off = { enabled: false, hasPassword: false, passwordCreatedAt: null }

async function mountAccess() {
    const session = useSessionStore()
    session.$patch({
        profile: { id: 'user', name: 'Anna', initials: 'A', color: 1 },
    } as never)
    const wrapper = mount(SubsonicAccess, {
        global: { plugins: [createWaveMpI18n('en')] },
        attachTo: document.body,
    })
    await flushPromises()
    return wrapper
}

function valueOf(wrapper: Awaited<ReturnType<typeof mountAccess>>, field: string): string {
    return wrapper.get(`[data-field="${field}"] [data-part="stat-value"]`).text()
}

describe('SubsonicAccess', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
        for (const mock of Object.values(api)) {
            mock.mockReset()
        }
    })

    it('shows what the apps need to sign in', async () => {
        api.get.mockResolvedValue(off)
        const wrapper = await mountAccess()

        const checkbox = wrapper.get('[data-field="subsonic-enabled"] input')
        expect((checkbox.element as HTMLInputElement).checked).toBe(false)
        expect(valueOf(wrapper, 'subsonic-server')).toBe(window.location.origin)
        expect(valueOf(wrapper, 'subsonic-user-name')).toBe('Anna')
        expect(valueOf(wrapper, 'subsonic-password-state')).toBe('Not created yet')
        expect(wrapper.get('[data-item="create-subsonic-password"]').text()).toBe(
            'Create app password',
        )
        expect(wrapper.find('[data-item="revoke-subsonic-password"]').exists()).toBe(false)
        wrapper.unmount()
    })

    it('switches the access on', async () => {
        api.get.mockResolvedValue(off)
        api.patch.mockResolvedValue({ ...off, enabled: true })
        const wrapper = await mountAccess()

        await wrapper.get('[data-field="subsonic-enabled"] input').setValue(true)
        await flushPromises()

        expect(api.patch).toHaveBeenCalledWith({ enabled: true })
        const checkbox = wrapper.get('[data-field="subsonic-enabled"] input')
        expect((checkbox.element as HTMLInputElement).checked).toBe(true)
        wrapper.unmount()
    })

    it('restores the switch when the change fails', async () => {
        api.get.mockResolvedValue(off)
        api.patch.mockRejectedValue(new Error('offline'))
        const wrapper = await mountAccess()

        await wrapper.get('[data-field="subsonic-enabled"] input').setValue(true)
        await flushPromises()

        const checkbox = wrapper.get('[data-field="subsonic-enabled"] input')
        expect((checkbox.element as HTMLInputElement).checked).toBe(false)
        expect(wrapper.get('[data-part="alert"]').text()).toBe(
            'The Subsonic access could not be changed.',
        )
        wrapper.unmount()
    })

    it('shows a new app password once', async () => {
        api.get.mockResolvedValue({ ...off, enabled: true })
        api.create.mockResolvedValue({
            password: 'abcdEFGH2345jkmnPQRS6789',
            access: {
                enabled: true,
                hasPassword: true,
                passwordCreatedAt: new Date('2026-11-18T08:00:00Z'),
            },
        })
        const wrapper = await mountAccess()

        await wrapper.get('[data-item="create-subsonic-password"]').trigger('click')
        await flushPromises()

        expect(valueOf(wrapper, 'subsonic-new-password')).toBe('abcdEFGH2345jkmnPQRS6789')
        expect(wrapper.get('[data-item="subsonic-password-once"]').text()).toContain(
            'only shown this once',
        )
        expect(valueOf(wrapper, 'subsonic-password-state')).toMatch(/^Created on /)
        expect(wrapper.get('[data-item="create-subsonic-password"]').text()).toBe(
            'Create new app password',
        )
        expect(wrapper.find('[data-item="revoke-subsonic-password"]').exists()).toBe(true)
        wrapper.unmount()
    })

    it('revokes the app password', async () => {
        api.get.mockResolvedValue({
            enabled: true,
            hasPassword: true,
            passwordCreatedAt: new Date('2026-11-18T08:00:00Z'),
        })
        api.revoke.mockResolvedValue(undefined)
        const wrapper = await mountAccess()

        await wrapper.get('[data-item="revoke-subsonic-password"]').trigger('click')
        await flushPromises()

        expect(api.revoke).toHaveBeenCalledTimes(1)
        expect(valueOf(wrapper, 'subsonic-password-state')).toBe('Not created yet')
        expect(wrapper.find('[data-item="revoke-subsonic-password"]').exists()).toBe(false)
        expect(wrapper.get('[data-part="alert"]').text()).toContain('was revoked')
        wrapper.unmount()
    })

    it('offers to load again after an error', async () => {
        api.get.mockRejectedValueOnce(new Error('offline')).mockResolvedValue(off)
        const wrapper = await mountAccess()

        expect(wrapper.get('[data-part="alert"]').text()).toBe(
            'The Subsonic access could not be loaded.',
        )
        await wrapper.get('[data-item="retry"]').trigger('click')
        await flushPromises()

        expect(wrapper.find('[data-field="subsonic-enabled"]').exists()).toBe(true)
        wrapper.unmount()
    })
})
