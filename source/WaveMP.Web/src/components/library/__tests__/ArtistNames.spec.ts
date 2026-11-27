import { describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import { createMemoryHistory, createRouter } from 'vue-router'
import type { ArtistCredit } from '@/api/generated/models'
import { createWaveMpI18n } from '@/i18n'
import { createRoutes } from '@/router/routes'
import ArtistNames from '../ArtistNames.vue'

const bob: ArtistCredit = { id: 'bob', name: 'Bob Sinclar', role: 'Main' }
const simon: ArtistCredit = { id: 'simon', name: 'Simon & Garfunkel', role: 'Main' }
const kflay: ArtistCredit = { id: 'kflay', name: 'K.Flay', role: 'Featured' }

/** The names in a paragraph, like in the details of a page; a click on the paragraph is counted. */
function mountNames(props: InstanceType<typeof ArtistNames>['$props'], onOuterClick = vi.fn()) {
    const router = createRouter({ history: createMemoryHistory(), routes: createRoutes() })
    const Host = defineComponent({
        setup: () => () => h('p', { onClick: onOuterClick }, [h(ArtistNames, props)]),
    })
    const wrapper = mount(Host, { global: { plugins: [createWaveMpI18n('en'), router] } })
    return { wrapper, router }
}

describe('ArtistNames', () => {
    it('links every artist on its own, the main ones first and the guests after "feat."', () => {
        const { wrapper } = mountNames({ artists: [kflay, bob, simon] })

        expect(wrapper.text()).toBe('Bob Sinclar, Simon & Garfunkel feat. K.Flay')
        const links = wrapper.findAll('a[data-part="link"]')
        expect(links.map((link) => [link.text(), link.attributes('href')])).toEqual([
            ['Bob Sinclar', '/library/artists/bob'],
            ['Simon & Garfunkel', '/library/artists/simon'],
            ['K.Flay', '/library/artists/kflay'],
        ])
        expect(links.map((link) => link.attributes('data-role'))).toEqual([
            'main',
            'main',
            'featured',
        ])
        expect(
            wrapper
                .findAll('[data-part="artist-separator"]')
                .map((separator) => separator.attributes('data-kind')),
        ).toEqual(['list', 'featuring'])
        // No element of its own: the links stand directly in the element around them.
        expect(wrapper.get('p > a').text()).toBe('Bob Sinclar')
    })

    it('shows artists without an id as text and the fallback without artists', () => {
        const { wrapper } = mountNames({ artists: [{ name: 'New Artist', role: 'Main' }] })
        expect(wrapper.get('[data-part="artist-name"]').text()).toBe('New Artist')
        expect(wrapper.find('a').exists()).toBe(false)

        const empty = mountNames({ artists: [], fallback: 'Unknown artist' }).wrapper
        expect(empty.get('[data-part="artist-name"]').attributes('data-state')).toBe('unknown')
        expect(empty.text()).toBe('Unknown artist')
    })

    it('shows names only without links', () => {
        const { wrapper } = mountNames({ artists: [bob, kflay], links: false })

        expect(wrapper.find('a').exists()).toBe(false)
        expect(wrapper.text()).toBe('Bob Sinclar feat. K.Flay')
    })

    it('keeps a click in a row from the row and is no tab stop there', async () => {
        const outer = vi.fn()
        const { wrapper, router } = mountNames(
            { artists: [bob, kflay], part: 'artist-link', inRow: true },
            outer,
        )

        const link = wrapper.findAll('a[data-part="artist-link"]')[1]!
        expect(link.attributes('tabindex')).toBe('-1')
        await link.trigger('click')
        await flushPromises()

        expect(outer).not.toHaveBeenCalled()
        expect(router.currentRoute.value.fullPath).toBe('/library/artists/kflay')
    })

    it('lets a click outside of rows reach the element around it', async () => {
        const outer = vi.fn()
        const { wrapper } = mountNames({ artists: [bob] }, outer)

        await wrapper.get('a').trigger('click')

        expect(outer).toHaveBeenCalledTimes(1)
        expect(wrapper.get('a').attributes('tabindex')).toBeUndefined()
    })
})
