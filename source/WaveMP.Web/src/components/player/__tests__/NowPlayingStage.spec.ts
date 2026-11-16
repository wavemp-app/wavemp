import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import type { PlayerViewMode, Track } from '@/api/generated/models'
import { createWaveMpI18n } from '@/i18n'
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

function mountStage(props: {
    track?: Track
    mode?: PlayerViewMode
    view?: StageView
    videoState?: VideoState
    audio?: AnalyserNode | null
}) {
    return mount(NowPlayingStage, {
        props,
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
        const wrapper = mountStage({ track: track(true), mode: 'Video', view: 'video' })
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

        wrapper.unmount()
        expect(wrapper.emitted('detachVideo')).toEqual([[element]])
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

    it('offers no views without a song', () => {
        const wrapper = mountStage({})

        expect(wrapper.find('[data-part="stage-actions"]').exists()).toBe(false)
        expect(wrapper.find('[data-part="empty-state"]').exists()).toBe(true)
        wrapper.unmount()
    })
})
