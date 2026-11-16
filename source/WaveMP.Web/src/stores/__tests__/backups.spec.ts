import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

const api = vi.hoisted(() => ({
    getJob: vi.fn(),
    list: vi.fn(),
    create: vi.fn(),
    remove: vi.fn(),
    jobId: vi.fn(),
    backupId: vi.fn(),
}))

vi.mock('@/api/client', () => ({
    apiClient: {
        api: {
            v1: {
                jobs: {
                    byId: (id: string) => {
                        api.jobId(id)
                        return { get: () => api.getJob() }
                    },
                },
                backups: {
                    get: (config: unknown) => api.list(config),
                    post: () => api.create(),
                    byId: (id: string) => {
                        api.backupId(id)
                        return { delete: () => api.remove() }
                    },
                },
            },
        },
    },
}))

const { useBackupsStore } = await import('../backups')

const job = { id: 'database-backup', state: 'Idle', dailyAt: '03:00' }
const backup = {
    id: 'wavemp-20261116T030000Z-schedule',
    fileName: 'wavemp-20261116T030000Z-schedule.dump',
    sizeBytes: 1000,
    trigger: 'Schedule',
}

describe('backups store', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
        for (const mock of Object.values(api)) {
            mock.mockReset()
        }
        api.getJob.mockResolvedValue(job)
        api.list.mockResolvedValue({ items: [backup], total: 1 })
    })

    it('loads the job of the backups and the exports', async () => {
        const store = useBackupsStore()

        await store.load()

        expect(api.jobId).toHaveBeenCalledWith('database-backup')
        expect(store.status).toBe('ready')
        expect(store.job).toEqual(job)
        expect(store.backups).toEqual([backup])
        expect(store.active).toBe(false)
    })

    it('knows when an export is running', async () => {
        api.getJob.mockResolvedValue({ ...job, state: 'Running' })
        const store = useBackupsStore()

        await store.load()

        expect(store.active).toBe(true)
    })

    it('reports errors while loading', async () => {
        api.list.mockRejectedValue(new Error('offline'))
        const store = useBackupsStore()

        await store.load()

        expect(store.status).toBe('error')
    })

    it('starts an export and tells when one is already running', async () => {
        const store = useBackupsStore()
        api.create.mockResolvedValueOnce({ id: 'run' })
        api.create.mockRejectedValueOnce({ responseStatusCode: 409, code: 'job.already_running' })
        api.create.mockRejectedValueOnce(new Error('offline'))

        expect(await store.create()).toBe('started')
        expect(await store.create()).toBe('alreadyRunning')
        expect(await store.create()).toBe('error')
    })

    it('deletes an export and loads the list again', async () => {
        const store = useBackupsStore()
        api.remove.mockResolvedValue(undefined)

        const result = await store.remove(backup.id)

        expect(result).toBe('deleted')
        expect(api.backupId).toHaveBeenCalledWith(backup.id)
        expect(api.list).toHaveBeenCalledTimes(1)
    })

    it('tells when the export to delete is gone', async () => {
        const store = useBackupsStore()
        api.remove.mockRejectedValue({ responseStatusCode: 404, code: 'backup.not_found' })

        expect(await store.remove(backup.id)).toBe('notFound')
    })
})
