import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
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
                    scrobbling: {
                        get: () => api.get(),
                        byService: (service: string) => ({
                            put: (body: unknown) => api.put(service, body),
                            delete: () => api.remove(service),
                        }),
                    },
                },
            },
        },
    },
}))

const { default: ScrobblingServices } = await import('../ScrobblingServices.vue')

const lastFm = {
    service: 'LastFm',
    isAvailable: true,
    status: 'NotConnected',
    queuedScrobbles: 0,
    authorizationUrl: 'https://www.last.fm/api/auth/?api_key=key',
}
const listenBrainz = {
    service: 'ListenBrainz',
    isAvailable: true,
    status: 'NotConnected',
    queuedScrobbles: 0,
}

async function mountServices() {
    const router = createRouter({
        history: createMemoryHistory(),
        routes: [
            { path: '/', name: 'settings', component: { render: () => null } },
            {
                path: '/settings/scrobbling/last-fm',
                name: 'settings-scrobbling-lastfm',
                component: { render: () => null },
            },
        ],
    })
    const wrapper = mount(ScrobblingServices, {
        global: { plugins: [createWaveMpI18n('en'), router] },
        attachTo: document.body,
    })
    await flushPromises()
    return wrapper
}

describe('ScrobblingServices', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
        for (const mock of Object.values(api)) {
            mock.mockReset()
        }
    })

    it('shows the state of each service', async () => {
        api.get.mockResolvedValue({
            services: [
                {
                    ...lastFm,
                    status: 'Connected',
                    accountName: 'anna',
                    queuedScrobbles: 3,
                    lastScrobbledAt: new Date('2026-11-18T10:00:00Z'),
                },
                { ...listenBrainz, status: 'AuthorizationFailed' },
            ],
        })

        const wrapper = await mountServices()

        const items = wrapper.findAll('[data-part="scrobble-item"]')
        expect(items.map((item) => item.attributes('data-item'))).toEqual([
            'last-fm',
            'listen-brainz',
        ])
        expect(items.map((item) => item.attributes('data-state'))).toEqual([
            'connected',
            'authorization-failed',
        ])
        expect(items[0]!.get('[data-field="account"]').text()).toBe('as anna')
        expect(items[0]!.get('[data-field="queued"]').text()).toBe('Waiting to be sent: 3')
        expect(items[0]!.find('[data-field="last-scrobbled"]').exists()).toBe(true)
        expect(items[0]!.find('[data-item="connect"]').exists()).toBe(false)
        expect(items[1]!.find('[data-field="authorization-failed"]').exists()).toBe(true)
        expect(items[1]!.get('[data-item="connect"]').text()).toBe('Connect ListenBrainz again')
        expect(items[1]!.find('[data-item="disconnect"]').exists()).toBe(true)
        wrapper.unmount()
    })

    it('marks Last.fm as unavailable without API account', async () => {
        api.get.mockResolvedValue({
            services: [{ ...lastFm, isAvailable: false, authorizationUrl: null }, listenBrainz],
        })

        const wrapper = await mountServices()

        const item = wrapper.get('[data-item="last-fm"]')
        expect(item.attributes('data-state')).toBe('unavailable')
        expect(item.find('[data-field="unavailable"]').exists()).toBe(true)
        expect(item.find('button').exists()).toBe(false)
        wrapper.unmount()
    })

    it('sends the user to Last.fm with the address to return to', async () => {
        api.get.mockResolvedValue({ services: [lastFm, listenBrainz] })
        const assign = vi.fn()
        const location = window.location
        Object.defineProperty(window, 'location', {
            configurable: true,
            value: { origin: 'https://music.example', assign },
        })
        try {
            const wrapper = await mountServices()

            await wrapper.get('[data-item="last-fm"] [data-item="connect"]').trigger('click')

            expect(assign).toHaveBeenCalledExactlyOnceWith(
                'https://www.last.fm/api/auth/?api_key=key&cb=https%3A%2F%2Fmusic.example%2Fsettings%2Fscrobbling%2Flast-fm',
            )
            wrapper.unmount()
        } finally {
            Object.defineProperty(window, 'location', { configurable: true, value: location })
        }
    })

    it('connects ListenBrainz with the token of the user', async () => {
        api.get.mockResolvedValue({ services: [lastFm, listenBrainz] })
        api.put.mockResolvedValue({ ...listenBrainz, status: 'Connected', accountName: 'anna' })
        const wrapper = await mountServices()

        await wrapper.get('[data-item="listen-brainz"] [data-item="connect"]').trigger('click')
        await wrapper.get('[data-field="scrobbling-token"] input').setValue('  secret  ')
        await wrapper.get('[data-item="scrobbling-token"] [data-part="form"]').trigger('submit')
        await flushPromises()

        expect(api.put).toHaveBeenCalledExactlyOnceWith('ListenBrainz', { token: 'secret' })
        expect(wrapper.get('[data-item="listen-brainz"]').attributes('data-state')).toBe(
            'connected',
        )
        expect(wrapper.get('[data-part="alert"]').text()).toContain('ListenBrainz is connected.')
        wrapper.unmount()
    })

    it('shows why the token was not accepted', async () => {
        api.get.mockResolvedValue({ services: [lastFm, listenBrainz] })
        api.put.mockRejectedValue({ code: 'scrobbling.token_invalid', responseStatusCode: 422 })
        const wrapper = await mountServices()

        await wrapper.get('[data-item="listen-brainz"] [data-item="connect"]').trigger('click')
        await wrapper.get('[data-field="scrobbling-token"] input').setValue('wrong')
        await wrapper.get('[data-item="scrobbling-token"] [data-part="form"]').trigger('submit')
        await flushPromises()

        expect(wrapper.get('[data-field="scrobbling-token"]').text()).toContain(
            'The service did not accept the token.',
        )
        wrapper.unmount()
    })

    it('disconnects a service and loads the states again', async () => {
        api.get
            .mockResolvedValueOnce({
                services: [lastFm, { ...listenBrainz, status: 'Connected', accountName: 'anna' }],
            })
            .mockResolvedValueOnce({ services: [lastFm, listenBrainz] })
        api.remove.mockResolvedValue(undefined)
        const wrapper = await mountServices()

        await wrapper.get('[data-item="listen-brainz"] [data-item="disconnect"]').trigger('click')
        await flushPromises()

        expect(api.remove).toHaveBeenCalledExactlyOnceWith('ListenBrainz')
        expect(wrapper.get('[data-item="listen-brainz"]').attributes('data-state')).toBe(
            'not-connected',
        )
        wrapper.unmount()
    })

    it('offers to load again after an error', async () => {
        api.get.mockRejectedValueOnce(new Error('offline')).mockResolvedValue({
            services: [lastFm, listenBrainz],
        })
        const wrapper = await mountServices()

        expect(wrapper.find('[data-part="scrobble-list"]').exists()).toBe(false)
        await wrapper.get('[data-item="retry"]').trigger('click')
        await flushPromises()

        expect(wrapper.findAll('[data-part="scrobble-item"]')).toHaveLength(2)
        wrapper.unmount()
    })
})
