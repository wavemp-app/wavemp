import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import type { Artist, ArtistAlias } from '@/api/generated/models'
import { createWaveMpI18n } from '@/i18n'

const api = vi.hoisted(() => ({
    artistAliasMaxLength: 512,
    listArtistAliases: vi.fn(),
    addArtistAlias: vi.fn(),
    removeArtistAlias: vi.fn(),
    importArtistAliases: vi.fn(),
}))

vi.mock('@/library/artistAliasApi', () => api)

const { default: ArtistAliasDialog } = await import('../ArtistAliasDialog.vue')

const artist: Artist = { id: 'pink', name: 'P!nk' }

const pink: ArtistAlias = { id: 'a1', name: 'Pink', source: 'MusicBrainz' }
const real: ArtistAlias = { id: 'a2', name: 'Alecia Moore', source: 'Admin' }

function problem(code: string, status = 409) {
    return Object.assign(new Error(code), { code, responseStatusCode: status })
}

async function mountDialog() {
    const wrapper = mount(ArtistAliasDialog, {
        props: { artist, open: false },
        global: { plugins: [createWaveMpI18n('en')] },
        attachTo: document.body,
    })
    await wrapper.setProps({ open: true })
    await flushPromises()
    return wrapper
}

type Wrapper = Awaited<ReturnType<typeof mountDialog>>

function names(wrapper: Wrapper) {
    return wrapper.findAll('[data-part="alias-name"]').map((name) => name.text())
}

async function typeAndAdd(wrapper: Wrapper, name: string) {
    await wrapper.get('[data-field="alias-name"] input').setValue(name)
    await wrapper.get('form[data-item="add-alias"]').trigger('submit')
    await flushPromises()
}

describe('ArtistAliasDialog', () => {
    beforeEach(() => {
        vi.clearAllMocks()
        api.listArtistAliases.mockResolvedValue({ items: [pink, real], total: 2 })
    })

    it('lists the aliases by name with their origin', async () => {
        const wrapper = await mountDialog()

        expect(api.listArtistAliases).toHaveBeenCalledWith('pink')
        expect(names(wrapper)).toEqual(['Alecia Moore', 'Pink'])
        expect(
            wrapper.findAll('[data-part="alias"]').map((alias) => alias.attributes('data-source')),
        ).toEqual(['admin', 'musicbrainz'])
        expect(wrapper.get('[data-part="alias-list"]').attributes('aria-label')).toBe(
            'Aliases of P!nk',
        )
        expect(wrapper.get('[data-part="artist-alias-editor"]').attributes('data-state')).toBe(
            'ready',
        )
        wrapper.unmount()
    })

    it('says so when the artist has no aliases', async () => {
        api.listArtistAliases.mockResolvedValue({ items: [], total: 0 })

        const wrapper = await mountDialog()

        expect(wrapper.find('[data-part="alias-list"]').exists()).toBe(false)
        expect(wrapper.get('[data-item="no-aliases"]').text()).toBe('No aliases yet.')
        wrapper.unmount()
    })

    it('adds a typed alias and empties the field', async () => {
        api.addArtistAlias.mockResolvedValue({ id: 'a3', name: 'Pinky', source: 'Admin' })
        const wrapper = await mountDialog()

        await typeAndAdd(wrapper, '  Pinky ')

        expect(api.addArtistAlias).toHaveBeenCalledWith('pink', 'Pinky')
        expect(names(wrapper)).toEqual(['Alecia Moore', 'Pink', 'Pinky'])
        expect(
            (wrapper.get('[data-field="alias-name"] input').element as HTMLInputElement).value,
        ).toBe('')
        expect(wrapper.get('[data-part="alert"]').text()).toContain('Pinky was added.')
        wrapper.unmount()
    })

    it('asks for a name before it sends an empty one', async () => {
        const wrapper = await mountDialog()

        await typeAndAdd(wrapper, '   ')

        expect(api.addArtistAlias).not.toHaveBeenCalled()
        expect(wrapper.get('[data-part="field-error"]').text()).toBe('Enter a name.')
        wrapper.unmount()
    })

    it('shows a known name at the field', async () => {
        api.addArtistAlias.mockRejectedValue(problem('artist.alias_exists'))
        const wrapper = await mountDialog()

        await typeAndAdd(wrapper, 'PINK')

        expect(wrapper.get('[data-part="field-error"]').text()).toBe(
            'The artist already has this name or alias.',
        )
        expect(names(wrapper)).toEqual(['Alecia Moore', 'Pink'])
        wrapper.unmount()
    })

    it('removes an alias', async () => {
        api.removeArtistAlias.mockResolvedValue(undefined)
        const wrapper = await mountDialog()

        const remove = wrapper.findAll('button[data-item="remove-alias"]')[1]!
        expect(remove.attributes('title')).toBe('Remove Pink')
        await remove.trigger('click')
        await flushPromises()

        expect(api.removeArtistAlias).toHaveBeenCalledWith('pink', 'a1')
        expect(names(wrapper)).toEqual(['Alecia Moore'])
        wrapper.unmount()
    })

    it('loads the list again when an alias is already gone', async () => {
        api.removeArtistAlias.mockRejectedValue(problem('artist.alias_not_found', 404))
        const wrapper = await mountDialog()
        api.listArtistAliases.mockResolvedValue({ items: [real], total: 1 })

        await wrapper.findAll('button[data-item="remove-alias"]')[1]!.trigger('click')
        await flushPromises()

        expect(api.listArtistAliases).toHaveBeenCalledTimes(2)
        expect(names(wrapper)).toEqual(['Alecia Moore'])
        expect(wrapper.get('[data-part="alert"]').text()).toContain('no longer exists')
        wrapper.unmount()
    })

    it('takes the aliases from MusicBrainz', async () => {
        api.importArtistAliases.mockResolvedValue({
            items: [pink, real, { id: 'a4', name: 'P!nk', source: 'MusicBrainz' }],
        })
        const wrapper = await mountDialog()

        await wrapper.get('button[data-item="import-aliases"]').trigger('click')
        await flushPromises()

        expect(api.importArtistAliases).toHaveBeenCalledWith('pink')
        expect(names(wrapper)).toEqual(['Alecia Moore', 'P!nk', 'Pink'])
        expect(wrapper.get('[data-part="alert"]').attributes('data-state')).toBe('success')
        wrapper.unmount()
    })

    it.each([
        ['artist.picture_lookup_disabled', 'Online lookup is off.'],
        ['artist.musicbrainz_artist_missing', 'not linked to MusicBrainz yet'],
        ['artist.picture_lookup_unavailable', 'cannot be reached right now'],
    ])('explains a failed import (%s)', async (code, text) => {
        api.importArtistAliases.mockRejectedValue(problem(code))
        const wrapper = await mountDialog()

        await wrapper.get('button[data-item="import-aliases"]').trigger('click')
        await flushPromises()

        const alert = wrapper.get('[data-part="alert"]')
        expect(alert.attributes('data-state')).toBe('error')
        expect(alert.text()).toContain(text)
        expect(names(wrapper)).toEqual(['Alecia Moore', 'Pink'])
        wrapper.unmount()
    })

    it('shows an error when the aliases cannot be loaded', async () => {
        api.listArtistAliases.mockRejectedValue(new Error('offline'))

        const wrapper = await mountDialog()

        expect(wrapper.get('[data-part="artist-alias-editor"]').attributes('data-state')).toBe(
            'error',
        )
        expect(wrapper.get('[data-part="alert"]').text()).toContain(
            'The aliases could not be loaded.',
        )
        wrapper.unmount()
    })
})
