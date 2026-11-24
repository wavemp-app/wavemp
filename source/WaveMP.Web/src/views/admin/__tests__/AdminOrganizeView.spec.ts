import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import type { OrganizeAvailability } from '@/api/generated/models'
import { createWaveMpI18n } from '@/i18n'
import { createRoutes } from '@/router/routes'

const api = vi.hoisted(() => ({
    getOrganizeAvailability: vi.fn(),
    getOrganizeOperation: vi.fn(),
    listOrganizeOperations: vi.fn(),
    createOrganizePreview: vi.fn(),
    createOrganizeUndoPreview: vi.fn(),
    getOrganizingSettings: vi.fn(),
    listOrganizeItems: vi.fn(),
    cancelOrganizeOperation: vi.fn(),
    listOwnAlbums: vi.fn(),
}))

vi.mock('@/admin/organizeApi', () => ({
    ...api,
    applyOrganizeOperation: vi.fn(),
    discardOrganizeOperation: vi.fn(),
}))

vi.mock('@/admin/ownAlbumApi', () => ({
    listOwnAlbums: api.listOwnAlbums,
}))

const { default: AdminOrganizeView } = await import('../AdminOrganizeView.vue')

async function mountView() {
    const router = createRouter({ history: createMemoryHistory(), routes: createRoutes() })
    await router.push('/admin/organize')
    const wrapper = mount(AdminOrganizeView, {
        attachTo: document.body,
        global: { plugins: [router, createWaveMpI18n('en')] },
    })
    await flushPromises()
    return wrapper
}

function availability(
    statuses: string[],
    activeOperationId: string | null = null,
): OrganizeAvailability {
    return {
        activeOperationId,
        sources: statuses.map((status, index) => ({
            sourceId: `s${index}`,
            name: `Source ${index + 1}`,
            status,
        })),
    } as OrganizeAvailability
}

describe('AdminOrganizeView', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
        for (const mock of Object.values(api)) {
            mock.mockReset()
        }
        api.listOrganizeOperations.mockResolvedValue({ items: [], total: 0 })
        api.listOwnAlbums.mockResolvedValue({ items: [], total: 0 })
        api.getOrganizingSettings.mockResolvedValue({ layoutTemplate: '{Artist}/{Title}' })
        const dialogPrototype = HTMLDialogElement.prototype as Partial<HTMLDialogElement>
        dialogPrototype.showModal = function (this: HTMLDialogElement) {
            this.setAttribute('open', '')
        }
        dialogPrototype.close = function (this: HTMLDialogElement) {
            this.removeAttribute('open')
        }
    })

    afterEach(() => {
        const dialogPrototype = HTMLDialogElement.prototype as Partial<HTMLDialogElement>
        delete dialogPrototype.showModal
        delete dialogPrototype.close
        document.body.innerHTML = ''
    })

    it('offers own albums, the preview of the library and the log while the folder can be written', async () => {
        api.getOrganizeAvailability.mockResolvedValue(availability(['Available']))
        const wrapper = await mountView()

        expect(wrapper.get('[data-part="organize-availability"]').attributes('data-state')).toBe(
            'available',
        )
        expect(
            wrapper.find('[data-part="organize-availability"] [data-part="alert"]').exists(),
        ).toBe(false)
        expect(wrapper.findAll('[data-part="section-title"]').map((title) => title.text())).toEqual(
            ['Own albums', 'Reorganize the library', 'Log'],
        )
        expect(wrapper.get('[data-item="layout-template"] [data-part="path-segment"]').text()).toBe(
            '{Artist}/{Title}',
        )
        expect(
            wrapper.get('button[data-item="preview-library"]').attributes('disabled'),
        ).toBeUndefined()
        expect(wrapper.find('[data-field="organize-source"]').exists()).toBe(false)
        wrapper.unmount()
    })

    it.each([
        ['ReadOnly', 'read-only', 'read-only'],
        ['Missing', 'missing', 'does not exist'],
        ['Inactive', 'inactive', 'switched off'],
    ])('disables organizing with the reason for a %s media folder', async (status, state, text) => {
        api.getOrganizeAvailability.mockResolvedValue(availability([status]))
        const wrapper = await mountView()

        const notice = wrapper.get('[data-part="organize-availability"]')
        expect(notice.attributes('data-state')).toBe(state)
        expect(notice.get('[data-part="alert"]').text()).toContain(text)
        expect(
            wrapper.get('button[data-item="preview-library"]').attributes('disabled'),
        ).toBeDefined()
        wrapper.unmount()
    })

    it('is busy and shows the progress while files are moved', async () => {
        api.getOrganizeAvailability.mockResolvedValue(availability(['Available'], 'run'))
        api.getOrganizeOperation.mockResolvedValue({
            id: 'run',
            kind: 'Library',
            status: 'Running',
            totalItems: 10,
            doneItems: 4,
        })
        const wrapper = await mountView()

        expect(wrapper.get('[data-part="organize-availability"]').attributes('data-state')).toBe(
            'busy',
        )
        const progress = wrapper.get('[data-part="organize-progress"]')
        expect(progress.attributes('data-state')).toBe('running')
        expect(
            wrapper.get('button[data-item="preview-library"]').attributes('disabled'),
        ).toBeDefined()

        api.cancelOrganizeOperation.mockResolvedValue({ id: 'run', status: 'Running' })
        await progress.get('button[data-item="stop"]').trigger('click')
        await flushPromises()
        expect(api.cancelOrganizeOperation).toHaveBeenCalledWith('run')
        expect(progress.attributes('data-state')).toBe('running stopping')
        wrapper.unmount()
    })

    it('names the sources that cannot be organized and lets the admin choose one', async () => {
        api.getOrganizeAvailability.mockResolvedValue(availability(['Available', 'ReadOnly']))
        const wrapper = await mountView()

        expect(wrapper.get('[data-part="organize-availability"]').attributes('data-state')).toBe(
            'available',
        )
        expect(wrapper.get('[data-part="organize-availability"]').text()).toContain('Source 2')
        const select = wrapper.get('[data-field="organize-source"] select')
        const options = select.findAll('option')
        expect(options.map((option) => option.attributes('disabled'))).toEqual([
            undefined,
            undefined,
            '',
        ])
        wrapper.unmount()
    })

    it('reports an error while loading the availability', async () => {
        api.getOrganizeAvailability.mockRejectedValue(new Error('offline'))
        const wrapper = await mountView()

        expect(wrapper.get('[data-part="organize-availability"]').attributes('data-state')).toBe(
            'error',
        )
        expect(
            wrapper.find('[data-part="organize-availability"] [data-part="alert"]').exists(),
        ).toBe(true)
        wrapper.unmount()
    })

    it('opens the preview of the library once it is computed', async () => {
        vi.useFakeTimers()
        try {
            api.getOrganizeAvailability.mockResolvedValue(availability(['Available']))
            api.createOrganizePreview.mockResolvedValue({
                id: 'p',
                kind: 'Library',
                status: 'Planning',
            })
            api.getOrganizeOperation.mockResolvedValue({
                id: 'p',
                kind: 'Library',
                status: 'Previewed',
                totalItems: 0,
            })
            api.listOrganizeItems.mockResolvedValue({ items: [], total: 0 })
            const wrapper = await mountView()

            await wrapper.get('button[data-item="preview-library"]').trigger('click')
            await flushPromises()
            expect(wrapper.get('[data-part="organize-progress"]').attributes('data-state')).toBe(
                'planning',
            )

            await vi.advanceTimersByTimeAsync(1000)
            await flushPromises()

            const dialog = wrapper.get('dialog[data-item="organize-preview"]')
            expect(dialog.attributes('open')).toBeDefined()
            expect(api.listOrganizeItems).toHaveBeenCalledWith('p', {
                filter: 'All',
                offset: 0,
                limit: 100,
            })
            wrapper.unmount()
        } finally {
            vi.useRealTimers()
        }
    })
})
