import { afterEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h } from 'vue'
import { flushPromises, mount } from '@vue/test-utils'
import { createRouter, createWebHistory } from 'vue-router'
import { createWaveMpI18n } from '@/i18n'
import { createRoutes } from '@/router/routes'
import BackButton from '../BackButton.vue'
import DetailHeader from '../DetailHeader.vue'

const songs = { name: 'library-section', params: { section: 'songs' } }

async function setup(paths: readonly string[]) {
    const router = createRouter({ history: createWebHistory(), routes: createRoutes() })
    for (const path of paths) {
        await router.push(path)
    }
    return router
}

function mountButton(router: Awaited<ReturnType<typeof setup>>, props = {}) {
    return mount(BackButton, {
        props,
        global: { plugins: [router, createWaveMpI18n('en')] },
    })
}

describe('BackButton', () => {
    afterEach(() => {
        window.history.replaceState(null, '', '/')
    })

    it('is a themeable button with icon and label', async () => {
        const router = await setup(['/library/tracks/1'])
        const wrapper = mountButton(router, { fallback: songs })

        const button = wrapper.get('button')
        expect(button.attributes('data-part')).toBe('btn-detail-back')
        expect(button.get('[data-part="icon"]').attributes('data-icon')).toBe('detail-back')
        expect(button.get('[data-part="label"]').text()).toBe('Back')
    })

    it('goes back to the previous page of the app', async () => {
        const router = await setup(['/library/songs?sort=Title', '/library/tracks/1'])
        const wrapper = mountButton(router, { fallback: songs })

        await wrapper.get('button').trigger('click')

        await vi.waitFor(() =>
            expect(router.currentRoute.value.fullPath).toBe('/library/songs?sort=Title'),
        )
    })

    it('opens the overview of a page that was opened directly', async () => {
        window.history.replaceState(null, '', '/library/tracks/1')
        const router = createRouter({ history: createWebHistory(), routes: createRoutes() })
        await router.replace('/library/tracks/1')
        const wrapper = mountButton(router, { fallback: songs })

        await wrapper.get('button').trigger('click')
        await flushPromises()

        expect(router.currentRoute.value.fullPath).toBe('/library/songs')
    })

    it('asks to close without a previous page and without an overview', async () => {
        const router = await setup(['/library/songs', '/library/tracks/1'])
        const wrapper = mountButton(router, { history: false })

        await wrapper.get('button').trigger('click')
        await flushPromises()

        expect(wrapper.emitted('close')).toHaveLength(1)
        expect(router.currentRoute.value.fullPath).toBe('/library/tracks/1')
    })
})

describe('DetailHeader', () => {
    it('shows the back button before the head when it knows the overview', async () => {
        const router = await setup(['/library/albums/a1'])
        const global = { plugins: [router, createWaveMpI18n('en')] }

        const withBack = mount(
            defineComponent({
                render: () => h('div', [h(DetailHeader, { title: 'A', back: songs })]),
            }),
            { global },
        )
        const parts = [...withBack.element.children].map((part) => part.getAttribute('data-part'))
        expect(parts).toEqual(['btn-detail-back', 'detail-header'])

        const withoutBack = mount(DetailHeader, { props: { title: 'A' }, global })
        expect(withoutBack.find('[data-part="btn-detail-back"]').exists()).toBe(false)
    })
})
