import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import AppToolbar from '../AppToolbar.vue'
import { searchDelay } from '@/composables/useSearchField'
import { createWaveMpI18n } from '@/i18n'
import { createRoutes } from '@/router/routes'
import { usePageTitleStore } from '@/stores/pageTitle'
import { defaultLayoutOptions, layoutOptions } from '@/themes/layoutOptions'

async function mountToolbar(path: string) {
    const router = createRouter({ history: createMemoryHistory(), routes: createRoutes() })
    await router.push(path)
    const wrapper = mount(AppToolbar, {
        global: { plugins: [router, createWaveMpI18n('en')] },
        attachTo: document.body,
    })
    await flushPromises()
    return { wrapper, router }
}

describe('AppToolbar', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
    })

    afterEach(() => {
        vi.useRealTimers()
    })

    it('searches the library from the search field', async () => {
        vi.useFakeTimers()
        const { wrapper, router } = await mountToolbar('/library/albums')

        await wrapper.get('[data-part="search"] input').setValue('balad')
        await vi.advanceTimersByTimeAsync(searchDelay + 10)
        await flushPromises()

        expect(router.currentRoute.value.fullPath).toBe('/library/search?q=balad')
        wrapper.unmount()
    })

    it('offers the sort options of the list and puts the choice into the route', async () => {
        const { wrapper, router } = await mountToolbar('/library/songs')

        const sort = wrapper.get('[data-part="sort"]')
        expect(sort.attributes('disabled')).toBeUndefined()
        await sort.trigger('click')
        await flushPromises()

        const items = wrapper.findAll('[data-part="menu-item"]')
        expect(items.map((item) => item.attributes('data-item'))).toEqual([
            'sort-album-artist',
            'sort-album',
            'sort-title',
            'sort-artist',
            'sort-duration',
            'sort-year',
            'sort-genre',
            'sort-first-seen-at',
            'sort-rating',
            'direction-asc',
            'direction-desc',
        ])
        expect(items[0]!.attributes('role')).toBe('menuitemradio')
        expect(items[0]!.attributes('aria-checked')).toBe('true')

        await wrapper.get('[data-item="sort-title"]').trigger('click')
        await flushPromises()
        expect(router.currentRoute.value.query).toEqual({ sort: 'Title' })

        await wrapper.get('[data-item="direction-desc"]').trigger('click')
        await flushPromises()
        expect(router.currentRoute.value.query).toEqual({ sort: 'Title', direction: 'Desc' })
        wrapper.unmount()
    })

    it('disables the sort options where nothing can be sorted', async () => {
        const { wrapper } = await mountToolbar('/library/recently-added')

        expect(wrapper.get('[data-part="sort"]').attributes('disabled')).toBeDefined()
        wrapper.unmount()
    })

    it('ends the breadcrumb of a detail page with its name', async () => {
        const { wrapper } = await mountToolbar('/library/albums/first')
        usePageTitleStore().set('First Album')
        await flushPromises()

        const labels = wrapper
            .findAll('[data-part="breadcrumb-link"] [data-part="label"]')
            .map((label) => label.text())
        expect(labels).toEqual(['Music', 'Library', 'Album', 'First Album'])
        wrapper.unmount()
    })

    describe('view options', () => {
        function stubDesktop(desktop: boolean): void {
            vi.stubGlobal(
                'matchMedia',
                (query: string) =>
                    ({
                        matches: query === '(width >= 64rem)' && desktop,
                        addEventListener: () => undefined,
                        removeEventListener: () => undefined,
                    }) as unknown as MediaQueryList,
            )
        }

        function offerViews(): void {
            document.documentElement.style.setProperty('--wmp-layout-view-options', 'on')
        }

        function viewEntries(wrapper: Awaited<ReturnType<typeof mountToolbar>>['wrapper']) {
            return wrapper.findAll(
                '[data-part="context-menu"][aria-label="View options"] [data-part="menu-item"]',
            )
        }

        afterEach(() => {
            vi.unstubAllGlobals()
            document.documentElement.style.removeProperty('--wmp-layout-view-options')
            layoutOptions.value = defaultLayoutOptions
        })

        it('stays disabled while the theme does not offer the view options', async () => {
            stubDesktop(true)
            const { wrapper } = await mountToolbar('/library/songs')

            expect(wrapper.get('[data-part="view-switch"]').attributes('disabled')).toBeDefined()
            expect(wrapper.findAll('[data-part="context-menu"]')).toHaveLength(1)
            wrapper.unmount()
        })

        it('offers grouped and list on a list of songs and puts the choice into the route', async () => {
            stubDesktop(true)
            offerViews()
            const { wrapper, router } = await mountToolbar('/library/songs?sort=Title')

            const button = wrapper.get('[data-part="view-switch"]')
            expect(button.attributes('disabled')).toBeUndefined()
            expect(button.attributes('aria-haspopup')).toBe('menu')
            await button.trigger('click')
            await flushPromises()

            let entries = viewEntries(wrapper)
            expect(entries.map((entry) => entry.attributes('data-item'))).toEqual([
                'view-grouped',
                'view-list',
            ])
            expect(entries.map((entry) => entry.text())).toEqual(['Grouped by album', 'List'])
            expect(entries.map((entry) => entry.attributes('role'))).toEqual([
                'menuitemradio',
                'menuitemradio',
            ])
            expect(entries.map((entry) => entry.attributes('aria-checked'))).toEqual([
                'true',
                'false',
            ])
            expect(entries[1]!.find('[data-icon="view-list"]').exists()).toBe(true)

            await entries[1]!.trigger('click')
            await flushPromises()
            expect(router.currentRoute.value.query).toEqual({ sort: 'Title', view: 'list' })
            entries = viewEntries(wrapper)
            expect(entries.map((entry) => entry.attributes('aria-checked'))).toEqual([
                'false',
                'true',
            ])

            await entries[0]!.trigger('click')
            await flushPromises()
            expect(router.currentRoute.value.query).toEqual({ sort: 'Title' })
            wrapper.unmount()
        })

        it.each(['/library/recently-added', '/library/genres/Rock', '/library/years/2001'])(
            'offers the views on %s',
            async (path) => {
                stubDesktop(true)
                offerViews()
                const { wrapper } = await mountToolbar(path)

                expect(
                    wrapper.get('[data-part="view-switch"]').attributes('disabled'),
                ).toBeUndefined()
                wrapper.unmount()
            },
        )

        it.each(['/library/albums', '/library/albums/first', '/library/search?q=rock', '/'])(
            'stays disabled on %s, which shows no list of songs',
            async (path) => {
                stubDesktop(true)
                offerViews()
                const { wrapper } = await mountToolbar(path)

                expect(
                    wrapper.get('[data-part="view-switch"]').attributes('disabled'),
                ).toBeDefined()
                expect(viewEntries(wrapper)).toHaveLength(0)
                wrapper.unmount()
            },
        )

        it('stays disabled on phones, whatever the theme sets', async () => {
            stubDesktop(false)
            offerViews()
            const { wrapper } = await mountToolbar('/library/songs')

            expect(wrapper.get('[data-part="view-switch"]').attributes('disabled')).toBeDefined()
            wrapper.unmount()
        })
    })
})
