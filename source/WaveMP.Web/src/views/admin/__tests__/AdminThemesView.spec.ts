import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import AdminThemesView from '../AdminThemesView.vue'
import type { Theme } from '@/api/generated/models'
import { createWaveMpI18n } from '@/i18n'
import { createRoutes } from '@/router/routes'
import { useThemesStore, type ThemeUploadResult } from '@/stores/themes'

const file = (digit: string, extension: string) =>
    `/api/v1/themes/aurora/files/${digit.repeat(32)}.${extension}`

const aurora: Theme = {
    id: 'aurora',
    name: 'Aurora',
    author: 'Someone',
    version: '1.2.0',
    themeApi: 5,
    variants: ['Light', 'Dark'],
    stylesheetUrl: file('a', 'css'),
    previews: [
        { variant: 'Light', url: file('b', 'png') },
        { variant: 'Dark', url: file('c', 'png') },
    ],
    sizeBytes: 2_000_000,
    installedAt: new Date('2026-11-18T12:00:00Z'),
    status: 'Current',
    problems: [],
}

async function mountView(themes: Theme[] | null = [aurora]) {
    const router = createRouter({ history: createMemoryHistory(), routes: createRoutes() })
    await router.push('/admin/themes')
    const store = useThemesStore()
    const load = vi.spyOn(store, 'load').mockImplementation(async () => {
        store.items = themes ?? []
        store.status = themes ? 'ready' : 'error'
    })
    const install = vi.spyOn(store, 'install')
    const replace = vi.spyOn(store, 'replace')
    const remove = vi.spyOn(store, 'remove')
    const wrapper = mount(AdminThemesView, {
        global: { plugins: [router, createWaveMpI18n('en')] },
        attachTo: document.body,
    })
    await flushPromises()
    return { wrapper, load, install, replace, remove }
}

/** Picks a file in the hidden file input, as the browser does after the button opened it. */
async function pickFile(wrapper: Awaited<ReturnType<typeof mountView>>['wrapper']): Promise<void> {
    const input = wrapper.get('input[type="file"]')
    const zip = new File([new Uint8Array([0x50, 0x4b, 3, 4])], 'aurora.zip', {
        type: 'application/zip',
    })
    Object.defineProperty(input.element, 'files', { value: [zip], configurable: true })
    await input.trigger('change')
    await flushPromises()
}

describe('AdminThemesView', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
    })

    it('shows the upload, the template and the uploaded themes', async () => {
        const { wrapper } = await mountView()

        expect(wrapper.get('[data-part="view"]').attributes('data-view')).toBe('admin-themes')
        expect(wrapper.get('[data-part="admin-nav-item"][aria-current="page"]').text()).toBe(
            'Themes',
        )
        expect(
            wrapper.findAll('section[data-part="section"]').map((s) => s.attributes('data-item')),
        ).toEqual(['theme-upload', 'themes'])
        const template = wrapper.get('[data-item="theme-template"]')
        expect(template.attributes('href')).toBe('/api/v1/theme-template')
        expect(template.attributes('download')).toBeDefined()
        expect(wrapper.get('input[type="file"]').attributes('accept')).toContain('.zip')

        const item = wrapper.get('[data-part="theme-item"]')
        expect(item.attributes('data-item')).toBe('aurora')
        expect(item.attributes('data-variants')).toBe('light dark')
        expect(item.get('[data-part="theme-name"]').text()).toBe('Aurora')
        expect(item.get('[data-part="theme-meta"]').text()).toContain('by Someone')
        expect(item.get('[data-part="theme-meta"]').text()).toContain('Version 1.2.0')
        expect(item.attributes('data-state')).toBeUndefined()
        expect(item.find('[data-part="badge"]').exists()).toBe(false)
        expect(item.find('[data-part="theme-problems"]').exists()).toBe(false)
        expect(item.get('[data-part="theme-preview-image"]').attributes('src')).toBe(
            file('b', 'png'),
        )
        wrapper.unmount()
    })

    it('marks an outdated theme and lists its problems', async () => {
        const outdated: Theme = {
            ...aurora,
            status: 'Outdated',
            problems: [
                { code: 'svg.script', path: 'images/glass.svg' },
                { code: 'css.import', path: 'theme.css', line: 12 },
            ],
        }
        const { wrapper } = await mountView([outdated])

        const first = wrapper.find('[data-part="theme-item"]')
        expect(first.attributes('data-state')).toBe('outdated')
        expect(first.get('[data-part="badge"][data-state="outdated"]').text()).toBe('Outdated')
        const problems = first.get('[data-part="theme-problems"]')
        expect(problems.get('[data-part="form-note"]').text()).toContain(
            'checks themes more strictly',
        )
        expect(problems.get('[data-part="theme-errors"]').attributes('aria-label')).toBe(
            'Problems of Aurora',
        )
        const errors = problems.findAll('[data-part="theme-error"]')
        expect(errors.map((error) => error.attributes('data-item'))).toEqual([
            'svg.script',
            'css.import',
        ])
        expect(errors[1]!.get('[data-part="theme-error-location"]').text()).toBe(
            'theme.css, line 12',
        )

        wrapper.unmount()
    })

    it('shows an empty state without uploaded themes', async () => {
        const { wrapper } = await mountView([])

        expect(wrapper.find('[data-part="theme-list"]').exists()).toBe(false)
        expect(wrapper.get('[data-part="empty-state-text"]').text()).toContain(
            'No themes have been uploaded yet.',
        )
        wrapper.unmount()
    })

    it('loads the themes again after an error', async () => {
        const { wrapper, load } = await mountView(null)

        expect(wrapper.get('[data-part="alert"]').text()).toBe('The themes could not be loaded.')
        await wrapper.get('[data-item="retry"]').trigger('click')

        expect(load).toHaveBeenCalledTimes(2)
        wrapper.unmount()
    })

    it('installs a picked package', async () => {
        const { wrapper, install } = await mountView()
        install.mockResolvedValue({ status: 'installed', theme: aurora })

        await pickFile(wrapper)

        expect(install).toHaveBeenCalledTimes(1)
        expect(install.mock.calls[0]![0]).toBeInstanceOf(ArrayBuffer)
        expect(wrapper.get('[data-part="alert"]').text()).toBe(
            'The theme Aurora is installed. Every profile can now choose it in the settings.',
        )
        wrapper.unmount()
    })

    it('lists every reason of a rejected package', async () => {
        const { wrapper, install } = await mountView()
        const result: ThemeUploadResult = {
            status: 'invalid',
            errors: [
                { code: 'css.external_url', path: 'theme.css', line: 12 },
                { code: 'package.too_large', max: 33_554_432 },
                {
                    code: 'manifest.unsupported_api_version',
                    path: 'manifest.json',
                    field: 'themeApi',
                    min: 5,
                    max: 5,
                },
            ],
        }
        install.mockResolvedValue(result)

        await pickFile(wrapper)

        const errors = wrapper.findAll('[data-part="theme-error"]')
        expect(errors.map((error) => error.attributes('data-item'))).toEqual([
            'css.external_url',
            'package.too_large',
            'manifest.unsupported_api_version',
        ])
        expect(errors[0]!.get('[data-part="theme-error-location"]').text()).toBe(
            'theme.css, line 12',
        )
        expect(errors[0]!.get('[data-part="theme-error-message"]').text()).toBe(
            'Themes cannot load anything from other servers or from data: addresses.',
        )
        expect(errors[1]!.find('[data-part="theme-error-location"]').exists()).toBe(false)
        expect(errors[1]!.text()).toContain('33.6 MB')
        expect(errors[2]!.text()).toContain('versions 5 to 5')
        wrapper.unmount()
    })

    it('uploads a new version of a theme', async () => {
        const { wrapper, replace } = await mountView()
        replace.mockResolvedValue({ status: 'replaced', theme: aurora })
        const click = vi.spyOn(HTMLInputElement.prototype, 'click').mockImplementation(() => {})

        await wrapper.get('[data-part="theme-actions"] [data-item="replace"]').trigger('click')
        await pickFile(wrapper)

        expect(click).toHaveBeenCalled()
        expect(replace).toHaveBeenCalledWith('aurora', expect.any(ArrayBuffer))
        expect(wrapper.get('[data-part="alert"]').text()).toContain('The theme Aurora is updated.')
        click.mockRestore()
        wrapper.unmount()
    })

    it.each([
        ['tooLarge', 'The file is larger than the server allows.'],
        ['unsupportedFormat', 'The file is no ZIP file.'],
        ['error', 'The theme could not be uploaded.'],
    ] as const)('tells why an upload failed: %s', async (status, text) => {
        const { wrapper, install } = await mountView()
        install.mockResolvedValue({ status })

        await pickFile(wrapper)

        expect(wrapper.get('[data-part="alert"]').text()).toBe(text)
        wrapper.unmount()
    })

    it('deletes a theme after asking', async () => {
        const { wrapper, remove } = await mountView()
        remove.mockResolvedValue('deleted')

        await wrapper.get('[data-part="theme-actions"] [data-item="delete"]').trigger('click')
        expect(wrapper.get('[data-part="dialog-title"]').text()).toBe('Delete theme')
        const confirm = wrapper
            .findAll('[data-part="dialog-footer"] button')
            .find((button) => button.text() === 'Delete')
        await confirm!.trigger('click')
        await flushPromises()

        expect(remove).toHaveBeenCalledWith('aurora')
        expect(wrapper.get('[data-part="alert"]').text()).toBe(
            'The theme is deleted. Profiles that used it now see the default theme.',
        )
        wrapper.unmount()
    })
})
