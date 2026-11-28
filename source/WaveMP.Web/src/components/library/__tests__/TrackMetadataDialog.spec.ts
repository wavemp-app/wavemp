import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import type { TrackMetadata, TrackSongSegments } from '@/api/generated/models'
import { createWaveMpI18n } from '@/i18n'

const api = vi.hoisted(() => ({
    getTrackMetadata: vi.fn(),
    updateTrackMetadata: vi.fn(),
    getCoverRule: vi.fn(),
    setCoverRule: vi.fn(),
    fetchCoverRulePreview: vi.fn(),
    getAlbumCoverFallback: vi.fn(),
    setAlbumCoverFallback: vi.fn(),
    listArtists: vi.fn(),
    getTrackSongSegments: vi.fn(),
    setTrackSongSegment: vi.fn(),
    resetTrackSongSegment: vi.fn(),
    analyzeTrackSong: vi.fn(),
}))

vi.mock('@/library/api', () => api)

const TrackMetadataDialog = (await import('../TrackMetadataDialog.vue')).default
const { useCoversStore } = await import('@/stores/covers')

const metadata: TrackMetadata = {
    trackId: 'song',
    status: 'Editable',
    audioFilePath: 'Band/Album/01 - Song.mp3',
    title: 'Song',
    artist: 'Band',
    album: 'Album',
    albumArtist: 'Band',
    year: 2001,
    trackNumber: 1,
    discNumber: null,
    genre: 'Rock',
}

const wholeFile = {
    method: 'WholeFile' as const,
    confidence: 1,
    confident: true,
    startMilliseconds: 0,
    endMilliseconds: 200_000,
    pieces: [],
}

const songSegments: TrackSongSegments = {
    trackId: 'song',
    audio: { durationMilliseconds: 200_000, detected: wholeFile, effective: wholeFile },
}

async function mountDialog() {
    const wrapper = mount(TrackMetadataDialog, {
        props: { trackId: 'song', open: false },
        global: { plugins: [createWaveMpI18n('en')] },
        attachTo: document.body,
    })
    await wrapper.setProps({ open: true })
    await flushPromises()
    return wrapper
}

type Wrapper = Awaited<ReturnType<typeof mountDialog>>

function input(wrapper: Wrapper, field: string) {
    return wrapper.get(`[data-part="field"][data-field="${field}"] input`)
}

/** The names of the chips of a token input of artists. */
function chips(wrapper: Wrapper, field: string): string[] {
    return wrapper
        .findAll(`[data-field="${field}"] [data-part="artist-token"] [data-field="name"]`)
        .map((chip) => chip.text())
}

function value(wrapper: Wrapper, field: string): string {
    return (input(wrapper, field).element as HTMLInputElement).value
}

async function submit(wrapper: Wrapper) {
    await wrapper.get('form').trigger('submit')
    await flushPromises()
}

function saveButton(wrapper: Wrapper) {
    return wrapper.get('[data-part="dialog-footer"] button[data-variant="primary"]')
}

async function chooseCoverRule(wrapper: Wrapper, item: string) {
    await wrapper
        .get(`[data-item="cover-rule"] [data-part="segment"][data-item="${item}"]`)
        .trigger('click')
    await flushPromises()
}

describe('TrackMetadataDialog', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
        for (const mock of Object.values(api)) {
            mock.mockReset()
        }
        api.getTrackMetadata.mockResolvedValue(metadata)
        api.getCoverRule.mockResolvedValue({
            rule: 'Inherit',
            inheritedPriority: 'TrackFirst',
            effectivePriority: 'TrackFirst',
        })
        api.fetchCoverRulePreview.mockResolvedValue(undefined)
        api.getTrackSongSegments.mockResolvedValue(songSegments)
    })

    it('loads the metadata into the fields and names the file', async () => {
        const wrapper = await mountDialog()

        expect(api.getTrackMetadata).toHaveBeenCalledWith('song')
        expect(wrapper.get('[data-part="dialog-title"]').text()).toBe('Edit metadata')
        expect(value(wrapper, 'title')).toBe('Song')
        expect(chips(wrapper, 'artist')).toEqual(['Band'])
        expect(value(wrapper, 'artist')).toBe('')
        expect(value(wrapper, 'album')).toBe('Album')
        expect(chips(wrapper, 'album-artist')).toEqual(['Band'])
        expect(value(wrapper, 'year')).toBe('2001')
        expect(value(wrapper, 'number')).toBe('1')
        expect(value(wrapper, 'disc')).toBe('')
        expect(value(wrapper, 'genre')).toBe('Rock')
        expect(input(wrapper, 'year').attributes('inputmode')).toBe('numeric')
        const tagRows = wrapper.findAll('form [data-part="field-row"] [data-part="field"]')
        expect(tagRows).toHaveLength(3)
        expect(wrapper.text()).toContain('The tags are written to Band/Album/01 - Song.mp3.')
        expect(saveButton(wrapper).attributes('disabled')).toBeUndefined()
        wrapper.unmount()
    })

    it.each([
        ['NoAudio', 'This song only has a video.'],
        ['NotWritable', 'The audio file cannot be written.'],
    ] as const)('disables the tags with a hint for %s', async (status, hint) => {
        api.getTrackMetadata.mockResolvedValue({ ...metadata, status })

        const wrapper = await mountDialog()

        const inputs = wrapper.findAll('[data-part="form"] input')
        expect(inputs).toHaveLength(8)
        expect(inputs.every((item) => item.attributes('disabled') !== undefined)).toBe(true)
        // The cover rule of the song can still be saved.
        expect(saveButton(wrapper).attributes('disabled')).toBeUndefined()
        expect(wrapper.get('[data-part="alert"][data-state="info"]').text()).toContain(hint)
        const notes = wrapper.findAll('[data-part="form-note"]').map((note) => note.text())
        expect(notes.join(' ')).not.toContain('Empty fields')
        wrapper.unmount()
    })

    it('sends only the changed values and null for cleared fields', async () => {
        const saved = { ...metadata, title: 'New Title', genre: null, year: 1999 }
        api.updateTrackMetadata.mockResolvedValue(saved)
        const wrapper = await mountDialog()

        await input(wrapper, 'title').setValue('  New   Title ')
        await input(wrapper, 'genre').setValue('')
        await input(wrapper, 'year').setValue('1999')
        await submit(wrapper)

        expect(api.updateTrackMetadata).toHaveBeenCalledWith('song', {
            title: 'New Title',
            genre: null,
            year: 1999,
        })
        expect(wrapper.emitted('saved')).toEqual([[saved]])
        expect(wrapper.emitted('update:open')?.at(-1)).toEqual([false])
        wrapper.unmount()
    })

    it('loads the covers again after a write, since the song may belong to another album', async () => {
        api.updateTrackMetadata.mockResolvedValue({ ...metadata, artist: 'Other Band' })
        const wrapper = await mountDialog()
        const invalidate = vi.spyOn(useCoversStore(), 'invalidate')

        await wrapper.get('[data-field="artist"] [data-item="remove"]').trigger('click')
        await input(wrapper, 'artist').setValue('Other Band')
        await input(wrapper, 'artist').trigger('keydown', { key: 'Enter' })
        await submit(wrapper)

        expect(api.updateTrackMetadata).toHaveBeenCalledWith('song', {
            artists: [{ name: 'Other Band', role: 'Main' }],
        })
        expect(invalidate).toHaveBeenCalledTimes(1)
        expect(invalidate).toHaveBeenCalledWith()
        wrapper.unmount()
    })

    it('closes without a request if nothing changed', async () => {
        const wrapper = await mountDialog()
        const invalidate = vi.spyOn(useCoversStore(), 'invalidate')

        // The same name again adds nothing.
        await input(wrapper, 'artist').setValue(' band ')
        await input(wrapper, 'artist').trigger('keydown', { key: 'Enter' })
        await input(wrapper, 'album').setValue(' Album ')
        await submit(wrapper)

        expect(api.updateTrackMetadata).not.toHaveBeenCalled()
        expect(invalidate).not.toHaveBeenCalled()
        expect(wrapper.emitted('update:open')?.at(-1)).toEqual([false])
        wrapper.unmount()
    })

    it('shows invalid numbers at their fields without a request', async () => {
        const wrapper = await mountDialog()

        await input(wrapper, 'number').setValue('0')
        await input(wrapper, 'disc').setValue('x')
        await submit(wrapper)

        expect(api.updateTrackMetadata).not.toHaveBeenCalled()
        const number = wrapper.get('[data-part="field"][data-field="number"]')
        expect(number.attributes('data-state')).toBe('invalid')
        expect(number.get('[data-part="field-error"]').text()).toBe(
            'Enter a whole number from 1 to 9999.',
        )
        expect(wrapper.get('[data-field="disc"]').attributes('data-state')).toBe('invalid')
        expect(wrapper.get('[data-field="year"]').attributes('data-state')).toBeUndefined()
        wrapper.unmount()
    })

    it('greys the fields out when the file turns out not to be writable', async () => {
        api.updateTrackMetadata.mockRejectedValue({
            code: 'track.not_writable',
            responseStatusCode: 409,
        })
        const wrapper = await mountDialog()

        await input(wrapper, 'title').setValue('Other')
        await submit(wrapper)

        expect(wrapper.get('[data-part="alert"][data-state="error"]').text()).toBe(
            'The changes could not be written to the audio file.',
        )
        expect(wrapper.get('[data-part="alert"][data-state="info"]').text()).toContain(
            'The audio file cannot be written.',
        )
        expect(input(wrapper, 'title').attributes('disabled')).toBeDefined()
        expect(wrapper.emitted('saved')).toBeUndefined()
        expect(wrapper.emitted('update:open')).toBeUndefined()
        wrapper.unmount()
    })

    it('marks the fields the server rejects', async () => {
        api.updateTrackMetadata.mockRejectedValue({
            code: 'request.invalid',
            responseStatusCode: 400,
            errors: [
                { field: 'title', code: 'max_length', max: 512 },
                { field: 'year', code: 'invalid_format' },
                { field: '', code: 'required' },
            ],
        })
        const wrapper = await mountDialog()

        await input(wrapper, 'title').setValue('Other')
        await submit(wrapper)

        expect(wrapper.get('[data-part="alert"][data-state="error"]').text()).toBe(
            'Please check the marked fields.',
        )
        expect(wrapper.get('[data-field="title"]').attributes('data-state')).toBe('invalid')
        expect(wrapper.get('[data-field="title"] [data-part="field-error"]').text()).toBe(
            'This text is too long (maximum length: 512).',
        )
        expect(wrapper.get('[data-field="year"]').attributes('data-state')).toBe('invalid')
        expect(wrapper.get('[data-field="year"] [data-part="field-error"]').text()).toBe(
            'This value has an invalid format.',
        )
        expect(wrapper.get('[data-field="artist"]').attributes('data-state')).toBeUndefined()
        wrapper.unmount()
    })

    it('reports other errors when saving', async () => {
        api.updateTrackMetadata.mockRejectedValue(new Error('offline'))
        const wrapper = await mountDialog()

        await input(wrapper, 'title').setValue('Other')
        await submit(wrapper)

        expect(wrapper.get('[data-part="alert"][data-state="error"]').text()).toBe(
            'The changes could not be saved.',
        )
        expect(input(wrapper, 'title').attributes('disabled')).toBeUndefined()
        wrapper.unmount()
    })

    it('shows the rule of the song with the inherited one and saves a changed rule after the tags', async () => {
        api.updateTrackMetadata.mockResolvedValue({ ...metadata, title: 'Other' })
        api.setCoverRule.mockResolvedValue({
            rule: 'TrackOnly',
            inheritedPriority: 'TrackFirst',
            effectivePriority: 'TrackOnly',
        })
        const wrapper = await mountDialog()
        const invalidate = vi.spyOn(useCoversStore(), 'invalidate')

        const segments = wrapper.findAll('[data-item="cover-rule"] [data-part="segment"]')
        expect(segments.map((segment) => segment.text())).toEqual([
            'Default',
            'Own first',
            'Album first',
            'Own only',
        ])
        expect(segments[0]?.attributes('aria-checked')).toBe('true')
        expect(api.fetchCoverRulePreview).toHaveBeenCalledWith('track', 'song', 'Inherit', 300)

        await input(wrapper, 'title').setValue('Other')
        await chooseCoverRule(wrapper, 'track-only')
        await saveButton(wrapper).trigger('click')
        await flushPromises()

        expect(api.updateTrackMetadata).toHaveBeenCalledWith('song', { title: 'Other' })
        expect(api.setCoverRule).toHaveBeenCalledWith('track', 'song', 'TrackOnly')
        expect(invalidate).toHaveBeenCalled()
        expect(wrapper.emitted('update:open')?.at(-1)).toEqual([false])
        wrapper.unmount()
    })

    it('saves only the cover rule when the file cannot be written', async () => {
        api.getTrackMetadata.mockResolvedValue({ ...metadata, status: 'NotWritable' })
        api.setCoverRule.mockResolvedValue({
            rule: 'AlbumFirst',
            inheritedPriority: 'TrackFirst',
            effectivePriority: 'AlbumFirst',
        })
        const wrapper = await mountDialog()

        await chooseCoverRule(wrapper, 'album-first')
        await saveButton(wrapper).trigger('click')
        await flushPromises()

        expect(api.updateTrackMetadata).not.toHaveBeenCalled()
        expect(api.setCoverRule).toHaveBeenCalledWith('track', 'song', 'AlbumFirst')
        expect(wrapper.emitted('update:open')?.at(-1)).toEqual([false])
        wrapper.unmount()
    })

    it('stays open with a message when the cover rule cannot be saved', async () => {
        api.setCoverRule.mockRejectedValue(new Error('offline'))
        const wrapper = await mountDialog()

        await chooseCoverRule(wrapper, 'album-first')
        await saveButton(wrapper).trigger('click')
        await flushPromises()

        expect(api.updateTrackMetadata).not.toHaveBeenCalled()
        expect(wrapper.get('[data-item="cover-rule"] [data-part="alert"]').text()).toBe(
            'The cover rule could not be saved.',
        )
        expect(wrapper.emitted('update:open')).toBeUndefined()
        wrapper.unmount()
    })

    it('saves a corrected start of the song with the tags and reports it to the player', async () => {
        const corrected: TrackSongSegments = {
            trackId: 'song',
            audio: {
                ...songSegments.audio,
                manualStartMilliseconds: 1_500,
                effective: { ...wholeFile, manual: true, startMilliseconds: 1_500 },
            },
        }
        api.updateTrackMetadata.mockResolvedValue(metadata)
        api.setTrackSongSegment.mockResolvedValue(corrected)
        const wrapper = await mountDialog()
        expect(wrapper.find('[data-item="song-segments"] [data-kind="audio"]').exists()).toBe(true)

        await wrapper
            .get('[data-item="song-segments"] [data-field="song-start"] input')
            .setValue('1.5')
        await saveButton(wrapper).trigger('click')
        await flushPromises()

        expect(api.setTrackSongSegment).toHaveBeenCalledWith('song', 'audio', 1_500, null)
        expect(wrapper.emitted('songSegmentsSaved')).toEqual([[corrected]])
        expect(wrapper.emitted('update:open')?.at(-1)).toEqual([false])
        wrapper.unmount()
    })

    it('stays open while a song segment holds no valid value', async () => {
        const wrapper = await mountDialog()

        await wrapper
            .get('[data-item="song-segments"] [data-field="song-start"] input')
            .setValue('abc')
        await saveButton(wrapper).trigger('click')
        await flushPromises()

        expect(api.setTrackSongSegment).not.toHaveBeenCalled()
        expect(wrapper.emitted('update:open')).toBeUndefined()
        wrapper.unmount()
    })

    it('offers to load again after an error', async () => {
        api.getTrackMetadata.mockRejectedValueOnce(new Error('offline'))
        const wrapper = await mountDialog()

        expect(wrapper.get('[data-part="alert"]').text()).toBe('The metadata could not be loaded.')
        expect(wrapper.find('form').exists()).toBe(false)
        await wrapper.get('[data-part="dialog-body"] button').trigger('click')
        await flushPromises()

        expect(api.getTrackMetadata).toHaveBeenCalledTimes(2)
        expect(value(wrapper, 'title')).toBe('Song')
        wrapper.unmount()
    })
})
