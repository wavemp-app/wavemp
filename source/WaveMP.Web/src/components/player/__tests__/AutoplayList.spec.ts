import { describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import type { Track } from '@/api/generated/models'
import { createWaveMpI18n } from '@/i18n'
import type { AutoplayEntry } from '@/player/autoplay'
import type { QueueItem } from '@/player/queue'
import { useContextMenuStore } from '@/stores/contextMenu'
import AutoplayList from '../AutoplayList.vue'
import PlayQueue from '../PlayQueue.vue'

vi.mock('@/library/api', () => ({ fetchAlbumCover: vi.fn(), fetchTrackCover: vi.fn() }))

function track(id: string, extra: Partial<Track> = {}): Track {
    return {
        id,
        title: id.toUpperCase(),
        hasAudio: true,
        hasCover: true,
        durationMilliseconds: 185_000,
        artist: { id: 'band', name: 'Band' },
        ...extra,
    }
}

const entries: AutoplayEntry[] = [
    { key: 'a-x', track: track('x') },
    { key: 'a-y', track: track('y', { hasAudio: false, hasVideo: true }) },
]

function mountList(props: { entries: readonly AutoplayEntry[]; loading: boolean }) {
    return mount(AutoplayList, {
        props,
        global: { plugins: [createPinia(), createWaveMpI18n('en')] },
        attachTo: document.body,
    })
}

describe('AutoplayList', () => {
    it('shows the songs autoplay plays next, under its title and hint', () => {
        const wrapper = mountList({ entries, loading: false })

        const section = wrapper.get('[data-part="autoplay"]')
        expect(section.attributes('data-state')).toBe('ready')
        expect(section.get('[data-part="section-title"]').text()).toBe('Autoplay')
        expect(section.get('[data-part="autoplay-hint"]').text()).toBe(
            'Plays similar songs when the queue ends.',
        )
        const items = wrapper.findAll('[data-part="autoplay-item"]')
        expect(items.map((item) => item.get('[data-part="autoplay-item-title"]').text())).toEqual([
            'X',
            'Y',
        ])
        expect(items[0]!.get('[data-part="autoplay-item-artist"]').text()).toBe('Band')
        expect(items[0]!.get('[data-part="autoplay-item-duration"]').text()).toBe('3:05')
        expect(items[0]!.attributes('data-state')).toBeUndefined()
        expect(items[1]!.attributes('data-state')).toBe('video-only')
        expect(wrapper.find('[data-part="autoplay-status"]').exists()).toBe(false)
        wrapper.unmount()
    })

    it('plays, removes and turns off', async () => {
        const wrapper = mountList({ entries, loading: false })

        await wrapper.findAll('[data-part="autoplay-item-info"]')[1]!.trigger('click')
        await wrapper.findAll('[data-item="remove"]')[0]!.trigger('click')
        await wrapper.get('[data-item="turn-off-autoplay"]').trigger('click')

        expect(wrapper.emitted('play')).toEqual([['a-y']])
        expect(wrapper.emitted('remove')).toEqual([['a-x']])
        expect(wrapper.emitted('turnOff')).toHaveLength(1)
        expect(
            wrapper.findAll('[data-part="autoplay-item-info"]')[0]!.attributes('aria-label'),
        ).toBe('Play X now')
        wrapper.unmount()
    })

    it('shows while songs are chosen and when none is left', () => {
        const loading = mountList({ entries: [], loading: true })
        const section = loading.get('[data-part="autoplay"]')
        expect(section.attributes('data-state')).toBe('loading')
        expect(section.attributes('aria-busy')).toBe('true')
        expect(loading.get('[data-part="autoplay-status"]').text()).toBe('Finding similar songs…')
        expect(loading.find('[data-part="autoplay-list"]').exists()).toBe(false)
        loading.unmount()

        const empty = mountList({ entries: [], loading: false })
        expect(empty.get('[data-part="autoplay"]').attributes('data-state')).toBe('empty')
        expect(empty.get('[data-part="autoplay-status"]').text()).toContain('No similar songs left')
        empty.unmount()
    })

    it('opens the menu of a song, whose "play" plays it like choosing it', async () => {
        const wrapper = mountList({ entries, loading: false })
        const menu = useContextMenuStore()

        await wrapper.findAll('[data-part="autoplay-item"]')[1]!.trigger('contextmenu', {
            clientX: 3,
            clientY: 4,
        })
        expect(menu.request).toMatchObject({
            target: { type: 'song', tracks: [entries[1]!.track] },
            x: 3,
            y: 4,
        })
        const target = menu.request?.target
        if (target?.type === 'song') {
            target.playFrom?.()
        }

        expect(wrapper.emitted('play')).toEqual([['a-y']])
        wrapper.unmount()
    })
})

describe('PlayQueue with autoplay', () => {
    it('marks the songs autoplay added and shows autoplay after the list', () => {
        const items: QueueItem[] = [
            { key: 'k-a', track: track('a') },
            { key: 'k-x', track: track('x'), autoplay: true },
            { key: 'k-y', track: track('y'), autoplay: true },
        ]
        const wrapper = mount(PlayQueue, {
            props: { items, currentIndex: 1 },
            slots: { default: '<section data-part="autoplay" />' },
            global: { plugins: [createPinia(), createWaveMpI18n('en')] },
        })

        expect(
            wrapper
                .findAll('[data-part="queue-item"]')
                .map((item) => item.attributes('data-state')),
        ).toEqual(['played', 'current autoplay', 'autoplay'])
        expect(wrapper.find('[data-part="queue-list"] + [data-part="autoplay"]').exists()).toBe(
            true,
        )
    })
})
