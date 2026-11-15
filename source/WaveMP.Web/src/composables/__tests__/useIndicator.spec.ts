import { afterEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { defineComponent, h, ref } from 'vue'
import { indicatorBox, useIndicator } from '../useIndicator'

function rect(left: number, width: number): DOMRect {
    return { left, width, top: 0, height: 10, right: left + width, bottom: 10 } as DOMRect
}

function element(left: number, width: number): HTMLElement {
    const node = document.createElement('div')
    node.getBoundingClientRect = () => rect(left, width)
    return node
}

describe('indicatorBox', () => {
    it('is the box of the active element relative to its container', () => {
        const container = element(100, 300)
        expect(indicatorBox(container, element(184.456, 92.333))).toEqual({
            x: 84.46,
            width: 92.33,
        })
    })

    it('counts the scrolled distance of the container', () => {
        const container = element(100, 300)
        container.scrollLeft = 40
        expect(indicatorBox(container, element(110, 50))).toEqual({ x: 50, width: 50 })
    })

    it('is empty without an active element', () => {
        expect(indicatorBox(element(100, 300), null)).toEqual({ x: 0, width: 0 })
    })
})

describe('useIndicator', () => {
    afterEach(() => {
        vi.unstubAllGlobals()
    })

    it('measures the active element on mount, on changes and on resizes', async () => {
        let resize: ResizeObserverCallback | undefined
        const observe = vi.fn()
        const disconnect = vi.fn()
        vi.stubGlobal(
            'ResizeObserver',
            class {
                constructor(callback: ResizeObserverCallback) {
                    resize = callback
                }
                observe = observe
                disconnect = disconnect
            },
        )
        const active = ref(0)
        const widths = [40, 60]
        let shift = 0
        const Probe = defineComponent({
            setup() {
                const container = ref<HTMLElement | null>(null)
                const box = useIndicator(container, '[data-active="true"]', () => active.value)
                return () =>
                    h(
                        'div',
                        {
                            ref: (el) => {
                                const node = el as HTMLElement | null
                                if (node) {
                                    node.getBoundingClientRect = () => rect(10, 200)
                                }
                                container.value = node
                            },
                            'data-x': box.value.x,
                            'data-width': box.value.width,
                        },
                        widths.map((width, index) =>
                            h('span', {
                                'data-active': String(index === active.value),
                                ref: (el) => {
                                    const node = el as HTMLElement | null
                                    if (node) {
                                        node.getBoundingClientRect = () =>
                                            rect(10 + shift + index * 40, width)
                                    }
                                },
                            }),
                        ),
                    )
            },
        })

        const wrapper = mount(Probe)
        await wrapper.vm.$nextTick()
        expect(wrapper.attributes('data-x')).toBe('0')
        expect(wrapper.attributes('data-width')).toBe('40')
        expect(observe).toHaveBeenCalledTimes(3)

        active.value = 1
        await wrapper.vm.$nextTick()
        await wrapper.vm.$nextTick()
        expect(wrapper.attributes('data-x')).toBe('40')
        expect(wrapper.attributes('data-width')).toBe('60')

        shift = 5
        resize?.([], {} as ResizeObserver)
        await wrapper.vm.$nextTick()
        expect(wrapper.attributes('data-x')).toBe('45')

        wrapper.unmount()
        expect(disconnect).toHaveBeenCalled()
    })
})
