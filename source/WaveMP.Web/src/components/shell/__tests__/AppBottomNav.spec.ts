import { describe, expect, it } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import AppBottomNav from '../AppBottomNav.vue'
import { createWaveMpI18n } from '@/i18n'
import { createRoutes } from '@/router/routes'

async function mountBottomNav(language: 'en' | 'de') {
    const router = createRouter({ history: createMemoryHistory(), routes: createRoutes() })
    await router.push('/now-playing')
    const wrapper = mount(AppBottomNav, {
        global: { plugins: [router, createWaveMpI18n(language)] },
    })
    await flushPromises()
    return wrapper
}

describe('AppBottomNav', () => {
    it.each([
        ['en', ['Home', 'Playing', 'Library', 'Playlists'], 'Now Playing'],
        ['de', ['Startseite', 'Wieder­gabe', 'Bibliothek', 'Playlists'], 'Aktuelle Wiedergabe'],
    ] as const)('shows the short label of Now Playing in %s', async (language, labels, full) => {
        const wrapper = await mountBottomNav(language)

        const items = wrapper.findAll('[data-part="bottom-nav-item"]')
        expect(items.map((item) => item.get('[data-part="label"]').text())).toEqual(labels)

        // The full label stays the tooltip and the accessible name of the shortened entry.
        const nowPlaying = items[1]!
        expect(nowPlaying.attributes('title')).toBe(full)
        expect(nowPlaying.attributes('aria-label')).toBe(full)
        expect(nowPlaying.attributes('aria-current')).toBe('page')
        wrapper.unmount()
    })

    it('gives every entry the same structure: an icon and a label', async () => {
        const wrapper = await mountBottomNav('en')

        const items = wrapper.findAll('[data-part="bottom-nav-item"]')
        expect(items).toHaveLength(4)
        for (const item of items) {
            expect(
                item.findAll(':scope > [data-part]').map((child) => child.attributes('data-part')),
            ).toEqual(['icon', 'label'])
        }
        // Entries with their full label need no tooltip of their own.
        for (const index of [0, 2, 3]) {
            expect(items[index]!.attributes('title')).toBeUndefined()
            expect(items[index]!.attributes('aria-label')).toBeUndefined()
        }
        wrapper.unmount()
    })
})
