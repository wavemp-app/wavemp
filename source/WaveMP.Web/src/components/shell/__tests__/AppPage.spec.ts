import { afterEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h, nextTick } from 'vue'
import { flushPromises, mount } from '@vue/test-utils'
import { createRouter, createWebHistory, type Router } from 'vue-router'
import { historyEntriesKey, trackHistoryEntries } from '@/router/entries'
import AppPage from '../AppPage.vue'

/** A page that counts how often it is built and shows how often it was built. */
function countingPage(name: string, counter: { count: number }) {
    return defineComponent({
        name,
        setup() {
            const instance = ++counter.count
            return () => h('div', { 'data-page': name, 'data-instance': instance })
        },
    })
}

function setup() {
    const songs = { count: 0 }
    const tracks = { count: 0 }
    const router = createRouter({
        history: createWebHistory(),
        routes: [
            {
                path: '/library/songs',
                component: countingPage('SongsPage', songs),
                meta: { keepAlive: true },
            },
            { path: '/library/tracks/:id', component: countingPage('TrackPage', tracks) },
        ],
    })
    const entries = trackHistoryEntries(router)
    return { router, entries, songs, tracks }
}

async function show(router: Router, entries: ReturnType<typeof trackHistoryEntries>) {
    await router.push('/library/songs')
    const wrapper = mount(AppPage, {
        global: { plugins: [router], provide: { [historyEntriesKey as symbol]: entries } },
    })
    await flushPromises()
    return wrapper
}

async function traverse(router: Router, move: () => void, path: string): Promise<void> {
    move()
    await vi.waitFor(() => expect(router.currentRoute.value.fullPath).toBe(path))
    await flushPromises()
    await nextTick()
}

describe('AppPage', () => {
    afterEach(() => {
        window.history.replaceState(null, '', '/')
    })

    it('returns to the same page on back, without building it again', async () => {
        const { router, entries, songs, tracks } = setup()
        const wrapper = await show(router, entries)
        expect(wrapper.find('[data-page="SongsPage"]').attributes('data-instance')).toBe('1')

        await router.push('/library/tracks/1')
        await flushPromises()
        expect(wrapper.find('[data-page="TrackPage"]').exists()).toBe(true)
        expect(wrapper.find('[data-page="SongsPage"]').exists()).toBe(false)

        await traverse(router, () => router.back(), '/library/songs')
        expect(wrapper.find('[data-page="SongsPage"]').attributes('data-instance')).toBe('1')
        expect(songs.count).toBe(1)

        // Pages without keepAlive are built anew.
        await traverse(router, () => router.forward(), '/library/tracks/1')
        expect(tracks.count).toBe(2)
        wrapper.unmount()
    })

    it('builds the page anew for a new navigation', async () => {
        const { router, entries, songs } = setup()
        const wrapper = await show(router, entries)

        await router.push('/library/tracks/1')
        await router.push('/library/songs')
        await flushPromises()

        expect(songs.count).toBe(2)
        expect(wrapper.find('[data-page="SongsPage"]').attributes('data-instance')).toBe('2')
        wrapper.unmount()
    })

    it('keeps the page when only its query is replaced', async () => {
        const { router, entries, songs } = setup()
        const wrapper = await show(router, entries)

        await router.replace('/library/songs?sort=Title')
        await flushPromises()

        expect(songs.count).toBe(1)
        expect(wrapper.find('[data-page="SongsPage"]').exists()).toBe(true)
        wrapper.unmount()
    })
})
