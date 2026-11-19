import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

const api = vi.hoisted(() => ({
    list: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    remove: vi.fn(),
    byId: vi.fn(),
}))

vi.mock('@/api/client', () => ({
    apiClient: {
        api: {
            v1: {
                sources: {
                    get: (config: unknown) => api.list(config),
                    post: (body: unknown) => api.create(body),
                    byId: (id: string) => {
                        api.byId(id)
                        return {
                            patch: (body: unknown) => api.update(body),
                            delete: () => api.remove(),
                        }
                    },
                },
            },
        },
    },
}))

const { useMediaSourcesStore } = await import('../mediaSources')

describe('media sources store', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
        for (const mock of Object.values(api)) {
            mock.mockReset()
        }
    })

    it('loads the folders once and tells whether there are several', async () => {
        api.list.mockResolvedValue({
            items: [
                { id: 'a', name: 'Media' },
                { id: 'b', name: 'Videos' },
            ],
            total: 2,
        })
        const store = useMediaSourcesStore()

        await store.ensureLoaded()
        await store.ensureLoaded()

        expect(api.list).toHaveBeenCalledTimes(1)
        expect(store.status).toBe('ready')
        expect(store.hasSeveral).toBe(true)
        expect(store.find('b')?.name).toBe('Videos')
        expect(store.find(null)).toBeUndefined()
    })

    it('reports a failed load and tries again on the next call', async () => {
        api.list.mockRejectedValueOnce(new Error('offline'))
        api.list.mockResolvedValueOnce({ items: [{ id: 'a', name: 'Media' }], total: 1 })
        const store = useMediaSourcesStore()

        await store.ensureLoaded()
        expect(store.status).toBe('error')
        await store.ensureLoaded()

        expect(store.status).toBe('ready')
        expect(store.hasSeveral).toBe(false)
    })

    it('keeps the list sorted by name after adding, changing and removing', async () => {
        const store = useMediaSourcesStore()
        store.sources = [{ id: 'm', name: 'Media' }]
        api.create.mockResolvedValue({ id: 'c', name: 'clips' })
        api.update.mockResolvedValue({ id: 'm', name: 'Zebra' })

        await store.create({ name: 'clips', path: '/clips' })
        expect(store.sources.map((source) => source.name)).toEqual(['clips', 'Media'])

        await store.update('m', { name: 'Zebra' })
        expect(api.byId).toHaveBeenCalledWith('m')
        expect(store.sources.map((source) => source.name)).toEqual(['clips', 'Zebra'])

        await store.remove('c')
        expect(store.sources.map((source) => source.id)).toEqual(['m'])
    })

    it('keeps the list when a change fails', async () => {
        const store = useMediaSourcesStore()
        store.sources = [{ id: 'm', name: 'Media' }]
        api.remove.mockRejectedValue({ code: 'scan.already_running' })

        await expect(store.remove('m')).rejects.toEqual({ code: 'scan.already_running' })
        expect(store.sources).toHaveLength(1)
    })
})
