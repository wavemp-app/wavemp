import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import type { Artist } from '@/api/generated/models'
import { createWaveMpI18n } from '@/i18n'
import { createRoutes } from '@/router/routes'

const api = vi.hoisted(() => ({
    fetchArtistPicture: vi.fn(),
    fetchUploadedArtistPicture: vi.fn(),
    fetchOnlineArtistPicture: vi.fn(),
    fetchCandidatePreview: vi.fn(),
    uploadArtistPicture: vi.fn(),
    removeUploadedArtistPicture: vi.fn(),
    removeOnlineArtistPicture: vi.fn(),
    listMusicBrainzMatches: vi.fn(),
    listPictureCandidates: vi.fn(),
    takeOverPicture: vi.fn(),
}))

vi.mock('@/library/artistPictureApi', () => api)

const image = vi.hoisted(() => ({
    loadPicture: vi.fn(),
    releasePicture: vi.fn(),
    drawCrop: vi.fn(),
}))

vi.mock('@/profiles/avatarImage', () => image)

const covers = vi.hoisted(() => ({
    acquire: vi.fn(() => Promise.resolve('blob:cover')),
    release: vi.fn(),
    versionOf: vi.fn((_kind: string, version?: string) => version),
    invalidate: vi.fn(),
}))

vi.mock('@/stores/covers', () => ({
    useCoversStore: () => covers,
    artistPictureSlotKinds: ['artist-uploaded', 'artist-online'],
    artistCandidateId: (artistId: string, candidateId: string) => `${artistId}/${candidateId}`,
}))

const { default: ArtistPictureDialog } = await import('../ArtistPictureDialog.vue')

const albumsOnly: Artist = {
    id: 'alpha',
    name: 'Alpha Band',
    picture: {
        kind: 'Albums',
        albumIds: ['a1'],
        hasLocalPicture: false,
        hasUploadedPicture: false,
        hasOnlinePicture: false,
    },
}

const chosen = { url: 'blob:chosen', image: {}, width: 800, height: 400 }
const preview = { url: 'blob:preview', image: {}, width: 500, height: 250 }

const matches = [
    {
        musicBrainzArtistId: 'mb-1',
        name: 'Alpha Band',
        disambiguation: 'rock band',
        type: 'Group',
        country: 'GB',
        lifeSpanBegin: '1991-03-01',
        lifeSpanEnd: '2004',
        score: 100,
        isRemembered: false,
    },
    { musicBrainzArtistId: 'mb-2', name: 'Alpha Band', type: 'Person', score: 80 },
]

const candidates = [
    {
        id: 'c1',
        provider: 'Wikimedia' as const,
        author: '<script>alert(1)</script>',
        license: 'CC BY-SA 4.0',
        licenseUrl: 'https://creativecommons.org/licenses/by-sa/4.0/',
        sourceUrl: 'https://commons.wikimedia.org/wiki/File:Alpha.jpg',
        width: 1280,
        height: 640,
    },
    { id: 'c2', provider: 'FanartTv' as const, sourceUrl: 'https://fanart.tv/artist/mb-1' },
]

async function mountDialog(artist: Artist = albumsOnly) {
    const router = createRouter({ history: createMemoryHistory(), routes: createRoutes() })
    await router.push('/library/artists/alpha')
    const wrapper = mount(ArtistPictureDialog, {
        props: { artist, open: false },
        global: { plugins: [router, createWaveMpI18n('en')] },
        attachTo: document.body,
    })
    await wrapper.setProps({ open: true })
    await flushPromises()
    return wrapper
}

type Wrapper = Awaited<ReturnType<typeof mountDialog>>

function button(wrapper: Wrapper, item: string) {
    return wrapper.get(`button[data-item="${item}"]`)
}

async function chooseFile(wrapper: Wrapper, file: File) {
    const input = wrapper.get('input[type="file"]')
    Object.defineProperty(input.element, 'files', { value: [file], configurable: true })
    await input.trigger('change')
    await flushPromises()
}

async function online(wrapper: Wrapper) {
    await wrapper.get('[data-item="artist-picture-mode"] [data-item="online"]').trigger('click')
    await flushPromises()
}

describe('ArtistPictureDialog', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
        for (const fn of Object.values(api)) {
            fn.mockReset()
        }
        api.uploadArtistPicture.mockResolvedValue({ ...albumsOnly, name: 'uploaded' })
        api.removeUploadedArtistPicture.mockResolvedValue(undefined)
        api.removeOnlineArtistPicture.mockResolvedValue(undefined)
        api.listMusicBrainzMatches.mockResolvedValue({ items: matches, total: 2 })
        api.listPictureCandidates.mockResolvedValue({ items: candidates, total: 2 })
        api.fetchCandidatePreview.mockResolvedValue(new Uint8Array([1, 2]).buffer)
        api.takeOverPicture.mockResolvedValue({ ...albumsOnly, name: 'taken' })
        image.loadPicture
            .mockReset()
            .mockImplementation(async (blob: Blob) =>
                blob.type === 'image/webp' ? preview : chosen,
            )
        image.releasePicture.mockReset()
        image.drawCrop.mockReset().mockResolvedValue(new Blob(['png'], { type: 'image/png' }))
        covers.invalidate.mockClear()
        document.body.innerHTML = ''
    })

    it('lists the sources in their order with the one viewers see', async () => {
        const wrapper = await mountDialog({
            ...albumsOnly,
            picture: {
                kind: 'Local',
                version: 'l-1',
                hasLocalPicture: true,
                hasUploadedPicture: true,
                hasOnlinePicture: false,
            },
        })

        expect(wrapper.get('[data-part="dialog"]').attributes('data-item')).toBe('artist-picture')
        const list = wrapper.get('[data-part="picture-sources"]')
        expect(list.element.tagName).toBe('OL')
        const rows = list.findAll('[data-part="picture-source"]')
        expect(
            rows.map((row) => [row.attributes('data-source'), row.attributes('data-state')]),
        ).toEqual([
            ['local', 'active'],
            ['uploaded', 'available'],
            ['online', 'missing'],
            ['albums', 'available'],
        ])
        expect(rows[0]!.attributes('aria-current')).toBe('true')
        expect(rows[1]!.get('[data-part="picture-source-name"]').text()).toBe('Uploaded picture')
        expect(rows[1]!.get('[data-part="picture-source-state"]').text()).toBe(
            'Stored, hidden by the picture in the artist folder',
        )
        expect(rows[2]!.get('[data-part="picture-source-state"]').text()).toBe('None taken over')
        wrapper.unmount()
    })

    it('offers upload and online as one segmented control', async () => {
        const wrapper = await mountDialog()

        const group = wrapper.get(
            '[data-part="segmented-control"][data-item="artist-picture-mode"]',
        )
        expect(group.attributes('role')).toBe('radiogroup')
        expect(group.find('[data-part="indicator"]').exists()).toBe(true)
        expect(group.attributes('style')).toContain('--wmp-item-count: 2')
        const segments = group.findAll('[data-part="segment"]')
        expect(segments.map((segment) => segment.text())).toEqual(['Upload', 'Online'])
        expect(segments[0]!.attributes('aria-checked')).toBe('true')
        expect(wrapper.get('[data-part="artist-picture-editor"]').attributes('data-state')).toBe(
            'upload',
        )
        wrapper.unmount()
    })

    it('crops and uploads a picture and keeps the dialog open with the result', async () => {
        const wrapper = await mountDialog()
        expect(button(wrapper, 'save-upload').attributes('disabled')).toBeDefined()

        await chooseFile(wrapper, new File(['x'], 'alpha.jpg', { type: 'image/jpeg' }))
        const crop = wrapper.get('[data-part="avatar-crop"]')
        expect(crop.attributes('style')).toContain('--wmp-crop-scale: 2')

        await button(wrapper, 'save-upload').trigger('click')
        await flushPromises()

        expect(image.drawCrop).toHaveBeenCalledWith(chosen, {
            x: 200,
            y: 0,
            side: 400,
            outputSize: 400,
        })
        expect(api.uploadArtistPicture).toHaveBeenCalledWith(
            'alpha',
            expect.any(Blob),
            'artist.png',
        )
        expect(covers.invalidate).toHaveBeenCalledWith([
            'artist',
            'artist-uploaded',
            'artist-online',
        ])
        expect(wrapper.emitted('changed')).toEqual([[{ ...albumsOnly, name: 'uploaded' }]])
        expect(wrapper.get('[data-part="alert"][data-state="success"]').text()).toBe(
            'The picture was uploaded.',
        )
        expect(wrapper.find('[data-part="avatar-crop"]').exists()).toBe(false)
        expect(wrapper.emitted('update:open')).toBeUndefined()
        wrapper.unmount()
    })

    it('refuses wrong files before reading them and shows errors of the server', async () => {
        const wrapper = await mountDialog()

        await chooseFile(wrapper, new File(['x'], 'alpha.gif', { type: 'image/gif' }))
        expect(wrapper.get('[data-part="alert"][data-state="error"]').text()).toBe(
            'Choose a JPEG, PNG or WebP picture.',
        )
        const large = new File(['x'], 'alpha.png', { type: 'image/png' })
        Object.defineProperty(large, 'size', { value: 11 * 1024 * 1024 })
        await chooseFile(wrapper, large)
        expect(wrapper.get('[data-part="alert"][data-state="error"]').text()).toBe(
            'The picture is too large (at most 10 MB).',
        )
        expect(image.loadPicture).not.toHaveBeenCalled()

        api.uploadArtistPicture.mockRejectedValue({
            responseStatusCode: 422,
            code: 'artist.picture_unreadable',
        })
        await chooseFile(wrapper, new File(['x'], 'alpha.png', { type: 'image/png' }))
        await button(wrapper, 'save-upload').trigger('click')
        await flushPromises()
        expect(wrapper.get('[data-part="alert"][data-state="error"]').text()).toBe(
            'The picture is damaged and cannot be read.',
        )
        expect(wrapper.emitted('changed')).toBeUndefined()
        wrapper.unmount()
    })

    it('removes the uploaded picture', async () => {
        const wrapper = await mountDialog({
            ...albumsOnly,
            picture: { kind: 'Uploaded', version: 'u-1', hasUploadedPicture: true },
        })

        expect(
            wrapper.find('[data-item="upload"] [data-part="cover"][data-kind="artist"]').exists(),
        ).toBe(true)
        await button(wrapper, 'remove-uploaded').trigger('click')
        await flushPromises()

        expect(api.removeUploadedArtistPicture).toHaveBeenCalledWith('alpha')
        expect(wrapper.emitted('changed')).toEqual([[undefined]])
        expect(wrapper.get('[data-part="alert"][data-state="success"]').text()).toBe(
            'The uploaded picture was removed.',
        )
        wrapper.unmount()
    })

    it('looks nothing up before the admin asks for it', async () => {
        const wrapper = await mountDialog()
        await online(wrapper)

        expect(wrapper.get('[data-item="online-privacy"]').text()).toContain('MusicBrainz')
        expect(api.listMusicBrainzMatches).not.toHaveBeenCalled()
        expect(api.listPictureCandidates).not.toHaveBeenCalled()
        expect(button(wrapper, 'take-over').attributes('disabled')).toBeDefined()
        wrapper.unmount()
    })

    it('tells that the lookup is switched off and leads to the settings', async () => {
        api.listMusicBrainzMatches.mockRejectedValue({
            responseStatusCode: 409,
            code: 'artist.picture_lookup_disabled',
        })
        const wrapper = await mountDialog()
        await online(wrapper)
        await button(wrapper, 'find-online').trigger('click')
        await flushPromises()

        expect(wrapper.get('[data-part="alert"][data-state="info"]').text()).toBe(
            'Online lookup is off. Switch it on under Administration, Library.',
        )
        expect(wrapper.get('[data-item="artist-picture-settings"]').attributes('href')).toBe(
            '/admin/library',
        )
        expect(api.listPictureCandidates).not.toHaveBeenCalled()
        wrapper.unmount()
    })

    it('finds the artist, shows the pictures with previews and takes one over cropped', async () => {
        const wrapper = await mountDialog()
        await online(wrapper)
        await button(wrapper, 'find-online').trigger('click')
        await flushPromises()

        const list = wrapper.get('[data-part="artist-match-list"]')
        expect(list.attributes('role')).toBe('radiogroup')
        expect(list.find('[data-part="indicator"]').exists()).toBe(true)
        const found = list.findAll('[data-part="artist-match"]')
        expect(found.map((match) => match.attributes('aria-checked'))).toEqual(['true', 'false'])
        expect(found[0]!.get('[data-part="artist-match-details"]').text()).toContain('1991 to 2004')
        expect(api.listPictureCandidates).toHaveBeenCalledWith('alpha', 'mb-1')

        const pictures = wrapper.findAll('[data-part="picture-candidate"]')
        expect(pictures).toHaveLength(2)
        expect(pictures[0]!.attributes('aria-checked')).toBe('true')
        expect(pictures[0]!.attributes('data-provider')).toBe('Wikimedia')
        expect(pictures[0]!.get('[data-part="picture-candidate-author"]').text()).toBe(
            '<script>alert(1)</script>',
        )
        expect(pictures[0]!.find('script').exists()).toBe(false)
        expect(pictures[0]!.get('[data-part="picture-candidate-size"]').text()).toBe(
            '1,280 × 640 pixels',
        )
        expect(covers.acquire).toHaveBeenCalledWith('artist-candidate', 'alpha/c1', 300, undefined)
        expect(wrapper.get('[data-item="candidates"] [data-part="artist-credit"]').text()).toBe(
            'Photo: <script>alert(1)</script>, CC BY-SA 4.0, Wikimedia Commons',
        )
        expect(api.fetchCandidatePreview).toHaveBeenCalledWith('alpha', 'c1', 500)
        expect(wrapper.find('[data-item="crop"] [data-part="avatar-crop"]').exists()).toBe(true)

        // The other artist of the same name shows its own pictures.
        await found[1]!.trigger('click')
        await flushPromises()
        expect(api.listPictureCandidates).toHaveBeenLastCalledWith('alpha', 'mb-2')

        await button(wrapper, 'take-over').trigger('click')
        await flushPromises()

        expect(api.takeOverPicture).toHaveBeenCalledWith('alpha', 'c1', { x: 0.25, y: 0, size: 1 })
        expect(wrapper.emitted('changed')).toEqual([[{ ...albumsOnly, name: 'taken' }]])
        expect(wrapper.get('[data-part="alert"][data-state="success"]').text()).toBe(
            'The online picture was taken over.',
        )
        expect(wrapper.find('[data-part="picture-candidate-list"]').exists()).toBe(false)
        wrapper.unmount()
    })

    it('uses the artist chosen last time and can choose another one', async () => {
        const wrapper = await mountDialog({
            ...albumsOnly,
            picture: {
                ...albumsOnly.picture,
                kind: 'Online',
                version: 'o-1',
                hasOnlinePicture: true,
                musicBrainzArtistId: 'mb-1',
                onlineCredit: { provider: 'FanartTv', sourceUrl: 'https://fanart.tv/artist/mb-1' },
            },
        })

        expect(wrapper.get('[data-part="artist-picture-editor"]').attributes('data-state')).toBe(
            'online',
        )
        expect(wrapper.get('[data-item="online-picture"] [data-part="artist-credit"]').text()).toBe(
            'Photo: fanart.tv',
        )
        await button(wrapper, 'find-online').trigger('click')
        await flushPromises()

        expect(api.listMusicBrainzMatches).not.toHaveBeenCalled()
        expect(api.listPictureCandidates).toHaveBeenCalledWith('alpha', null)
        expect(button(wrapper, 'change-match').text()).toBe('Choose another artist')

        await button(wrapper, 'change-match').trigger('click')
        await flushPromises()
        expect(api.listMusicBrainzMatches).toHaveBeenCalledWith('alpha')
        expect(wrapper.findAll('[data-part="artist-match"]')).toHaveLength(2)

        await button(wrapper, 'remove-online').trigger('click')
        await flushPromises()
        expect(api.removeOnlineArtistPicture).toHaveBeenCalledWith('alpha')
        wrapper.unmount()
    })

    it('gives direction when nothing is found or the services cannot be reached', async () => {
        api.listPictureCandidates.mockResolvedValue({ items: [], total: 0 })
        const wrapper = await mountDialog()
        await online(wrapper)
        await button(wrapper, 'find-online').trigger('click')
        await flushPromises()

        expect(wrapper.get('[data-item="candidates"] [data-part="alert"]').text()).toBe(
            'No pictures found for this artist on Wikimedia Commons or fanart.tv.',
        )
        wrapper.unmount()

        api.listMusicBrainzMatches.mockRejectedValue({
            responseStatusCode: 502,
            code: 'artist.picture_lookup_unavailable',
        })
        const failing = await mountDialog()
        await online(failing)
        await button(failing, 'find-online').trigger('click')
        await flushPromises()

        expect(failing.get('[data-item="matches"]').attributes('data-state')).toBe('unavailable')
        expect(failing.get('[data-item="matches"] [data-part="alert"]').text()).toBe(
            'MusicBrainz cannot be reached. Try again later.',
        )
        expect(button(failing, 'change-match').text()).toBe('Search again')
        failing.unmount()
    })

    it('takes a picture over without crop when its preview cannot be shown', async () => {
        api.fetchCandidatePreview.mockRejectedValue({
            responseStatusCode: 502,
            code: 'artist.picture_lookup_unavailable',
        })
        const wrapper = await mountDialog()
        await online(wrapper)
        await button(wrapper, 'find-online').trigger('click')
        await flushPromises()

        expect(wrapper.get('[data-item="crop"]').attributes('data-state')).toBe('error')
        await button(wrapper, 'take-over').trigger('click')
        await flushPromises()

        expect(api.takeOverPicture).toHaveBeenCalledWith('alpha', 'c1', null)
        wrapper.unmount()
    })
})
