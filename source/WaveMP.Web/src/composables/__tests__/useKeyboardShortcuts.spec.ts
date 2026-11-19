import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { defineComponent, h, nextTick, ref } from 'vue'
import { useKeyboardShortcuts } from '../useKeyboardShortcuts'
import { useMiniPlayerStore } from '@/stores/miniPlayer'
import { useShortcutsStore } from '@/stores/shortcuts'

const player = vi.hoisted(() => ({
    state: {
        playback: 'paused',
        trackId: 'track-1',
        position: 10,
        duration: 100,
        volume: 0.5,
        muted: false,
        repeat: 'off',
    },
    togglePlay: vi.fn(),
    seek: vi.fn(),
    previous: vi.fn(),
    next: vi.fn(),
    toggleShuffle: vi.fn(),
    cycleRepeat: vi.fn(),
    setVolume: vi.fn(),
    toggleMute: vi.fn(),
}))

vi.mock('@/stores/player', () => ({ usePlayerStore: () => player }))

const appHidden = ref(false)

const Shell = defineComponent({
    setup() {
        useKeyboardShortcuts(appHidden)
        return () =>
            h('div', { 'data-part': 'app' }, [
                h('div', { 'data-part': 'toolbar' }, [
                    h('div', { 'data-part': 'search' }, [
                        h('input', { 'data-part': 'input', type: 'search', value: 'old' }),
                    ]),
                ]),
                h('button', { type: 'button', id: 'button' }, 'Button'),
            ])
    },
})

function press(target: EventTarget, key: string, options: KeyboardEventInit = {}): KeyboardEvent {
    const event = new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true, ...options })
    target.dispatchEvent(event)
    return event
}

describe('useKeyboardShortcuts', () => {
    let wrapper: ReturnType<typeof mount> | undefined

    beforeEach(() => {
        setActivePinia(createPinia())
        appHidden.value = false
        vi.clearAllMocks()
        // jsdom has no layout: the search field counts as shown.
        vi.spyOn(HTMLElement.prototype, 'getClientRects').mockReturnValue([
            {},
        ] as unknown as DOMRectList)
        wrapper = mount(Shell, { attachTo: document.body })
    })

    afterEach(() => {
        wrapper?.unmount()
        vi.restoreAllMocks()
    })

    it('controls the player', () => {
        press(document.body, ' ')
        press(document.body, 'm')
        press(document.body, 'ArrowRight', { ctrlKey: true })

        expect(player.togglePlay).toHaveBeenCalledOnce()
        expect(player.toggleMute).toHaveBeenCalledOnce()
        expect(player.next).toHaveBeenCalledOnce()
    })

    it('focuses the search field of the toolbar with Ctrl+F', () => {
        const event = press(document.body, 'f', { ctrlKey: true })

        const input = document.querySelector<HTMLInputElement>("[data-part='input']")!
        expect(document.activeElement).toBe(input)
        expect(event.defaultPrevented).toBe(true)

        // Typing in the field does not trigger the shortcuts.
        press(input, 'm')
        expect(player.toggleMute).not.toHaveBeenCalled()
    })

    it('opens and closes the overview with ?', () => {
        const shortcuts = useShortcutsStore()

        press(document.getElementById('button')!, '?', { shiftKey: true })
        expect(shortcuts.overviewOpen).toBe(true)

        press(document.body, '?', { shiftKey: true })
        expect(shortcuts.overviewOpen).toBe(false)
    })

    it('leaves search and overview alone while the mini player replaces the app', () => {
        appHidden.value = true
        const shortcuts = useShortcutsStore()

        const search = press(document.body, 'f', { ctrlKey: true })
        press(document.body, '?')

        expect(search.defaultPrevented).toBe(false)
        expect(shortcuts.overviewOpen).toBe(false)
        // The player shortcuts go on.
        press(document.body, ' ')
        expect(player.togglePlay).toHaveBeenCalledOnce()
    })

    it('shows the app in full screen with F and leaves it again', () => {
        const request = vi.fn(() => Promise.resolve())
        const exit = vi.fn(() => Promise.resolve())
        Object.defineProperty(document, 'fullscreenEnabled', { value: true, configurable: true })
        Object.defineProperty(document, 'fullscreenElement', { value: null, configurable: true })
        document.documentElement.requestFullscreen = request
        document.exitFullscreen = exit

        press(document.body, 'f')
        expect(request).toHaveBeenCalledOnce()

        Object.defineProperty(document, 'fullscreenElement', {
            value: document.documentElement,
            configurable: true,
        })
        press(document.body, 'f')
        expect(exit).toHaveBeenCalledOnce()

        Reflect.deleteProperty(document, 'fullscreenEnabled')
        Reflect.deleteProperty(document, 'fullscreenElement')
    })

    it('listens to the floating mini player window as well', async () => {
        const floating = document.implementation.createHTMLDocument('')
        const root = floating.createElement('div')
        floating.body.append(root)
        useMiniPlayerStore().target = root
        await nextTick()

        press(root, 'm')
        // Search and full screen need the app; the key does what it does there.
        expect(press(root, 'f').defaultPrevented).toBe(false)

        expect(player.toggleMute).toHaveBeenCalledOnce()
        wrapper?.unmount()
        wrapper = undefined
        press(root, 'm')
        expect(player.toggleMute).toHaveBeenCalledOnce()
    })
})
