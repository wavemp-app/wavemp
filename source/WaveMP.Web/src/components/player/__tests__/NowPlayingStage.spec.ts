import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { useContextMenuStore } from '@/stores/contextMenu'
import type { PlayerViewMode, Track } from '@/api/generated/models'
import { createWaveMpI18n } from '@/i18n'
import type { TrackLyrics } from '@/lyrics/timing'
import type { StageView, VideoState } from '@/player/videoSync'
import { visualizationDependencies } from '@/visualization/modules'
import { FakeClock, FakeModule, fakeAnalyser } from '@/visualization/__tests__/fakes'
import NowPlayingStage from '../NowPlayingStage.vue'

vi.mock('@/library/api', () => ({ fetchAlbumCover: vi.fn(), fetchTrackCover: vi.fn() }))

function track(hasVideo: boolean): Track {
    return {
        id: 'a',
        title: 'A',
        hasAudio: true,
        hasVideo,
        hasCover: false,
        artist: { id: 'band', name: 'Band' },
    }
}

function mountStage(
    props: {
        track?: Track
        mode?: PlayerViewMode
        view?: StageView
        videoState?: VideoState
        audio?: AnalyserNode | null
        lyrics?: TrackLyrics | null
        casting?: boolean
        castNotice?: string
        remoteNotice?: string
    },
    attrs: Record<string, unknown> = {},
) {
    return mount(NowPlayingStage, {
        props,
        attrs,
        global: { plugins: [createPinia(), createWaveMpI18n('en')] },
        attachTo: document.body,
    })
}

type Wrapper = ReturnType<typeof mountStage>

function segment(wrapper: Wrapper, item: string) {
    return wrapper.get(`[data-part="stage-actions"] [data-part="segment"][data-item="${item}"]`)
}

function checked(wrapper: Wrapper): Record<string, string | undefined> {
    return Object.fromEntries(
        ['cover', 'video', 'visualization'].map((item) => [
            item,
            segment(wrapper, item).attributes('aria-checked'),
        ]),
    )
}

function viewState(wrapper: Wrapper, kind: string): string | undefined {
    return wrapper.get(`[data-part="stage-view"][data-kind="${kind}"]`).attributes('data-state')
}

describe('NowPlayingStage', () => {
    const original = { ...visualizationDependencies }
    let module: FakeModule

    beforeEach(() => {
        module = new FakeModule()
        visualizationDependencies.module = () => module
        visualizationDependencies.clock = new FakeClock()
    })

    afterEach(() => {
        Object.assign(visualizationDependencies, original)
    })

    it('shows the cover of a song without a video and offers no video', () => {
        const wrapper = mountStage({ track: track(false), mode: 'Video', view: 'cover' })

        expect(wrapper.get('[data-part="stage"]').attributes('data-state')).toBe('cover')
        expect(viewState(wrapper, 'cover')).toBe('active')
        expect(wrapper.find('[data-kind="video"]').exists()).toBe(false)
        expect(segment(wrapper, 'video').attributes('disabled')).toBeDefined()
        expect(checked(wrapper)).toEqual({
            cover: 'true',
            video: 'false',
            visualization: 'false',
        })
        wrapper.unmount()
    })

    it('opens its context menu with the views and the full screen', async () => {
        const pinia = createPinia()
        setActivePinia(pinia)
        const wrapper = mount(NowPlayingStage, {
            props: { track: track(true), mode: 'Video', view: 'cover' },
            global: { plugins: [pinia, createWaveMpI18n('en')] },
            attachTo: document.body,
        })
        const menu = useContextMenuStore()

        await wrapper.get('[data-part="stage-views"]').trigger('contextmenu', {
            clientX: 30,
            clientY: 40,
        })
        const target = menu.request?.target
        expect(target).toMatchObject({
            type: 'now-playing',
            selected: 'Video',
            views: [
                { mode: 'Cover', available: true },
                { mode: 'Video', available: true },
                { mode: 'Visualization', available: true },
                { mode: 'Lyrics', available: false },
            ],
            fullscreen: { available: false, active: false },
        })
        if (target?.type === 'now-playing') {
            target.selectView('Cover')
        }
        expect(wrapper.emitted('selectMode')).toEqual([['Cover']])

        await wrapper
            .get('[data-part="stage-actions"] [data-part="btn-item-menu"]')
            .trigger('click')
        expect(menu.request?.sequence).toBe(2)
        wrapper.unmount()
    })

    it('chooses the view with a segmented control', async () => {
        const wrapper = mountStage({ track: track(true), mode: 'Cover', view: 'cover' })
        const group = wrapper.get('[data-part="stage-actions"] [data-part="segmented-control"]')

        expect(group.attributes('role')).toBe('radiogroup')
        expect(group.attributes('data-item')).toBe('stage-view')
        await segment(wrapper, 'video').trigger('click')
        await segment(wrapper, 'visualization').trigger('click')
        await segment(wrapper, 'cover').trigger('click')

        expect(wrapper.emitted('selectMode')).toEqual([['Video'], ['Visualization'], ['Cover']])
        wrapper.unmount()
    })

    it('keeps the video in the page while the cover or the visualization is shown', async () => {
        // The wrapper forgets its recorded events when it is unmounted, including the ones emitted
        // while unmounting, so a listener records the detach that happens then.
        const detached = vi.fn()
        const wrapper = mountStage(
            { track: track(true), mode: 'Video', view: 'video' },
            { onDetachVideo: detached },
        )
        const element = wrapper.get('[data-part="video"]').element as HTMLVideoElement

        expect(wrapper.get('[data-part="stage"]').attributes('data-state')).toBe('video')
        expect(viewState(wrapper, 'video')).toBe('active')
        expect(viewState(wrapper, 'cover')).toBeUndefined()
        expect(element.muted).toBe(true)
        expect(wrapper.emitted('attachVideo')).toEqual([[element]])
        expect(checked(wrapper).video).toBe('true')

        await wrapper.setProps({ mode: 'Cover', view: 'cover' })
        expect(viewState(wrapper, 'video')).toBeUndefined()
        expect(viewState(wrapper, 'cover')).toBe('active')
        expect(wrapper.get('[data-kind="video"]').attributes('aria-hidden')).toBe('true')

        await wrapper.setProps({ mode: 'Visualization', view: 'visualization' })
        expect(viewState(wrapper, 'visualization')).toBe('active')
        expect(wrapper.emitted('attachVideo')).toHaveLength(1)
        expect(wrapper.emitted('detachVideo')).toBeUndefined()
        expect(detached).not.toHaveBeenCalled()

        wrapper.unmount()
        expect(detached).toHaveBeenCalledOnce()
        expect(detached).toHaveBeenCalledWith(element)
    })

    it('keeps the cover while the video loads, with the video checked', () => {
        const wrapper = mountStage({
            track: track(true),
            mode: 'Video',
            view: 'cover',
            videoState: 'loading',
        })

        expect(viewState(wrapper, 'cover')).toBe('active')
        expect(viewState(wrapper, 'video')).toBe('loading')
        expect(checked(wrapper).video).toBe('true')
        expect(wrapper.find('[data-part="stage-notice"]').exists()).toBe(false)
        // jsdom has no full screen, and the video view is not shown yet.
        expect(wrapper.get('[data-part="btn-fullscreen"]').attributes('disabled')).toBeDefined()
        wrapper.unmount()
    })

    it('says that a failed video cannot be played while the music goes on', () => {
        const wrapper = mountStage({
            track: track(true),
            mode: 'Video',
            view: 'cover',
            videoState: 'failed',
        })

        expect(viewState(wrapper, 'video')).toBe('failed')
        expect(wrapper.get('[data-part="stage-notice"]').text()).toBe(
            'The video cannot be played. The song goes on.',
        )
        expect(checked(wrapper).cover).toBe('true')
        wrapper.unmount()
    })

    it('draws the music in the visualization view and hands changes on', async () => {
        const wrapper = mountStage({
            track: track(false),
            mode: 'Visualization',
            view: 'visualization',
            audio: fakeAnalyser(),
        })
        await module.finishLoad()
        await flushPromises()

        const visualization = wrapper.get('[data-part="visualization"]')
        expect(visualization.attributes('data-state')).toBe('running')
        expect(checked(wrapper).visualization).toBe('true')

        await wrapper.get('[data-part="btn-auto-change"]').trigger('click')
        expect(wrapper.emitted('updateVisualization')).toEqual([[{ autoChange: false }]])
        wrapper.unmount()
    })

    it('loads no visualization while another view is shown', async () => {
        const wrapper = mountStage({ track: track(false), mode: 'Cover', view: 'cover' })
        await flushPromises()

        expect(wrapper.get('[data-part="visualization"]').attributes('data-state')).toBe('idle')
        expect(module.loads).toBe(0)
        wrapper.unmount()
    })

    it('offers the lyrics only for a song with lyrics', () => {
        const wrapper = mountStage({ track: track(false), mode: 'Lyrics', view: 'cover' })

        expect(segment(wrapper, 'lyrics').attributes('disabled')).toBeDefined()
        expect(segment(wrapper, 'lyrics').attributes('aria-checked')).toBe('false')
        expect(segment(wrapper, 'cover').attributes('aria-checked')).toBe('true')
        expect(wrapper.find('[data-kind="lyrics"]').exists()).toBe(false)
        wrapper.unmount()
    })

    it('shows the lyrics and seeks to a chosen line', async () => {
        const lyrics: TrackLyrics = {
            trackId: 'a',
            type: 'LineSynchronized',
            source: 'File',
            lines: [
                { start: 1, end: 3, text: 'One', words: [] },
                { start: 3, end: null, text: 'Two', words: [] },
            ],
        }
        const wrapper = mountStage({ track: track(false), mode: 'Lyrics', view: 'lyrics', lyrics })

        expect(segment(wrapper, 'lyrics').attributes('disabled')).toBeUndefined()
        expect(segment(wrapper, 'lyrics').attributes('aria-checked')).toBe('true')
        expect(segment(wrapper, 'lyrics').text()).toBe('Lyrics')
        expect(wrapper.get('[data-part="stage"]').attributes('data-state')).toBe('lyrics')
        expect(viewState(wrapper, 'lyrics')).toBe('active')
        await wrapper.findAll('[data-part="lyrics-line-button"]')[1]!.trigger('click')

        expect(wrapper.emitted('seek')).toEqual([[3]])
        wrapper.unmount()
    })

    it('ignores lyrics of another song', () => {
        const lyrics: TrackLyrics = {
            trackId: 'other',
            type: 'Static',
            source: 'Tags',
            lines: [{ start: null, end: null, text: 'One', words: [] }],
        }
        const wrapper = mountStage({ track: track(false), mode: 'Lyrics', view: 'cover', lyrics })

        expect(segment(wrapper, 'lyrics').attributes('disabled')).toBeDefined()
        expect(wrapper.find('[data-kind="lyrics"]').exists()).toBe(false)
        wrapper.unmount()
    })

    it('offers no views without a song', () => {
        const wrapper = mountStage({})

        expect(wrapper.find('[data-part="stage-actions"]').exists()).toBe(false)
        expect(wrapper.find('[data-part="empty-state"]').exists()).toBe(true)
        wrapper.unmount()
    })

    it('offers no visualization while casting and names the device that plays', () => {
        const wrapper = mountStage({
            track: track(true),
            mode: 'Visualization',
            view: 'cover',
            casting: true,
            castNotice: 'Playing on Living Room',
        })

        expect(segment(wrapper, 'visualization').attributes('disabled')).toBeDefined()
        expect(segment(wrapper, 'visualization').text()).toContain('not available while casting')
        expect(checked(wrapper).cover).toBe('true')
        const notice = wrapper.get('[data-part="stage-notice"][data-kind="cast"]')
        expect(notice.text()).toBe('Playing on Living Room')
        wrapper.unmount()
    })

    it('offers no visualization while another device plays and names that device', () => {
        const wrapper = mountStage({
            track: track(true),
            mode: 'Visualization',
            view: 'cover',
            remoteNotice: 'Playing on Living room PC',
        })

        expect(segment(wrapper, 'visualization').attributes('disabled')).toBeDefined()
        expect(segment(wrapper, 'visualization').text()).toBe('Visualization')
        expect(checked(wrapper).cover).toBe('true')
        const notice = wrapper.get('[data-part="stage-notice"][data-kind="remote"]')
        expect(notice.text()).toBe('Playing on Living room PC')
        expect(wrapper.find('[data-part="stage-notice"][data-kind="cast"]').exists()).toBe(false)
        wrapper.unmount()
    })
})
