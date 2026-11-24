import { describe, expect, it, vi } from 'vitest'

const client = vi.hoisted(() => {
    const tracks = vi.fn().mockResolvedValue({ items: [], total: 0 })
    const albums = vi.fn().mockResolvedValue({ items: [], total: 0 })
    const artists = vi.fn().mockResolvedValue({ items: [], total: 0 })
    return {
        tracks,
        albums,
        artists,
        apiClient: {
            api: {
                v1: {
                    tracks: { groups: { get: tracks } },
                    albums: { groups: { get: albums } },
                    artists: { groups: { get: artists } },
                },
            },
        },
    }
})

vi.mock('@/api/client', () => ({ apiClient: client.apiClient }))
import { listAlbumGroups, listArtistGroups, listTrackGroups } from '../api'

describe('group requests', () => {
    it('ask the group endpoints with the query of their list', async () => {
        await listTrackGroups({ offset: 0, limit: 1000, sort: 'Album', myMusic: true })
        await listAlbumGroups({ offset: 1000, limit: 1000, sort: 'Year', direction: 'Desc' })
        await listArtistGroups({ offset: 0, limit: 1000, q: 'a' })

        expect(client.tracks).toHaveBeenCalledWith({
            queryParameters: { offset: 0, limit: 1000, sort: 'Album', myMusic: true },
        })
        expect(client.albums).toHaveBeenCalledWith({
            queryParameters: { offset: 1000, limit: 1000, sort: 'Year', direction: 'Desc' },
        })
        expect(client.artists).toHaveBeenCalledWith({
            queryParameters: { offset: 0, limit: 1000, q: 'a' },
        })
    })
})
