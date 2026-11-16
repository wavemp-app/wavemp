// Theme-capable DOM: every group with one active element (tabs, bottom navigation, the navigation
// of the administration, the tree, the color picker, segmented controls) carries an indicator part
// as its first child and tells the theme the active element with runtime variables, so a theme can
// move a line or a highlight to it with CSS only.
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import AdminNav from '@/components/admin/AdminNav.vue'
import BaseColorPicker from '@/components/base/BaseColorPicker.vue'
import BaseTabs, { type TabItem } from '@/components/base/BaseTabs.vue'
import BaseTree from '@/components/base/BaseTree.vue'
import type { TreeNode } from '@/components/base/BaseTreeItem.vue'
import { createWaveMpI18n } from '@/i18n'
import { createRoutes } from '@/router/routes'

async function mountWith(component: unknown, props: Record<string, unknown>, path = '/') {
    const router = createRouter({ history: createMemoryHistory(), routes: createRoutes() })
    await router.push(path)
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const wrapper = mount(component as any, {
        props,
        global: { plugins: [router, createWaveMpI18n('en')] },
        attachTo: document.body,
    })
    await flushPromises()
    return { wrapper, router }
}

function group(wrapper: VueWrapper, part: string): HTMLElement {
    return wrapper.get(`[data-part="${part}"]`).element as HTMLElement
}

/** The runtime variables of the active element; jsdom has no layout, so the box is empty. */
function variables(element: HTMLElement) {
    const value = (name: string) => element.style.getPropertyValue(name)
    return {
        count: value('--wmp-item-count'),
        index: value('--wmp-active-index'),
        box: [
            value('--wmp-indicator-x'),
            value('--wmp-indicator-y'),
            value('--wmp-indicator-width'),
            value('--wmp-indicator-height'),
        ],
    }
}

function expectIndicator(element: HTMLElement): void {
    const indicator = element.firstElementChild
    expect(indicator?.getAttribute('data-part')).toBe('indicator')
    expect(indicator?.getAttribute('aria-hidden')).toBe('true')
    expect(indicator?.textContent).toBe('')
}

const emptyBox = ['0px', '0px', '0px', '0px']

const tabs: TabItem[] = [
    { id: 'home', label: 'Home', icon: 'home', to: '/' },
    { id: 'library', label: 'Library', icon: 'library', to: '/library' },
    { id: 'playlists', label: 'Playlists', icon: 'playlists', to: '/playlists' },
]

describe('groups with an active element', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
    })

    afterEach(() => {
        document.body.innerHTML = ''
    })

    for (const part of ['tabs', 'bottom-nav'] as const) {
        it(`${part} holds an indicator and tells the active entry`, async () => {
            const { wrapper } = await mountWith(BaseTabs, {
                items: tabs,
                label: 'Main',
                active: 'library',
                part,
            })
            const navigation = group(wrapper, part)

            expectIndicator(navigation)
            expect(variables(navigation)).toEqual({ count: '3', index: '1', box: emptyBox })

            await wrapper.setProps({ active: 'playlists' })
            expect(variables(navigation).index).toBe('2')

            await wrapper.setProps({ active: undefined })
            expect(variables(navigation).index).toBe('-1')
            wrapper.unmount()
        })
    }

    it('keeps the arrow of the active tab inside the tab', async () => {
        const { wrapper } = await mountWith(BaseTabs, {
            items: tabs,
            label: 'Main',
            active: 'home',
        })

        const arrow = wrapper.get(
            '[data-part="tab"][aria-current="page"] > [data-part="indicator"]',
        )
        expect(arrow.attributes('aria-hidden')).toBe('true')
        expect(arrow.get('[data-part="icon"]').attributes('data-icon')).toBe('tab-indicator')
        expect(wrapper.findAll('[data-part="bottom-nav-item"]')).toHaveLength(0)
        wrapper.unmount()
    })

    it('admin-nav holds an indicator and tells the current page', async () => {
        const { wrapper, router } = await mountWith(AdminNav, {}, '/admin/cache')
        const navigation = group(wrapper, 'admin-nav')

        expectIndicator(navigation)
        expect(variables(navigation)).toEqual({ count: '6', index: '2', box: emptyBox })

        await router.push('/admin/users')
        await flushPromises()
        expect(variables(navigation).index).toBe('5')
        wrapper.unmount()
    })

    it('tree holds an indicator and tells the active item among the visible items', async () => {
        const nodes: TreeNode[] = [
            {
                id: 'library',
                label: 'Library',
                children: [
                    { id: 'artists', label: 'Artists' },
                    { id: 'albums', label: 'Albums' },
                ],
            },
            { id: 'playlists', label: 'Playlists' },
        ]
        const { wrapper } = await mountWith(BaseTree, {
            nodes,
            label: 'Library',
            active: 'playlists',
            expanded: ['library'],
        })
        const tree = group(wrapper, 'tree')

        expectIndicator(tree)
        expect(tree.firstElementChild?.tagName).toBe('LI')
        expect(tree.firstElementChild?.getAttribute('role')).toBe('none')
        expect(variables(tree)).toEqual({ count: '4', index: '3', box: emptyBox })

        await wrapper.setProps({ active: 'albums' })
        expect(variables(tree).index).toBe('2')

        // The active item is hidden in a collapsed node.
        await wrapper.setProps({ expanded: [] })
        expect(variables(tree)).toEqual({ count: '2', index: '-1', box: emptyBox })
        wrapper.unmount()
    })

    it('color-picker holds an indicator and tells the selected swatch', async () => {
        const { wrapper } = await mountWith(BaseColorPicker, {
            label: 'Color',
            options: [
                { value: '#1f6fd6', label: 'Blue' },
                { value: '#2e9d4f', label: 'Green' },
            ],
            modelValue: '#2E9D4F',
        })
        const picker = group(wrapper, 'color-picker')

        expectIndicator(picker)
        expect(variables(picker)).toEqual({ count: '2', index: '1', box: emptyBox })

        await wrapper.setProps({ modelValue: null })
        expect(variables(picker).index).toBe('-1')
        wrapper.unmount()
    })
})
