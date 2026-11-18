import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import type { Component } from 'vue'
import { createWaveMpI18n } from '@/i18n'

// Loading and error states of the administration: while a page loads it is busy, a failed load
// shows an error with a button that loads it again.

const api = vi.hoisted(() => ({
    jobs: vi.fn(),
    users: vi.fn(),
    cacheSettings: vi.fn(),
}))

vi.mock('@/api/client', () => ({
    apiClient: {
        api: {
            v1: {
                jobs: { get: () => api.jobs() },
                users: { get: () => api.users() },
                settings: { cache: { get: () => api.cacheSettings() } },
            },
        },
    },
}))

const { default: AdminJobsView } = await import('../admin/AdminJobsView.vue')
const { default: AdminUsersView } = await import('../admin/AdminUsersView.vue')
const { default: CacheSettingsForm } = await import('@/components/admin/CacheSettingsForm.vue')

function deferred<T>() {
    let resolve!: (value: T) => void
    const promise = new Promise<T>((done) => {
        resolve = done
    })
    return { promise, resolve }
}

async function mountPage(component: Component) {
    const router = createRouter({
        history: createMemoryHistory(),
        routes: [
            { path: '/', name: 'admin-jobs', component: { render: () => null } },
            ...[
                'admin-library',
                'admin-files',
                'admin-review',
                'admin-cache',
                'admin-transcoding',
                'admin-backups',
                'admin-themes',
            ].map((name) => ({ path: `/${name}`, name, component: { render: () => null } })),
            { path: '/users', name: 'admin-users', component: { render: () => null } },
            { path: '/library', name: 'library', component: { render: () => null } },
            { path: '/profiles', name: 'profiles', component: { render: () => null } },
        ],
    })
    await router.push('/')
    return mount(component, { global: { plugins: [createWaveMpI18n('en'), router] } })
}

describe('loading and error states of the administration', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
        for (const mock of Object.values(api)) {
            mock.mockReset()
        }
    })

    it('marks the jobs page as busy while it loads', async () => {
        const page = deferred<unknown>()
        api.jobs.mockReturnValue(page.promise)
        const wrapper = await mountPage(AdminJobsView)
        await flushPromises()

        expect(wrapper.get('[data-view="admin-jobs"]').attributes('aria-busy')).toBe('true')

        page.resolve({ items: [{ id: 'library-scan', state: 'Idle' }], total: 1 })
        await flushPromises()

        expect(wrapper.get('[data-view="admin-jobs"]').attributes('aria-busy')).toBeUndefined()
        expect(wrapper.find('[data-part="alert"]').exists()).toBe(false)
    })

    it('loads the jobs again after an error', async () => {
        api.jobs.mockRejectedValueOnce(new TypeError('Failed to fetch'))
        api.jobs.mockResolvedValueOnce({ items: [{ id: 'library-scan', state: 'Idle' }], total: 1 })
        const wrapper = await mountPage(AdminJobsView)
        await flushPromises()

        expect(wrapper.get('[data-part="alert"]').text()).toBe('The jobs could not be loaded.')
        await wrapper.get('[data-item="retry"]').trigger('click')
        await flushPromises()

        expect(api.jobs).toHaveBeenCalledTimes(2)
        expect(wrapper.find('[data-part="alert"]').exists()).toBe(false)
        expect(wrapper.find('[data-item="retry"]').exists()).toBe(false)
    })

    it('marks the profiles page as busy and loads it again after an error', async () => {
        const failure = deferred<unknown>()
        api.users.mockReturnValueOnce(failure.promise.then(() => Promise.reject(new Error('down'))))
        api.users.mockResolvedValueOnce({
            items: [
                {
                    id: 'admin',
                    name: 'Admin',
                    initials: 'A',
                    color: '#1565c0',
                    role: 'Admin',
                    isBuiltIn: true,
                },
            ],
            total: 1,
        })
        const wrapper = await mountPage(AdminUsersView)
        await flushPromises()
        expect(wrapper.get('[data-view="admin-users"]').attributes('aria-busy')).toBe('true')

        failure.resolve(undefined)
        await flushPromises()
        expect(wrapper.get('[data-view="admin-users"]').attributes('aria-busy')).toBeUndefined()
        expect(wrapper.get('[data-part="alert"]').text()).toBe('The profiles could not be loaded.')

        await wrapper.get('[data-item="retry"]').trigger('click')
        await flushPromises()

        expect(wrapper.find('[data-part="alert"]').exists()).toBe(false)
        expect(wrapper.text()).toContain('Admin')
    })

    it('loads the settings of a form again after an error', async () => {
        api.cacheSettings.mockRejectedValueOnce(new TypeError('Failed to fetch'))
        api.cacheSettings.mockResolvedValueOnce({ maxSizeMegabytes: 2000 })
        const wrapper = await mountPage(CacheSettingsForm)
        await flushPromises()

        expect(wrapper.find('[data-part="form"]').exists()).toBe(false)
        await wrapper.get('[data-item="retry"]').trigger('click')
        await flushPromises()

        expect(api.cacheSettings).toHaveBeenCalledTimes(2)
        expect(wrapper.get('[data-part="form"]').attributes('aria-busy')).toBeUndefined()
        expect(wrapper.get('select').element.value).toBe('2000')
    })
})
