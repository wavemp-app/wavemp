import { afterEach, describe, expect, it, vi } from 'vitest'
import { createRouter, createWebHistory, type Router } from 'vue-router'
import { trackHistoryEntries } from '../entries'

const component = { render: () => null }

function setup() {
    const router = createRouter({
        history: createWebHistory(),
        routes: [
            { path: '/library/songs', component },
            { path: '/library/tracks/:id', component },
            { path: '/now-playing', component },
        ],
    })
    const entries = trackHistoryEntries(router)
    return { router, entries }
}

async function traverse(router: Router, move: () => void, path: string): Promise<void> {
    move()
    await vi.waitFor(() => expect(router.currentRoute.value.fullPath).toBe(path))
}

describe('trackHistoryEntries', () => {
    afterEach(() => {
        window.history.replaceState(null, '', '/')
    })

    it('gives every new navigation a new entry', async () => {
        const { router, entries } = setup()

        await router.push('/library/songs')
        const songs = entries.current.value
        await router.push('/library/tracks/1')
        const track = entries.current.value
        await router.push('/library/songs')

        expect(new Set([songs, track, entries.current.value]).size).toBe(3)
    })

    it('returns to the same entry on back and forward', async () => {
        const { router, entries } = setup()
        await router.push('/library/songs')
        const songs = entries.current.value
        await router.push('/library/tracks/1')
        const track = entries.current.value

        await traverse(router, () => router.back(), '/library/songs')
        expect(entries.current.value).toBe(songs)

        await traverse(router, () => router.forward(), '/library/tracks/1')
        expect(entries.current.value).toBe(track)
    })

    it('keeps the entry when it is only replaced', async () => {
        const { router, entries } = setup()
        await router.push('/library/songs')
        const songs = entries.current.value

        await router.replace('/library/songs?sort=Title')

        expect(entries.current.value).toBe(songs)
        await router.push('/now-playing')
        await traverse(router, () => router.back(), '/library/songs?sort=Title')
        expect(entries.current.value).toBe(songs)
    })

    it('gives a new entry to a page opened again after going back', async () => {
        const { router, entries } = setup()
        await router.push('/now-playing')
        await router.push('/library/songs')
        const first = entries.current.value

        await traverse(router, () => router.back(), '/now-playing')
        await router.push('/library/songs')

        expect(entries.current.value).not.toBe(first)
    })

    it('keeps the entry when a navigation fails', async () => {
        const { router, entries } = setup()
        await router.push('/library/songs')
        const songs = entries.current.value
        router.beforeEach((to) => to.path !== '/now-playing')

        await router.push('/now-playing')

        expect(router.currentRoute.value.fullPath).toBe('/library/songs')
        expect(entries.current.value).toBe(songs)
    })
})
