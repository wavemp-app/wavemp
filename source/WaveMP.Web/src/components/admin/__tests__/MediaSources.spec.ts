import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import MediaSourceDeleteDialog from '../MediaSourceDeleteDialog.vue'
import MediaSourceDialog from '../MediaSourceDialog.vue'
import MediaSourceList from '../MediaSourceList.vue'
import MediaSourcesPanel from '../MediaSourcesPanel.vue'
import ScanIssues from '../ScanIssues.vue'
import type { MediaSource } from '@/api/generated/models'
import { createWaveMpI18n } from '@/i18n'
import { useMediaSourcesStore } from '@/stores/mediaSources'

const media: MediaSource = {
    id: 'media',
    name: 'Media',
    path: '/media',
    followsMediaPath: true,
    isActive: true,
    isAvailable: true,
    isWatched: true,
    trackCount: 1200,
    fileCount: 2500,
}

const videos: MediaSource = {
    id: 'videos',
    name: 'Music videos',
    path: '/media-videos',
    followsMediaPath: false,
    isActive: false,
    isAvailable: false,
    isWatched: false,
    trackCount: 3,
    fileCount: 3,
}

const i18n = () => createWaveMpI18n('en')

async function mountDialog(source: MediaSource | null) {
    const wrapper = mount(MediaSourceDialog, {
        props: { source, open: false },
        global: { plugins: [i18n()] },
        attachTo: document.body,
    })
    await wrapper.setProps({ open: true })
    return wrapper
}

async function fill(
    wrapper: Awaited<ReturnType<typeof mountDialog>>,
    name: string,
    path: string,
): Promise<void> {
    const inputs = wrapper.findAll('input[data-part="input"], input:not([type="checkbox"])')
    await inputs[0]?.setValue(name)
    await inputs[1]?.setValue(path)
}

async function submit(wrapper: Awaited<ReturnType<typeof mountDialog>>) {
    await wrapper.get('form').trigger('submit')
    await flushPromises()
}

function fieldErrors(wrapper: Awaited<ReturnType<typeof mountDialog>>): string[] {
    return wrapper.findAll('[data-part="field-error"]').map((error) => error.text())
}

describe('MediaSourceList', () => {
    it('shows name, path, states and size of every folder', () => {
        const wrapper = mount(MediaSourceList, {
            props: { sources: [media, videos] },
            global: { plugins: [i18n()] },
        })

        const items = wrapper.findAll('[data-part="source-item"]')
        expect(items.map((item) => item.attributes('data-state'))).toEqual([
            'active watched media-path',
            'inactive unavailable',
        ])
        expect(items[0]?.get('[data-part="source-path"]').text()).toBe('/media')
        const badges = items[0]
            ?.findAll('[data-part="badge"]')
            .map((badge) => badge.attributes('data-state'))
        expect(badges).toEqual(['active', 'watched', 'media-path'])
        expect(items[0]?.get('[data-part="source-meta"]').text()).toContain(
            'Songs: 1,200, files: 2,500',
        )
        expect(items[1]?.text()).toContain('Folder missing')
        expect(items[1]?.get('[data-item="toggle"]').text()).toBe('Switch on')
    })

    it('reports the actions and disables the folder that is being saved', async () => {
        const wrapper = mount(MediaSourceList, {
            props: { sources: [media, videos], busyId: 'videos' },
            global: { plugins: [i18n()] },
        })
        const [first, second] = wrapper.findAll('[data-part="source-item"]')

        await first?.get('[data-item="toggle"]').trigger('click')
        await first?.get('[data-item="edit"]').trigger('click')
        await first?.get('[data-item="delete"]').trigger('click')

        expect(wrapper.emitted('toggle')).toEqual([[media]])
        expect(wrapper.emitted('edit')).toEqual([[media]])
        expect(wrapper.emitted('delete')).toEqual([[media]])
        expect(second?.get('[data-item="toggle"]').attributes('disabled')).toBeDefined()
        expect(second?.get('[data-item="delete"]').attributes('disabled')).toBeDefined()
    })
})

describe('MediaSourceDialog', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
    })

    it('adds a folder with the entered values', async () => {
        const store = useMediaSourcesStore()
        const create = vi.spyOn(store, 'create').mockResolvedValue({ id: 'new', name: 'Clips' })
        const wrapper = await mountDialog(null)

        expect(wrapper.get('[data-part="dialog-title"]').text()).toBe('Add media folder')
        await fill(wrapper, '  Clips ', ' /media-clips ')
        await submit(wrapper)

        expect(create).toHaveBeenCalledWith({ name: 'Clips', path: '/media-clips', isActive: true })
        expect(wrapper.emitted('saved')).toEqual([[{ id: 'new', name: 'Clips' }]])
        expect(wrapper.emitted('update:open')?.at(-1)).toEqual([false])
        wrapper.unmount()
    })

    it('sends only the changed values of a folder', async () => {
        const store = useMediaSourcesStore()
        const update = vi.spyOn(store, 'update').mockResolvedValue(videos)
        const wrapper = await mountDialog(videos)

        expect(wrapper.get('[data-part="dialog-title"]').text()).toBe('Edit media folder')
        await wrapper.get('input[type="checkbox"]').setValue(true)
        await submit(wrapper)

        expect(update).toHaveBeenCalledWith('videos', { isActive: true })
        wrapper.unmount()
    })

    it('explains that the first folder comes from MEDIA_PATH', async () => {
        const wrapper = await mountDialog(media)

        expect(wrapper.text()).toContain('Comes from MEDIA_PATH.')
        wrapper.unmount()
    })

    it('needs a name and a folder', async () => {
        const store = useMediaSourcesStore()
        const create = vi.spyOn(store, 'create')
        const wrapper = await mountDialog(null)

        await submit(wrapper)

        expect(create).not.toHaveBeenCalled()
        expect(fieldErrors(wrapper)).toEqual(['Please enter a name.', 'Please enter the folder.'])
        wrapper.unmount()
    })

    it.each([
        ['source.name_taken', 409, 'A media folder with this name already exists.'],
        [
            'source.path_not_found',
            422,
            'This folder does not exist in the container. Check the volumes in docker-compose.yml.',
        ],
        [
            'source.path_overlaps',
            409,
            'This folder is another media folder, lies inside one or contains one.',
        ],
    ])('shows the problem %s at its field', async (code, status, message) => {
        const store = useMediaSourcesStore()
        vi.spyOn(store, 'create').mockRejectedValue({ code, responseStatusCode: status })
        const wrapper = await mountDialog(null)

        await fill(wrapper, 'Clips', '/media/clips')
        await submit(wrapper)

        expect(fieldErrors(wrapper)).toEqual([message])
        expect(wrapper.emitted('saved')).toBeUndefined()
        wrapper.unmount()
    })

    it('asks for an absolute path when the server rejects the path', async () => {
        const store = useMediaSourcesStore()
        vi.spyOn(store, 'create').mockRejectedValue({
            responseStatusCode: 400,
            code: 'request.invalid',
            errors: [{ field: 'path', code: 'invalid_format' }],
        })
        const wrapper = await mountDialog(null)

        await fill(wrapper, 'Clips', 'clips')
        await submit(wrapper)

        expect(fieldErrors(wrapper)).toEqual(['Enter the full path, starting with /.'])
        expect(wrapper.get('[data-part="alert"]').text()).toBe('Please check your input.')
        wrapper.unmount()
    })

    it('tells that a scan blocks moving the folder', async () => {
        const store = useMediaSourcesStore()
        vi.spyOn(store, 'update').mockRejectedValue({
            code: 'scan.already_running',
            responseStatusCode: 409,
        })
        const wrapper = await mountDialog(videos)

        await fill(wrapper, 'Music videos', '/elsewhere')
        await submit(wrapper)

        expect(wrapper.get('[data-part="alert"]').text()).toContain('A scan is running.')
        wrapper.unmount()
    })
})

describe('MediaSourceDeleteDialog', () => {
    it('names the folder and the number of its songs', async () => {
        const wrapper = mount(MediaSourceDeleteDialog, {
            props: { source: media, open: false },
            global: { plugins: [i18n()] },
            attachTo: document.body,
        })
        await wrapper.setProps({ open: true })

        expect(wrapper.text()).toContain(
            'Remove Media from the library? Songs in this folder: 1,200.',
        )
        expect(wrapper.text()).toContain('The files in the folder stay untouched.')
        wrapper.unmount()
    })
})

describe('MediaSourcesPanel', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
    })

    it('removes a folder after asking and reports it', async () => {
        const store = useMediaSourcesStore()
        vi.spyOn(store, 'load').mockImplementation(async () => {
            store.sources = [media, videos]
            store.status = 'ready'
        })
        const remove = vi.spyOn(store, 'remove').mockImplementation(async (id) => {
            store.sources = store.sources.filter((source) => source.id !== id)
        })
        const wrapper = mount(MediaSourcesPanel, {
            global: { plugins: [i18n()] },
            attachTo: document.body,
        })
        await flushPromises()

        await wrapper.findAll('[data-item="delete"]')[1]?.trigger('click')
        await flushPromises()
        const confirm = wrapper
            .findAll('[data-part="dialog"] button')
            .find((button) => button.text() === 'Remove')
        await confirm?.trigger('click')
        await flushPromises()

        expect(remove).toHaveBeenCalledWith('videos')
        expect(wrapper.findAll('[data-part="source-item"]')).toHaveLength(1)
        expect(wrapper.text()).toContain('Music videos is removed from the library.')
        expect(wrapper.emitted('changed')).toHaveLength(1)
        wrapper.unmount()
    })

    it('reports a failed removal', async () => {
        const store = useMediaSourcesStore()
        vi.spyOn(store, 'load').mockImplementation(async () => {
            store.sources = [media]
            store.status = 'ready'
        })
        vi.spyOn(store, 'remove').mockRejectedValue({
            code: 'scan.already_running',
            responseStatusCode: 409,
        })
        const wrapper = mount(MediaSourcesPanel, {
            global: { plugins: [i18n()] },
            attachTo: document.body,
        })
        await flushPromises()

        await wrapper.get('[data-item="delete"]').trigger('click')
        await flushPromises()
        await wrapper
            .findAll('[data-part="dialog"] button')
            .find((button) => button.text() === 'Remove')
            ?.trigger('click')
        await flushPromises()

        expect(wrapper.get('[data-part="alert"][data-state="error"]').text()).toContain(
            'A scan is running.',
        )
        wrapper.unmount()
    })
})

describe('ScanIssues', () => {
    it('names the media folder of a skipped folder and of problems in several folders', () => {
        const wrapper = mount(ScanIssues, {
            props: {
                failed: 2,
                issues: [
                    {
                        path: '',
                        code: 'source.not_found',
                        sourceId: 'videos',
                        sourceName: 'Videos',
                    },
                    {
                        path: 'Band/Song.mp3',
                        code: 'file.probe_failed',
                        sourceId: 'media',
                        sourceName: 'Media',
                    },
                ],
            },
            global: { plugins: [i18n()] },
        })

        const items = wrapper.findAll('[data-part="issue-item"]')
        expect(items[0]?.get('[data-part="issue-source"]').text()).toBe('Videos')
        expect(items[0]?.find('[data-part="issue-path"]').exists()).toBe(false)
        expect(items[0]?.text()).toContain('Folder missing, skipped')
        expect(items[1]?.get('[data-part="issue-source"]').text()).toBe('Media')
        expect(items[1]?.get('[data-part="issue-path"]').text()).toBe('Band/Song.mp3')
    })

    it('leaves out the media folder while all problems lie in one', () => {
        const wrapper = mount(ScanIssues, {
            props: {
                failed: 1,
                issues: [
                    {
                        path: 'Song.mp3',
                        code: 'file.unreadable',
                        sourceId: 'media',
                        sourceName: 'Media',
                    },
                ],
            },
            global: { plugins: [i18n()] },
        })

        expect(wrapper.find('[data-part="issue-source"]').exists()).toBe(false)
    })
})
