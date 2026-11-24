import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import type { OrganizeItem, OrganizeOperation, Track } from '@/api/generated/models'
import { createWaveMpI18n } from '@/i18n'
import { createRoutes } from '@/router/routes'

const api = vi.hoisted(() => ({
    listOrganizeItems: vi.fn(),
    applyOrganizeOperation: vi.fn(),
    discardOrganizeOperation: vi.fn(),
    getOrganizingSettings: vi.fn(),
    updateOrganizingSettings: vi.fn(),
    previewLayoutTemplate: vi.fn(),
}))

vi.mock('@/admin/organizeApi', () => ({
    listOrganizeItems: api.listOrganizeItems,
    applyOrganizeOperation: api.applyOrganizeOperation,
    discardOrganizeOperation: api.discardOrganizeOperation,
    getOrganizingSettings: api.getOrganizingSettings,
    updateOrganizingSettings: api.updateOrganizingSettings,
    previewLayoutTemplate: api.previewLayoutTemplate,
    getOrganizeAvailability: vi.fn(),
    getOrganizeOperation: vi.fn(),
    listOrganizeOperations: vi.fn(),
    createOrganizePreview: vi.fn(),
    createOrganizeUndoPreview: vi.fn(),
    cancelOrganizeOperation: vi.fn(),
}))

const { default: TemplateField } = await import('../TemplateField.vue')
const { default: OrganizePreviewDialog } = await import('../OrganizePreviewDialog.vue')
const { default: OrganizingSettingsForm } = await import('../OrganizingSettingsForm.vue')
const { default: OwnAlbumTrackList } = await import('../OwnAlbumTrackList.vue')
const { default: PathRoute } = await import('../PathRoute.vue')
const { default: TrackPickerList } = await import('../TrackPickerList.vue')
const { routeSegments, templatePresets } = await import('@/admin/organize')

const dialogPrototype = HTMLDialogElement.prototype as Partial<HTMLDialogElement>

async function mountWith<T>(component: T, props: Record<string, unknown> = {}) {
    const router = createRouter({ history: createMemoryHistory(), routes: createRoutes() })
    await router.push('/admin/library')
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return mount(component as any, {
        props,
        attachTo: document.body,
        global: { plugins: [router, createWaveMpI18n('en')] },
    })
}

beforeEach(() => {
    setActivePinia(createPinia())
    for (const mock of Object.values(api)) {
        mock.mockReset()
    }
    dialogPrototype.showModal = function (this: HTMLDialogElement) {
        this.setAttribute('open', '')
    }
    dialogPrototype.close = function (this: HTMLDialogElement) {
        this.removeAttribute('open')
    }
})

afterEach(() => {
    delete dialogPrototype.showModal
    delete dialogPrototype.close
    document.body.innerHTML = ''
})

describe('TemplateField', () => {
    it('offers the presets as a radiogroup and switches to a preset', async () => {
        const wrapper = await mountWith(TemplateField, {
            modelValue: templatePresets.flat,
            'onUpdate:modelValue': (value: string) => wrapper.setProps({ modelValue: value }),
        })

        const field = wrapper.get('[data-part="template-field"]')
        expect(field.attributes('data-state')).toBe('flat')
        const group = wrapper.get('[data-part="segmented-control"][data-item="template-preset"]')
        expect(group.attributes('role')).toBe('radiogroup')
        expect(group.find('[data-part="indicator"]').exists()).toBe(true)
        expect(group.attributes('style')).toContain('--wmp-active-index: 1')
        const segments = group.findAll('[data-part="segment"]')
        expect(segments.map((segment) => segment.attributes('aria-checked'))).toEqual([
            'false',
            'true',
            'false',
        ])

        await segments[0]!.trigger('click')

        expect((wrapper.props() as { modelValue: string }).modelValue).toBe(templatePresets.folders)
        expect(field.attributes('data-state')).toBe('folders')

        await segments[2]!.trigger('click')
        expect(field.attributes('data-state')).toBe('custom')
        expect((wrapper.props() as { modelValue: string }).modelValue).toBe(templatePresets.folders)
        wrapper.unmount()
    })

    it('inserts a placeholder at the cursor', async () => {
        const wrapper = await mountWith(TemplateField, {
            modelValue: '{Artist}/{Title}',
            'onUpdate:modelValue': (value: string) => wrapper.setProps({ modelValue: value }),
        })

        const input = wrapper.get<HTMLInputElement>('input[data-part="input"]')
        input.element.setSelectionRange(9, 9)
        const year = wrapper.get('[data-part="template-token"][data-item="year"]')
        expect(year.attributes('aria-label')).toBe('Insert Year')
        expect(year.get('[data-field="token"]').text()).toBe('{Year}')

        await year.trigger('click')
        await flushPromises()

        expect((wrapper.props() as { modelValue: string }).modelValue).toBe(
            '{Artist}/{Year}{Title}',
        )
        expect(input.element.selectionStart).toBe(15)
        expect(wrapper.get('[data-part="template-field"]').attributes('data-state')).toBe('custom')
        wrapper.unmount()
    })

    it('shows the first error of the template as a translated code', async () => {
        const wrapper = await mountWith(TemplateField, {
            modelValue: '{Artist}/{Foo}',
            errors: [{ code: 'unknown_placeholder', placeholder: 'Foo', position: 9 }],
        })

        expect(wrapper.get('[data-part="field-error"]').text()).toContain('Foo')
        expect(wrapper.get('input[data-part="input"]').attributes('aria-invalid')).toBe('true')
        wrapper.unmount()
    })
})

describe('OrganizingSettingsForm', () => {
    beforeEach(() => {
        vi.useFakeTimers()
    })

    afterEach(() => {
        vi.useRealTimers()
    })

    it('previews the template while typing, and with example songs for an empty library', async () => {
        api.getOrganizingSettings.mockResolvedValue({ layoutTemplate: templatePresets.folders })
        api.previewLayoutTemplate.mockImplementation(async (request: { samples?: unknown[] }) =>
            request.samples
                ? {
                      isValid: true,
                      errors: [],
                      paths: [
                          { folders: ['Artist', 'Album'], baseName: '03 Song', extension: 'flac' },
                      ],
                  }
                : { isValid: true, errors: [], paths: [] },
        )
        const wrapper = await mountWith(OrganizingSettingsForm)
        await flushPromises()

        expect(api.previewLayoutTemplate).toHaveBeenCalledTimes(2)
        expect(api.previewLayoutTemplate.mock.calls[0]![0]).toEqual({
            template: templatePresets.folders,
        })
        expect(api.previewLayoutTemplate.mock.calls[1]![0].samples).toHaveLength(2)
        const preview = wrapper.get('[data-part="template-preview"]')
        expect(preview.attributes('data-state')).toBe('samples')
        expect(preview.findAll('[data-part="template-preview-item"]')).toHaveLength(1)

        const input = wrapper.get('input[data-part="input"]')
        await input.setValue('{Artist}/{T')
        await input.setValue('{Artist}/{Ti')
        await input.setValue('{Artist}/{Title}')
        await vi.advanceTimersByTimeAsync(299)
        expect(api.previewLayoutTemplate).toHaveBeenCalledTimes(2)

        await vi.advanceTimersByTimeAsync(1)
        await flushPromises()
        expect(api.previewLayoutTemplate).toHaveBeenCalledTimes(3)
        expect(api.previewLayoutTemplate.mock.calls[2]![0]).toEqual({
            template: '{Artist}/{Title}',
            samples: expect.any(Array),
        })
        wrapper.unmount()
    })

    it('keeps an invalid template from being saved', async () => {
        api.getOrganizingSettings.mockResolvedValue({ layoutTemplate: templatePresets.folders })
        api.previewLayoutTemplate.mockImplementation(async (request: { template: string }) =>
            request.template.includes('{Title}')
                ? { isValid: true, errors: [], paths: [{ trackId: 't', baseName: 'x' }] }
                : { isValid: false, errors: [{ code: 'missing_title' }], paths: [] },
        )
        const wrapper = await mountWith(OrganizingSettingsForm)
        await flushPromises()

        await wrapper.get('input[data-part="input"]').setValue('{Artist}')
        await vi.advanceTimersByTimeAsync(300)
        await flushPromises()

        expect(wrapper.get('[data-part="field-error"]').text()).toContain('{Title}')
        expect(wrapper.get('button[type="submit"]').attributes('disabled')).toBeDefined()
        expect(wrapper.find('[data-part="template-preview"]').exists()).toBe(false)
        wrapper.unmount()
    })
})

describe('OrganizePreviewDialog', () => {
    const preview: OrganizeOperation = {
        id: 'op',
        kind: 'Library',
        status: 'Previewed',
        totalItems: 3,
        conflictItems: 1,
        warningItems: 1,
    }
    const items: OrganizeItem[] = [
        {
            id: 'a',
            kind: 'Track',
            status: 'Pending',
            displayTitle: 'Song A',
            fromDirectoryPath: 'Inbox',
            fromBaseName: 'Song A',
            toDirectoryPath: 'Artist/Album',
            toBaseName: '01 Song A',
            moves: [{ fromPath: 'Inbox/Song A.flac', toPath: 'Artist/Album/01 Song A.flac' }],
        },
        {
            id: 'b',
            kind: 'Track',
            status: 'Conflict',
            conflict: 'target_exists',
            displayTitle: 'Song B',
            fromDirectoryPath: 'Inbox',
            fromBaseName: 'Song B',
            toDirectoryPath: 'Artist/Album',
            toBaseName: '02 Song B',
        },
    ]

    function mountDialog(props: Record<string, unknown> = {}) {
        return mountWith(OrganizePreviewDialog, { operation: preview, open: true, ...props })
    }

    it('lists the items and filters them by conflicts and warnings', async () => {
        api.listOrganizeItems.mockResolvedValue({ items, total: 2 })
        const wrapper = await mountDialog()
        await flushPromises()

        expect(api.listOrganizeItems).toHaveBeenCalledWith('op', {
            filter: 'All',
            offset: 0,
            limit: 100,
        })
        const summary = wrapper.get('[data-part="organize-summary"]')
        expect(summary.attributes('data-state')).toBe('conflicts warnings')
        const rows = wrapper.findAll('[data-part="organize-item"]')
        expect(rows.map((row) => row.attributes('data-state'))).toEqual(['pending', 'conflict'])
        expect(rows[1]!.get('[data-part="organize-problem"]').attributes('data-state')).toBe(
            'conflict',
        )
        const segments = wrapper.findAll('[data-item="organize-filter"] > [data-part="segment"]')
        expect(segments.map((segment) => segment.attributes('data-item'))).toEqual([
            'all',
            'conflicts',
            'warnings',
        ])

        api.listOrganizeItems.mockResolvedValue({ items: [items[1]], total: 1 })
        await segments[1]!.trigger('click')
        await flushPromises()

        expect(api.listOrganizeItems).toHaveBeenLastCalledWith('op', {
            filter: 'Conflicts',
            offset: 0,
            limit: 100,
        })
        expect(wrapper.findAll('[data-part="organize-item"]')).toHaveLength(1)
        expect(segments[1]!.attributes('aria-checked')).toBe('true')
        wrapper.unmount()
    })

    it('moves the files only after the conflicts are skipped', async () => {
        api.listOrganizeItems.mockResolvedValue({ items, total: 2 })
        api.applyOrganizeOperation.mockResolvedValue({ ...preview, status: 'Queued' })
        const wrapper = await mountDialog()
        await flushPromises()

        const apply = wrapper.get('button[data-item="apply"]')
        expect(apply.attributes('disabled')).toBeDefined()
        expect(wrapper.text()).toContain('Skip songs with conflicts')

        await wrapper.get('[data-field="skip-conflicts"] input').setValue(true)
        expect(apply.attributes('disabled')).toBeUndefined()

        await apply.trigger('click')
        await flushPromises()

        expect(api.applyOrganizeOperation).toHaveBeenCalledWith('op', true)
        expect(wrapper.emitted('applied')).toHaveLength(1)
        expect(wrapper.emitted('update:open')?.at(-1)).toEqual([false])
        expect(api.discardOrganizeOperation).not.toHaveBeenCalled()
        wrapper.unmount()
    })

    it('stays disabled with the reason while the media folder cannot be written', async () => {
        api.listOrganizeItems.mockResolvedValue({ items: [items[0]], total: 1 })
        const wrapper = await mountDialog({
            operation: { ...preview, conflictItems: 0 },
            blocked: 'readOnly',
        })
        await flushPromises()

        expect(wrapper.find('[data-field="skip-conflicts"]').exists()).toBe(false)
        expect(wrapper.get('button[data-item="apply"]').attributes('disabled')).toBeDefined()
        expect(wrapper.get('[data-part="alert"]').text()).toContain('read-only')
        wrapper.unmount()
    })

    it('throws a preview away when it is closed without moving files', async () => {
        api.listOrganizeItems.mockResolvedValue({ items: [items[0]], total: 1 })
        api.discardOrganizeOperation.mockResolvedValue(undefined)
        const wrapper = await mountDialog({ operation: { ...preview, conflictItems: 0 } })
        await flushPromises()

        await wrapper.get('button[data-item="cancel"]').trigger('click')
        await wrapper.setProps({ open: false })
        await flushPromises()

        expect(api.discardOrganizeOperation).toHaveBeenCalledWith('op')
        expect(wrapper.emitted('discarded')).toHaveLength(1)
        wrapper.unmount()
    })

    it('shows the changes of a run with the status of each item', async () => {
        api.listOrganizeItems.mockResolvedValue({
            items: [{ ...items[0], status: 'Done' }],
            total: 1,
        })
        const wrapper = await mountDialog({
            operation: { ...preview, status: 'Completed', conflictItems: 0 },
            mode: 'changes',
        })
        await flushPromises()

        expect(wrapper.get('[data-part="organize-panel"]').attributes('data-state')).toBe('changes')
        expect(wrapper.find('button[data-item="apply"]').exists()).toBe(false)
        expect(
            wrapper.get('[data-part="organize-item-header"] [data-state~="status"]').text(),
        ).toBe('Moved')
        wrapper.unmount()
    })
})

describe('PathRoute', () => {
    it('marks the folders that change and hides the separators from assistive technology', async () => {
        const wrapper = await mountWith(PathRoute, {
            route: routeSegments(['Inbox', 'Song'], ['Artist', 'Album', '01 Song']),
        })

        const route = wrapper.get('[data-part="path-route"]')
        expect(route.attributes('data-state')).toBe('move')
        const to = wrapper.findAll('[data-part="path-to"] > [data-part="path-segment"]')
        expect(to.map((segment) => [segment.text(), segment.attributes('data-state')])).toEqual([
            ['Artist', 'changed'],
            ['Album', 'new'],
            ['01 Song', 'changed'],
        ])
        expect(to.at(-1)!.attributes('data-kind')).toBe('file')
        const separators = wrapper.findAll('[data-part="path-separator"]')
        expect(separators).toHaveLength(3)
        expect(
            separators.every((separator) => separator.attributes('aria-hidden') === 'true'),
        ).toBe(true)
        wrapper.unmount()
    })
})

describe('OwnAlbumTrackList', () => {
    const tracks: Track[] = ['one', 'two', 'three'].map((id) => ({
        id,
        title: `Song ${id}`,
        hasAudio: id !== 'three',
        artist: { name: 'Artist' },
        album: { title: 'Album' },
    }))

    it('numbers the songs and moves them with the buttons', async () => {
        const wrapper = await mountWith(OwnAlbumTrackList, { tracks })

        const rows = wrapper.findAll('[data-part="own-album-track"]')
        expect(rows.map((row) => row.get('[data-part="own-album-track-number"]').text())).toEqual([
            '1',
            '2',
            '3',
        ])
        expect(rows[2]!.attributes('data-state')).toBe('no-audio')
        expect(rows[0]!.get('[data-item="move-up"]').attributes('disabled')).toBeDefined()
        expect(rows[2]!.get('[data-item="move-down"]').attributes('disabled')).toBeDefined()

        await rows[0]!.get('[data-item="move-down"]').trigger('click')
        await rows[2]!.get('[data-item="move-up"]').trigger('click')
        await rows[1]!.get('[data-item="remove"]').trigger('click')

        expect(wrapper.emitted('move')).toEqual([
            [0, 1],
            [2, 1],
        ])
        expect(wrapper.emitted('remove')).toEqual([[1]])
        wrapper.unmount()
    })

    it('moves a song with Alt and the arrow keys, but not past the ends', async () => {
        const wrapper = await mountWith(OwnAlbumTrackList, { tracks })
        const rows = wrapper.findAll('[data-part="own-album-track"]')

        await rows[1]!.trigger('keydown', { key: 'ArrowUp', altKey: true })
        await rows[1]!.trigger('keydown', { key: 'ArrowDown', altKey: true })
        await rows[0]!.trigger('keydown', { key: 'ArrowUp', altKey: true })
        await rows[1]!.trigger('keydown', { key: 'ArrowUp' })

        expect(wrapper.emitted('move')).toEqual([
            [1, 0],
            [1, 2],
        ])
        wrapper.unmount()
    })

    it('stays still while disabled', async () => {
        const wrapper = await mountWith(OwnAlbumTrackList, { tracks, disabled: true })

        await wrapper.findAll('[data-part="own-album-track"]')[1]!.trigger('keydown', {
            key: 'ArrowUp',
            altKey: true,
        })

        expect(wrapper.emitted('move')).toBeUndefined()
        expect(wrapper.get('[data-part="own-album-tracks"]').attributes('aria-disabled')).toBe(
            'true',
        )
        wrapper.unmount()
    })
})

describe('TrackPickerList', () => {
    it('greys out songs without audio and songs already in the album', async () => {
        const tracks: Track[] = [
            { id: 'a', title: 'A', hasAudio: true },
            { id: 'b', title: 'B', hasAudio: false },
            { id: 'c', title: 'C', hasAudio: true },
        ]
        const wrapper = await mountWith(TrackPickerList, {
            tracks,
            chosenIds: ['a'],
            excludeIds: ['c'],
        })

        const rows = wrapper.findAll('[data-part="track-picker-item"]')
        expect(rows.map((row) => row.attributes('data-state'))).toEqual([
            'selected',
            'disabled',
            'disabled',
        ])
        expect(rows[1]!.get('[data-field="reason"]').text()).toBe('No audio file')
        expect(rows[2]!.get('[data-field="reason"]').text()).toBe('Already in the album')

        await rows[0]!.get('input').trigger('change')
        expect(wrapper.emitted('toggle')).toEqual([[tracks[0]]])
        wrapper.unmount()
    })
})
