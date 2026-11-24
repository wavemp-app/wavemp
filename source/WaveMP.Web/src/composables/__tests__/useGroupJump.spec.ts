import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { defineComponent, h, nextTick, ref } from 'vue'
import { useGroupJump, type GroupJumpOptions } from '../useGroupJump'
import { shortcutRegistry } from '@/shortcuts/defaults'
import { useFullPlayerStore } from '@/stores/fullPlayer'
import { useMiniPlayerStore } from '@/stores/miniPlayer'

type Jump = ReturnType<typeof useGroupJump>

/** Positions of the groups the list renders; a jump replaces them. */
const rendered = ref<number[]>([0, 10, 20])

function onKeydown(event: KeyboardEvent): void {
    shortcutRegistry.handle(event)
}

const mounted: VueWrapper[] = []

async function mountList(options: Partial<Omit<GroupJumpOptions, 'list'>> = {}) {
    let jump: Jump | undefined
    const Host = defineComponent({
        setup() {
            const list = ref<HTMLElement | null>(null)
            jump = useGroupJump({ list, available: true, ...options })
            return () =>
                h('div', { ref: list, 'data-part': 'list' }, [
                    ...rendered.value.map((position) =>
                        h('div', { key: position, 'data-position': String(position) }, [
                            h('button', { 'data-part': 'btn-group-jump' }, `Group ${position}`),
                        ]),
                    ),
                    h('input', { type: 'search' }),
                ])
        },
    })
    const wrapper = mount(Host, { attachTo: document.body })
    mounted.push(wrapper)
    await nextTick()
    // jsdom lays out nothing: the list counts as shown, its groups as one below the other.
    const element = wrapper.element as HTMLElement
    element.getClientRects = () => [{}] as unknown as DOMRectList
    return { wrapper, element, jump: jump! }
}

function stubGroupBoxes(element: HTMLElement): void {
    element.querySelectorAll<HTMLElement>('[data-position]').forEach((group, index) => {
        group.getBoundingClientRect = () =>
            ({ top: index * 300 - 400, bottom: index * 300 - 100 }) as DOMRect
        group.scrollIntoView = vi.fn()
    })
}

function press(target: EventTarget, key: string, init: KeyboardEventInit = {}): KeyboardEvent {
    const event = new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true, ...init })
    target.dispatchEvent(event)
    return event
}

beforeEach(() => {
    setActivePinia(createPinia())
    rendered.value = [0, 10, 20]
    document.addEventListener('keydown', onKeydown)
})

afterEach(() => {
    mounted.splice(0).forEach((wrapper) => wrapper.unmount())
    document.removeEventListener('keydown', onKeydown)
    document.body.replaceChildren()
})

describe('useGroupJump', () => {
    it('opens with Ctrl+Minus and Escape at the group in view and takes the key', async () => {
        const { element, jump } = await mountList()
        stubGroupBoxes(element)

        const minus = press(document.body, '-', { ctrlKey: true })
        expect(minus.defaultPrevented).toBe(true)
        expect(jump.open.value).toBe(true)
        // The second group reaches below the top of the page.
        expect(jump.current.value).toBe(10)

        jump.close()
        await nextTick()
        const escape = press(document.body, 'Escape')
        expect(escape.defaultPrevented).toBe(true)
        expect(jump.open.value).toBe(true)
    })

    it('opens from a heading with its group as the current one', async () => {
        const loadGroups = vi.fn(() => Promise.resolve())
        const { element, jump } = await mountList({ loadGroups })
        const heading = element.querySelector<HTMLElement>('[data-position="20"] button')!

        expect(jump.openOverview({ position: 20, opener: heading })).toBe(true)
        expect(jump.current.value).toBe(20)
        expect(loadGroups).toHaveBeenCalledTimes(1)
    })

    it('leaves the keys to the browser and the app where the overview does not apply', async () => {
        const available = ref(false)
        const { jump } = await mountList({ available })

        // No two groups: Ctrl+Minus zooms, Escape does what it does otherwise.
        expect(press(document.body, '-', { ctrlKey: true }).defaultPrevented).toBe(false)
        expect(press(document.body, 'Escape').defaultPrevented).toBe(false)
        expect(jump.open.value).toBe(false)

        available.value = true
        // The full-screen player and the mini player in place of the app take Escape.
        useFullPlayerStore().expand()
        expect(press(document.body, 'Escape').defaultPrevented).toBe(false)
        useFullPlayerStore().collapse()
        useMiniPlayerStore().mode = 'inline'
        expect(press(document.body, 'Escape').defaultPrevented).toBe(false)
        useMiniPlayerStore().mode = 'closed'

        // The open drawer takes it as well.
        const sidebar = document.createElement('aside')
        sidebar.dataset.part = 'sidebar'
        sidebar.dataset.state = 'open expanded'
        document.body.append(sidebar)
        expect(press(document.body, 'Escape').defaultPrevented).toBe(false)
        sidebar.remove()

        // An open dialog and a field keep their keys.
        const dialog = document.createElement('dialog')
        dialog.setAttribute('open', '')
        document.body.append(dialog)
        expect(press(document.body, '-', { ctrlKey: true }).defaultPrevented).toBe(false)
        dialog.remove()
        const field = document.querySelector('input')!
        expect(press(field, 'Escape').defaultPrevented).toBe(false)
        expect(jump.open.value).toBe(false)

        expect(press(document.body, 'Escape').defaultPrevented).toBe(true)
        expect(jump.open.value).toBe(true)
    })

    it('leaves the keys alone while the list is not shown', async () => {
        const { element, jump } = await mountList()
        element.getClientRects = () => [] as unknown as DOMRectList

        expect(press(document.body, '-', { ctrlKey: true }).defaultPrevented).toBe(false)
        expect(jump.open.value).toBe(false)
    })

    it('opens with a pinch of two fingers on the list', async () => {
        const { element, jump } = await mountList()
        const send = (type: string, xs: number[]) => {
            const event = new Event(type, { bubbles: true, cancelable: true })
            const touches = xs.map((clientX) => ({ clientX, clientY: 0, target: element }))
            Object.defineProperty(event, 'touches', { value: touches })
            element.dispatchEvent(event)
            return event
        }

        send('touchstart', [0, 200])
        const move = send('touchmove', [80, 120])
        expect(jump.open.value).toBe(true)
        expect(move.defaultPrevented).toBe(false)
    })

    it('scrolls to a rendered group and focuses its heading', async () => {
        const jumpTo = vi.fn(() => Promise.resolve(true))
        const { element, jump } = await mountList({ jumpTo })
        stubGroupBoxes(element)
        jump.openOverview({ position: 0 })

        await jump.choose(20)
        await flushPromises()

        const group = element.querySelector<HTMLElement>('[data-position="20"]')!
        expect(jump.open.value).toBe(false)
        expect(jumpTo).not.toHaveBeenCalled()
        expect(group.scrollIntoView).toHaveBeenCalledWith({ block: 'start', behavior: 'smooth' })
        expect(document.activeElement).toBe(group.querySelector('button'))
    })

    it('loads a group that is not loaded and scrolls to it at once', async () => {
        const jumpTo = vi.fn(async (position: number) => {
            rendered.value = [position, position + 7]
            return true
        })
        const { element, jump } = await mountList({ jumpTo })
        jump.openOverview()

        const choosing = jump.choose(4000)
        await nextTick()
        await nextTick()
        stubGroupBoxes(element)
        await choosing

        expect(jumpTo).toHaveBeenCalledWith(4000)
        const group = element.querySelector<HTMLElement>('[data-position="4000"]')!
        expect(group.scrollIntoView).toHaveBeenCalledWith({ block: 'start', behavior: 'auto' })
        expect(document.activeElement).toBe(group.querySelector('button'))
    })

    it('scrolls at once with reduced motion', async () => {
        vi.stubGlobal('matchMedia', (query: string) => ({
            matches: query === '(prefers-reduced-motion: reduce)',
        }))
        try {
            const { element, jump } = await mountList()
            stubGroupBoxes(element)
            jump.openOverview()
            await jump.choose(10)

            const group = element.querySelector<HTMLElement>('[data-position="10"]')!
            expect(group.scrollIntoView).toHaveBeenCalledWith({ block: 'start', behavior: 'auto' })
        } finally {
            vi.unstubAllGlobals()
        }
    })

    it('returns the focus to where it was when it closes without a choice', async () => {
        const { element, jump } = await mountList()
        const heading = element.querySelector<HTMLElement>('[data-position="10"] button')!
        heading.focus()
        jump.openOverview({ position: 10, opener: heading })
        await nextTick()
        heading.blur()

        jump.close()
        await nextTick()
        expect(document.activeElement).toBe(heading)
    })

    it('does not open twice and not without two groups', async () => {
        const available = ref(true)
        const { jump } = await mountList({ available })

        expect(jump.openOverview()).toBe(true)
        expect(jump.openOverview()).toBe(false)
        jump.close()
        available.value = false
        expect(jump.openOverview()).toBe(false)
    })
})
