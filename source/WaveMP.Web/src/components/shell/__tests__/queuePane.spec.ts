import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import type { Track } from '@/api/generated/models'

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
const { createWaveMpI18n } = await import('@/i18n')
const { createRoutes } = await import('@/router/routes')
const { useHealthStore } = await import('@/stores/health')
const { usePlayerStore } = await import('@/stores/player')
const { playTracks } = await import('@/player/queue')
const { defaultLayoutOptions, layoutOptions } = await import('@/themes/layoutOptions')

function stubDesktop(desktop: boolean): void {
    vi.stubGlobal(
        'matchMedia',
        (query: string) =>
            ({
                matches: query === '(width >= 64rem)' && desktop,
                addEventListener: () => undefined,
                removeEventListener: () => undefined,
            }) as unknown as MediaQueryList,
    )
}

function track(id: string): Track {
    return {
        id,
        title: id.toUpperCase(),
        hasAudio: true,
        hasCover: false,
        durationMilliseconds: 60_000,
        artist: { id: 'band', name: 'Band' },
    }
}

/** The shell on a page, its areas but the queue pane replaced by empty stand-ins. */
async function mountShell(path: string) {
    const router = createRouter({ history: createMemoryHistory(), routes: createRoutes() })
    await router.push(path)
    useHealthStore().check = vi.fn(() => Promise.resolve())
    const wrapper = mount(AppShell, {
        global: {
            plugins: [router, createWaveMpI18n('en')],
            stubs: {
                AppHeader: true,
                AppToolbar: true,
                AppSidebar: true,
                AppTransport: true,
                AppBottomNav: true,
                ContextMenuHost: true,
                ShortcutOverview: true,
                CompactPlayer: true,
            },
        },
        attachTo: document.body,
    })
    await flushPromises()
    return { wrapper, router }
}

function childParts(wrapper: Awaited<ReturnType<typeof mountShell>>['wrapper']): string[] {
    return wrapper
        .findAll('[data-part="app"] > [data-part]')
        .map((part) => part.attributes('data-part') ?? '')
}

describe('docked queue pane of the shell', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
    })

    afterEach(() => {
        vi.unstubAllGlobals()
        document.documentElement.style.removeProperty('--wmp-layout-queue-pane')
        layoutOptions.value = defaultLayoutOptions
        document.body.innerHTML = ''
    })

    it('is missing while the theme sets no layout option', async () => {
        stubDesktop(true)
        const { wrapper } = await mountShell('/library/songs')

        expect(wrapper.find('[data-part="queue-pane"]').exists()).toBe(false)
        wrapper.unmount()
    })

    it('follows the content where the theme docks it, with the queue of the player', async () => {
        stubDesktop(true)
        document.documentElement.style.setProperty('--wmp-layout-queue-pane', 'docked')
        usePlayerStore().queue = playTracks([track('a'), track('b')], 1, false)
        const { wrapper } = await mountShell('/library/songs')

        const parts = childParts(wrapper)
        expect(parts.indexOf('queue-pane')).toBe(parts.indexOf('content') + 1)
        const pane = wrapper.get('[data-part="app"] > [data-part="queue-pane"]')
        expect(pane.findAll('[data-part="queue-item-title"]').map((title) => title.text())).toEqual(
            ['A', 'B'],
        )
        expect(pane.get('[data-part="queue-pane-title"]').text()).toBe('B')
        expect(pane.get('[data-field="count"]').text()).toBe('2 songs')
        wrapper.unmount()
    })

    it('leaves Now Playing to its own queue', async () => {
        stubDesktop(true)
        document.documentElement.style.setProperty('--wmp-layout-queue-pane', 'docked')
        const { wrapper, router } = await mountShell('/now-playing')

        expect(wrapper.find('[data-part="queue-pane"]').exists()).toBe(false)

        await router.push('/library/albums')
        await flushPromises()
        expect(wrapper.find('[data-part="queue-pane"]').exists()).toBe(true)
        wrapper.unmount()
    })

    it('is missing on phones, whatever the theme sets', async () => {
        stubDesktop(false)
        document.documentElement.style.setProperty('--wmp-layout-queue-pane', 'docked')
        const { wrapper } = await mountShell('/library/songs')

        expect(wrapper.find('[data-part="queue-pane"]').exists()).toBe(false)
        wrapper.unmount()
    })
})
