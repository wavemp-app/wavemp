import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import type { LyricsLookupSummary } from '@/api/generated/models'
import { createWaveMpI18n } from '@/i18n'
import LyricsLookupPanel from '../LyricsLookupPanel.vue'
import LyricsLookupResults from '../LyricsLookupResults.vue'

const api = vi.hoisted(() => ({
    getLyricsLookupSummary: vi.fn(),
    forgetLyricsLookups: vi.fn(),
}))

vi.mock('@/lyrics/lookupApi', () => api)

const summary: LyricsLookupSummary = {
    notFoundCount: 1_234,
    instrumentalCount: 5,
    lookUpAgainAfterMinutes: 43_200,
}

async function mountPanel() {
    const wrapper = mount(LyricsLookupPanel, {
        global: { plugins: [createWaveMpI18n('en')] },
        attachTo: document.body,
    })
    await flushPromises()
    return wrapper
}

type Wrapper = Awaited<ReturnType<typeof mountPanel>>

function stats(wrapper: Wrapper) {
    return wrapper
        .findAll('[data-part="stat-item"]')
        .map((item) => [
            item.attributes('data-item'),
            item.get('[data-part="label"]').text(),
            item.get('[data-part="stat-value"]').text(),
        ])
}

function forgetButton(wrapper: Wrapper) {
    return wrapper.get('[data-part="button"][data-item="forget-lyrics-lookups"]')
}

describe('LyricsLookupPanel', () => {
    beforeEach(() => {
        api.getLyricsLookupSummary.mockReset().mockResolvedValue(summary)
        api.forgetLyricsLookups.mockReset().mockResolvedValue(undefined)
    })

    it('shows how many songs the lookup found no lyrics for and the wait', async () => {
        const wrapper = await mountPanel()

        expect(wrapper.get('[data-part="lyrics-lookups"]').attributes('data-state')).toBe(
            'has-results',
        )
        expect(stats(wrapper)).toEqual([
            ['not-found', 'No lyrics found', '1,234'],
            ['instrumental', 'Listed as instrumental', '5'],
            ['wait', 'Looked up again after', '30 days'],
        ])
        expect(forgetButton(wrapper).text()).toBe('Forget results')
        expect(forgetButton(wrapper).attributes('disabled')).toBeUndefined()
        wrapper.unmount()
    })

    it('forgets the results and shows the numbers afterwards', async () => {
        const wrapper = await mountPanel()
        api.getLyricsLookupSummary.mockResolvedValue({
            notFoundCount: 0,
            instrumentalCount: 0,
            lookUpAgainAfterMinutes: 43_200,
        })

        await forgetButton(wrapper).trigger('click')
        await flushPromises()

        expect(api.forgetLyricsLookups).toHaveBeenCalledOnce()
        expect(wrapper.get('[data-part="alert"][data-state="success"]').text()).toBe(
            'Results forgotten. The next lyrics download looks these songs up again.',
        )
        expect(wrapper.get('[data-part="lyrics-lookups"]').attributes('data-state')).toBe('empty')
        expect(forgetButton(wrapper).attributes('disabled')).toBeDefined()
        wrapper.unmount()
    })

    it('reports results that cannot be forgotten or loaded', async () => {
        api.forgetLyricsLookups.mockRejectedValue(new Error('offline'))
        const wrapper = await mountPanel()

        await forgetButton(wrapper).trigger('click')
        await flushPromises()
        expect(wrapper.get('[data-part="alert"][data-state="error"]').text()).toBe(
            'The results could not be forgotten.',
        )
        wrapper.unmount()

        api.getLyricsLookupSummary.mockRejectedValue(new Error('offline'))
        const failed = await mountPanel()
        expect(failed.get('[data-part="alert"]').text()).toBe(
            'The results of the lyrics lookup could not be loaded.',
        )
        expect(failed.find('[data-part="lyrics-lookups"]').exists()).toBe(false)
        failed.unmount()
    })
})

describe('LyricsLookupResults', () => {
    it('says that songs are never looked up again and waits while loading', () => {
        const wrapper = mount(LyricsLookupResults, {
            props: { summary: { ...summary, lookUpAgainAfterMinutes: null } },
            global: { plugins: [createWaveMpI18n('de')] },
        })
        expect(wrapper.get('[data-item="wait"] [data-part="stat-value"]').text()).toBe('Nie')
        expect(wrapper.get('[data-item="not-found"] [data-part="stat-value"]').text()).toBe('1.234')

        const loading = mount(LyricsLookupResults, {
            props: { summary: null },
            global: { plugins: [createWaveMpI18n('en')] },
        })
        expect(loading.get('[data-part="lyrics-lookups"]').attributes('data-state')).toBe('loading')
        expect(loading.get('[data-part="lyrics-lookups"]').attributes('aria-busy')).toBe('true')
        expect(loading.find('[data-part="stat-list"]').exists()).toBe(false)
        expect(
            loading.get('[data-item="forget-lyrics-lookups"]').attributes('disabled'),
        ).toBeDefined()
    })

    it('emits forget when the button is pressed', async () => {
        const wrapper = mount(LyricsLookupResults, {
            props: { summary },
            global: { plugins: [createWaveMpI18n('en')] },
        })

        await wrapper.get('[data-item="forget-lyrics-lookups"]').trigger('click')

        expect(wrapper.emitted('forget')).toHaveLength(1)
    })
})
