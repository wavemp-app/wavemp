import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import type { ArtistPicture as ArtistPictureModel } from '@/api/generated/models'
import { createWaveMpI18n } from '@/i18n'
import { createRoutes } from '@/router/routes'

const libraryApi = vi.hoisted(() => ({
    fetchAlbumCover: vi.fn(),
    fetchTrackCover: vi.fn(),
}))
const artistApi = vi.hoisted(() => ({
    fetchArtistPicture: vi.fn(),
    fetchUploadedArtistPicture: vi.fn(),
    fetchOnlineArtistPicture: vi.fn(),
    fetchCandidatePreview: vi.fn(),
}))

vi.mock('@/library/api', () => libraryApi)
vi.mock('@/library/artistPictureApi', () => artistApi)

const ArtistPicture = (await import('../ArtistPicture.vue')).default
const ArtistCredit = (await import('../ArtistCredit.vue')).default
const TileList = (await import('../TileList.vue')).default
const { useLibraryTiles } = await import('@/composables/useLibraryTiles')

async function mountWith(component: unknown, props: Record<string, unknown>) {
    const router = createRouter({ history: createMemoryHistory(), routes: createRoutes() })
    await router.push('/library/artists')
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const wrapper = mount(component as any, {
        props,
        global: { plugins: [router, createWaveMpI18n('en')] },
        attachTo: document.body,
    })
    await flushPromises()
    return wrapper
}

describe('ArtistPicture', () => {
    let created = 0

    beforeEach(() => {
        setActivePinia(createPinia())
        created = 0
        libraryApi.fetchAlbumCover.mockReset().mockResolvedValue(new Uint8Array([1]).buffer)
        artistApi.fetchArtistPicture.mockReset().mockResolvedValue(new Uint8Array([2]).buffer)
        vi.stubGlobal('URL', {
            ...URL,
            createObjectURL: () => `blob:picture-${++created}`,
            revokeObjectURL: vi.fn(),
        })
    })

    afterEach(() => {
        vi.unstubAllGlobals()
        vi.restoreAllMocks()
    })

    it('shows the own picture of an artist in the shape of artist pictures', async () => {
        const wrapper = await mountWith(ArtistPicture, {
            id: 'alpha',
            picture: { kind: 'Uploaded', version: 'u-1', hasUploadedPicture: true },
        })

        const cover = wrapper.get('[data-part="cover"]')
        expect(cover.attributes('data-kind')).toBe('artist')
        expect(cover.get('[data-part="cover-image"]').attributes('src')).toBe('blob:picture-1')
        expect(artistApi.fetchArtistPicture).toHaveBeenCalledWith('alpha', 300)
        expect(wrapper.find('[data-part="artist-collage"]').exists()).toBe(false)
        wrapper.unmount()
    })

    it('loads a new picture for a new version', async () => {
        const wrapper = await mountWith(ArtistPicture, {
            id: 'alpha',
            picture: { kind: 'Local', version: 'l-1' },
        })
        await wrapper.setProps({ picture: { kind: 'Online', version: 'o-2' } })
        await flushPromises()

        expect(artistApi.fetchArtistPicture).toHaveBeenCalledTimes(2)
        expect(wrapper.get('[data-part="cover-image"]').attributes('src')).toBe('blob:picture-2')
        wrapper.unmount()
    })

    it('loads the sharp size for the shown width and the pixel density', async () => {
        vi.spyOn(HTMLElement.prototype, 'offsetWidth', 'get').mockReturnValue(160)
        vi.stubGlobal('devicePixelRatio', 3)

        const wrapper = await mountWith(ArtistPicture, {
            id: 'alpha',
            picture: { kind: 'Local', version: 'l-1' },
        })

        expect(artistApi.fetchArtistPicture).toHaveBeenCalledWith('alpha', 600)
        wrapper.unmount()
    })

    it('loads the covers of a collage tile for its larger side', async () => {
        vi.spyOn(HTMLElement.prototype, 'offsetWidth', 'get').mockReturnValue(80)
        vi.spyOn(HTMLElement.prototype, 'offsetHeight', 'get').mockReturnValue(160)

        const wrapper = await mountWith(ArtistPicture, {
            id: 'alpha',
            picture: { kind: 'Albums', albumIds: ['a', 'b'] },
        })

        expect(libraryApi.fetchAlbumCover).toHaveBeenCalledWith('a', 192)
        expect(libraryApi.fetchAlbumCover).toHaveBeenCalledWith('b', 192)
        wrapper.unmount()
    })

    it.each([1, 2, 3, 4])('lays out a collage of %i album covers', async (count) => {
        const albumIds = ['a', 'b', 'c', 'd'].slice(0, count)
        const wrapper = await mountWith(ArtistPicture, {
            id: 'alpha',
            picture: { kind: 'Albums', albumIds } satisfies ArtistPictureModel,
        })

        const collage = wrapper.get('[data-part="artist-collage"]')
        expect(collage.attributes('data-count')).toBe(String(count))
        expect(collage.attributes('aria-hidden')).toBe('true')
        expect(collage.attributes('style')).toContain(`--wmp-item-count: ${count}`)
        const tiles = collage.findAll('[data-part="artist-collage-tile"]')
        expect(tiles.map((tile) => tile.attributes('data-index'))).toEqual(
            albumIds.map((_, index) => String(index)),
        )
        expect(tiles.every((tile) => tile.find('[data-part="cover-image"]').exists())).toBe(true)
        expect(libraryApi.fetchAlbumCover).toHaveBeenCalledTimes(count)
        expect(artistApi.fetchArtistPicture).not.toHaveBeenCalled()
        wrapper.unmount()
    })

    it('shows the icon without a picture and album covers', async () => {
        for (const picture of [null, { kind: 'None' }]) {
            const wrapper = await mountWith(ArtistPicture, { id: 'alpha', picture })

            const cover = wrapper.get('[data-part="cover"]')
            expect(cover.attributes('data-kind')).toBe('artist')
            expect(cover.get('[data-part="icon"]').attributes('data-icon')).toBe('artist')
            wrapper.unmount()
        }
        expect(artistApi.fetchArtistPicture).not.toHaveBeenCalled()
    })
})

describe('artist tiles', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
        libraryApi.fetchAlbumCover.mockReset().mockResolvedValue(new Uint8Array([1]).buffer)
        artistApi.fetchArtistPicture.mockReset().mockResolvedValue(new Uint8Array([2]).buffer)
        vi.stubGlobal('URL', {
            ...URL,
            createObjectURL: () => 'blob:tile',
            revokeObjectURL: vi.fn(),
        })
    })

    afterEach(() => {
        vi.unstubAllGlobals()
    })

    it('show the picture of the artist or the collage of its album covers', async () => {
        const Host = {
            props: ['artists'],
            setup() {
                return { tiles: useLibraryTiles() }
            },
            components: { TileList },
            template: '<TileList label="Artists" :tiles="artists.map(tiles.artist)" />',
        }
        const wrapper = await mountWith(Host, {
            artists: [
                { id: 'alpha', name: 'Alpha', picture: { kind: 'Local', version: 'l-1' } },
                { id: 'beta', name: 'Beta', picture: { kind: 'Albums', albumIds: ['x', 'y'] } },
            ],
        })

        const [alpha, beta] = wrapper.findAll('[data-part="tile"]')
        expect(alpha!.get('[data-part="cover"]').attributes('data-kind')).toBe('artist')
        expect(alpha!.find('[data-part="cover-image"]').exists()).toBe(true)
        expect(beta!.get('[data-part="artist-collage"]').attributes('data-count')).toBe('2')
        expect(artistApi.fetchArtistPicture).toHaveBeenCalledWith('alpha', 300)
        wrapper.unmount()
    })
})

describe('ArtistCredit', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
    })

    it('names author, license and source with links to https pages', async () => {
        const wrapper = await mountWith(ArtistCredit, {
            credit: {
                provider: 'Wikimedia',
                author: 'Jane Doe',
                license: 'CC BY-SA 4.0',
                licenseUrl: 'https://creativecommons.org/licenses/by-sa/4.0/',
                sourceUrl: 'https://commons.wikimedia.org/wiki/File:Jane.jpg',
            },
        })

        const credit = wrapper.get('[data-part="artist-credit"]')
        expect(credit.element.tagName).toBe('P')
        expect(credit.attributes('data-provider')).toBe('Wikimedia')
        expect(credit.text()).toBe('Photo: Jane Doe, CC BY-SA 4.0, Wikimedia Commons')
        const links = credit.findAll('a[data-part="link"]')
        expect(links.map((link) => link.attributes('href'))).toEqual([
            'https://creativecommons.org/licenses/by-sa/4.0/',
            'https://commons.wikimedia.org/wiki/File:Jane.jpg',
        ])
        for (const link of links) {
            expect(link.attributes('rel')).toBe('noopener noreferrer')
            expect(link.attributes('target')).toBe('_blank')
        }
        wrapper.unmount()
    })

    it('shows texts of the services as text and links nothing that is not https', async () => {
        const wrapper = await mountWith(ArtistCredit, {
            credit: {
                provider: 'Wikimedia',
                author: '<img src=x onerror=alert(1)><script>alert(2)</script>',
                license: '<b>CC BY</b>',
                licenseUrl: 'javascript:alert(3)',
                sourceUrl: 'http://commons.wikimedia.org/wiki/File:A.jpg',
            },
        })

        const credit = wrapper.get('[data-part="artist-credit"]')
        expect(credit.find('script').exists()).toBe(false)
        expect(credit.find('img').exists()).toBe(false)
        expect(credit.find('b').exists()).toBe(false)
        expect(credit.find('a').exists()).toBe(false)
        expect(credit.get('[data-part="artist-credit-author"]').text()).toBe(
            '<img src=x onerror=alert(1)><script>alert(2)</script>',
        )
        expect(credit.get('[data-part="artist-credit-license"]').text()).toBe('<b>CC BY</b>')
        expect(credit.get('[data-part="artist-credit-source"]').text()).toBe('Wikimedia Commons')
        wrapper.unmount()
    })

    it('names only the source of fanart.tv, which names no author or license', async () => {
        const wrapper = await mountWith(ArtistCredit, {
            credit: { provider: 'FanartTv', sourceUrl: 'https://fanart.tv/artist/abc' },
        })

        expect(wrapper.get('[data-part="artist-credit"]').text()).toBe('Photo: fanart.tv')
        expect(wrapper.find('[data-part="artist-credit-author"]').exists()).toBe(false)
        wrapper.unmount()

        const empty = await mountWith(ArtistCredit, { credit: null })
        expect(empty.find('[data-part="artist-credit"]').exists()).toBe(false)
        empty.unmount()
    })
})
