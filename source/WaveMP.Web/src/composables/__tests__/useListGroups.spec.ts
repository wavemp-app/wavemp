import { describe, expect, it, vi } from 'vitest'
import type { ListGroup } from '@/api/generated/models'
import { groupPageSize, useListGroups } from '../useListGroups'

/** Groups of consecutive entries, each of the same size. */
function groupsOf(count: number, size: number): ListGroup[] {
    return Array.from({ length: count }, (_, index) => ({
        kind: 'Album',
        position: index * size,
        count: size,
    }))
}

function server(all: ListGroup[]) {
    return vi.fn((offset: number, limit: number) =>
        Promise.resolve({ items: all.slice(offset, offset + limit), total: all.length }),
    )
}

describe('useListGroups', () => {
    it('loads nothing until the groups are needed and then all pages of them', async () => {
        const fetchPage = server(groupsOf(2500, 3))
        const groups = useListGroups(fetchPage)

        expect(fetchPage).not.toHaveBeenCalled()
        expect(groups.status.value).toBe('idle')

        await groups.load()

        expect(groups.status.value).toBe('ready')
        expect(groups.groups.value).toHaveLength(2500)
        expect(fetchPage.mock.calls).toEqual([
            [0, groupPageSize],
            [1000, groupPageSize],
            [2000, groupPageSize],
        ])
    })

    it('loads the groups once, also when asked twice at the same time', async () => {
        const fetchPage = server(groupsOf(10, 1))
        const groups = useListGroups(fetchPage)

        await Promise.all([groups.load(), groups.load()])
        await groups.load()

        expect(fetchPage).toHaveBeenCalledTimes(1)
    })

    it('loads again after the list was invalidated and drops older answers', async () => {
        let resolveOld: (value: unknown) => void = () => {}
        const fetchPage = vi
            .fn()
            .mockReturnValueOnce(new Promise((resolve) => (resolveOld = resolve)))
            .mockResolvedValueOnce({ items: groupsOf(2, 5), total: 2 })
        const groups = useListGroups(fetchPage)

        const old = groups.load()
        groups.invalidate()
        await groups.load()
        resolveOld({ items: groupsOf(7, 1), total: 7 })
        await old

        expect(groups.groups.value).toHaveLength(2)
        expect(groups.status.value).toBe('ready')
        expect(fetchPage).toHaveBeenCalledTimes(2)
    })

    it('reports errors and tries again on the next load', async () => {
        const fetchPage = vi
            .fn()
            .mockRejectedValueOnce(new Error('offline'))
            .mockResolvedValueOnce({ items: groupsOf(3, 1), total: 3 })
        const groups = useListGroups(fetchPage)

        await groups.load()
        expect(groups.status.value).toBe('error')

        await groups.load()
        expect(groups.status.value).toBe('ready')
        expect(groups.groups.value).toHaveLength(3)
    })

    it('finds the group of a position by the range of its entries', async () => {
        // A list of 6300 entries in 900 groups.
        const groups = useListGroups(server(groupsOf(900, 7)))

        expect(groups.groupAt(0)).toBeUndefined()
        await groups.load()

        expect(groups.groupAt(0)?.position).toBe(0)
        expect(groups.groupAt(6)?.position).toBe(0)
        expect(groups.groupAt(7)?.position).toBe(7)
        expect(groups.groupAt(6299)?.position).toBe(6293)
        expect(groups.groupAt(6300)).toBeUndefined()
        expect(groups.groupAt(-1)).toBeUndefined()
    })
})
