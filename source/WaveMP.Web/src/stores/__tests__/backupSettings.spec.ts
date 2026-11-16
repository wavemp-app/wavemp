import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

const api = vi.hoisted(() => ({
    get: vi.fn(),
    patch: vi.fn(),
}))

vi.mock('@/api/client', () => ({
    apiClient: {
        api: {
            v1: {
                settings: {
                    backup: {
                        get: () => api.get(),
                        patch: (body: unknown) => api.patch(body),
                    },
                },
            },
        },
    },
}))

const { useBackupSettingsStore } = await import('../backupSettings')

describe('backup settings store', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
        api.get.mockReset()
        api.patch.mockReset()
    })

    it('loads the settings', async () => {
        api.get.mockResolvedValue({ scheduleTime: '03:00', retentionCount: 7 })
        const store = useBackupSettingsStore()

        await store.load()

        expect(store.status).toBe('ready')
        expect(store.settings).toEqual({ scheduleTime: '03:00', retentionCount: 7 })
    })

    it('reports errors while loading', async () => {
        api.get.mockRejectedValue(new Error('offline'))
        const store = useBackupSettingsStore()

        await store.load()

        expect(store.status).toBe('error')
    })

    it('keeps the saved settings and passes errors on', async () => {
        api.patch.mockResolvedValueOnce({ scheduleTime: '22:15', retentionCount: 14 })
        api.patch.mockRejectedValueOnce(new Error('offline'))
        const store = useBackupSettingsStore()

        await store.update({ scheduleTime: '22:15', retentionCount: 14 })

        expect(api.patch).toHaveBeenCalledWith({ scheduleTime: '22:15', retentionCount: 14 })
        expect(store.settings).toEqual({ scheduleTime: '22:15', retentionCount: 14 })
        await expect(store.update({ retentionCount: 3 })).rejects.toThrow('offline')
        expect(store.settings).toEqual({ scheduleTime: '22:15', retentionCount: 14 })
    })
})
