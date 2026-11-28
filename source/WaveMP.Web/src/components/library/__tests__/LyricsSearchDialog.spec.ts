import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createWaveMpI18n } from '@/i18n'
import type { LyricsMatch, LyricsSearch } from '@/lyrics/api'

const api = vi.hoisted(() => ({
    searchLyrics: vi.fn(),
    chooseLyrics: vi.fn(),
}))

vi.mock('@/lyrics/api', () => api)

const LyricsSearchDialog = (await import('../LyricsSearchDialog.vue')).default

const fitting: LyricsMatch = {
    id: 1,
    title: 'Diamond Hard',
    artist: 'Kerli',
    album: 'Single',
    duration: 343,
    synchronized: true,
    fitsDuration: true,
}

const longer: LyricsMatch = {
    id: 2,
    title: 'Diamond Hard',
    artist: 'Kerli',
    album: null,
    duration: 242,
    synchronized: false,
    fitsDuration: false,
}

const found: LyricsSearch = { title: 'Diamond Hard', artist: 'Kerli', matches: [fitting, longer] }

function problem(code: string): Error {
    return Object.assign(new Error(code), { code })
}

async function mountDialog() {
    const wrapper = mount(LyricsSearchDialog, {
        props: { trackId: 'song', open: false },
        global: {
            plugins: [createWaveMpI18n('en')],
            stubs: { RouterLink: { template: '<a data-part="link"><slot /></a>' } },
        },
        attachTo: document.body,
    })
    await wrapper.setProps({ open: true })
    await flushPromises()
    return wrapper
}

type Wrapper = Awaited<ReturnType<typeof mountDialog>>

function search(wrapper: Wrapper) {
    return wrapper.get('[data-part="online-search"]')
}

function matches(wrapper: Wrapper) {
    return wrapper.findAll('[data-part="online-match"]')
}

function applyButton(wrapper: Wrapper) {
    return wrapper.get('[data-part="dialog-footer"] [data-item="apply"]')
}

describe('LyricsSearchDialog', () => {
    beforeEach(() => {
        api.searchLyrics.mockReset().mockResolvedValue(found)
        api.chooseLyrics.mockReset()
    })

    it('searches with the names of the song and selects the best track', async () => {
        const wrapper = await mountDialog()

        expect(api.searchLyrics).toHaveBeenCalledWith('song', undefined)
        expect(search(wrapper).attributes('data-state')).toBe('ready')
        expect(search(wrapper).attributes('data-owner')).toBe('lyrics')
        expect((wrapper.get('[data-field="title"] input').element as HTMLInputElement).value).toBe(
            'Diamond Hard',
        )
        expect((wrapper.get('[data-field="artist"] input').element as HTMLInputElement).value).toBe(
            'Kerli',
        )
        expect(matches(wrapper)).toHaveLength(2)
        expect(matches(wrapper)[0]?.attributes('aria-checked')).toBe('true')
        expect(matches(wrapper)[0]?.get('[data-field="duration"]').text()).toBe('5:43')
        expect(matches(wrapper)[0]?.get('[data-field="synchronized"]').text()).toBe('Synced')
        expect(matches(wrapper)[0]?.find('[data-field="other-version"]').exists()).toBe(false)
        expect(matches(wrapper)[1]?.get('[data-part="online-match-album"]').text()).toBe(
            'Without album',
        )
        expect(
            matches(wrapper)[1]?.get('[data-field="synchronized"]').attributes('data-state'),
        ).toBe('static')
        expect(matches(wrapper)[1]?.get('[data-field="other-version"]').text()).toBe('Other length')
        expect(wrapper.find('[data-part="form-note"][data-item="other-version"]').exists()).toBe(
            false,
        )
        wrapper.unmount()
    })

    it('searches again with changed names, an empty artist by title only', async () => {
        const wrapper = await mountDialog()
        api.searchLyrics.mockResolvedValueOnce({ title: 'Other', artist: null, matches: [] })

        await wrapper.get('[data-field="title"] input').setValue('Other')
        await wrapper.get('[data-field="artist"] input').setValue('')
        await wrapper.get('[data-part="online-search-form"]').trigger('submit')
        await flushPromises()

        expect(api.searchLyrics).toHaveBeenLastCalledWith('song', { title: 'Other', artist: '' })
        expect(wrapper.get('[data-part="alert"]').text()).toContain('LRCLIB has no lyrics')
        expect(applyButton(wrapper).attributes('disabled')).toBeDefined()
        wrapper.unmount()
    })

    it('asks for a title before it searches', async () => {
        const wrapper = await mountDialog()

        await wrapper.get('[data-field="title"] input').setValue('  ')
        await wrapper.get('[data-part="online-search-form"]').trigger('submit')
        await flushPromises()

        expect(api.searchLyrics).toHaveBeenCalledTimes(1)
        expect(wrapper.get('[data-field="title"] [data-part="field-error"]').text()).toBe(
            'Enter a title.',
        )
        wrapper.unmount()
    })

    it('saves the chosen track and marks lyrics of another version', async () => {
        const saved = {
            trackId: 'song',
            type: 'Static',
            source: 'File',
            lines: [{ start: null, end: null, text: 'Line', words: [] }],
            fromOtherVersion: true,
        }
        api.chooseLyrics.mockResolvedValue(saved)
        const wrapper = await mountDialog()

        await matches(wrapper)[1]?.trigger('click')
        expect(wrapper.get('[data-part="form-note"][data-item="other-version"]').text()).toContain(
            'Only its text is kept',
        )
        await applyButton(wrapper).trigger('click')
        await flushPromises()

        expect(api.chooseLyrics).toHaveBeenCalledWith('song', 2)
        expect(search(wrapper).attributes('data-state')).toBe('applied')
        expect(wrapper.findAll('[data-part="alert"]').map((alert) => alert.text())).toEqual([
            'The lyrics are saved.',
            'These lyrics come from another version of the song with a different length, ' +
                'so they are not synced to this one.',
        ])
        expect(wrapper.emitted('saved')?.[0]).toEqual([saved])
        wrapper.unmount()
    })

    it('reports a lyrics file that cannot be replaced and stays open', async () => {
        api.chooseLyrics.mockRejectedValue(problem('lyrics.file_not_writable'))
        const wrapper = await mountDialog()

        await applyButton(wrapper).trigger('click')
        await flushPromises()

        expect(search(wrapper).attributes('data-state')).toBe('ready')
        expect(wrapper.get('[data-part="alert"]').text()).toContain('cannot be replaced')
        expect(wrapper.emitted('saved')).toBeUndefined()
        wrapper.unmount()
    })

    it.each([
        ['lyrics.download_disabled', 'disabled'],
        ['lyrics.service_unavailable', 'unavailable'],
        ['lyrics.rate_limited', 'rateLimited'],
        ['track.not_found', 'notFound'],
        [undefined, 'error'],
    ])('shows why the search failed (%s)', async (code, state) => {
        api.searchLyrics.mockRejectedValue(code ? problem(code) : new Error('offline'))
        const wrapper = await mountDialog()

        expect(search(wrapper).attributes('data-state')).toBe(state)
        expect(wrapper.find('[data-part="alert"]').exists()).toBe(true)
        wrapper.unmount()
    })
})
