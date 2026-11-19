import { beforeEach, describe, expect, it, vi } from 'vitest'
import { nextTick } from 'vue'
import { createPinia, setActivePinia } from 'pinia'
import type { SmartPlaylist } from '@/api/generated/models'

const api = vi.hoisted(() => ({
    listSmartPlaylists: vi.fn(),
    getSmartPlaylist: vi.fn(),
    createSmartPlaylist: vi.fn(),
    updateSmartPlaylist: vi.fn(),
    deleteSmartPlaylist: vi.fn(),
    listSmartPlaylistTracks: vi.fn(),
    convertSmartPlaylist: vi.fn(),
    listSmartPlaylistTemplates: vi.fn(),
}))

vi.mock('@/playlists/smartApi', () => api)
vi.mock('@/playlists/api', () => ({ listPlaylists: vi.fn() }))

const { useSmartPlaylistsStore } = await import('../smartPlaylists')
const { usePlaylistsStore } = await import('../playlists')
const { useSessionStore } = await import('../session')

function smart(id: string, name: string): SmartPlaylist {
    return { id, name, trackCount: 0, rules: [] }
}

describe('smart playlists store', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
        for (const mock of Object.values(api)) {
            mock.mockReset()
        }
    })

    it('loads all pages of the smart playlists and sorts them by name', async () => {
        api.listSmartPlaylists
            .mockResolvedValueOnce({ items: [smart('2', 'beta'), smart('1', 'Alpha')], total: 3 })
            .mockResolvedValueOnce({ items: [smart('3', 'Charlie')], total: 3 })
        const store = useSmartPlaylistsStore()

        await store.ensureLoaded()

        expect(store.status).toBe('ready')
        expect(store.smartPlaylists.map((item) => item.name)).toEqual(['Alpha', 'beta', 'Charlie'])
        expect(api.listSmartPlaylists).toHaveBeenNthCalledWith(1, {
            offset: 0,
            limit: 100,
            sort: 'Name',
        })

        await store.ensureLoaded()
        expect(api.listSmartPlaylists).toHaveBeenCalledTimes(2)
    })

    it('reports a failed load', async () => {
        api.listSmartPlaylists.mockRejectedValue(new Error('offline'))
        const store = useSmartPlaylistsStore()

        await store.load()

        expect(store.status).toBe('error')
    })

    it('loads the templates once', async () => {
        api.listSmartPlaylistTemplates.mockResolvedValue({
            items: [{ id: 'Favorites' }],
            total: 1,
        })
        const store = useSmartPlaylistsStore()

        await Promise.all([store.loadTemplates(), store.loadTemplates()])
        const templates = await store.loadTemplates()

        expect(templates).toEqual([{ id: 'Favorites' }])
        expect(api.listSmartPlaylistTemplates).toHaveBeenCalledTimes(1)
    })

    it('adds created and replaces updated smart playlists', async () => {
        api.createSmartPlaylist.mockResolvedValue(smart('1', 'New'))
        api.updateSmartPlaylist.mockResolvedValue(smart('1', 'Renamed'))
        const store = useSmartPlaylistsStore()
        const body = { name: 'New', rules: [] }

        await store.create(body)
        expect(store.find('1')?.name).toBe('New')

        await store.update('1', { ...body, name: 'Renamed' })
        expect(store.smartPlaylists.map((item) => item.name)).toEqual(['Renamed'])
        expect(api.updateSmartPlaylist).toHaveBeenCalledWith('1', { name: 'Renamed', rules: [] })
    })

    it('removes deleted smart playlists', async () => {
        api.deleteSmartPlaylist.mockResolvedValue(undefined)
        const store = useSmartPlaylistsStore()
        store.apply(smart('1', 'Gone'))

        await store.remove('1')

        expect(store.smartPlaylists).toEqual([])
    })

    it('replaces a converted smart playlist with the new playlist', async () => {
        const playlist = { id: 'p1', name: 'Mix', trackCount: 4 }
        api.convertSmartPlaylist.mockResolvedValue(playlist)
        const store = useSmartPlaylistsStore()
        const playlists = usePlaylistsStore()
        store.apply(smart('1', 'Mix'))

        const created = await store.convert('1')

        expect(created).toEqual(playlist)
        expect(store.smartPlaylists).toEqual([])
        expect(playlists.playlists).toEqual([playlist])
    })

    it('keeps the smart playlist when converting fails', async () => {
        api.convertSmartPlaylist.mockRejectedValue(new Error('full'))
        const store = useSmartPlaylistsStore()
        store.apply(smart('1', 'Mix'))

        await expect(store.convert('1')).rejects.toThrow('full')

        expect(store.find('1')).toBeDefined()
    })

    it('forgets the smart playlists of the previous profile', async () => {
        const store = useSmartPlaylistsStore()
        store.apply(smart('1', 'Mine'))
        const session = useSessionStore()

        session.token = 'other-profile'
        await nextTick()

        expect(store.smartPlaylists).toEqual([])
        expect(store.status).toBe('idle')
    })
})
