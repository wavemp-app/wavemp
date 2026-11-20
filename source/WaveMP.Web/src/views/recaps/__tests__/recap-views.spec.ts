import type { Component } from 'vue'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import { DateOnly } from '@microsoft/kiota-abstractions'
import type { ListeningStatistics } from '@/api/generated/models'
import { createWaveMpI18n } from '@/i18n'
import { createRoutes } from '@/router/routes'
import RecapView from '../RecapView.vue'
import StatisticsView from '../StatisticsView.vue'

const api = vi.hoisted(() => ({
    getStatistics: vi.fn(),
    listRecapShares: vi.fn(),
    shareRecap: vi.fn(),
    stopSharingRecap: vi.fn(),
}))

vi.mock('@/recaps/api', () => api)

const statistics: ListeningStatistics = {
    from: new DateOnly({ year: 2026, month: 10, day: 21 }),
    to: new DateOnly({ year: 2026, month: 11, day: 20 }),
    totals: { plays: 3, tracks: 2, artists: 1, listeningMilliseconds: 360_000, activeDays: 1 },
    topTracks: [
        { plays: 2, track: { id: 't1', title: 'First Song', artist: { id: 'a', name: 'Nova' } } },
        { plays: 1, track: { id: 't2', title: 'Second Song', artist: { id: 'a', name: 'Nova' } } },
    ],
    topArtists: [{ plays: 3, tracks: 2, artist: { id: 'a', name: 'Nova' } }],
    hours: [{ hour: 20, plays: 3 }],
}

async function mountView(path: string, component: Component) {
    const router = createRouter({ history: createMemoryHistory(), routes: createRoutes([]) })
    await router.push(path)
    const wrapper = mount(component, {
        global: { plugins: [router, createPinia(), createWaveMpI18n('en')] },
        attachTo: document.body,
    })
    await flushPromises()
    return wrapper
}

describe('recap pages', () => {
    const warnings: string[] = []

    beforeEach(() => {
        warnings.length = 0
        vi.spyOn(console, 'warn').mockImplementation((message: unknown) => {
            warnings.push(String(message))
        })
        api.getStatistics.mockResolvedValue(statistics)
        api.listRecapShares.mockResolvedValue({ items: [], total: 0 })
    })

    afterEach(() => {
        vi.restoreAllMocks()
        document.body.innerHTML = ''
    })

    it('shows the story of the period once', async () => {
        const wrapper = await mountView('/recap', RecapView)

        expect(api.getStatistics).toHaveBeenCalledTimes(1)
        expect(wrapper.find("[data-view='recap']").attributes('data-state')).toBe('ready')
        const slide = wrapper.find("[data-part='recap-slide']")
        expect(slide.attributes('data-kind')).toBe('intro')
        expect(wrapper.findAll("[data-part='recap-progress-segment']").length).toBeGreaterThan(3)
        expect(warnings.filter((warning) => warning.includes('recursive'))).toEqual([])
        wrapper.unmount()
    })

    it('shows the statistics with tables and charts', async () => {
        const wrapper = await mountView('/recap/statistics', StatisticsView)

        expect(api.getStatistics).toHaveBeenCalledTimes(1)
        expect(wrapper.find("[data-part='stat-item'][data-item='plays']").text()).toContain('3')
        expect(wrapper.findAll("[data-part='chart']").length).toBeGreaterThan(0)
        expect(
            wrapper.findAll("[data-item='top-tracks'] [data-part='stats-table-row']"),
        ).toHaveLength(3)
        expect(warnings.filter((warning) => warning.includes('recursive'))).toEqual([])
        wrapper.unmount()
    })
})
