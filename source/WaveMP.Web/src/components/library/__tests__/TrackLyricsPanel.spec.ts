import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createWaveMpI18n } from '@/i18n'
import type { TrackLyrics } from '@/lyrics/timing'
import TrackLyricsPanel from '../TrackLyricsPanel.vue'

const api = vi.hoisted(() => ({
    getLyrics: vi.fn(),
    downloadLyrics: vi.fn(),
}))

vi.mock('@/lyrics/api', () => api)

const found: TrackLyrics = {
    trackId: 'a',
    type: 'WordSynchronized',
    source: 'File',
    lines: [{ start: 1, end: null, text: 'One', words: [] }],
}

async function mountPanel() {
    const wrapper = mount(TrackLyricsPanel, {
        props: { trackId: 'a' },
        global: { plugins: [createWaveMpI18n('en')] },
        attachTo: document.body,
    })
    await flushPromises()
    return wrapper
}

type Wrapper = Awaited<ReturnType<typeof mountPanel>>

function status(wrapper: Wrapper) {
    return wrapper.get('[data-part="form-note"][data-item="lyrics-status"]')
}

describe('TrackLyricsPanel', () => {
    beforeEach(() => {
        api.getLyrics.mockReset()
        api.downloadLyrics.mockReset()
    })

    it('says which lyrics the song has', async () => {
        api.getLyrics.mockResolvedValue(found)
        const wrapper = await mountPanel()

        expect(api.getLyrics).toHaveBeenCalledWith('a')
        expect(status(wrapper).attributes('data-state')).toBe('found')
        expect(status(wrapper).text()).toBe('Synchronized word by word, from the lyrics file.')
        wrapper.unmount()
    })

    it('downloads lyrics and hands them on', async () => {
        api.getLyrics.mockResolvedValue(null)
        api.downloadLyrics.mockResolvedValue({
            ...found,
            type: 'LineSynchronized',
            source: 'Download',
        })
        const wrapper = await mountPanel()
        expect(status(wrapper).attributes('data-state')).toBe('none')
        expect(status(wrapper).text()).toBe('This song has no lyrics.')

        const button = wrapper.get('[data-part="button"][data-item="download-lyrics"]')
        expect(button.text()).toBe('Download lyrics')
        await button.trigger('click')
        await flushPromises()

        expect(api.downloadLyrics).toHaveBeenCalledWith('a')
        expect(status(wrapper).text()).toBe(
            'Synchronized line by line, downloaded and stored in the database.',
        )
        expect(wrapper.get('[data-part="alert"][data-state="success"]').text()).toBe(
            'The lyrics are downloaded.',
        )
        expect(wrapper.emitted('downloaded')).toHaveLength(1)
        wrapper.unmount()
    })

    it('tells a download that found nothing from a failure', async () => {
        api.getLyrics.mockResolvedValue(null)
        api.downloadLyrics.mockRejectedValueOnce({
            responseStatusCode: 404,
            code: 'lyrics.download_not_found',
        })
        api.downloadLyrics.mockRejectedValueOnce({
            responseStatusCode: 503,
            code: 'lyrics.service_unavailable',
        })
        const wrapper = await mountPanel()
        const button = wrapper.get('[data-part="button"][data-item="download-lyrics"]')

        await button.trigger('click')
        await flushPromises()
        expect(wrapper.get('[data-part="alert"]').attributes('data-state')).toBe('info')
        expect(wrapper.get('[data-part="alert"]').text()).toBe(
            'LRCLIB has no lyrics for this song.',
        )

        await button.trigger('click')
        await flushPromises()
        expect(wrapper.get('[data-part="alert"]').attributes('data-state')).toBe('error')
        expect(wrapper.get('[data-part="alert"]').text()).toBe(
            'LRCLIB cannot be reached. Check the internet connection of the server.',
        )
        expect(wrapper.emitted('downloaded')).toBeUndefined()
        wrapper.unmount()
    })

    it('reports lyrics that cannot be loaded', async () => {
        api.getLyrics.mockRejectedValue(new Error('offline'))
        const wrapper = await mountPanel()

        expect(wrapper.get('[data-part="alert"]').text()).toBe('The lyrics could not be loaded.')
        expect(wrapper.find('[data-item="download-lyrics"]').exists()).toBe(false)
        wrapper.unmount()
    })
})
