import { afterEach, describe, expect, it, vi } from 'vitest'
import { createRouter, createWebHistory, type RouteLocationNormalized } from 'vue-router'
import { createScrollMemory, scrollTarget, type ScrollMemoryOptions } from '../scroll'

function route(fullPath: string): RouteLocationNormalized {
    return { path: fullPath.split(/[?#]/)[0], fullPath } as RouteLocationNormalized
}

/** A content area that can scroll up to `max` pixels, like a browser clamps scrollTop. */
function contentArea(max = 1000) {
    const element = document.createElement('main')
    let top = 0
    const area = { element, max }
    Object.defineProperty(element, 'scrollTop', {
        get: () => top,
        set: (value: number) => {
            top = Math.max(0, Math.min(value, area.max))
        },
    })
    return area
}

describe('scrollTarget', () => {
    const songs = route('/library/songs')
    const nowPlaying = route('/now-playing')

    it('starts another page at its top', () => {
        expect(scrollTarget(nowPlaying, songs, false, 300)).toEqual({ kind: 'top' })
    })

    it('restores the position of a page on back and forward', () => {
        expect(scrollTarget(songs, nowPlaying, true, 300)).toEqual({ kind: 'restore', top: 300 })
    })

    it('starts at the top on back and forward to a page without a remembered position', () => {
        expect(scrollTarget(songs, nowPlaying, true, undefined)).toEqual({ kind: 'top' })
    })

    it('keeps the position when only the query or the hash changes', () => {
        const sorted = route('/library/songs?sort=Title')
        expect(scrollTarget(sorted, songs, false, undefined)).toEqual({ kind: 'keep' })
        expect(scrollTarget(route('/library/songs#top'), songs, false, 0)).toEqual({
            kind: 'keep',
        })
    })
})

describe('createScrollMemory', () => {
    const songs = route('/library/songs')
    const nowPlaying = route('/now-playing')
    const fromHistory = { left: 0, top: 0 }

    function setup(options: Partial<ScrollMemoryOptions> = {}) {
        const area = contentArea()
        let position = 1
        const frames: Array<() => void> = []
        let time = 0
        const memory = createScrollMemory({
            container: () => area.element,
            entry: () => position,
            schedule: (callback) => frames.push(callback),
            now: () => time,
            ...options,
        })
        return {
            area,
            memory,
            frames,
            at: (value: number) => (position = value),
            later: (ms: number) => (time += ms),
            /** Runs the scheduled frames, one after the other. */
            runFrames(): number {
                let count = 0
                while (frames.length > 0) {
                    frames.shift()!()
                    count++
                }
                return count
            },
        }
    }

    it('scrolls the content area of a new page to its top', () => {
        const { area, memory } = setup()
        memory.enter(songs)
        area.element.scrollTop = 300

        memory.remember()
        const result = memory.scrollBehavior(nowPlaying, songs, null)

        expect(area.element.scrollTop).toBe(0)
        expect(result).toEqual({ left: 0, top: 0 })
    })

    it('restores the position of a page on back', () => {
        const { area, memory, at } = setup()
        at(1)
        memory.enter(songs)
        area.element.scrollTop = 300
        memory.remember()

        at(2)
        memory.scrollBehavior(nowPlaying, songs, null)
        memory.enter(nowPlaying)
        area.element.scrollTop = 120
        memory.remember()

        at(1)
        const result = memory.scrollBehavior(songs, nowPlaying, fromHistory)
        memory.enter(songs)
        expect(area.element.scrollTop).toBe(300)
        expect(result).toBe(fromHistory)

        memory.remember()
        at(2)
        memory.scrollBehavior(nowPlaying, songs, fromHistory)
        expect(area.element.scrollTop).toBe(120)
    })

    it('keeps the position when only the query changes', () => {
        const { area, memory } = setup()
        memory.enter(songs)
        area.element.scrollTop = 300

        memory.remember()
        const result = memory.scrollBehavior(route('/library/songs?sort=Title'), songs, null)

        expect(area.element.scrollTop).toBe(300)
        expect(result).toBe(false)
    })

    it('tells the same page in different history entries apart', () => {
        const { area, memory, at } = setup()
        at(1)
        memory.enter(songs)
        area.element.scrollTop = 300
        memory.remember()

        // The same page opened again further on in the history starts at its top.
        at(3)
        memory.scrollBehavior(songs, nowPlaying, fromHistory)
        expect(area.element.scrollTop).toBe(0)
    })

    it('waits for a page that is not tall enough yet to restore its position', () => {
        const { area, memory, at, later, runFrames, frames } = setup()
        at(1)
        memory.enter(songs)
        area.element.scrollTop = 600
        memory.remember()
        at(2)
        memory.enter(nowPlaying)
        memory.remember()

        // The list of the songs is still loading.
        area.max = 200
        at(1)
        memory.scrollBehavior(songs, nowPlaying, fromHistory)
        expect(area.element.scrollTop).toBe(200)

        frames.shift()!()
        expect(area.element.scrollTop).toBe(200)
        area.max = 1000
        later(100)
        runFrames()
        expect(area.element.scrollTop).toBe(600)
        expect(frames).toHaveLength(0)
    })

    it('gives up restoring after the timeout', () => {
        const { area, memory, at, later, frames } = setup({ restoreTimeout: 500 })
        at(1)
        memory.enter(songs)
        area.element.scrollTop = 600
        memory.remember()
        at(2)
        memory.enter(nowPlaying)
        memory.remember()

        area.max = 200
        at(1)
        memory.scrollBehavior(songs, nowPlaying, fromHistory)
        later(500)
        frames.shift()!()

        expect(frames).toHaveLength(0)
        area.max = 1000
        expect(area.element.scrollTop).toBe(200)
    })

    it('stops restoring as soon as the user scrolls', () => {
        const { area, memory, at, frames } = setup()
        at(1)
        memory.enter(songs)
        area.element.scrollTop = 600
        memory.remember()
        at(2)
        memory.enter(nowPlaying)
        memory.remember()

        area.max = 200
        at(1)
        memory.scrollBehavior(songs, nowPlaying, fromHistory)
        area.element.dispatchEvent(new Event('wheel'))
        area.element.scrollTop = 50
        area.max = 1000
        frames.shift()!()

        expect(area.element.scrollTop).toBe(50)
        expect(frames).toHaveLength(0)
    })

    it('stops restoring when the next navigation starts', () => {
        const { area, memory, at, frames } = setup()
        at(1)
        memory.enter(songs)
        area.element.scrollTop = 600
        memory.remember()
        at(2)
        memory.enter(nowPlaying)
        memory.remember()

        area.max = 200
        at(1)
        memory.scrollBehavior(songs, nowPlaying, fromHistory)
        memory.enter(songs)
        memory.remember()
        area.max = 1000
        frames.shift()!()

        expect(area.element.scrollTop).toBe(200)
        expect(frames).toHaveLength(0)
    })

    it('does nothing without a content area', () => {
        const memory = createScrollMemory({ container: () => null, entry: () => 1 })
        memory.enter(songs)
        memory.remember()

        expect(memory.scrollBehavior(nowPlaying, songs, null)).toEqual({ left: 0, top: 0 })
    })
})

describe('createScrollMemory with the router', () => {
    afterEach(() => {
        document.body.replaceChildren()
        vi.restoreAllMocks()
    })

    it('starts every new page at its top and restores the position on back', async () => {
        vi.spyOn(window, 'scrollTo').mockImplementation(() => {})
        // Browsers know it, jsdom does not: the router saves the positions of back and forward
        // only with it.
        Object.assign(window.history, { scrollRestoration: 'manual' })
        const area = contentArea()
        document.body.append(area.element)
        const memory = createScrollMemory({ container: () => area.element })
        const component = { render: () => null }
        const router = createRouter({
            history: createWebHistory(),
            routes: [
                { path: '/library/songs', component },
                { path: '/now-playing', component },
            ],
            scrollBehavior: memory.scrollBehavior,
        })
        router.beforeEach(() => {
            memory.remember()
        })
        router.afterEach((to, _from, failure) => {
            if (!failure) {
                memory.enter(to)
            }
        })
        // The scroll behavior runs after the next tick of Vue, once the new page is rendered.
        const settled = () => new Promise((resolve) => setTimeout(resolve, 0))

        await router.push('/library/songs')
        await settled()
        area.element.scrollTop = 300

        await router.push('/now-playing')
        await settled()
        expect(area.element.scrollTop).toBe(0)
        area.element.scrollTop = 40

        router.back()
        await vi.waitFor(() => expect(router.currentRoute.value.path).toBe('/library/songs'))
        await settled()
        expect(area.element.scrollTop).toBe(300)

        router.forward()
        await vi.waitFor(() => expect(router.currentRoute.value.path).toBe('/now-playing'))
        await settled()
        expect(area.element.scrollTop).toBe(40)
    })
})
