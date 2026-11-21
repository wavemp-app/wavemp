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

const lookupApi = vi.hoisted(() => ({
    getLyricsLookup: vi.fn(),
    forgetLyricsLookup: vi.fn(),
}))

vi.mock('@/lyrics/lookupApi', () => lookupApi)

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
        lookupApi.getLyricsLookup.mockReset().mockResolvedValue(null)
        lookupApi.forgetLyricsLookup.mockReset()
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

    it('says when the last lookup found no lyrics and forgets the result', async () => {
        api.getLyrics.mockResolvedValue(null)
        lookupApi.getLyricsLookup.mockResolvedValue({
            status: 'NotFound',
            checkedAt: new Date(Date.UTC(2026, 9, 1, 12)),
            isForgettable: true,
        })
        lookupApi.forgetLyricsLookup.mockResolvedValue(undefined)
        const wrapper = await mountPanel()

        expect(lookupApi.getLyricsLookup).toHaveBeenCalledWith('a')
        const note = wrapper.get('[data-part="form-note"][data-item="lyrics-lookup"]')
        expect(note.attributes('data-state')).toBe('not-found')
        expect(note.text()).toBe('LRCLIB had no lyrics for this song on Oct 1, 2026.')

        const button = wrapper.get('[data-part="button"][data-item="forget-lyrics-lookup"]')
        expect(button.text()).toBe('Forget result')
        await button.trigger('click')
        await flushPromises()

        expect(lookupApi.forgetLyricsLookup).toHaveBeenCalledWith('a')
        expect(wrapper.find('[data-item="lyrics-lookup"]').exists()).toBe(false)
        expect(wrapper.find('[data-item="forget-lyrics-lookup"]').exists()).toBe(false)
        expect(wrapper.get('[data-part="alert"][data-state="success"]').text()).toBe(
            'Result forgotten. The next lyrics download looks this song up again.',
        )
        wrapper.unmount()
    })

    it('marks instrumental songs and reports results that cannot be forgotten', async () => {
        api.getLyrics.mockResolvedValue(null)
        lookupApi.getLyricsLookup.mockResolvedValue({
            status: 'Instrumental',
            checkedAt: new Date(Date.UTC(2026, 9, 1, 12)),
            isForgettable: true,
        })
        lookupApi.forgetLyricsLookup.mockRejectedValue({
            responseStatusCode: 409,
            code: 'lyrics.lookup_found_lyrics',
        })
        const wrapper = await mountPanel()
        expect(wrapper.get('[data-item="lyrics-lookup"]').attributes('data-state')).toBe(
            'instrumental',
        )

        await wrapper.get('[data-item="forget-lyrics-lookup"]').trigger('click')
        await flushPromises()

        expect(wrapper.get('[data-part="alert"][data-state="error"]').text()).toBe(
            'The last lookup found lyrics. Only results without lyrics can be forgotten.',
        )
        wrapper.unmount()
    })

    it('shows no lookup note for songs whose lookup found lyrics or failed to load', async () => {
        api.getLyrics.mockResolvedValue(found)
        lookupApi.getLyricsLookup.mockResolvedValueOnce({
            status: 'SavedToFile',
            checkedAt: new Date(),
            isForgettable: false,
        })
        const wrapper = await mountPanel()
        expect(wrapper.find('[data-item="lyrics-lookup"]').exists()).toBe(false)
        expect(wrapper.find('[data-item="forget-lyrics-lookup"]').exists()).toBe(false)
        wrapper.unmount()

        lookupApi.getLyricsLookup.mockRejectedValueOnce(new Error('offline'))
        const failed = await mountPanel()
        expect(status(failed).attributes('data-state')).toBe('found')
        expect(failed.find('[data-item="lyrics-lookup"]').exists()).toBe(false)
        failed.unmount()
    })

    it('reports lyrics that cannot be loaded', async () => {
        api.getLyrics.mockRejectedValue(new Error('offline'))
        const wrapper = await mountPanel()

        expect(wrapper.get('[data-part="alert"]').text()).toBe('The lyrics could not be loaded.')
        expect(wrapper.find('[data-item="download-lyrics"]').exists()).toBe(false)
        wrapper.unmount()
    })
})
