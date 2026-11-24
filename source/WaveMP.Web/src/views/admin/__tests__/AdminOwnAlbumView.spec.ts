import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import { createWaveMpI18n } from '@/i18n'
import { createRoutes } from '@/router/routes'
import { useOrganizeStore } from '@/stores/organize'

const api = vi.hoisted(() => ({
    getOwnAlbum: vi.fn(),
    createOwnAlbum: vi.fn(),
    updateOwnAlbum: vi.fn(),
    deleteOwnAlbum: vi.fn(),
    listOwnAlbums: vi.fn(),
    listOwnAlbumTracks: vi.fn(),
    setOwnAlbumTracks: vi.fn(),
    addOwnAlbumTracks: vi.fn(),
    fetchOwnAlbumCover: vi.fn(),
    uploadOwnAlbumCover: vi.fn(),
    deleteOwnAlbumCover: vi.fn(),
    getTrack: vi.fn(),
    getOrganizeAvailability: vi.fn(),
    createOrganizePreview: vi.fn(),
}))

vi.mock('@/admin/ownAlbumApi', () => ({
    getOwnAlbum: api.getOwnAlbum,
    createOwnAlbum: api.createOwnAlbum,
    updateOwnAlbum: api.updateOwnAlbum,
    deleteOwnAlbum: api.deleteOwnAlbum,
    listOwnAlbums: api.listOwnAlbums,
    listOwnAlbumTracks: api.listOwnAlbumTracks,
    setOwnAlbumTracks: api.setOwnAlbumTracks,
    addOwnAlbumTracks: api.addOwnAlbumTracks,
    fetchOwnAlbumCover: api.fetchOwnAlbumCover,
    uploadOwnAlbumCover: api.uploadOwnAlbumCover,
    deleteOwnAlbumCover: api.deleteOwnAlbumCover,
    maxOwnAlbumTracks: 1000,
}))

vi.mock('@/admin/organizeApi', () => ({
    getOrganizeAvailability: api.getOrganizeAvailability,
    createOrganizePreview: api.createOrganizePreview,
    getOrganizeOperation: vi.fn(),
    listOrganizeOperations: vi.fn().mockResolvedValue({ items: [], total: 0 }),
    listOrganizeItems: vi.fn().mockResolvedValue({ items: [], total: 0 }),
    createOrganizeUndoPreview: vi.fn(),
    applyOrganizeOperation: vi.fn(),
    cancelOrganizeOperation: vi.fn(),
    discardOrganizeOperation: vi.fn(),
}))

vi.mock('@/library/api', async (original) => ({
    ...(await original<typeof import('@/library/api')>()),
    getTrack: api.getTrack,
}))

const { default: AdminOwnAlbumView } = await import('../AdminOwnAlbumView.vue')

async function mountAt(path: string) {
    const router = createRouter({ history: createMemoryHistory(), routes: createRoutes() })
    await router.push(path)
    const id = router.currentRoute.value.params.id
    const wrapper = mount(AdminOwnAlbumView, {
        props: typeof id === 'string' ? { id } : {},
        attachTo: document.body,
        global: { plugins: [router, createWaveMpI18n('en')] },
    })
    await flushPromises()
    return { wrapper, router }
}

describe('AdminOwnAlbumView', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
        for (const mock of Object.values(api)) {
            mock.mockReset()
        }
        api.getOrganizeAvailability.mockResolvedValue({
            sources: [{ sourceId: 's', status: 'Available' }],
        })
        api.listOwnAlbums.mockResolvedValue({ items: [], total: 0 })
        document.body.innerHTML = ''
    })

    it('creates an album with the songs chosen in a menu, in their order', async () => {
        useOrganizeStore().pendingTrackIds = ['b', 'a', 'video']
        api.getTrack.mockImplementation(async (id: string) => ({
            id,
            title: `Song ${id}`,
            hasAudio: id !== 'video',
        }))
        api.createOwnAlbum.mockResolvedValue({ id: 'new', title: 'Mix', albumArtist: 'Various' })
        api.setOwnAlbumTracks.mockResolvedValue({
            id: 'new',
            title: 'Mix',
            albumArtist: 'Various',
            trackCount: 2,
        })
        api.getOwnAlbum.mockResolvedValue({ id: 'new', title: 'Mix', albumArtist: 'Various' })
        api.listOwnAlbumTracks.mockResolvedValue({ items: [], total: 0 })
        const { wrapper, router } = await mountAt('/admin/organize/albums/new')

        expect(wrapper.get('[data-view="admin-own-album"]').attributes('data-state')).toBe('create')
        const rows = wrapper.findAll('[data-part="own-album-track"]')
        expect(rows.map((row) => row.get('[data-field="title"]').text())).toEqual([
            'Song b',
            'Song a',
        ])

        await rows[1]!.trigger('keydown', { key: 'ArrowUp', altKey: true })
        await wrapper.get('[data-field="title"] input').setValue('Mix')
        await wrapper.get('[data-field="album-artist"] input').setValue('Various')
        await wrapper.get('form[data-item="own-album"]').trigger('submit')
        await flushPromises()

        expect(api.createOwnAlbum).toHaveBeenCalledWith({ title: 'Mix', albumArtist: 'Various' })
        expect(api.setOwnAlbumTracks).toHaveBeenCalledWith('new', ['a', 'b'])
        expect(router.currentRoute.value.name).toBe('admin-own-album')
        expect(router.currentRoute.value.params.id).toBe('new')
        wrapper.unmount()
    })

    it('asks for the title and the album artist before saving', async () => {
        const { wrapper } = await mountAt('/admin/organize/albums/new')

        await wrapper.get('form[data-item="own-album"]').trigger('submit')
        await flushPromises()

        expect(api.createOwnAlbum).not.toHaveBeenCalled()
        expect(wrapper.findAll('[data-part="field-error"]')).toHaveLength(2)
        wrapper.unmount()
    })

    it('saves only what changed of an existing album', async () => {
        api.getOwnAlbum.mockResolvedValue({
            id: 'x',
            title: 'Mix',
            albumArtist: 'Various',
            year: 2020,
            status: 'Applied',
        })
        api.listOwnAlbumTracks.mockResolvedValue({
            items: [
                { position: 2, state: 'Member', track: { id: 'b', title: 'B', hasAudio: true } },
                { position: 1, state: 'Member', track: { id: 'a', title: 'A', hasAudio: true } },
                { position: 3, state: 'Removed', track: { id: 'c', title: 'C', hasAudio: true } },
            ],
            total: 3,
        })
        api.updateOwnAlbum.mockResolvedValue({ id: 'x', title: 'Mix', albumArtist: 'Various' })
        const { wrapper } = await mountAt('/admin/organize/albums/x')

        expect(wrapper.get('[data-view="admin-own-album"]').attributes('data-state')).toBe('edit')
        expect(
            wrapper
                .findAll('[data-part="own-album-track"] [data-field="title"]')
                .map((title) => title.text()),
        ).toEqual(['A', 'B'])

        await wrapper.get('[data-field="year"] input').setValue('')
        await wrapper.get('form[data-item="own-album"]').trigger('submit')
        await flushPromises()

        expect(api.updateOwnAlbum).toHaveBeenCalledWith('x', { year: 0 })
        expect(api.setOwnAlbumTracks).not.toHaveBeenCalled()
        wrapper.unmount()
    })
})
