import { describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { useContextMenuStore } from '@/stores/contextMenu'
import type { Track } from '@/api/generated/models'
import { createWaveMpI18n } from '@/i18n'
import type { QueueItem } from '@/player/queue'
import PlayQueue from '../PlayQueue.vue'

vi.mock('@/library/api', () => ({ fetchAlbumCover: vi.fn(), fetchTrackCover: vi.fn() }))

const items: QueueItem[] = ['a', 'b', 'c', 'd', 'e', 'f', 'g'].map((id) => ({
    key: `k-${id}`,
    track: {
        id,
        title: id.toUpperCase(),
        hasAudio: true,
        hasCover: id !== 'g',
        artist: { id: 'band', name: 'Band' },
    } satisfies Track,
}))

function mountQueue(currentIndex: number) {
    return mount(PlayQueue, {
        props: { items, currentIndex },
        global: { plugins: [createPinia(), createWaveMpI18n('en')] },
        attachTo: document.body,
    })
}

type Wrapper = ReturnType<typeof mountQueue>

function shown(wrapper: Wrapper): [string, string | undefined][] {
    return wrapper
        .findAll('[data-part="queue-item"]')
        .map((item) => [
            item.get('[data-part="queue-item-title"]').text(),
            item.attributes('data-state'),
        ])
}

describe('PlayQueue', () => {
    it('shows the last three played songs, the current one and all that follow', () => {
        const wrapper = mountQueue(4)

        expect(shown(wrapper)).toEqual([
            ['B', 'played'],
            ['C', 'played'],
            ['D', 'played'],
            ['E', 'current'],
            ['F', undefined],
            ['G', undefined],
        ])
        expect(wrapper.get('[data-part="queue-list"]').attributes('start')).toBe('2')
        expect(wrapper.get('[aria-current="true"] [data-part="queue-item-title"]').text()).toBe('E')
        wrapper.unmount()
    })

    it('shows a cover in every entry, or its placeholder', () => {
        const wrapper = mountQueue(0)

        const covers = wrapper.findAll('[data-part="queue-item-info"] > [data-part="cover"]')
        expect(covers).toHaveLength(7)
        expect(covers.at(-1)!.attributes('data-state')).toBe('placeholder')
        expect(wrapper.get('[data-part="queue-list"]').attributes('start')).toBe('1')
        wrapper.unmount()
    })

    it('acts on the entries by their place in the whole queue', async () => {
        const wrapper = mountQueue(6)
        const entries = wrapper.findAll('[data-part="queue-item"]')

        expect(entries).toHaveLength(4)
        expect(entries[0]!.get('[data-item="move-up"]').attributes('disabled')).toBeUndefined()
        expect(entries[3]!.get('[data-item="move-down"]').attributes('disabled')).toBeDefined()
        expect(entries[3]!.get('[data-item="remove"]').attributes('disabled')).toBeDefined()

        await entries[0]!.get('[data-item="move-up"]').trigger('click')
        await entries[1]!.get('[data-part="queue-item-info"]').trigger('click')
        expect(wrapper.emitted('move')).toEqual([['k-d', -1]])
        expect(wrapper.emitted('play')).toEqual([['k-e']])
        wrapper.unmount()
    })

    it('opens the context menu of an entry by right click, keyboard and its button', async () => {
        const wrapper = mountQueue(4)
        const menu = useContextMenuStore()
        const entries = wrapper.findAll('[data-part="queue-item"]')

        await entries[2]!.trigger('contextmenu', { clientX: 7, clientY: 8 })
        expect(menu.request).toMatchObject({
            target: {
                type: 'queue-entry',
                key: 'k-d',
                index: 3,
                count: 7,
                currentIndex: 4,
            },
            x: 7,
            y: 8,
        })

        await entries[3]!.get('[data-part="queue-item-info"]').trigger('keydown', {
            key: 'F10',
            shiftKey: true,
        })
        expect(menu.request?.target).toMatchObject({ key: 'k-e', index: 4 })

        await entries[0]!.get('[data-part="btn-item-menu"]').trigger('click')
        expect(menu.request?.target).toMatchObject({ key: 'k-b', index: 1 })
        expect(wrapper.emitted('play')).toBeUndefined()
        wrapper.unmount()
    })

    it('keeps the current entry in view where the queue scrolls itself', async () => {
        // A queue 100 pixels high whose entries are 40 pixels high, one below the other.
        const rect = vi
            .spyOn(HTMLElement.prototype, 'getBoundingClientRect')
            .mockImplementation(function (this: HTMLElement) {
                const top =
                    this.dataset.part === 'queue-item'
                        ? [...this.parentElement!.children].indexOf(this) * 40
                        : 0
                const height = this.dataset.part === 'queue-item' ? 40 : 100
                return {
                    top,
                    bottom: top + height,
                    left: 0,
                    right: 100,
                    width: 100,
                    height,
                } as DOMRect
            })
        const wrapper = mountQueue(0)
        const queue = wrapper.get('[data-part="queue"]').element as HTMLElement
        queue.style.overflowY = 'auto'

        // F is the fourth shown entry (after C, D and E): 120 to 160 pixels.
        await wrapper.setProps({ currentIndex: 5 })
        expect(queue.scrollTop).toBe(60)

        // Where the queue lies in the page, the page is not scrolled.
        queue.style.overflowY = 'visible'
        queue.scrollTop = 0
        await wrapper.setProps({ currentIndex: 6 })
        expect(queue.scrollTop).toBe(0)
        wrapper.unmount()
        rect.mockRestore()
    })
})
