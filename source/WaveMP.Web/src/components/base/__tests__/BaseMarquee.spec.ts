import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import BaseMarquee from '../BaseMarquee.vue'
import { marqueeScrollDuration, marqueeStartDelay } from '../marquee'

let textWidth = 0
const boxWidth = 200
let wrapper: VueWrapper | undefined

beforeEach(() => {
    vi.useFakeTimers()
    // jsdom has no layout: the text is as wide as the test says, its place 200 pixels.
    vi.spyOn(HTMLElement.prototype, 'scrollWidth', 'get').mockImplementation(function (
        this: HTMLElement,
    ) {
        return this.dataset.part === 'marquee' ? textWidth : 0
    })
    vi.spyOn(HTMLElement.prototype, 'clientWidth', 'get').mockImplementation(function (
        this: HTMLElement,
    ) {
        return this.dataset.part === 'now-playing-title' ? boxWidth : 0
    })
})

afterEach(() => {
    wrapper?.unmount()
    wrapper = undefined
    vi.restoreAllMocks()
    vi.useRealTimers()
})

async function mountMarquee(text: string) {
    wrapper = mount(BaseMarquee, { props: { part: 'now-playing-title', text } })
    await flushPromises()
    return wrapper
}

describe('BaseMarquee', () => {
    it('keeps a text that fits still', async () => {
        textWidth = 120
        const marquee = (await mountMarquee('Song')).get('[data-part="marquee"]')

        expect(marquee.text()).toBe('Song')
        expect(marquee.attributes('data-state')).toBe('fit')
        expect(marquee.attributes('style')).toContain('--wmp-marquee-distance: 0px')
    })

    it('scrolls a text too long for its place after a moment', async () => {
        textWidth = 290
        const host = await mountMarquee('A very long title of a song')
        const marquee = host.get('[data-part="marquee"]')

        expect(host.attributes('data-part')).toBe('now-playing-title')
        expect(marquee.attributes('data-state')).toBe('overflow waiting')
        expect(marquee.attributes('style')).toContain('--wmp-marquee-distance: 90px')
        expect(marquee.attributes('style')).toContain(
            `--wmp-marquee-duration: ${marqueeScrollDuration(90)}ms`,
        )

        vi.advanceTimersByTime(marqueeStartDelay)
        await host.vm.$nextTick()
        expect(marquee.attributes('data-state')).toBe('overflow scrolling')

        vi.advanceTimersByTime(marqueeScrollDuration(90))
        await host.vm.$nextTick()
        expect(marquee.attributes('data-state')).toBe('overflow end')
    })

    it('starts another text from its beginning', async () => {
        textWidth = 290
        const host = await mountMarquee('A very long title of a song')
        vi.advanceTimersByTime(marqueeStartDelay)
        await host.vm.$nextTick()

        await host.setProps({ text: 'Another very long title' })
        await flushPromises()
        expect(host.get('[data-part="marquee"]').attributes('data-state')).toBe('overflow waiting')
    })

    it('does not move with reduced motion', async () => {
        textWidth = 290
        vi.stubGlobal(
            'matchMedia',
            (query: string) =>
                ({
                    matches: query.includes('reduce'),
                    addEventListener: () => undefined,
                    removeEventListener: () => undefined,
                }) as unknown as MediaQueryList,
        )
        try {
            const marquee = (await mountMarquee('A very long title of a song')).get(
                '[data-part="marquee"]',
            )
            expect(marquee.attributes('data-state')).toBe('overflow')
            vi.advanceTimersByTime(marqueeStartDelay * 3)
            await flushPromises()
            expect(marquee.attributes('data-state')).toBe('overflow')
        } finally {
            vi.unstubAllGlobals()
        }
    })
})
