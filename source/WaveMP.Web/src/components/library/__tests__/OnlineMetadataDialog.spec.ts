import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import type { OnlineAlbumMatches, OnlineTrackMatches } from '@/api/generated/models'
import { createWaveMpI18n } from '@/i18n'

const api = vi.hoisted(() => ({
    searchTrackOnline: vi.fn(),
    searchAlbumOnline: vi.fn(),
    fetchReleaseCover: vi.fn(),
    applyTrackOnline: vi.fn(),
    applyAlbumOnline: vi.fn(),
}))

vi.mock('@/library/onlineMetadata', async (importOriginal) => ({
    ...(await importOriginal<typeof import('@/library/onlineMetadata')>()),
    ...api,
}))

const OnlineMetadataDialog = (await import('../OnlineMetadataDialog.vue')).default
const { useCoversStore } = await import('@/stores/covers')

const trackMatches: OnlineTrackMatches = {
    current: {
        trackId: 'song',
        status: 'Editable',
        title: 'Song',
        artist: 'Band',
        album: null,
        albumArtist: null,
        year: 1999,
        trackNumber: null,
        discNumber: null,
        genre: null,
    },
    matches: [
        {
            recordingId: 'rec-1',
            release: {
                id: 'rel-1',
                date: '2001-05-01',
                country: 'DE',
                format: 'CD',
                trackCount: 12,
            },
            agreement: 92,
            title: 'Song',
            artist: 'Band',
            album: 'Album',
            albumArtist: 'Band',
            year: 2001,
            trackNumber: 3,
            discNumber: 1,
            genre: null,
        },
        {
            recordingId: 'rec-2',
            release: { id: 'rel-2', date: '2010', trackCount: 20 },
            agreement: 61,
            title: 'Song',
            artist: 'Band',
            album: 'Best Of',
            albumArtist: 'Various Artists',
            year: 2010,
            trackNumber: 7,
            discNumber: null,
            genre: null,
        },
    ],
}

function problem(code: string): Error {
    return Object.assign(new Error(code), { code })
}

async function mountDialog(props: { owner?: 'track' | 'album'; hasCover?: boolean } = {}) {
    const wrapper = mount(OnlineMetadataDialog, {
        props: {
            owner: props.owner ?? 'track',
            id: 'song',
            hasCover: props.hasCover ?? false,
            open: false,
        },
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

function row(wrapper: Wrapper, field: string) {
    return wrapper.get(`[data-part="online-compare-row"][data-field="${field}"]`)
}

function checkbox(wrapper: Wrapper, field: string) {
    return row(wrapper, field).get('input[type="checkbox"]')
}

function applyButton(wrapper: Wrapper) {
    return wrapper.get('[data-part="dialog-footer"] [data-item="apply"]')
}

describe('OnlineMetadataDialog', () => {
    let created = 0
    const createObjectURL = vi.fn(() => `blob:cover-${++created}`)
    const revokeObjectURL = vi.fn()

    beforeEach(() => {
        setActivePinia(createPinia())
        created = 0
        createObjectURL.mockClear()
        revokeObjectURL.mockClear()
        vi.stubGlobal('URL', { ...URL, createObjectURL, revokeObjectURL })
        for (const mock of Object.values(api)) {
            mock.mockReset()
        }
        api.searchTrackOnline.mockResolvedValue(trackMatches)
        api.fetchReleaseCover.mockResolvedValue(new Uint8Array([1, 2, 3]).buffer)
    })

    it('shows the matches, the best selected, with their agreement and release', async () => {
        const wrapper = await mountDialog()

        expect(api.searchTrackOnline).toHaveBeenCalledWith('song')
        expect(search(wrapper).attributes('data-state')).toBe('ready')
        const matches = wrapper.findAll('[data-part="online-match"]')
        expect(matches).toHaveLength(2)
        expect(matches[0]!.attributes('aria-checked')).toBe('true')
        expect(matches[0]!.attributes('data-state')).toBe('selected')
        expect(matches[0]!.get('[data-part="online-match-album"]').text()).toBe('Album')
        expect(matches[0]!.get('[data-part="online-match-release"]').text()).toContain('DE')
        expect(matches[0]!.get('[data-part="online-match-release"]').text()).toContain('Songs: 12')
        expect(matches[0]!.get('[data-part="meter"]').attributes('aria-valuetext')).toBe(
            '92 % agreement',
        )
        expect(matches[1]!.attributes('aria-checked')).toBe('false')
        expect(api.fetchReleaseCover).toHaveBeenCalledWith('rel-1')
        wrapper.unmount()
    })

    it('compares the fields and chooses only the missing ones', async () => {
        const wrapper = await mountDialog()

        expect(row(wrapper, 'title').attributes('data-state')).toBe('same')
        expect(row(wrapper, 'album').attributes('data-state')).toBe('missing')
        expect(row(wrapper, 'year').attributes('data-state')).toBe('different')
        expect(row(wrapper, 'genre').attributes('data-state')).toBe('unknown')
        expect(row(wrapper, 'album').get('[data-part="online-compare-current"]').text()).toBe(
            'empty',
        )
        expect(row(wrapper, 'year').get('[data-part="online-compare-suggested"]').text()).toBe(
            '2001',
        )
        expect((checkbox(wrapper, 'album').element as HTMLInputElement).checked).toBe(true)
        expect((checkbox(wrapper, 'year').element as HTMLInputElement).checked).toBe(false)
        expect(checkbox(wrapper, 'title').attributes('disabled')).toBeDefined()
        expect(checkbox(wrapper, 'genre').attributes('disabled')).toBeDefined()
        expect(row(wrapper, 'cover').attributes('data-state')).toBe('ready')
        expect((checkbox(wrapper, 'cover').element as HTMLInputElement).checked).toBe(true)
        wrapper.unmount()
    })

    it('applies the chosen fields and the cover', async () => {
        api.applyTrackOnline.mockResolvedValue({ cover: 'MediaFolder' })
        const wrapper = await mountDialog()
        const invalidate = vi.spyOn(useCoversStore(), 'invalidate')

        await checkbox(wrapper, 'year').setValue(true)
        await applyButton(wrapper).trigger('click')
        await flushPromises()

        expect(api.applyTrackOnline).toHaveBeenCalledWith(
            'song',
            {
                album: 'Album',
                albumArtist: 'Band',
                year: 2001,
                trackNumber: 3,
                discNumber: 1,
            },
            'rel-1',
        )
        expect(search(wrapper).attributes('data-state')).toBe('applied')
        expect(wrapper.findAll('[data-part="alert"]').map((alert) => alert.text())).toEqual([
            'The tags were written.',
            'The cover was saved in the music folder.',
        ])
        expect(invalidate).toHaveBeenCalledTimes(1)
        expect(wrapper.emitted('applied')?.[0]?.[0]).toEqual({ albumId: null, cover: true })
        wrapper.unmount()
    })

    it('keeps the cover of a song that has one unless chosen', async () => {
        api.applyTrackOnline.mockResolvedValue({ cover: 'None' })
        const wrapper = await mountDialog({ hasCover: true })

        expect((checkbox(wrapper, 'cover').element as HTMLInputElement).checked).toBe(false)
        await applyButton(wrapper).trigger('click')
        await flushPromises()

        expect(api.applyTrackOnline.mock.calls[0]![2]).toBeNull()
        wrapper.unmount()
    })

    it('loads the covers again after writing tags, since the song may belong to another album', async () => {
        api.applyTrackOnline.mockResolvedValue({ cover: 'None' })
        const wrapper = await mountDialog({ hasCover: true })
        const invalidate = vi.spyOn(useCoversStore(), 'invalidate')

        await checkbox(wrapper, 'year').setValue(true)
        await applyButton(wrapper).trigger('click')
        await flushPromises()

        expect(api.applyTrackOnline.mock.calls[0]![1]).not.toBeNull()
        expect(api.applyTrackOnline.mock.calls[0]![2]).toBeNull()
        expect(invalidate).toHaveBeenCalledTimes(1)
        wrapper.unmount()
    })

    it('loads the fields and the cover of another match', async () => {
        const wrapper = await mountDialog()

        await wrapper.findAll('[data-part="online-match"]')[1]!.trigger('click')
        await flushPromises()

        expect(api.fetchReleaseCover).toHaveBeenLastCalledWith('rel-2')
        expect(revokeObjectURL).toHaveBeenCalledWith('blob:cover-1')
        expect(row(wrapper, 'album').get('[data-part="online-compare-suggested"]').text()).toBe(
            'Best Of',
        )
        wrapper.unmount()
    })

    it('says when the release has no cover', async () => {
        api.fetchReleaseCover.mockRejectedValue(problem('lookup.cover_not_found'))
        const wrapper = await mountDialog()

        expect(row(wrapper, 'cover').attributes('data-state')).toBe('missing')
        expect(row(wrapper, 'cover').text()).toContain('No cover in the Cover Art Archive')
        expect(checkbox(wrapper, 'cover').attributes('disabled')).toBeDefined()
        wrapper.unmount()
    })

    it('can apply nothing until something is chosen', async () => {
        api.fetchReleaseCover.mockResolvedValue(undefined)
        const wrapper = await mountDialog({ hasCover: true })

        await checkbox(wrapper, 'album').setValue(false)
        await checkbox(wrapper, 'album-artist').setValue(false)
        await checkbox(wrapper, 'number').setValue(false)
        await checkbox(wrapper, 'disc').setValue(false)

        expect(applyButton(wrapper).attributes('disabled')).toBeDefined()
        expect(wrapper.get('[data-part="form-note"]').text()).toBe(
            'Choose the fields or the cover you want to apply.',
        )
        wrapper.unmount()
    })

    it('only offers the cover for a song that cannot be written', async () => {
        api.searchTrackOnline.mockResolvedValue({
            ...trackMatches,
            current: { ...trackMatches.current, status: 'NotWritable' },
        })
        const wrapper = await mountDialog()

        expect(checkbox(wrapper, 'album').attributes('disabled')).toBeDefined()
        expect(wrapper.get('[data-item="compare"] [data-part="alert"]').text()).toContain(
            'The cover can still be applied.',
        )
        expect(applyButton(wrapper).attributes('disabled')).toBeUndefined()
        wrapper.unmount()
    })

    it('points to the settings while the search is off', async () => {
        api.searchTrackOnline.mockRejectedValue(problem('lookup.disabled'))
        const wrapper = await mountDialog()

        expect(search(wrapper).attributes('data-state')).toBe('disabled')
        expect(wrapper.text()).toContain('The online search is off.')
        expect(wrapper.get('[data-item="online-settings"]').text()).toBe(
            'Open the library settings',
        )
        wrapper.unmount()
    })

    it('offers to search again without internet', async () => {
        api.searchTrackOnline.mockRejectedValueOnce(problem('lookup.unavailable'))
        const wrapper = await mountDialog()

        expect(search(wrapper).attributes('data-state')).toBe('unavailable')
        expect(wrapper.text()).toContain('MusicBrainz cannot be reached.')
        await wrapper.get('[data-item="search-again"]').trigger('click')
        await flushPromises()

        expect(api.searchTrackOnline).toHaveBeenCalledTimes(2)
        expect(search(wrapper).attributes('data-state')).toBe('ready')
        wrapper.unmount()
    })

    it('says when there is no match', async () => {
        api.searchTrackOnline.mockResolvedValue({ ...trackMatches, matches: [] })
        const wrapper = await mountDialog()

        expect(wrapper.text()).toContain('MusicBrainz knows no match.')
        expect(applyButton(wrapper).attributes('disabled')).toBeDefined()
        wrapper.unmount()
    })

    it('keeps the choice and names the problem when applying fails', async () => {
        api.applyTrackOnline.mockRejectedValue(problem('lookup.unavailable'))
        const wrapper = await mountDialog()

        await applyButton(wrapper).trigger('click')
        await flushPromises()

        expect(search(wrapper).attributes('data-state')).toBe('ready')
        expect(wrapper.get('[data-part="alert"][data-state="error"]').text()).toBe(
            'The Cover Art Archive cannot be reached. Nothing was changed; try again later.',
        )
        expect((checkbox(wrapper, 'album').element as HTMLInputElement).checked).toBe(true)
        wrapper.unmount()
    })

    describe('album', () => {
        const albumMatches: OnlineAlbumMatches = {
            current: { album: 'Album', albumArtist: null, year: null, genre: 'Rock' },
            trackCount: 10,
            editableTrackCount: 8,
            matches: [
                {
                    release: { id: 'rel-9', date: '2003', trackCount: 10 },
                    agreement: 80,
                    values: { album: 'Album', albumArtist: 'Band', year: 2003, genre: null },
                },
            ],
        }

        it('compares only the album fields and names the songs it cannot write', async () => {
            api.searchAlbumOnline.mockResolvedValue(albumMatches)
            const wrapper = await mountDialog({ owner: 'album' })

            expect(api.searchAlbumOnline).toHaveBeenCalledWith('song')
            expect(
                wrapper
                    .findAll('[data-part="online-compare-row"]')
                    .map((item) => item.attributes('data-field')),
            ).toEqual(['album', 'album-artist', 'year', 'genre', 'cover'])
            expect(wrapper.get('[data-item="compare"] [data-part="alert"]').text()).toBe(
                'The tags are written into 8 of 10 songs; the others cannot be written.',
            )
            wrapper.unmount()
        })

        it('applies the album fields and reports the songs', async () => {
            api.searchAlbumOnline.mockResolvedValue(albumMatches)
            api.applyAlbumOnline.mockResolvedValue({
                albumId: 'album-2',
                tracksWritten: 8,
                tracksSkipped: 2,
                cover: 'DataFolder',
            })
            const wrapper = await mountDialog({ owner: 'album' })

            await applyButton(wrapper).trigger('click')
            await flushPromises()

            expect(api.applyAlbumOnline).toHaveBeenCalledWith(
                'song',
                { albumArtist: 'Band', year: 2003 },
                'rel-9',
            )
            expect(wrapper.findAll('[data-part="alert"]').map((alert) => alert.text())).toEqual([
                'Songs with the new tags: 8',
                'Songs that could not be written: 2',
                'The cover was saved for this album.',
            ])
            expect(wrapper.emitted('applied')?.[0]?.[0]).toEqual({
                albumId: 'album-2',
                cover: true,
            })
            wrapper.unmount()
        })
    })
})
