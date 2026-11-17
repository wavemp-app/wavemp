import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { ref } from 'vue'
import { createMemoryHistory, createRouter } from 'vue-router'
import type { MediaFile, TrackFileSelection } from '@/api/generated/models'

const api = vi.hoisted(() => ({
    listFileSelections: vi.fn(),
    getTrackFileSelection: vi.fn(),
    setTrackFileSelection: vi.fn(),
    resetTrackFileSelection: vi.fn(),
}))
const library = vi.hoisted(() => ({
    getTrack: vi.fn(),
    listTrackFiles: vi.fn(),
}))

vi.mock('@/admin/fileSelectionApi', () => ({ ...api, fileSelectionRevision: ref(0) }))
vi.mock('@/library/api', () => library)

const { default: AdminTrackFilesView } = await import('../AdminTrackFilesView.vue')
const { createWaveMpI18n } = await import('@/i18n')
const { createRoutes } = await import('@/router/routes')

const files: MediaFile[] = [
    {
        id: 'flac',
        kind: 'Audio',
        relativePath: 'Band/Song.flac',
        isPrimary: true,
        codec: 'flac',
        isLossless: true,
        size: 1000,
    },
    { id: 'mp3', kind: 'Audio', relativePath: 'Band/Song.mp3', codec: 'mp3', size: 500 },
    { id: 'jpg', kind: 'Image', relativePath: 'Band/Song.jpg', size: 100 },
]

function selection(overrides: Partial<TrackFileSelection> = {}): TrackFileSelection {
    return {
        trackId: 'track-1',
        audio: {
            state: 'Automatic',
            activeFileId: 'flac',
            automaticFileId: 'flac',
            candidateFileIds: ['flac', 'mp3'],
        },
        video: { state: 'Automatic', candidateFileIds: [] },
        cover: {
            state: 'Automatic',
            activeFileId: 'jpg',
            automaticFileId: 'jpg',
            candidateFileIds: ['jpg', 'flac'],
        },
        ...overrides,
    }
}

async function mountView() {
    const router = createRouter({ history: createMemoryHistory(), routes: createRoutes() })
    await router.push('/admin/files/track-1')
    const wrapper = mount(AdminTrackFilesView, {
        props: { id: 'track-1' },
        global: { plugins: [router, createWaveMpI18n('en')] },
        attachTo: document.body,
    })
    await flushPromises()
    return { wrapper, router }
}

function choices(wrapper: Awaited<ReturnType<typeof mountView>>['wrapper'], category: string) {
    return wrapper.findAll(
        `[data-part="file-choices"][data-item="${category}"] [data-part="file-choice"]`,
    )
}

describe('AdminTrackFilesView', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
        library.getTrack.mockReset().mockResolvedValue({
            id: 'track-1',
            title: 'Song',
            artist: { id: 'band', name: 'Band' },
        })
        library.listTrackFiles.mockReset().mockResolvedValue({ items: files, total: files.length })
        api.getTrackFileSelection.mockReset().mockResolvedValue(selection())
        api.setTrackFileSelection.mockReset()
        api.resetTrackFileSelection.mockReset().mockResolvedValue(undefined)
    })

    afterEach(() => {
        document.body.innerHTML = ''
    })

    it('shows the files per category with the automatic choice checked', async () => {
        const { wrapper } = await mountView()

        expect(wrapper.get('[data-part="detail-title"]').text()).toBe('Song')
        expect(wrapper.get('[data-part="admin-nav-item"][aria-current="page"]').text()).toBe(
            'Files',
        )
        const audio = choices(wrapper, 'audio')
        expect(audio.map((choice) => choice.attributes('aria-checked'))).toEqual([
            'true',
            'false',
            'false',
        ])
        expect(audio[0]?.get('[data-part="file-meta"]').text()).toBe('Best quality: Band/Song.flac')
        expect(audio[1]?.attributes('data-state')).toBe('active best')
        expect(audio[1]?.text()).toContain('FLAC')
        expect(audio[1]?.text()).toContain('Lossless')
        expect(wrapper.find('[data-part="file-choices"][data-item="video"]').exists()).toBe(false)
        expect(wrapper.get('[data-part="section"][data-item="video"]').text()).toContain(
            'This song has no video.',
        )
        expect(choices(wrapper, 'cover')[2]?.find('[data-state="embedded"]').exists()).toBe(true)
        expect(wrapper.find('[data-item="reset-file-selection"]').exists()).toBe(false)
        wrapper.unmount()
    })

    it('selects a file by click and keeps the other categories', async () => {
        const updated = selection({
            audio: {
                state: 'Selected',
                selectedFileId: 'mp3',
                selectedPath: 'Band/Song.mp3',
                activeFileId: 'mp3',
                automaticFileId: 'flac',
                candidateFileIds: ['flac', 'mp3'],
            },
        })
        api.setTrackFileSelection.mockResolvedValue(updated)
        const { wrapper } = await mountView()

        await choices(wrapper, 'audio')[2]?.trigger('click')
        await flushPromises()

        expect(api.setTrackFileSelection).toHaveBeenCalledWith('track-1', {
            audioFileId: 'mp3',
            videoFileId: null,
            coverFileId: null,
        })
        expect(library.listTrackFiles).toHaveBeenCalledTimes(2)
        expect(choices(wrapper, 'audio')[2]?.attributes('aria-checked')).toBe('true')
        expect(wrapper.get('[data-part="alert"]').text()).toBe('The selection was saved.')
        expect(wrapper.find('[data-item="reset-file-selection"]').exists()).toBe(true)
        wrapper.unmount()
    })

    it('shows a selected file that is gone and resets the selection', async () => {
        api.getTrackFileSelection.mockResolvedValueOnce(
            selection({
                cover: {
                    state: 'Missing',
                    selectedFileId: 'png',
                    selectedPath: 'Band/Song.png',
                    activeFileId: 'jpg',
                    automaticFileId: 'jpg',
                    candidateFileIds: ['jpg'],
                },
            }),
        )
        const { wrapper } = await mountView()

        const cover = wrapper.get('[data-part="section"][data-item="cover"]')
        expect(cover.get('[data-part="alert"]').text()).toContain('Band/Song.png')
        const missing = choices(wrapper, 'cover').at(-1)
        expect(missing?.attributes('data-item')).toBe('missing')
        expect(missing?.attributes('aria-checked')).toBe('true')
        expect(missing?.get('[data-part="file-path"]').text()).toBe('Band/Song.png')

        // A change of another category sends the missing selection again, so it stays.
        api.setTrackFileSelection.mockResolvedValue(selection())
        await choices(wrapper, 'audio')[2]?.trigger('click')
        await flushPromises()
        expect(api.setTrackFileSelection).toHaveBeenLastCalledWith('track-1', {
            audioFileId: 'mp3',
            videoFileId: null,
            coverFileId: 'png',
        })

        api.getTrackFileSelection.mockResolvedValue(
            selection({
                cover: { state: 'Selected', selectedFileId: 'jpg', candidateFileIds: ['jpg'] },
            }),
        )
        api.setTrackFileSelection.mockResolvedValue(
            selection({
                cover: { state: 'Selected', selectedFileId: 'jpg', candidateFileIds: ['jpg'] },
            }),
        )
        await choices(wrapper, 'cover')[1]?.trigger('click')
        await flushPromises()
        api.getTrackFileSelection.mockResolvedValue(selection())
        await wrapper.get('[data-item="reset-file-selection"]').trigger('click')
        await flushPromises()

        expect(api.resetTrackFileSelection).toHaveBeenCalledWith('track-1')
        expect(wrapper.find('[data-item="reset-file-selection"]').exists()).toBe(false)
        wrapper.unmount()
    })

    it('loads the files again when a file can no longer be selected', async () => {
        api.setTrackFileSelection.mockRejectedValue({
            errors: [{ field: 'audioFileId', code: 'invalid_value' }],
        })
        const { wrapper } = await mountView()

        await choices(wrapper, 'audio')[2]?.trigger('click')
        await flushPromises()

        expect(api.getTrackFileSelection).toHaveBeenCalledTimes(2)
        expect(wrapper.get('[data-part="alert"]').text()).toBe(
            'This file can no longer be selected. The files were loaded again.',
        )
        wrapper.unmount()
    })

    it('tells when the song does not exist', async () => {
        library.getTrack.mockRejectedValue({ responseStatusCode: 404 })
        const { wrapper } = await mountView()

        expect(wrapper.get('[data-part="empty-state"]').text()).toContain('To the file selection')
        wrapper.unmount()
    })
})
