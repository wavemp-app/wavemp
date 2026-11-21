import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount, type VueWrapper } from '@vue/test-utils'
import { defineComponent, h, ref } from 'vue'
import { useSwipeDismiss } from '../useSwipeDismiss'
import { swipeSnapDuration } from '@/components/shell/swipe'

const height = 800

const Host = defineComponent({
    props: { enabled: { type: Boolean, default: true } },
    emits: ['dismiss', 'tap'],
    setup(props, { emit }) {
        const element = ref<HTMLElement | null>(null)
        const swipe = useSwipeDismiss(element, {
            enabled: () => props.enabled,
            onDismiss: () => emit('dismiss'),
        })
        return () =>
            h(
                'section',
                {
                    ref: element,
                    'data-phase': swipe.phase.value,
                    'data-offset': String(swipe.offset.value),
                    'data-duration': String(swipe.duration.value),
                    'data-progress': String(swipe.progress.value),
                },
                [h('button', { onClick: () => emit('tap') }), h('div', { 'data-part': 'slider' })],
            )
    },
})

let wrapper: VueWrapper | undefined

function mountHost(enabled = true) {
    wrapper = mount(Host, { props: { enabled }, attachTo: document.body })
    const section = wrapper.element as HTMLElement
    Object.defineProperty(section, 'clientHeight', { value: height, configurable: true })
    return wrapper
}

/** A mouse event of the pointer at a height, at a time in milliseconds. */
function pointer(type: string, y: number, time: number, target: Element): void {
    const event = new MouseEvent(type, { bubbles: true, cancelable: true, clientX: 50, clientY: y })
    Object.defineProperty(event, 'pointerId', { value: 1 })
    Object.defineProperty(event, 'pointerType', { value: 'mouse' })
    Object.defineProperty(event, 'timeStamp', { value: time })
    target.dispatchEvent(event)
}

function state(host: VueWrapper) {
    const element = host.element as HTMLElement
    return {
        phase: element.dataset.phase,
        offset: Number(element.dataset.offset),
        duration: Number(element.dataset.duration),
        progress: Number(element.dataset.progress),
    }
}

beforeEach(() => {
    vi.useFakeTimers()
})

afterEach(() => {
    wrapper?.unmount()
    wrapper = undefined
    vi.useRealTimers()
})

describe('useSwipeDismiss', () => {
    it('follows a slow drag and closes after enough distance', async () => {
        const host = mountHost()
        const button = host.get('button').element
        pointer('pointerdown', 100, 0, button)
        for (let time = 20; time <= 1000; time += 20) {
            pointer('pointermove', 100 + time * 0.3, time, button)
        }
        await host.vm.$nextTick()
        expect(state(host).phase).toBe('dragging')
        // From where the drag started (past the threshold), so the player never jumps.
        expect(state(host).offset).toBe(288)
        expect(state(host).progress).toBe(0.36)

        pointer('pointerup', 400, 1010, button)
        await host.vm.$nextTick()
        expect(state(host).phase).toBe('dismissing')
        expect(state(host).offset).toBe(height)
        expect(host.emitted('dismiss')).toBeUndefined()

        vi.advanceTimersByTime(state(host).duration)
        await host.vm.$nextTick()
        expect(host.emitted('dismiss')).toHaveLength(1)
        expect(state(host).phase).toBe('idle')
        expect(state(host).offset).toBe(0)
    })

    it('snaps back after a short slow drag', async () => {
        const host = mountHost()
        const button = host.get('button').element
        pointer('pointerdown', 100, 0, button)
        for (let time = 20; time <= 600; time += 20) {
            pointer('pointermove', 100 + time * 0.2, time, button)
        }
        pointer('pointerup', 220, 600, button)
        await host.vm.$nextTick()
        expect(state(host).phase).toBe('settling')
        expect(state(host).offset).toBe(0)
        expect(state(host).duration).toBe(swipeSnapDuration)

        vi.advanceTimersByTime(swipeSnapDuration)
        await host.vm.$nextTick()
        expect(state(host).phase).toBe('idle')
        expect(host.emitted('dismiss')).toBeUndefined()
    })

    it('closes on a short fast flick, as fast as the finger', async () => {
        const host = mountHost()
        const button = host.get('button').element
        pointer('pointerdown', 100, 0, button)
        pointer('pointermove', 130, 16, button)
        pointer('pointermove', 160, 32, button)
        pointer('pointermove', 190, 48, button)
        pointer('pointerup', 190, 50, button)
        await host.vm.$nextTick()

        expect(state(host).phase).toBe('dismissing')
        // About 2 pixels per millisecond for the rest of the way, at least the shortest closing.
        expect(state(host).duration).toBeLessThan(400)
        vi.advanceTimersByTime(state(host).duration)
        expect(host.emitted('dismiss')).toHaveLength(1)
    })

    it('swallows the click after a drag, not a tap', async () => {
        const host = mountHost()
        const button = host.get('button')
        pointer('pointerdown', 100, 0, button.element)
        pointer('pointerup', 101, 100, button.element)
        await button.trigger('click')
        expect(host.emitted('tap')).toHaveLength(1)

        pointer('pointerdown', 100, 200, button.element)
        pointer('pointermove', 150, 300, button.element)
        pointer('pointerup', 150, 1000, button.element)
        await button.trigger('click')
        expect(host.emitted('tap')).toHaveLength(1)
    })

    it('leaves sideways and upward moves, sliders and a closed element alone', async () => {
        const host = mountHost()
        const button = host.get('button').element
        pointer('pointerdown', 100, 0, button)
        pointer('pointermove', 60, 20, button)
        pointer('pointermove', 300, 40, button)
        await host.vm.$nextTick()
        expect(state(host).phase).toBe('idle')

        const slider = host.get('[data-part="slider"]').element
        pointer('pointerdown', 100, 100, slider)
        pointer('pointermove', 300, 120, slider)
        await host.vm.$nextTick()
        expect(state(host).phase).toBe('idle')

        wrapper?.unmount()
        const closed = mountHost(false)
        const closedButton = closed.get('button').element
        pointer('pointerdown', 100, 0, closedButton)
        pointer('pointermove', 300, 20, closedButton)
        await closed.vm.$nextTick()
        expect(state(closed).phase).toBe('idle')
    })

    it('ends the gesture when the element closes in another way', async () => {
        const host = mountHost()
        const button = host.get('button').element
        pointer('pointerdown', 100, 0, button)
        pointer('pointermove', 200, 20, button)
        await host.vm.$nextTick()
        expect(state(host).phase).toBe('dragging')

        await host.setProps({ enabled: false })
        expect(state(host).phase).toBe('idle')
        expect(state(host).offset).toBe(0)
    })
})
