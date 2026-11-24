import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { defineComponent, h, ref } from 'vue'
import { createMemoryHistory, createRouter } from 'vue-router'

vi.mock('@/library/api', () => ({ fetchAlbumCover: vi.fn(), fetchTrackCover: vi.fn() }))
vi.mock('@/player/api', () => ({
    getPlayback: vi.fn(),
    getVideoPlayback: vi.fn(),
    recordPlay: vi.fn(),
    reportNowPlaying: vi.fn(),
    getPlaybackState: vi.fn(() => Promise.resolve(null)),
    savePlaybackState: vi.fn(() => Promise.resolve()),
    clearPlaybackState: vi.fn(() => Promise.resolve()),
}))

const { default: AppShell } = await import('../AppShell.vue')
const { useGroupJump } = await import('@/composables/useGroupJump')
const { createWaveMpI18n } = await import('@/i18n')
const { createRoutes } = await import('@/router/routes')
const { useFullPlayerStore } = await import('@/stores/fullPlayer')
const { useHealthStore } = await import('@/stores/health')

/** Whether the page shows a list with two groups. */
const grouped = ref(true)
let jump: ReturnType<typeof useGroupJump> | undefined

/** A page with a grouped list, like the songs grouped by album. */
const Page = defineComponent({
    setup() {
        const list = ref<HTMLElement | null>(null)
        jump = useGroupJump({ list, available: grouped })
        return () =>
            h('div', { ref: list, 'data-part': 'list' }, [
                h('div', { 'data-position': '0' }, 'First'),
                h('div', { 'data-position': '12' }, 'Second'),
            ])
    },
})

async function mountShell() {
    const router = createRouter({ history: createMemoryHistory(), routes: createRoutes() })
    await router.push('/library/songs')
    useHealthStore().check = vi.fn(() => Promise.resolve())
    const wrapper = mount(AppShell, {
        slots: { default: () => h(Page) },
        global: {
            plugins: [router, createWaveMpI18n('en')],
            stubs: {
                AppHeader: true,
                AppToolbar: true,
                AppTransport: true,
                AppBottomNav: true,
                ContextMenuHost: true,
                ShortcutOverview: true,
                CompactPlayer: true,
                QueuePane: true,
            },
        },
        attachTo: document.body,
    })
    await flushPromises()
    // jsdom lays out nothing: the list counts as shown.
    const list = wrapper.find('[data-part="content"] [data-part="list"]').element as HTMLElement
    list.getClientRects = () => [{}] as unknown as DOMRectList
    return wrapper
}

function press(key: string, init: KeyboardEventInit = {}): KeyboardEvent {
    const event = new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true, ...init })
    document.body.dispatchEvent(event)
    return event
}

describe('keys of the quick jump in the app shell', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
        grouped.value = true
    })

    afterEach(() => {
        document.body.innerHTML = ''
    })

    it('opens the overview with Escape and Ctrl+Minus where a grouped list is shown', async () => {
        const wrapper = await mountShell()

        expect(press('Escape').defaultPrevented).toBe(true)
        expect(jump?.open.value).toBe(true)
        jump?.close()
        await flushPromises()

        expect(press('-', { ctrlKey: true }).defaultPrevented).toBe(true)
        expect(jump?.open.value).toBe(true)
        wrapper.unmount()
    })

    it('lets one Escape close the drawer without opening the overview', async () => {
        const wrapper = await mountShell()
        wrapper.findComponent({ name: 'AppHeader' }).vm.$emit('toggle-sidebar')
        await flushPromises()
        const sidebar = wrapper.find('[data-part="sidebar"]')
        expect(sidebar.attributes('data-state')).toContain('open')

        press('Escape')
        await flushPromises()
        expect(sidebar.attributes('data-state')).toContain('closed')
        expect(jump?.open.value).toBe(false)

        // The next Escape opens it.
        press('Escape')
        expect(jump?.open.value).toBe(true)
        wrapper.unmount()
    })

    it('lets one Escape close the full-screen player without opening the overview', async () => {
        const wrapper = await mountShell()
        const fullPlayer = useFullPlayerStore()
        fullPlayer.expand()
        await flushPromises()

        press('Escape')
        expect(fullPlayer.expanded).toBe(false)
        expect(jump?.open.value).toBe(false)
        wrapper.unmount()
    })

    it('leaves Ctrl+Minus to the browser where no grouped list is shown', async () => {
        grouped.value = false
        const wrapper = await mountShell()

        expect(press('-', { ctrlKey: true }).defaultPrevented).toBe(false)
        expect(press('Escape').defaultPrevented).toBe(false)
        expect(jump?.open.value).toBe(false)
        wrapper.unmount()
    })
})
