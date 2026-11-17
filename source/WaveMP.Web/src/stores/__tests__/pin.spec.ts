import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

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

const { usePinStore } = await import('../pin')

describe('pin store', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
        for (const mock of Object.values(api)) {
            mock.mockReset()
        }
    })

    it('loads the state of the own PIN', async () => {
        api.get.mockResolvedValue({ isSet: true, isManagedByEnvironment: true })
        const store = usePinStore()

        await store.load()

        expect(store.status).toBe('ready')
        expect(store.isSet).toBe(true)
        expect(store.isManagedByEnvironment).toBe(true)
    })

    it('reports errors while loading', async () => {
        api.get.mockRejectedValue(new Error('offline'))
        const store = usePinStore()

        await store.load()

        expect(store.status).toBe('error')
        expect(store.isSet).toBe(false)
    })

    it('sets the PIN and takes over the answer', async () => {
        api.put.mockResolvedValue({ isSet: true, isManagedByEnvironment: false })
        const store = usePinStore()

        await store.set('4826')

        expect(api.put).toHaveBeenCalledWith({ pin: '4826' })
        expect(store.isSet).toBe(true)
    })

    it('removes the PIN', async () => {
        api.get.mockResolvedValue({ isSet: true, isManagedByEnvironment: false })
        api.remove.mockResolvedValue(undefined)
        const store = usePinStore()
        await store.load()

        await store.remove()

        expect(api.remove).toHaveBeenCalledOnce()
        expect(store.isSet).toBe(false)
    })

    it('passes errors of changes on and keeps the state', async () => {
        api.get.mockResolvedValue({ isSet: true, isManagedByEnvironment: true })
        api.remove.mockRejectedValue({
            code: 'pin.managed_by_environment',
            responseStatusCode: 409,
        })
        const store = usePinStore()
        await store.load()

        await expect(store.remove()).rejects.toMatchObject({ code: 'pin.managed_by_environment' })
        expect(store.isSet).toBe(true)
    })
})
