import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createWaveMpI18n } from '@/i18n'
import type { VideoDiagnosticsSnapshot } from '@/player/videoDiagnostics'
import VideoDiagnostics from '../VideoDiagnostics.vue'
import VideoStage from '../VideoStage.vue'

function snapshot(extra: Partial<VideoDiagnosticsSnapshot> = {}): VideoDiagnosticsSnapshot {
    return {
        delivery: 'hls',
        elapsed: 12,
        drift: -0.123,
        rate: 1.05,
        readyState: 4,
        action: 'rate',
        buffered: { start: 0, end: 42 },
        bufferedAhead: 12.5,
        seeks: 3,
        missedSeeks: 1,
        seekLatency: 0.42,
        droppedFrames: 7,
        totalFrames: 700,
        stuttering: false,
        stutters: 1,
        events: [
            {
                at: 2,
                kind: 'seek',
                position: 20,
                drift: -0.6,
                rate: 1,
                readyState: 4,
                bufferedAhead: 3,
                action: 'seek',
                value: 20.4,
            },
            {
                at: 3,
                kind: 'stutter-start',
                position: 21,
                drift: -0.4,
                rate: 1,
                readyState: 2,
                bufferedAhead: 0,
                action: 'wait',
                value: 9,
            },
        ],
        ...extra,
    }
}

function mountDiagnostics(value: VideoDiagnosticsSnapshot) {
    return mount(VideoDiagnostics, {
        props: { snapshot: value },
        global: { plugins: [createWaveMpI18n('en')] },
    })
}

describe('VideoDiagnostics', () => {
    afterEach(() => {
        vi.unstubAllGlobals()
    })

    it('shows the values of the last check', () => {
        const wrapper = mountDiagnostics(snapshot())
        const value = (field: string) =>
            wrapper.get(`[data-field="${field}"] [data-part="diagnostics-figure"]`).text()

        expect(wrapper.attributes('data-delivery')).toBe('hls')
        expect(wrapper.attributes('data-state')).toBe('smooth')
        expect(value('delivery')).toBe('HLS (transcoded)')
        expect(value('drift')).toBe('-123 ms')
        expect(value('rate')).toBe('1.05×')
        expect(value('action')).toBe('Adjusts the speed')
        expect(value('seeks')).toBe('3 (1 missed in a row)')
        expect(value('seek-latency')).toBe('0.42 s')
        expect(value('buffered')).toBe('0.0 to 42.0 s (12.5 s ahead)')
        expect(value('dropped-frames')).toBe('7 of 700')
    })

    it('marks a stutter and shows the newest entries of the log first', () => {
        const wrapper = mountDiagnostics(snapshot({ stuttering: true }))

        expect(wrapper.attributes('data-state')).toBe('stutter')
        const events = wrapper.findAll('[data-part="diagnostics-event"]')
        expect(events.map((event) => event.attributes('data-kind'))).toEqual([
            'stutter-start',
            'seek',
        ])
        expect(events[1]?.text()).toContain('Seek to 20.40 s')
    })

    it('says what it does not know', () => {
        const wrapper = mountDiagnostics(
            snapshot({ drift: null, droppedFrames: null, buffered: null, events: [] }),
        )

        expect(wrapper.get('[data-field="drift"] dd').text()).toBe('Unknown')
        expect(wrapper.get('[data-field="dropped-frames"] dd').text()).toBe('Unknown')
        expect(wrapper.get('[data-field="buffered"] dd').text()).toBe('Nothing at the position')
        expect(wrapper.find('[data-part="diagnostics-log"]').exists()).toBe(false)
    })

    it('copies the whole log as JSON', async () => {
        const writeText = vi.fn((_text: string) => Promise.resolve())
        vi.stubGlobal('navigator', { userAgent: 'Test', clipboard: { writeText } })
        const wrapper = mountDiagnostics(snapshot())

        await wrapper.get('[data-item="copy-video-diagnostics"]').trigger('click')
        await flushPromises()

        const copied = JSON.parse(writeText.mock.calls[0]?.[0] ?? '{}') as Record<string, unknown>
        expect(copied.userAgent).toBe('Test')
        expect(copied.events).toHaveLength(2)
        expect(wrapper.get('[data-item="copy-video-diagnostics"]').text()).toBe('Log copied')
    })

    it('lies on the video stage only while there are diagnostics', async () => {
        const stage = mount(VideoStage, {
            props: { diagnostics: null },
            global: { plugins: [createWaveMpI18n('en')] },
        })
        expect(stage.find('[data-part="video-diagnostics"]').exists()).toBe(false)

        await stage.setProps({ diagnostics: snapshot() })

        expect(stage.find('[data-part="video-diagnostics"]').exists()).toBe(true)
    })
})
