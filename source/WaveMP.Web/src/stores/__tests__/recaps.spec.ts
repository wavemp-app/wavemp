import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { RecapPeriodObject } from '@/api/generated/models'

const api = vi.hoisted(() => ({
    getStatistics: vi.fn(),
    listRecapShares: vi.fn(),
    shareRecap: vi.fn(),
    stopSharingRecap: vi.fn(),
}))

vi.mock('@/recaps/api', () => api)

const { useRecapsStore } = await import('../recaps')
const { useSessionStore } = await import('../session')

describe('recaps store', () => {
    beforeEach(() => {
        vi.useFakeTimers({ toFake: ['Date'] })
        vi.setSystemTime(new Date(Date.UTC(2026, 10, 20, 12)))
        setActivePinia(createPinia())
        for (const mock of Object.values(api)) {
            mock.mockReset()
        }
        api.getStatistics.mockResolvedValue({ totals: { plays: 3 } })
        api.listRecapShares.mockResolvedValue({ items: [], total: 0 })
    })

    afterEach(() => {
        vi.useRealTimers()
    })

    it('loads the last month by default', async () => {
        const recaps = useRecapsStore()
        await recaps.load()

        expect(recaps.period).toBe(RecapPeriodObject.LastMonth)
        expect(recaps.status).toBe('ready')
        expect(recaps.statistics?.totals?.plays).toBe(3)
        const query = api.getStatistics.mock.calls[0]?.[0]
        expect(query.to).toEqual(recaps.range?.to)
        expect(query.from).toEqual(recaps.range?.from)
        expect(query.timeZone).toBe(recaps.timeZone)
    })

    it('loads a period only once until it changes', async () => {
        const recaps = useRecapsStore()
        await recaps.load()
        await recaps.load()
        expect(api.getStatistics).toHaveBeenCalledTimes(1)

        recaps.setPeriod(RecapPeriodObject.AllTime)
        await recaps.load()
        expect(api.getStatistics).toHaveBeenCalledTimes(2)
        expect(api.getStatistics.mock.calls[1]?.[0].from).toBeNull()

        await recaps.load(true)
        expect(api.getStatistics).toHaveBeenCalledTimes(3)
    })

    it('does not ask for an invalid custom period', async () => {
        const recaps = useRecapsStore()
        recaps.setPeriod(RecapPeriodObject.Custom)
        recaps.setCustom({ from: '2026-11-10', to: '2026-11-01' })
        await recaps.load()

        expect(recaps.status).toBe('invalid')
        expect(api.getStatistics).not.toHaveBeenCalled()

        recaps.setCustom({ from: '2026-11-01', to: '2026-11-10' })
        await recaps.load()
        expect(recaps.status).toBe('ready')
        expect(api.getStatistics.mock.calls[0]?.[0].from).toEqual({ year: 2026, month: 11, day: 1 })
    })

    it('keeps only the answer of the latest period', async () => {
        let answerFirst: (value: unknown) => void = () => {}
        api.getStatistics.mockReturnValueOnce(new Promise((resolve) => (answerFirst = resolve)))
        api.getStatistics.mockResolvedValueOnce({ totals: { plays: 9 } })
        const recaps = useRecapsStore()

        const first = recaps.load()
        recaps.setPeriod(RecapPeriodObject.LastYear)
        await recaps.load()
        answerFirst({ totals: { plays: 1 } })
        await first

        expect(recaps.statistics?.totals?.plays).toBe(9)
    })

    it('reports errors and tries again', async () => {
        api.getStatistics.mockRejectedValueOnce(new Error('offline'))
        const recaps = useRecapsStore()
        await recaps.load()
        expect(recaps.status).toBe('error')

        await recaps.load()
        expect(recaps.status).toBe('ready')
    })

    it('shares the shown period and finds its share', async () => {
        const recaps = useRecapsStore()
        const range = recaps.range!
        const shared = {
            id: 's1',
            period: RecapPeriodObject.LastMonth,
            from: range.from,
            to: range.to,
            timeZone: recaps.timeZone,
            design: 'frost',
        }
        api.shareRecap.mockResolvedValue(shared)

        expect(recaps.currentShare).toBeUndefined()
        await recaps.share('frost')

        expect(api.shareRecap).toHaveBeenCalledWith({
            period: RecapPeriodObject.LastMonth,
            from: range.from,
            to: range.to,
            timeZone: recaps.timeZone,
            design: 'frost',
        })
        expect(recaps.currentShare?.id).toBe('s1')

        recaps.setPeriod(RecapPeriodObject.LastYear)
        expect(recaps.currentShare).toBeUndefined()

        await recaps.stopSharing('s1')
        expect(api.stopSharingRecap).toHaveBeenCalledWith('s1')
        expect(recaps.ownShares).toEqual([])
    })

    it('forgets everything when the profile changes', async () => {
        const recaps = useRecapsStore()
        const session = useSessionStore()
        await recaps.load()

        session.$patch({ token: 'other' } as never)
        await Promise.resolve()

        expect(recaps.statistics).toBeNull()
        expect(recaps.status).toBe('idle')
    })
})
