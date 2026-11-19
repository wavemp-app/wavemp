import { afterAll, beforeAll, describe, expect, it } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import BaseContextMenu, { type MenuItem } from '../BaseContextMenu.vue'

const items: MenuItem[] = [
    { id: 'play', label: 'Play' },
    {
        id: 'rate',
        label: 'Rate',
        children: [
            { id: 'rate-0', label: 'No rating', checked: false },
            { id: 'rate-3', label: '3 stars', checked: true },
            { id: 'rate-5', label: '5 stars', checked: false, disabled: true },
        ],
    },
    { id: 'separator', separator: true },
    { id: 'details', label: 'Details' },
]

// jsdom has no Popover API and keeps the content of a closed popover from the focus. The stand-in
// shows a popover by taking its attribute away and hides it by giving it back.
beforeAll(() => {
    HTMLElement.prototype.showPopover = function (this: HTMLElement) {
        this.dataset.popover = this.getAttribute('popover') ?? 'auto'
        this.removeAttribute('popover')
    }
    HTMLElement.prototype.hidePopover = function (this: HTMLElement) {
        if (this.dataset.popover) {
            this.setAttribute('popover', this.dataset.popover)
            delete this.dataset.popover
        }
    }
})

afterAll(() => {
    // @ts-expect-error The stand-ins are removed again.
    delete HTMLElement.prototype.showPopover
    // @ts-expect-error The stand-ins are removed again.
    delete HTMLElement.prototype.hidePopover
})

async function open() {
    const wrapper = mount(BaseContextMenu, {
        props: { items, label: 'Song actions' },
        attachTo: document.body,
    })
    await wrapper.vm.openAt(10, 20)
    await flushPromises()
    return wrapper
}

function entry(id: string): HTMLButtonElement {
    return document.querySelector(`[data-item="${id}"]`) as HTMLButtonElement
}

describe('BaseContextMenu', () => {
    it('marks an entry with a submenu and leaves its entries out of the menu itself', async () => {
        const wrapper = await open()

        const rate = entry('rate')
        expect(rate.getAttribute('aria-haspopup')).toBe('menu')
        expect(rate.getAttribute('aria-expanded')).toBe('false')
        expect(rate.querySelector('[data-part="submenu-indicator"]')).not.toBeNull()
        const submenu = document.getElementById(rate.getAttribute('aria-controls')!)!
        expect(submenu.dataset.kind).toBe('submenu')
        expect(submenu.querySelectorAll('[data-part="menu-item"]')).toHaveLength(0)
        expect(submenu.getAttribute('popover')).toBe('auto')
        expect(document.activeElement).toBe(entry('play'))
        wrapper.unmount()
    })

    it('opens the submenu with the right arrow key on the chosen entry and closes it with the left', async () => {
        const wrapper = await open()

        entry('play').dispatchEvent(
            new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true }),
        )
        await flushPromises()
        expect(document.activeElement).toBe(entry('rate'))

        entry('rate').dispatchEvent(
            new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true }),
        )
        await flushPromises()
        expect(entry('rate').getAttribute('aria-expanded')).toBe('true')
        expect(document.activeElement).toBe(entry('rate-3'))
        expect(entry('rate-3').getAttribute('role')).toBe('menuitemradio')
        expect(entry('rate-3').getAttribute('aria-checked')).toBe('true')

        entry('rate-3').dispatchEvent(
            new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true }),
        )
        await flushPromises()
        // The disabled entry is skipped.
        expect(document.activeElement).toBe(entry('rate-0'))

        entry('rate-0').dispatchEvent(
            new KeyboardEvent('keydown', { key: 'ArrowLeft', bubbles: true }),
        )
        await flushPromises()
        expect(entry('rate').getAttribute('aria-expanded')).toBe('false')
        expect(document.activeElement).toBe(entry('rate'))
        wrapper.unmount()
    })

    it('emits the entry chosen in the submenu', async () => {
        const wrapper = await open()

        entry('rate').click()
        await flushPromises()
        entry('rate-0').click()
        await flushPromises()

        expect(wrapper.emitted('select')).toEqual([['rate-0']])
        expect(wrapper.vm.isOpen).toBe(false)
        wrapper.unmount()
    })
})
