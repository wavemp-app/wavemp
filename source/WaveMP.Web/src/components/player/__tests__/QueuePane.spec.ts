import { describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import type { Track } from '@/api/generated/models'
import { createWaveMpI18n } from '@/i18n'
import type { TrackLyrics } from '@/lyrics/timing'
import type { QueueItem } from '@/player/queue'
import QueuePane from '../QueuePane.vue'

vi.mock('@/library/api', () => ({ fetchAlbumCover: vi.fn(), fetchTrackCover: vi.fn() }))

function item(id: string, durationMilliseconds: number | null = 125_000): QueueItem {
    return {
        key: `k-${id}`,
        track: {
            id,
            title: id.toUpperCase(),
            hasAudio: true,
            hasCover: false,
            durationMilliseconds,
            artist: { id: 'band', name: 'Band' },
        } satisfies Track,
    }
}

const items = [item('a'), item('b'), item('c', 3_600_000)]

const lyrics: TrackLyrics = {
    trackId: 'b',
    type: 'LineSynchronized',
    source: 'File',
    lines: [
        { start: 2, end: 4, text: 'First line', words: [] },
        { start: 4, end: null, text: 'Second line', words: [] },
    ],
}

function mountPane(
    props: { items?: QueueItem[]; currentIndex?: number; lyrics?: TrackLyrics | null } = {},
    locale = 'en',
) {
    return mount(QueuePane, {
        props: { items, currentIndex: 1, ...props },
        global: { plugins: [createPinia(), createWaveMpI18n(locale)] },
        attachTo: document.body,
    })
}

type Wrapper = ReturnType<typeof mountPane>

function childParts(wrapper: Wrapper): string[] {
    return wrapper
        .findAll('[data-part="queue-pane"] > [data-part]')
        .filter((child) => child.attributes('hidden') === undefined)
        .map((child) => child.attributes('data-part') ?? '')
}

function summary(wrapper: Wrapper): Record<string, string> {
    return Object.fromEntries(
        wrapper
            .findAll('[data-part="queue-pane-summary"] > [data-part="label"]')
            .map((label) => [label.attributes('data-field') ?? '', label.text()]),
    )
}

describe('QueuePane', () => {
    it('is a labelled pane with the view switch, the current song, the queue and the summary', () => {
        const wrapper = mountPane()

        const pane = wrapper.get('aside[data-part="queue-pane"]')
        expect(pane.attributes('aria-label')).toBe('Play queue')
        expect(childParts(wrapper)).toEqual([
            'segmented-control',
            'queue-pane-header',
            'queue',
            'queue-pane-summary',
        ])

        const views = pane.get('[data-part="segmented-control"]')
        expect(views.attributes('data-item')).toBe('queue-pane-view')
        expect(views.attributes('role')).toBe('radiogroup')
        expect(
            views
                .findAll('[data-part="segment"]')
                .map((segment) => [
                    segment.attributes('data-item'),
                    segment.attributes('aria-checked'),
                ]),
        ).toEqual([
            ['queue', 'true'],
            ['lyrics', 'false'],
        ])

        const header = pane.get('[data-part="queue-pane-header"]')
        expect(header.attributes('data-state')).toBeUndefined()
        expect(header.find('[data-part="cover"]').exists()).toBe(true)
        expect(header.get('[data-part="queue-pane-title"]').text()).toBe('B')
        expect(header.get('[data-part="queue-pane-subtitle"]').text()).toBe('Band')

        expect(pane.findAll('[data-part="queue-item-title"]').map((title) => title.text())).toEqual(
            ['A', 'B', 'C'],
        )
        expect(pane.find('[data-part="autoplay"]').exists()).toBe(false)
        wrapper.unmount()
    })

    it('sums up the number of songs and their duration', () => {
        const wrapper = mountPane()

        expect(summary(wrapper)).toEqual({ count: '3 songs', duration: '1:04:10 in total' })
        wrapper.unmount()
    })

    it('says "at least" while a duration is unknown, and uses the singular for one song', () => {
        const wrapper = mountPane({ items: [item('a', null)], currentIndex: 0 })

        expect(summary(wrapper)).toEqual({ count: '1 song', duration: 'At least 0:00 in total' })
        wrapper.unmount()
    })

    it('speaks German', () => {
        const wrapper = mountPane({}, 'de')

        expect(wrapper.get('[data-part="queue-pane"]').attributes('aria-label')).toBe(
            'Warteschlange',
        )
        expect(summary(wrapper)).toEqual({ count: '3 Titel', duration: '1:04:10 insgesamt' })
        wrapper.unmount()
    })

    it('shows an empty state and an empty header without a queue', () => {
        const wrapper = mountPane({ items: [], currentIndex: -1 })

        expect(childParts(wrapper)).toEqual([
            'segmented-control',
            'queue-pane-header',
            'empty-state',
            'queue-pane-summary',
        ])
        const header = wrapper.get('[data-part="queue-pane-header"]')
        expect(header.attributes('data-state')).toBe('empty')
        expect(header.find('[data-part="cover"]').exists()).toBe(false)
        expect(header.get('[data-part="queue-pane-title"]').text()).toBe('Nothing is playing')
        expect(wrapper.get('[data-part="empty-state-title"]').text()).toBe('The queue is empty')
        expect(summary(wrapper)).toEqual({ count: '0 songs', duration: '0:00 in total' })
        wrapper.unmount()
    })

    it('hands the actions of the queue on', async () => {
        const wrapper = mountPane()

        const entries = wrapper.findAll('[data-part="queue-item"]')
        await entries[2]!.get('[data-part="queue-item-info"]').trigger('click')
        await entries[2]!.get('[data-item="move-up"]').trigger('click')
        await entries[0]!.get('[data-item="remove"]').trigger('click')
        await wrapper.get('[data-item="clear-queue"]').trigger('click')

        expect(wrapper.emitted('play')).toEqual([['k-c']])
        expect(wrapper.emitted('move')).toEqual([['k-c', -1]])
        expect(wrapper.emitted('remove')).toEqual([['k-a']])
        expect(wrapper.emitted('clear')).toHaveLength(1)
        wrapper.unmount()
    })

    it('shows the lyrics of the current song in their tab, following the music only there', async () => {
        const wrapper = mountPane({ lyrics })

        const stage = wrapper.getComponent({ name: 'LyricsStage' })
        expect(stage.props('active')).toBe(false)
        expect(stage.attributes('hidden')).toBeDefined()

        await wrapper.get('[data-part="segment"][data-item="lyrics"]').trigger('click')

        expect(childParts(wrapper)).toEqual([
            'segmented-control',
            'queue-pane-header',
            'lyrics',
            'queue-pane-summary',
        ])
        expect(stage.props('active')).toBe(true)
        expect(wrapper.get('[data-part="queue"]').attributes('hidden')).toBeDefined()
        expect(wrapper.findAll('[data-part="lyrics-line"]').map((line) => line.text())).toEqual([
            'First line',
            'Second line',
        ])

        await wrapper.findAll('[data-part="lyrics-line-button"]')[1]!.trigger('click')
        expect(wrapper.emitted('seek')).toEqual([[4]])

        await wrapper.get('[data-part="segment"][data-item="queue"]').trigger('click')
        expect(stage.props('active')).toBe(false)
        expect(childParts(wrapper)).toContain('queue')
        wrapper.unmount()
    })

    it('says so when the current song has no lyrics', async () => {
        // Lyrics of another song (e.g. the one before, still loading) do not count.
        const wrapper = mountPane({ lyrics: { ...lyrics, trackId: 'a' } })

        await wrapper.get('[data-part="segment"][data-item="lyrics"]').trigger('click')

        expect(wrapper.find('[data-part="lyrics"]').exists()).toBe(false)
        expect(childParts(wrapper)).toEqual([
            'segmented-control',
            'queue-pane-header',
            'empty-state',
            'queue-pane-summary',
        ])
        expect(wrapper.get('[data-part="empty-state-text"]').text()).toBe(
            'This song has no lyrics.',
        )
        wrapper.unmount()
    })
})
