import { afterEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { defineComponent, h, nextTick, ref } from 'vue'
import { usePinchIn } from '../usePinchIn'

interface FakeTouch {
    readonly clientX: number
    readonly clientY: number
    readonly target: EventTarget
}

/** A touch event with the given touches; passive listeners must not prevent it. */
function touch(target: EventTarget, type: string, touches: FakeTouch[]): Event {
    const event = new Event(type, { bubbles: true, cancelable: true })
    Object.defineProperty(event, 'touches', { value: touches })
    target.dispatchEvent(event)
    return event
}

function finger(target: EventTarget, clientX: number, clientY = 100): FakeTouch {
    return { clientX, clientY, target }
}

async function mountList(enabled = ref(true)) {
    const onPinch = vi.fn()
    const Host = defineComponent({
        setup() {
            const list = ref<HTMLElement | null>(null)
            usePinchIn(list, onPinch, enabled)
            return () => h('div', { ref: list }, [h('span', { id: 'row' }, 'Row')])
        },
    })
    const wrapper = mount(Host, { attachTo: document.body })
    // The listeners follow the element once it is rendered.
    await nextTick()
    const row = wrapper.element.querySelector('#row')!
    return { wrapper, row, onPinch }
}

function setScale(scale: number | undefined): void {
    Object.defineProperty(window, 'visualViewport', {
        configurable: true,
        value: scale === undefined ? undefined : { scale },
    })
}

afterEach(() => {
    setScale(undefined)
    document.body.replaceChildren()
})

describe('usePinchIn', () => {
    it('fires once when two fingers in the list come together to 0.7 of their distance', async () => {
        setScale(1)
        const { row, onPinch } = await mountList()

        const start = touch(row, 'touchstart', [finger(row, 0), finger(row, 200)])
        touch(row, 'touchmove', [finger(row, 40), finger(row, 190)])
        expect(onPinch).not.toHaveBeenCalled()

        const move = touch(row, 'touchmove', [finger(row, 60), finger(row, 200)])
        touch(row, 'touchmove', [finger(row, 90), finger(row, 120)])
        expect(onPinch).toHaveBeenCalledTimes(1)

        // The browser keeps the gesture: nothing is prevented.
        expect(start.defaultPrevented).toBe(false)
        expect(move.defaultPrevented).toBe(false)

        // A new gesture fires again.
        touch(row, 'touchend', [])
        touch(row, 'touchstart', [finger(row, 0), finger(row, 200)])
        touch(row, 'touchmove', [finger(row, 50), finger(row, 150)])
        expect(onPinch).toHaveBeenCalledTimes(2)
    })

    it('ignores spreading fingers, one finger and a third finger', async () => {
        setScale(1)
        const { row, onPinch } = await mountList()

        touch(row, 'touchstart', [finger(row, 50), finger(row, 100)])
        touch(row, 'touchmove', [finger(row, 0), finger(row, 300)])
        touch(row, 'touchstart', [finger(row, 0)])
        touch(row, 'touchmove', [finger(row, 10)])
        touch(row, 'touchstart', [finger(row, 0), finger(row, 100), finger(row, 200)])
        touch(row, 'touchmove', [finger(row, 40), finger(row, 50), finger(row, 60)])

        expect(onPinch).not.toHaveBeenCalled()
    })

    it('leaves a zoomed page to the browser', async () => {
        setScale(1.5)
        const { row, onPinch } = await mountList()

        touch(row, 'touchstart', [finger(row, 0), finger(row, 200)])
        touch(row, 'touchmove', [finger(row, 90), finger(row, 110)])

        expect(onPinch).not.toHaveBeenCalled()
    })

    it('needs both fingers to start in the list', async () => {
        setScale(1)
        const { row, onPinch } = await mountList()
        const outside = document.createElement('div')
        document.body.append(outside)

        touch(row, 'touchstart', [finger(row, 0), finger(outside, 200)])
        touch(row, 'touchmove', [finger(row, 90), finger(outside, 110)])

        expect(onPinch).not.toHaveBeenCalled()
    })

    it('does nothing while it is not enabled', async () => {
        setScale(1)
        const enabled = ref(false)
        const { row, onPinch } = await mountList(enabled)

        touch(row, 'touchstart', [finger(row, 0), finger(row, 200)])
        touch(row, 'touchmove', [finger(row, 90), finger(row, 110)])
        expect(onPinch).not.toHaveBeenCalled()

        enabled.value = true
        touch(row, 'touchend', [])
        touch(row, 'touchstart', [finger(row, 0), finger(row, 200)])
        touch(row, 'touchmove', [finger(row, 90), finger(row, 110)])
        expect(onPinch).toHaveBeenCalledTimes(1)
    })

    it('stops listening when the component goes', async () => {
        setScale(1)
        const { wrapper, row, onPinch } = await mountList()
        const element = wrapper.element
        wrapper.unmount()
        document.body.append(element)

        touch(row, 'touchstart', [finger(row, 0), finger(row, 200)])
        touch(row, 'touchmove', [finger(row, 90), finger(row, 110)])
        expect(onPinch).not.toHaveBeenCalled()
    })
})
