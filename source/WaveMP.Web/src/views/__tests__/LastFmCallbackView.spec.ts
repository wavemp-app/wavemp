import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import { createWaveMpI18n } from '@/i18n'

const api = vi.hoisted(() => ({ put: vi.fn() }))

vi.mock('@/api/client', () => ({
    apiClient: {
        api: {
            v1: {
                me: {
                    scrobbling: {
                        byService: (service: string) => ({
                            put: (body: unknown) => api.put(service, body),
                        }),
                    },
                },
            },
        },
    },
}))

const { default: LastFmCallbackView } = await import('../LastFmCallbackView.vue')

async function mountView(query: string) {
    const router = createRouter({
        history: createMemoryHistory(),
        routes: [
            { path: '/settings', name: 'settings', component: { render: () => null } },
            {
                path: '/settings/scrobbling/last-fm',
                name: 'settings-scrobbling-lastfm',
                component: { render: () => null },
            },
        ],
    })
    await router.push(`/settings/scrobbling/last-fm${query}`)
    const wrapper = mount(LastFmCallbackView, {
        global: { plugins: [createWaveMpI18n('en'), router] },
    })
    await flushPromises()
    return { wrapper, router }
}

describe('LastFmCallbackView', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
        api.put.mockReset()
    })

    it('hands the token to the server and returns to the settings', async () => {
        api.put.mockResolvedValue({ service: 'LastFm', status: 'Connected' })

        const { router } = await mountView('?token=abc')

        expect(api.put).toHaveBeenCalledExactlyOnceWith('LastFm', { token: 'abc' })
        expect(router.currentRoute.value.name).toBe('settings')
    })

    it('shows an error without token', async () => {
        const { wrapper } = await mountView('')

        expect(api.put).not.toHaveBeenCalled()
        expect(
            wrapper.get('[data-view="settings-scrobbling-lastfm"]').attributes('data-state'),
        ).toBe('failed')
        expect(wrapper.get('[data-part="alert"]').text()).toContain('Last.fm did not allow')
    })

    it('shows why the server did not connect Last.fm', async () => {
        api.put.mockRejectedValue({
            code: 'scrobbling.service_unavailable',
            responseStatusCode: 503,
        })

        const { wrapper, router } = await mountView('?token=abc')

        expect(router.currentRoute.value.name).toBe('settings-scrobbling-lastfm')
        expect(wrapper.get('[data-part="alert"]').text()).toContain('cannot be reached')
        expect(wrapper.find('[data-item="back-to-settings"]').exists()).toBe(true)
    })
})
