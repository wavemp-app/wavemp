import { beforeEach, describe, expect, it } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import AppSidebar from '../AppSidebar.vue'
import { createWaveMpI18n } from '@/i18n'
import { createRoutes } from '@/router/routes'

async function mountSidebar(props: { open?: boolean; collapsed?: boolean } = {}) {
    const router = createRouter({ history: createMemoryHistory(), routes: createRoutes() })
    await router.push('/library/albums')
    const wrapper = mount(AppSidebar, {
        props,
        global: { plugins: [router, createWaveMpI18n('en')] },
    })
    await flushPromises()
    return wrapper
}

describe('AppSidebar', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
    })

    it('is expanded with a button that collapses it', async () => {
        const wrapper = await mountSidebar()

        const sidebar = wrapper.get('[data-part="sidebar"]')
        expect(sidebar.attributes('data-state')).toBe('closed expanded')
        const toggle = sidebar.get('[data-part="btn-sidebar-toggle"]')
        expect(sidebar.element.firstElementChild).toBe(toggle.element)
        expect(toggle.attributes('aria-expanded')).toBe('true')
        expect(toggle.attributes('aria-controls')).toBe(sidebar.attributes('id'))
        expect(toggle.get('[data-part="label"]').text()).toBe('Collapse sidebar')
        expect(toggle.get('[data-part="icon"]').attributes('data-icon')).toBe('sidebar-collapse')
        expect(sidebar.find('[data-part="tree-item"][title]').exists()).toBe(false)

        await toggle.trigger('click')
        expect(wrapper.emitted('toggleCollapsed')).toHaveLength(1)
    })

    it('shows the labels of the tree as tooltips while it is collapsed', async () => {
        const wrapper = await mountSidebar({ collapsed: true })

        const sidebar = wrapper.get('[data-part="sidebar"]')
        expect(sidebar.attributes('data-state')).toBe('closed collapsed')
        const toggle = sidebar.get('[data-part="btn-sidebar-toggle"]')
        expect(toggle.attributes('aria-expanded')).toBe('false')
        expect(toggle.get('[data-part="label"]').text()).toBe('Expand sidebar')
        expect(toggle.get('[data-part="icon"]').attributes('data-icon')).toBe('sidebar-expand')

        const items = sidebar.findAll('[data-part="tree-item"]')
        expect(items.length).toBeGreaterThan(0)
        for (const item of items) {
            expect(item.attributes('title')).toBe(item.get('[data-part="label"]').text())
        }
    })

    it('keeps the drawer state next to the collapsed state', async () => {
        const wrapper = await mountSidebar({ open: true })

        expect(wrapper.get('[data-part="sidebar"]').attributes('data-state')).toBe('open expanded')
        expect(wrapper.get('[data-part="sidebar-backdrop"]').attributes('data-state')).toBe('open')
    })
})
