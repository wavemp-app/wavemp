import { describe, expect, it, vi } from 'vitest'
import { usePagedList } from '../usePagedList'

function page(items: string[], total: number) {
    return { items, total }
}

describe('usePagedList', () => {
    it('loads the first page and then the next ones', async () => {
        const fetchPage = vi
            .fn()
            .mockResolvedValueOnce(page(['a', 'b'], 3))
            .mockResolvedValueOnce(page(['c'], 3))
        const list = usePagedList<string>(fetchPage, 2)

        await list.reload()
        const firstHasMore = list.hasMore.value
        await list.loadMore()

        expect(firstHasMore).toBe(true)
        expect(list.items.value).toEqual(['a', 'b', 'c'])
        expect(list.status.value).toBe('ready')
        expect(list.hasMore.value).toBe(false)
        expect(fetchPage.mock.calls).toEqual([
            [0, 2],
            [2, 2],
        ])
    })

    it('drops the answer of an older reload', async () => {
        let resolveFirst: (value: unknown) => void = () => {}
        const fetchPage = vi
            .fn()
            .mockReturnValueOnce(new Promise((resolve) => (resolveFirst = resolve)))
            .mockResolvedValueOnce(page(['new'], 1))
        const list = usePagedList<string>(fetchPage)

        const first = list.reload()
        await list.reload()
        resolveFirst(page(['old'], 1))
        await first

        expect(list.items.value).toEqual(['new'])
    })

    it('reports errors of the first page and keeps the list on errors of further pages', async () => {
        const failing = usePagedList<string>(vi.fn().mockRejectedValue(new Error('offline')))
        await failing.reload()
        expect(failing.status.value).toBe('error')

        const fetchPage = vi
            .fn()
            .mockResolvedValueOnce(page(['a'], 2))
            .mockRejectedValueOnce(new Error('offline'))
        const list = usePagedList<string>(fetchPage, 1)
        await list.reload()
        await list.loadMore()

        expect(list.items.value).toEqual(['a'])
        expect(list.loadingMore.value).toBe(false)
        expect(list.hasMore.value).toBe(true)
    })

    it('ends the list at an empty page', async () => {
        const fetchPage = vi
            .fn()
            .mockResolvedValueOnce(page(['a'], 5))
            .mockResolvedValueOnce(page([], 5))
        const list = usePagedList<string>(fetchPage, 1)

        await list.reload()
        await list.loadMore()

        expect(list.total.value).toBe(1)
        expect(list.hasMore.value).toBe(false)
    })

    it('jumps to a position with one page and loads the pages before it on demand', async () => {
        const fetchPage = vi.fn((offset: number, limit: number) =>
            Promise.resolve(
                page(
                    Array.from({ length: Math.min(limit, 6000 - offset) }, (_, index) =>
                        String(offset + index),
                    ),
                    6000,
                ),
            ),
        )
        const list = usePagedList<string>(fetchPage, 100)
        await list.reload()

        const found = await list.jumpTo(4321)

        expect(found).toBe(true)
        expect(list.start.value).toBe(4321)
        expect(list.items.value[0]).toBe('4321')
        expect(list.items.value).toHaveLength(100)
        expect(list.hasPrevious.value).toBe(true)
        expect(list.hasMore.value).toBe(true)

        await list.loadPrevious()
        await list.loadMore()

        expect(list.start.value).toBe(4221)
        expect(list.items.value[0]).toBe('4221')
        expect(list.items.value.at(-1)).toBe('4520')
        expect(fetchPage.mock.calls).toEqual([
            [0, 100],
            [4321, 100],
            [4221, 100],
            [4421, 100],
        ])
    })

    it('loads only what is left before the first page and stops at the start', async () => {
        const fetchPage = vi.fn((offset: number, limit: number) =>
            Promise.resolve(
                page(
                    Array.from({ length: limit }, (_, index) => String(offset + index)),
                    500,
                ),
            ),
        )
        const list = usePagedList<string>(fetchPage, 100)

        await list.jumpTo(30)
        await list.loadPrevious()

        expect(fetchPage.mock.calls.at(-1)).toEqual([0, 30])
        expect(list.start.value).toBe(0)
        expect(list.items.value[0]).toBe('0')
        expect(list.items.value[30]).toBe('30')
        expect(list.hasPrevious.value).toBe(false)
    })

    it('loads nothing when the position is already in the window', async () => {
        const fetchPage = vi.fn().mockResolvedValue(page(['a', 'b', 'c'], 3))
        const list = usePagedList<string>(fetchPage, 3)
        await list.reload()

        const found = await list.jumpTo(2)

        expect(found).toBe(true)
        expect(fetchPage).toHaveBeenCalledTimes(1)
        expect(list.start.value).toBe(0)
    })

    it('keeps the window when a jump fails or finds nothing', async () => {
        const fetchPage = vi
            .fn()
            .mockResolvedValueOnce(page(['a'], 10))
            .mockRejectedValueOnce(new Error('offline'))
            .mockResolvedValueOnce(page([], 1))
        const list = usePagedList<string>(fetchPage, 1)
        await list.reload()

        const failed = await list.jumpTo(5)
        const empty = await list.jumpTo(8)

        expect(failed).toBe(false)
        expect(empty).toBe(false)
        expect(list.items.value).toEqual(['a'])
        expect(list.start.value).toBe(0)
        expect(list.status.value).toBe('ready')
    })

    it('drops pages of an older window after a jump or a reload', async () => {
        let resolveMore: (value: unknown) => void = () => {}
        let resolvePrevious: (value: unknown) => void = () => {}
        const fetchPage = vi
            .fn()
            .mockResolvedValueOnce(page(['50'], 100))
            .mockReturnValueOnce(new Promise((resolve) => (resolveMore = resolve)))
            .mockReturnValueOnce(new Promise((resolve) => (resolvePrevious = resolve)))
            .mockResolvedValueOnce(page(['80'], 100))
            .mockResolvedValueOnce(page(['0'], 100))
        const list = usePagedList<string>(fetchPage, 1)

        await list.jumpTo(50)
        const more = list.loadMore()
        const previous = list.loadPrevious()
        await list.jumpTo(80)
        resolveMore(page(['51'], 100))
        resolvePrevious(page(['49'], 100))
        await Promise.all([more, previous])

        expect(list.items.value).toEqual(['80'])
        expect(list.start.value).toBe(80)
        expect(list.loadingMore.value).toBe(false)
        expect(list.loadingPrevious.value).toBe(false)

        await list.reload()

        expect(list.items.value).toEqual(['0'])
        expect(list.start.value).toBe(0)
        expect(list.hasPrevious.value).toBe(false)
    })

    it('drops a jump that a newer reload overtook', async () => {
        let resolveJump: (value: unknown) => void = () => {}
        const fetchPage = vi
            .fn()
            .mockReturnValueOnce(new Promise((resolve) => (resolveJump = resolve)))
            .mockResolvedValueOnce(page(['0'], 100))
        const list = usePagedList<string>(fetchPage, 1)

        const jump = list.jumpTo(40)
        await list.reload()
        resolveJump(page(['40'], 100))

        expect(await jump).toBe(false)
        expect(list.items.value).toEqual(['0'])
        expect(list.start.value).toBe(0)
    })
})
