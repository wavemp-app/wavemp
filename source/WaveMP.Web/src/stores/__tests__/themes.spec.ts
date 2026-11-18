import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { customThemesStorageKey } from '@/themes/custom'
import { customThemes, themeById } from '@/themes/themes'

const api = vi.hoisted(() => ({
    list: vi.fn(),
    install: vi.fn(),
    replace: vi.fn(),
    remove: vi.fn(),
    themeId: vi.fn(),
}))

vi.mock('@/api/client', () => ({
    apiClient: {
        api: {
            v1: {
                themes: {
                    get: (config: unknown) => api.list(config),
                    post: (body: ArrayBuffer) => api.install(body),
                    byId: (id: string) => {
                        api.themeId(id)
                        return {
                            put: (body: ArrayBuffer) => api.replace(body),
                            delete: () => api.remove(),
                        }
                    },
                },
            },
        },
    },
}))

const { useThemesStore } = await import('../themes')

const stylesheet = `/api/v1/themes/aurora/files/${'a'.repeat(32)}.css`
const theme = {
    id: 'aurora',
    name: 'Aurora',
    author: 'Someone',
    version: '1.0.0',
    themeApi: 5,
    variants: ['Dark'],
    stylesheetUrl: stylesheet,
    previews: [],
    sizeBytes: 100,
}

describe('themes store', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
        for (const mock of Object.values(api)) {
            mock.mockReset()
        }
        api.list.mockResolvedValue({ items: [theme], total: 1 })
        localStorage.clear()
    })

    afterEach(() => {
        customThemes.value = []
    })

    it('loads the uploaded themes into the registry and remembers them', async () => {
        const store = useThemesStore()

        await store.load()

        expect(store.status).toBe('ready')
        expect(store.items).toEqual([theme])
        expect(themeById('aurora').stylesheet).toBe(stylesheet)
        expect(JSON.parse(localStorage.getItem(customThemesStorageKey) ?? '[]')).toEqual([
            {
                id: 'aurora',
                name: 'Aurora',
                stylesheet,
                variants: ['dark'],
                previews: {},
            },
        ])
    })

    it('keeps the registry when the list cannot be loaded', async () => {
        const store = useThemesStore()
        await store.load()
        api.list.mockRejectedValue(new Error('offline'))

        await store.load()

        expect(store.status).toBe('error')
        expect(themeById('aurora').id).toBe('aurora')
    })

    it('installs a package and loads the list again', async () => {
        api.install.mockResolvedValue(theme)
        const store = useThemesStore()
        const content = new ArrayBuffer(4)

        const result = await store.install(content)

        expect(api.install).toHaveBeenCalledWith(content)
        expect(result).toEqual({ status: 'installed', theme })
        expect(api.list).toHaveBeenCalled()
    })

    it('replaces a theme with a new version', async () => {
        api.replace.mockResolvedValue(theme)
        const store = useThemesStore()

        const result = await store.replace('aurora', new ArrayBuffer(4))

        expect(api.themeId).toHaveBeenCalledWith('aurora')
        expect(result).toEqual({ status: 'replaced', theme })
    })

    it('returns the reasons of a rejected package', async () => {
        api.install.mockRejectedValue({
            code: 'theme.invalid',
            errors: [{ code: 'svg.script', path: 'images/a.svg' }],
        })
        const store = useThemesStore()

        const result = await store.install(new ArrayBuffer(4))

        expect(result).toEqual({
            status: 'invalid',
            errors: [{ code: 'svg.script', path: 'images/a.svg' }],
        })
    })

    it.each([
        ['theme.too_large', 'tooLarge'],
        ['theme.unsupported_format', 'unsupportedFormat'],
        ['theme.already_installed', 'alreadyInstalled'],
        ['theme.not_found', 'notFound'],
        ['server.error', 'error'],
    ])('maps the problem %s', async (code, status) => {
        api.install.mockRejectedValue({ code })
        const store = useThemesStore()

        expect(await store.install(new ArrayBuffer(4))).toEqual({ status })
    })

    it('deletes a theme and loads the list again', async () => {
        api.remove.mockResolvedValue(undefined)
        api.list.mockResolvedValue({ items: [], total: 0 })
        const store = useThemesStore()

        expect(await store.remove('aurora')).toBe('deleted')
        expect(api.themeId).toHaveBeenCalledWith('aurora')
        expect(store.items).toEqual([])
        expect(themeById('aurora').id).toBe('midnight')
        expect(localStorage.getItem(customThemesStorageKey)).toBeNull()

        api.remove.mockRejectedValue({ code: 'theme.not_found' })
        expect(await store.remove('aurora')).toBe('notFound')
        api.remove.mockRejectedValue(new Error('offline'))
        expect(await store.remove('aurora')).toBe('error')
    })
})
