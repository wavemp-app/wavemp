import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createWaveMpI18n } from '@/i18n'
import type { TrackLyrics } from '@/lyrics/timing'

const api = vi.hoisted(() => ({
    uploadLyrics: vi.fn(),
}))

vi.mock('@/lyrics/api', () => api)

const LyricsUploadDialog = (await import('../LyricsUploadDialog.vue')).default

const saved: TrackLyrics = {
    trackId: 'song',
    type: 'LineSynchronized',
    source: 'File',
    lines: [{ start: 1, end: null, text: 'Line', words: [] }],
}

function problem(code: string, extra: object = {}): Error {
    return Object.assign(new Error(code), { code, ...extra })
}

async function mountDialog() {
    const wrapper = mount(LyricsUploadDialog, {
        props: { trackId: 'song', open: false },
        global: { plugins: [createWaveMpI18n('en')] },
        attachTo: document.body,
    })
    await wrapper.setProps({ open: true })
    await flushPromises()
    return wrapper
}

type Wrapper = Awaited<ReturnType<typeof mountDialog>>

function textarea(wrapper: Wrapper) {
    return wrapper.get('[data-field="lyrics-text"] textarea')
}

function saveButton(wrapper: Wrapper) {
    return wrapper.get('[data-part="dialog-footer"] [data-item="save-lyrics"]')
}

describe('LyricsUploadDialog', () => {
    beforeEach(() => {
        api.uploadLyrics
            .mockReset()
            .mockResolvedValue({ lyrics: saved, publication: 'NotRequested' })
    })

    it('saves pasted lyrics and tells whether they have times', async () => {
        const wrapper = await mountDialog()
        const field = wrapper.get('[data-field="lyrics-text"]')
        expect(field.attributes('data-variant')).toBe('multiline')
        expect(textarea(wrapper).attributes('data-part')).toBe('input')

        await textarea(wrapper).setValue('Plain line')
        expect(field.get('[data-part="field-hint"]').text()).toBe(
            'Text only: the lines have no times.',
        )
        await textarea(wrapper).setValue('[00:01.00]Line')
        expect(field.get('[data-part="field-hint"]').text()).toBe('Synced: the lines have times.')
        await saveButton(wrapper).trigger('click')
        await flushPromises()

        expect(api.uploadLyrics).toHaveBeenCalledWith('song', '[00:01.00]Line', false)
        expect(wrapper.findAll('[data-part="alert"]').map((alert) => alert.text())).toEqual([
            'The lyrics are saved.',
        ])
        expect(wrapper.emitted('saved')?.[0]).toEqual([saved])
        wrapper.unmount()
    })

    it('publishes on request and reports the result', async () => {
        api.uploadLyrics.mockResolvedValue({ lyrics: saved, publication: 'MissingMetadata' })
        const wrapper = await mountDialog()

        await textarea(wrapper).setValue('[00:01.00]Line')
        await wrapper.get('[data-field="publish-lyrics"] input').setValue(true)
        await saveButton(wrapper).trigger('click')
        await flushPromises()

        expect(api.uploadLyrics).toHaveBeenCalledWith('song', '[00:01.00]Line', true)
        const publication = wrapper.get('[data-part="alert"][data-item="lyrics-publication"]')
        expect(publication.text()).toContain('LRCLIB needs title, artist, album and length')
        wrapper.unmount()
    })

    it('reads a chosen file into the text field', async () => {
        const wrapper = await mountDialog()
        const input = wrapper.get('input[type="file"]')
        const file = new File([new Uint8Array([0x47, 0x72, 0xfc, 0xdf, 0x65])], 'Song.lrc')
        Object.defineProperty(input.element, 'files', { value: [file] })

        await input.trigger('change')
        await flushPromises()

        expect((textarea(wrapper).element as HTMLTextAreaElement).value).toBe('Grüße')
        expect(wrapper.get('[data-item="lyrics-file"]').text()).toBe('Read from Song.lrc')
        wrapper.unmount()
    })

    it('asks for lyrics and keeps the text after a failure', async () => {
        const wrapper = await mountDialog()

        await saveButton(wrapper).trigger('click')
        expect(wrapper.get('[data-field="lyrics-text"] [data-part="field-error"]').text()).toBe(
            'Enter the lyrics or choose a file.',
        )
        expect(api.uploadLyrics).not.toHaveBeenCalled()

        api.uploadLyrics.mockRejectedValueOnce(
            problem('request.invalid', { errors: [{ field: 'text', code: 'required' }] }),
        )
        await textarea(wrapper).setValue('[ar:Band]')
        await saveButton(wrapper).trigger('click')
        await flushPromises()
        expect(wrapper.find('[data-field="lyrics-text"] [data-part="field-error"]').exists()).toBe(
            true,
        )

        api.uploadLyrics.mockRejectedValueOnce(problem('lyrics.file_not_writable'))
        await textarea(wrapper).setValue('Line')
        await saveButton(wrapper).trigger('click')
        await flushPromises()
        expect(wrapper.get('[data-part="alert"]').text()).toContain('cannot be replaced')
        expect((textarea(wrapper).element as HTMLTextAreaElement).value).toBe('Line')
        expect(wrapper.emitted('saved')).toBeUndefined()
        wrapper.unmount()
    })
})
