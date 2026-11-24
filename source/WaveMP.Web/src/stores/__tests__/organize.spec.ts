import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { effectScope } from 'vue'
import type { OrganizeOperation } from '@/api/generated/models'

const api = vi.hoisted(() => ({
    getOrganizeAvailability: vi.fn(),
    getOrganizeOperation: vi.fn(),
    listOrganizeOperations: vi.fn(),
    createOrganizePreview: vi.fn(),
    createOrganizeUndoPreview: vi.fn(),
    applyOrganizeOperation: vi.fn(),
    cancelOrganizeOperation: vi.fn(),
    discardOrganizeOperation: vi.fn(),
    listOwnAlbums: vi.fn(),
}))

vi.mock('@/admin/organizeApi', () => ({
    getOrganizeAvailability: api.getOrganizeAvailability,
    getOrganizeOperation: api.getOrganizeOperation,
    listOrganizeOperations: api.listOrganizeOperations,
    createOrganizePreview: api.createOrganizePreview,
    createOrganizeUndoPreview: api.createOrganizeUndoPreview,
    applyOrganizeOperation: api.applyOrganizeOperation,
    cancelOrganizeOperation: api.cancelOrganizeOperation,
    discardOrganizeOperation: api.discardOrganizeOperation,
}))

vi.mock('@/admin/ownAlbumApi', () => ({
    listOwnAlbums: api.listOwnAlbums,
}))

const { organizePollIntervalMs, useOrganizeStore } = await import('../organize')

function operation(status: OrganizeOperation['status'], extra: OrganizeOperation = {}) {
    return { id: 'op', kind: 'Library', status, ...extra } as OrganizeOperation
}

describe('organize store', () => {
    const scope = effectScope()

    beforeEach(() => {
        vi.useFakeTimers()
        setActivePinia(createPinia())
        for (const mock of Object.values(api)) {
            mock.mockReset()
        }
        api.getOrganizeAvailability.mockResolvedValue({
            activeOperationId: null,
            sources: [{ sourceId: 's', status: 'Available' }],
        })
        api.listOrganizeOperations.mockResolvedValue({ items: [], total: 0 })
        api.listOwnAlbums.mockResolvedValue({ items: [], total: 0 })
    })

    afterEach(() => {
        vi.useRealTimers()
    })

    function store() {
        return scope.run(() => useOrganizeStore())!
    }

    it('polls a library preview until it is computed, then loads the log again', async () => {
        const organize = store()
        api.createOrganizePreview.mockResolvedValue(operation('Planning'))
        api.getOrganizeOperation
            .mockResolvedValueOnce(operation('Planning', { totalItems: 4 }))
            .mockResolvedValueOnce(operation('Previewed', { totalItems: 4 }))

        await organize.previewLibrary('s')

        expect(api.createOrganizePreview).toHaveBeenCalledWith({ kind: 'Library', sourceId: 's' })
        expect(organize.polling).toBe(true)
        expect(organize.active).toBe(true)

        await vi.advanceTimersByTimeAsync(organizePollIntervalMs)
        expect(api.getOrganizeOperation).toHaveBeenCalledTimes(1)
        expect(organize.polling).toBe(true)

        await vi.advanceTimersByTimeAsync(organizePollIntervalMs)
        expect(api.getOrganizeOperation).toHaveBeenCalledTimes(2)
        expect(organize.current?.status).toBe('Previewed')
        expect(organize.polling).toBe(false)
        expect(api.listOrganizeOperations).toHaveBeenCalled()
        expect(api.getOrganizeAvailability).toHaveBeenCalled()

        await vi.advanceTimersByTimeAsync(organizePollIntervalMs * 3)
        expect(api.getOrganizeOperation).toHaveBeenCalledTimes(2)
    })

    it('follows a run after applying and is busy until it ends', async () => {
        const organize = store()
        await organize.loadAvailability()
        expect(organize.blocked).toBeNull()

        api.applyOrganizeOperation.mockResolvedValue(operation('Queued'))
        api.getOrganizeOperation.mockResolvedValue(operation('Completed', { doneItems: 3 }))

        await organize.apply('op', true)

        expect(api.applyOrganizeOperation).toHaveBeenCalledWith('op', true)
        expect(organize.blocked).toBe('busy')
        expect(organize.polling).toBe(true)

        await vi.advanceTimersByTimeAsync(organizePollIntervalMs)
        expect(organize.current?.status).toBe('Completed')
        expect(organize.polling).toBe(false)
        expect(organize.blocked).toBeNull()
    })

    it('follows an operation that already runs when the page opens', async () => {
        api.getOrganizeAvailability.mockResolvedValue({
            activeOperationId: 'op',
            sources: [{ sourceId: 's', status: 'Available' }],
        })
        api.getOrganizeOperation.mockResolvedValue(operation('Running'))
        const organize = store()

        await organize.loadAvailability()

        expect(organize.blocked).toBe('busy')
        expect(organize.current?.id).toBe('op')
        expect(organize.polling).toBe(true)
        organize.forgetCurrent()
        expect(organize.polling).toBe(false)
    })

    it('reports a read-only media folder', async () => {
        api.getOrganizeAvailability.mockResolvedValue({
            sources: [{ sourceId: 's', status: 'ReadOnly' }],
        })
        const organize = store()

        await organize.loadAvailability()

        expect(organize.availabilityStatus).toBe('ready')
        expect(organize.blocked).toBe('readOnly')
    })

    it('reports an error while loading the availability', async () => {
        api.getOrganizeAvailability.mockRejectedValue(new Error('offline'))
        const organize = store()

        await organize.loadAvailability()

        expect(organize.availabilityStatus).toBe('error')
    })

    it('pages through the log', async () => {
        api.listOrganizeOperations
            .mockResolvedValueOnce({ items: [{ id: 'a' }], total: 2 })
            .mockResolvedValueOnce({ items: [{ id: 'b' }], total: 2 })
        const organize = store()

        await organize.loadOperations()
        expect(organize.hasMoreOperations).toBe(true)
        await organize.loadOperations({ reset: false })

        expect(organize.operations.map((item) => item.id)).toEqual(['a', 'b'])
        expect(organize.hasMoreOperations).toBe(false)
        expect(api.listOrganizeOperations).toHaveBeenLastCalledWith({ offset: 1, limit: 20 })
    })

    it('throws a preview away and stops following it', async () => {
        api.listOrganizeOperations.mockResolvedValue({ items: [{ id: 'op' }], total: 1 })
        const organize = store()
        await organize.loadOperations()
        organize.follow(operation('Previewed'))

        await organize.discard('op')

        expect(api.discardOrganizeOperation).toHaveBeenCalledWith('op')
        expect(organize.operations).toEqual([])
        expect(organize.current).toBeNull()
    })

    it('gives the songs chosen in a menu out once', () => {
        const organize = store()
        organize.pendingTrackIds = ['a', 'b']

        expect(organize.takePendingTracks()).toEqual(['a', 'b'])
        expect(organize.takePendingTracks()).toEqual([])
    })
})
