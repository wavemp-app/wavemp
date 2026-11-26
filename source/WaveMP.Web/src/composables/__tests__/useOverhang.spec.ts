import { afterEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { defineComponent, h, ref } from 'vue'
import { overhang, useOverhang } from '../useOverhang'

function rect(top: number, height: number): DOMRect {
    return { left: 0, width: 100, top, height, right: 100, bottom: top + height } as DOMRect
}

describe('overhang', () => {
    it('is how far the element reaches above the top edge of the other one', () => {
        // A notice of 56 pixels with a margin of 4 pixels above a mini player at 700.
        expect(overhang({ top: 640, height: 56 }, { top: 700, height: 64 })).toBe(60)
    })

    it('rounds up to whole pixels, so the room is never too small', () => {
        expect(overhang({ top: 639.25, height: 56 }, { top: 700, height: 64 })).toBe(61)
    })

    it('is 0 without the element or the other one', () => {
        expect(overhang(null, { top: 700, height: 64 })).toBe(0)
        expect(overhang({ top: 640, height: 56 }, null)).toBe(0)
    })

    it('is 0 while the element is not rendered, e.g. hidden in the full-screen player', () => {
        expect(overhang({ top: 0, height: 0 }, { top: 0, height: 839 })).toBe(0)
    })

    it('is 0 when the element starts at or below the top edge, e.g. below a bar at the top', () => {
        expect(overhang({ top: 120, height: 56 }, { top: 60, height: 60 })).toBe(0)
        expect(overhang({ top: 60, height: 56 }, { top: 60, height: 60 })).toBe(0)
    })
})

describe('useOverhang', () => {
    afterEach(() => {
        vi.unstubAllGlobals()
    })

    it('measures when the element appears, on resizes and when it goes', async () => {
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
        const shown = ref(false)
        let noticeHeight = 56
        const Probe = defineComponent({
            setup() {
                const notice = ref<HTMLElement | null>(null)
                const bar = ref<HTMLElement | null>(null)
                const space = useOverhang(notice, bar)
                return () =>
                    h(
                        'section',
                        {
                            'data-space': space.value,
                            ref: (el) => {
                                const node = el as HTMLElement | null
                                if (node) {
                                    node.getBoundingClientRect = () => rect(700, 64)
                                }
                                bar.value = node
                            },
                        },
                        shown.value
                            ? [
                                  h('div', {
                                      ref: (el) => {
                                          const node = el as HTMLElement | null
                                          if (node) {
                                              node.getBoundingClientRect = () =>
                                                  rect(700 - 4 - noticeHeight, noticeHeight)
                                          }
                                          notice.value = node
                                      },
                                  }),
                              ]
                            : [],
                    )
            },
        })

        const wrapper = mount(Probe)
        await wrapper.vm.$nextTick()
        expect(wrapper.attributes('data-space')).toBe('0')

        shown.value = true
        await wrapper.vm.$nextTick()
        await wrapper.vm.$nextTick()
        expect(wrapper.attributes('data-space')).toBe('60')
        expect(observe).toHaveBeenCalledTimes(3)

        // The text wraps to two lines on a narrow screen.
        noticeHeight = 72
        resize?.([], {} as ResizeObserver)
        await wrapper.vm.$nextTick()
        expect(wrapper.attributes('data-space')).toBe('76')

        shown.value = false
        await wrapper.vm.$nextTick()
        await wrapper.vm.$nextTick()
        expect(wrapper.attributes('data-space')).toBe('0')

        wrapper.unmount()
        expect(disconnect).toHaveBeenCalled()
    })
})
