import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

const api = vi.hoisted(() => ({
    fetchAlbumCover: vi.fn(),
    fetchTrackCover: vi.fn(),
}))

vi.mock('@/library/api', () => api)

const playlistApi = vi.hoisted(() => ({ fetchPlaylistCover: vi.fn() }))

vi.mock('@/playlists/api', () => playlistApi)

const avatarApi = vi.hoisted(() => ({ fetchAvatar: vi.fn(), fetchAvatarPreset: vi.fn() }))

vi.mock('@/profiles/avatarApi', () => avatarApi)

const artistApi = vi.hoisted(() => ({
    fetchArtistPicture: vi.fn(),
    fetchUploadedArtistPicture: vi.fn(),
    fetchOnlineArtistPicture: vi.fn(),
    fetchCandidatePreview: vi.fn(),
}))

vi.mock('@/library/artistPictureApi', () => artistApi)

const { artistCandidateId, artistPictureSlotKinds, maxCachedCovers, useCoversStore } =
    await import('../covers')

describe('covers store', () => {
    let created = 0
    const createObjectURL = vi.fn(() => `blob:cover-${++created}`)
    const revokeObjectURL = vi.fn()

    beforeEach(() => {
        setActivePinia(createPinia())
        created = 0
        api.fetchAlbumCover.mockReset().mockResolvedValue(new Uint8Array([1, 2, 3]).buffer)
        api.fetchTrackCover.mockReset().mockResolvedValue(new Uint8Array([4]).buffer)
        playlistApi.fetchPlaylistCover.mockReset().mockResolvedValue(new Uint8Array([5]).buffer)
        avatarApi.fetchAvatar.mockReset().mockResolvedValue(new Uint8Array([6]).buffer)
        avatarApi.fetchAvatarPreset.mockReset().mockResolvedValue(new Uint8Array([7]).buffer)
        for (const fetch of Object.values(artistApi)) {
            fetch.mockReset().mockResolvedValue(new Uint8Array([8]).buffer)
        }
        createObjectURL.mockClear()
        revokeObjectURL.mockClear()
        vi.stubGlobal('URL', { ...URL, createObjectURL, revokeObjectURL })
    })

    afterEach(() => {
        vi.unstubAllGlobals()
    })

    it('loads a cover once for all components that show it', async () => {
        const store = useCoversStore()

        const [first, second] = await Promise.all([
            store.acquire('album', 'a'),
            store.acquire('album', 'a'),
        ])
        const third = await store.acquire('album', 'a')

        expect(first).toBe('blob:cover-1')
        expect(second).toBe(first)
        expect(third).toBe(first)
        expect(api.fetchAlbumCover).toHaveBeenCalledTimes(1)
        expect(api.fetchAlbumCover).toHaveBeenCalledWith('a', 300)
    })

    it('loads every size of a cover once and frees each size on its own', async () => {
        const store = useCoversStore()

        const small = await store.acquire('album', 'a', 96)
        const large = await store.acquire('album', 'a', 1000)
        const smallAgain = await store.acquire('album', 'a', 96)
        store.release('album', 'a', 1000)

        expect(small).toBe('blob:cover-1')
        expect(large).toBe('blob:cover-2')
        expect(smallAgain).toBe(small)
        expect(api.fetchAlbumCover).toHaveBeenCalledTimes(2)
        expect(api.fetchAlbumCover).toHaveBeenNthCalledWith(1, 'a', 96)
        expect(api.fetchAlbumCover).toHaveBeenNthCalledWith(2, 'a', 1000)
        expect(store.size()).toBe(2)
    })

    it('loads every version of the own cover of a playlist on its own', async () => {
        const store = useCoversStore()

        const first = await store.acquire('playlist', 'p', 300, 'v1')
        const again = await store.acquire('playlist', 'p', 300, 'v1')
        const changed = await store.acquire('playlist', 'p', 300, 'v2')
        store.release('playlist', 'p', 300, 'v1')
        store.release('playlist', 'p', 300, 'v1')

        expect(again).toBe(first)
        expect(changed).not.toBe(first)
        expect(playlistApi.fetchPlaylistCover).toHaveBeenCalledTimes(2)
        expect(playlistApi.fetchPlaylistCover).toHaveBeenCalledWith('p', 300)
        expect(api.fetchAlbumCover).not.toHaveBeenCalled()
        expect(api.fetchTrackCover).not.toHaveBeenCalled()
    })

    it('loads avatars and built-in avatars through their endpoints', async () => {
        const store = useCoversStore()

        expect(await store.acquire('avatar', 'u', 96, 'v1')).toBe('blob:cover-1')
        expect(await store.acquire('avatar', 'u', 96, 'v2')).toBe('blob:cover-2')
        expect(await store.acquire('avatar-preset', 'leaf', 300)).toBe('blob:cover-3')

        expect(avatarApi.fetchAvatar).toHaveBeenCalledTimes(2)
        expect(avatarApi.fetchAvatar).toHaveBeenCalledWith('u', 96)
        expect(avatarApi.fetchAvatarPreset).toHaveBeenCalledWith('leaf', 300)
        expect(api.fetchAlbumCover).not.toHaveBeenCalled()
    })

    it('loads the pictures of artists and the previews of candidates through their endpoints', async () => {
        const store = useCoversStore()

        expect(await store.acquire('artist', 'a', 96, 'u-1')).toBe('blob:cover-1')
        expect(await store.acquire('artist', 'a', 96, 'u-2')).toBe('blob:cover-2')
        expect(await store.acquire('artist-uploaded', 'a', 300)).toBe('blob:cover-3')
        expect(await store.acquire('artist-online', 'a', 1000)).toBe('blob:cover-4')
        expect(await store.acquire('artist-candidate', artistCandidateId('a', 'c1'), 300)).toBe(
            'blob:cover-5',
        )

        expect(artistApi.fetchArtistPicture).toHaveBeenCalledTimes(2)
        expect(artistApi.fetchArtistPicture).toHaveBeenCalledWith('a', 96)
        expect(artistApi.fetchUploadedArtistPicture).toHaveBeenCalledWith('a', 300)
        expect(artistApi.fetchOnlineArtistPicture).toHaveBeenCalledWith('a', 1000)
        expect(artistApi.fetchCandidatePreview).toHaveBeenCalledWith('a', 'c1', 300)
        expect(api.fetchAlbumCover).not.toHaveBeenCalled()
    })

    it('loads the uploaded and online pictures of artists again after a change', async () => {
        const store = useCoversStore()
        await store.acquire('artist-uploaded', 'a', 300, store.versionOf('artist-uploaded'))
        store.release('artist-uploaded', 'a', 300, store.versionOf('artist-uploaded'))

        store.invalidate(artistPictureSlotKinds)
        await store.acquire('artist-uploaded', 'a', 300, store.versionOf('artist-uploaded'))

        expect(artistApi.fetchUploadedArtistPicture).toHaveBeenCalledTimes(2)
        expect(store.versionOf('album')).toBeUndefined()
    })

    it('loads covers of tracks through their endpoint', async () => {
        const store = useCoversStore()

        expect(await store.acquire('track', 't')).toBe('blob:cover-1')
        expect(api.fetchTrackCover).toHaveBeenCalledWith('t', 300)
        expect(api.fetchAlbumCover).not.toHaveBeenCalled()
    })

    it('remembers a missing cover but loads again after other errors', async () => {
        api.fetchAlbumCover
            .mockRejectedValueOnce({ responseStatusCode: 404, code: 'cover.not_found' })
            .mockRejectedValueOnce(new Error('offline'))
            .mockResolvedValueOnce(new Uint8Array([9]).buffer)
        const store = useCoversStore()

        expect(await store.acquire('album', 'missing')).toBeNull()
        expect(await store.acquire('album', 'missing')).toBeNull()
        expect(await store.acquire('album', 'offline')).toBeNull()
        expect(await store.acquire('album', 'offline')).toBe('blob:cover-1')
        expect(api.fetchAlbumCover).toHaveBeenCalledTimes(3)
    })

    it('forgets a cover that failed to load once it is not shown', async () => {
        let fail: (reason: unknown) => void = () => {}
        api.fetchAlbumCover
            .mockRejectedValueOnce(new Error('offline'))
            .mockReturnValueOnce(new Promise((_, reject) => (fail = reject)))
        const store = useCoversStore()

        expect(await store.acquire('album', 'shown')).toBeNull()
        expect(store.size()).toBe(1)
        store.release('album', 'shown')
        expect(store.size()).toBe(0)

        const released = store.acquire('album', 'released')
        store.release('album', 'released')
        expect(store.size()).toBe(1)
        fail(new Error('offline'))
        expect(await released).toBeNull()
        expect(store.size()).toBe(0)
    })

    it('frees the least recently used covers that are not shown', async () => {
        const store = useCoversStore()
        const shown = await store.acquire('album', 'shown')

        for (let index = 0; index <= maxCachedCovers; index++) {
            await store.acquire('track', `t${index}`)
            store.release('track', `t${index}`)
        }

        expect(store.size()).toBe(maxCachedCovers + 1)
        expect(revokeObjectURL).toHaveBeenCalledTimes(1)
        expect(revokeObjectURL).toHaveBeenCalledWith('blob:cover-2')
        expect(revokeObjectURL).not.toHaveBeenCalledWith(shown)
    })

    it('keeps the versions of covers until a cover rule changes', () => {
        const store = useCoversStore()

        expect(store.versionOf('album')).toBeUndefined()
        expect(store.versionOf('playlist', 'v1')).toBe('v1')

        store.invalidate()

        expect(store.revisionOf('album')).toBe(1)
        expect(store.revisionOf('track')).toBe(1)
        expect(store.revisionOf('playlist')).toBe(0)
        expect(store.versionOf('album')).toBe('r1')
        expect(store.versionOf('track', 'v2')).toBe('v2.r1')
        expect(store.versionOf('playlist', 'v1')).toBe('v1')
    })

    it('frees the covers of albums and tracks after a cover rule changed', async () => {
        const store = useCoversStore()
        const idle = await store.acquire('album', 'idle')
        store.release('album', 'idle')
        const shown = await store.acquire('track', 'shown')
        const playlist = await store.acquire('playlist', 'p')
        store.release('playlist', 'p')

        store.invalidate()

        expect(revokeObjectURL).toHaveBeenCalledWith(idle)
        expect(revokeObjectURL).not.toHaveBeenCalledWith(shown)
        expect(revokeObjectURL).not.toHaveBeenCalledWith(playlist)
        expect(store.size()).toBe(2)

        const reloaded = await store.acquire('track', 'shown', 300, store.versionOf('track'))
        expect(reloaded).not.toBe(shown)
        expect(api.fetchTrackCover).toHaveBeenCalledTimes(2)

        store.release('track', 'shown')
        expect(revokeObjectURL).toHaveBeenCalledWith(shown)
        expect(revokeObjectURL).not.toHaveBeenCalledWith(reloaded)
    })

    it('frees a replaced cover that was loading once its load ends', async () => {
        const store = useCoversStore()
        const pending = store.acquire('album', 'a')
        store.release('album', 'a')

        store.invalidate(['album'])
        const url = await pending

        expect(revokeObjectURL).toHaveBeenCalledWith(url)
        expect(store.size()).toBe(0)
        expect(store.revisionOf('track')).toBe(0)
    })
})
