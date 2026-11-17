import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createWaveMpI18n } from '@/i18n'
import type { TrackLyrics } from '@/lyrics/timing'
import LyricsStage from '../LyricsStage.vue'

const words: TrackLyrics = {
    trackId: 'a',
    type: 'WordSynchronized',
    source: 'File',
    lines: [
        {
            start: 1,
            end: 3,
            text: 'Hello world',
            words: [
                { start: 1, end: 2, text: 'Hello ' },
                { start: 2, end: 3, text: 'world' },
            ],
        },
        { start: 3, end: 5, text: '', words: [] },
        {
            start: 5,
            end: null,
            text: 'Again',
            words: [{ start: 5, end: null, text: 'Again' }],
        },
    ],
}

const lines: TrackLyrics = {
    trackId: 'a',
    type: 'LineSynchronized',
    source: 'Download',
    lines: [
        { start: 2, end: 4, text: 'First', words: [] },
        { start: 4, end: null, text: 'Second', words: [] },
    ],
}

const unsynchronized: TrackLyrics = {
    trackId: 'a',
    type: 'Static',
    source: 'Tags',
    lines: [
        { start: null, end: null, text: 'One', words: [] },
        { start: null, end: null, text: 'Two', words: [] },
    ],
}

function mountLyrics(props: Record<string, unknown>) {
    return mount(LyricsStage, {
        props,
        global: { plugins: [createWaveMpI18n('en')] },
        attachTo: document.body,
    })
}

type Wrapper = ReturnType<typeof mountLyrics>

function lineStates(wrapper: Wrapper): (string | undefined)[] {
    return wrapper.findAll('[data-part="lyrics-line"]').map((line) => line.attributes('data-state'))
}

function rootStyle(wrapper: Wrapper, name: string): string {
    return (wrapper.get('[data-part="lyrics"]').element as HTMLElement).style.getPropertyValue(name)
}

describe('LyricsStage', () => {
    afterEach(() => {
        vi.unstubAllGlobals()
    })

    it('marks the current line and word and their progress', async () => {
        const wrapper = mountLyrics({ lyrics: words, position: 2.5, duration: 10 })
        await flushPromises()

        const root = wrapper.get('[data-part="lyrics"]')
        expect(root.attributes('data-state')).toBe('synchronized')
        expect(root.attributes('data-words')).toBe('true')
        expect(rootStyle(wrapper, '--wmp-item-count')).toBe('3')
        expect(rootStyle(wrapper, '--wmp-active-index')).toBe('0')
        expect(lineStates(wrapper)).toEqual(['active', 'upcoming', 'upcoming'])
        const active = wrapper.get('[data-part="lyrics-line"][data-index="0"]')
        expect(active.attributes('aria-current')).toBe('true')
        expect((active.element as HTMLElement).style.getPropertyValue('--wmp-line-progress')).toBe(
            '0.7500',
        )
        const lineWords = active.findAll('[data-part="lyrics-word"]')
        expect(lineWords.map((word) => word.attributes('data-state'))).toEqual(['past', 'active'])
        expect(
            (lineWords[1]!.element as HTMLElement).style.getPropertyValue('--wmp-word-progress'),
        ).toBe('0.5000')
        expect(
            wrapper
                .findAll('[data-part="lyrics-line"][data-index="2"] [data-part="lyrics-word"]')
                .map((word) => word.attributes('data-state')),
        ).toEqual(['upcoming'])
        wrapper.unmount()
    })

    it('follows a new position while the music is paused', async () => {
        const wrapper = mountLyrics({ lyrics: lines, position: 1, duration: 10 })
        await flushPromises()
        expect(lineStates(wrapper)).toEqual(['upcoming', 'upcoming'])
        expect(rootStyle(wrapper, '--wmp-active-index')).toBe('-1')

        await wrapper.setProps({ position: 4.5 })
        await flushPromises()

        expect(lineStates(wrapper)).toEqual(['past', 'active'])
        expect(rootStyle(wrapper, '--wmp-active-index')).toBe('1')
        wrapper.unmount()
    })

    it('reads the exact position from the clock frame by frame while the music plays', async () => {
        const frames: FrameRequestCallback[] = []
        vi.stubGlobal('requestAnimationFrame', (callback: FrameRequestCallback) => {
            frames.push(callback)
            return frames.length
        })
        vi.stubGlobal('cancelAnimationFrame', vi.fn())
        let position = 2.5
        const wrapper = mountLyrics({
            lyrics: lines,
            playing: true,
            position: 0,
            duration: 10,
            clock: () => position,
        })
        await flushPromises()
        expect(lineStates(wrapper)).toEqual(['active', 'upcoming'])

        position = 4.2
        frames.shift()!(0)
        await flushPromises()

        expect(lineStates(wrapper)).toEqual(['past', 'active'])
        wrapper.unmount()
    })

    it('seeks to the start of a chosen line and names breaks', async () => {
        const wrapper = mountLyrics({ lyrics: words, position: 0, duration: 10 })
        const buttons = wrapper.findAll('[data-part="lyrics-line-button"]')

        expect(buttons[1]!.attributes('aria-label')).toBe('Instrumental part')
        expect(
            wrapper.get('[data-part="lyrics-line"][data-index="1"]').attributes('data-empty'),
        ).toBe('true')
        await buttons[2]!.trigger('click')
        await buttons[0]!.trigger('click')

        expect(wrapper.emitted('seek')).toEqual([[5], [1]])
        wrapper.unmount()
    })

    it('shows static lyrics as text without states or buttons', async () => {
        const wrapper = mountLyrics({ lyrics: unsynchronized, position: 30, duration: 100 })
        await flushPromises()

        const root = wrapper.get('[data-part="lyrics"]')
        expect(root.attributes('data-state')).toBe('static')
        expect(root.attributes('data-words')).toBeUndefined()
        expect(wrapper.find('[data-part="lyrics-line-button"]').exists()).toBe(false)
        expect(lineStates(wrapper)).toEqual([undefined, undefined])
        expect(wrapper.findAll('[data-part="lyrics-line"]').map((line) => line.text())).toEqual([
            'One',
            'Two',
        ])
        expect(wrapper.get('[data-part="lyrics-lines"]').attributes('aria-label')).toBe(
            'Lyrics of the song',
        )
        wrapper.unmount()
    })

    it('does no work while the view is hidden', async () => {
        const request = vi.fn(() => 1)
        vi.stubGlobal('requestAnimationFrame', request)
        vi.stubGlobal('cancelAnimationFrame', vi.fn())
        const wrapper = mountLyrics({
            lyrics: lines,
            active: false,
            playing: true,
            position: 3,
            duration: 10,
        })
        await flushPromises()

        expect(request).not.toHaveBeenCalled()
        expect(lineStates(wrapper)).toEqual(['upcoming', 'upcoming'])
        wrapper.unmount()
    })
})
