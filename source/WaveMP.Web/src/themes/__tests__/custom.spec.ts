import { afterEach, describe, expect, it } from 'vitest'
import type { Theme } from '@/api/generated/models'
import { shownTheme } from '@/composables/useThemeSync'
import {
    customThemesStorageKey,
    parseCustomThemes,
    readCustomThemes,
    toThemeDefinition,
    toThemeDefinitions,
    writeCustomThemes,
} from '../custom'
import {
    applyTheme,
    availableThemes,
    customThemes,
    isThemeId,
    resolveThemeId,
    themeById,
    themePreview,
    themes,
} from '../themes'

const file = (id: string, digit: string, extension: string) =>
    `/api/v1/themes/${id}/files/${digit.repeat(32)}.${extension}`

const aurora: Theme = {
    id: 'aurora',
    name: 'Aurora',
    author: 'Someone',
    version: '1.0.0',
    themeApi: 5,
    variants: ['Light', 'Dark'],
    stylesheetUrl: file('aurora', 'a', 'css'),
    previews: [
        { variant: 'Light', url: file('aurora', 'b', 'png') },
        { variant: 'Dark', url: file('aurora', 'c', 'svg') },
    ],
    sizeBytes: 1000,
}

class MemoryStorage implements Storage {
    private values = new Map<string, string>()

    get length(): number {
        return this.values.size
    }

    clear(): void {
        this.values.clear()
    }

    getItem(key: string): string | null {
        return this.values.get(key) ?? null
    }

    key(index: number): string | null {
        return [...this.values.keys()][index] ?? null
    }

    removeItem(key: string): void {
        this.values.delete(key)
    }

    setItem(key: string, value: string): void {
        this.values.set(key, value)
    }
}

class FailingStorage extends MemoryStorage {
    override getItem(): string | null {
        throw new DOMException('denied', 'SecurityError')
    }

    override setItem(): void {
        throw new DOMException('full', 'QuotaExceededError')
    }
}

describe('uploaded themes', () => {
    afterEach(() => {
        customThemes.value = []
    })

    it('become entries of the registry with their files', () => {
        expect(toThemeDefinition(aurora)).toEqual({
            id: 'aurora',
            name: 'Aurora',
            stylesheet: file('aurora', 'a', 'css'),
            variants: ['light', 'dark'],
            previews: { light: file('aurora', 'b', 'png'), dark: file('aurora', 'c', 'svg') },
            custom: true,
        })
    })

    it.each([
        ['an external stylesheet', { stylesheetUrl: 'https://example.com/theme.css' }],
        ['a stylesheet of another theme', { stylesheetUrl: file('borealis', 'a', 'css') }],
        ['a picture as stylesheet', { stylesheetUrl: file('aurora', 'a', 'svg') }],
        ['a protocol-relative stylesheet', { stylesheetUrl: '//example.com/a.css' }],
        [
            'the id of a shipped theme',
            { id: 'midnight', stylesheetUrl: file('midnight', 'a', 'css') },
        ],
        ['an invalid id', { id: 'Aurora', stylesheetUrl: file('Aurora', 'a', 'css') }],
        ['no variant', { variants: [] }],
        ['a variant twice', { variants: ['Dark', 'Dark'] }],
        ['an empty name', { name: ' ' }],
    ])('are left out with %s', (_, change) => {
        expect(toThemeDefinition({ ...aurora, ...(change as Partial<Theme>) })).toBeNull()
    })

    it('keep only previews that are pictures of the theme', () => {
        const definition = toThemeDefinition({
            ...aurora,
            previews: [
                { variant: 'Light', url: 'https://example.com/preview.png' },
                { variant: 'Dark', url: file('aurora', 'c', 'css') },
            ],
        })

        expect(definition?.previews).toEqual({})
    })

    it('are listed once per id', () => {
        expect(
            toThemeDefinitions([aurora, { ...aurora, name: 'Again' }]).map((t) => t.name),
        ).toEqual(['Aurora'])
    })

    it('are left out of the registry while the server blocks them', () => {
        const blocked: Theme = { ...aurora, status: 'Blocked' }

        customThemes.value = toThemeDefinitions([
            blocked,
            {
                ...aurora,
                id: 'borealis',
                stylesheetUrl: file('borealis', 'a', 'css'),
                status: 'Outdated',
            },
        ])

        expect(customThemes.value.map((theme) => theme.id)).toEqual(['borealis'])
        expect(isThemeId('aurora')).toBe(false)
        expect(resolveThemeId('aurora')).toBe('midnight')
        // The administration still shows the blocked theme with its problems.
        expect(toThemeDefinition(blocked)?.id).toBe('aurora')
    })

    it('can be chosen once they are known', () => {
        expect(isThemeId('aurora')).toBe(false)
        expect(resolveThemeId('aurora')).toBe('midnight')

        customThemes.value = toThemeDefinitions([aurora])

        expect(isThemeId('aurora')).toBe(true)
        expect(resolveThemeId('aurora')).toBe('aurora')
        expect(themeById('aurora').stylesheet).toBe(file('aurora', 'a', 'css'))
        expect(availableThemes().map((theme) => theme.id)).toEqual([
            ...themes.map((theme) => theme.id),
            'aurora',
        ])
        expect(shownTheme({ id: 'aurora', appearance: 'dark' }, false, '')).toEqual({
            id: 'aurora',
            variant: 'dark',
        })
    })

    it('show their own previews', () => {
        const [definition] = toThemeDefinitions([aurora])

        expect(themePreview(definition!, 'dark')).toBe(file('aurora', 'c', 'svg'))
        expect(themePreview(themeById('accent'), 'dark')).toBe('/themes/accent/preview-dark.webp')
    })

    it('are loaded through the theme link', () => {
        customThemes.value = toThemeDefinitions([aurora])
        document.head.innerHTML = ''

        applyTheme('aurora', 'light')

        expect(document.documentElement.dataset.theme).toBe('aurora')
        expect(document.documentElement.dataset.themeVariant).toBe('light')
        expect(document.getElementById('wavemp-theme')?.getAttribute('href')).toBe(
            file('aurora', 'a', 'css'),
        )
    })

    it('are remembered for the first paint', () => {
        const storage = new MemoryStorage()
        const definitions = toThemeDefinitions([aurora])

        writeCustomThemes(definitions, storage)

        expect(readCustomThemes(storage)).toEqual(definitions)
        writeCustomThemes([], storage)
        expect(storage.getItem(customThemesStorageKey)).toBeNull()
    })

    it('ignore invalid or tampered remembered entries', () => {
        expect(
            parseCustomThemes([
                {
                    id: 'aurora',
                    name: 'Aurora',
                    stylesheet: 'https://example.com/evil.css',
                    variants: ['light'],
                },
                {
                    id: 'night',
                    name: 'Night',
                    stylesheet: file('night', 'd', 'css'),
                    variants: ['dark'],
                },
                'garbage',
            ]),
        ).toEqual([
            {
                id: 'night',
                name: 'Night',
                stylesheet: file('night', 'd', 'css'),
                variants: ['dark'],
                previews: {},
                custom: true,
            },
        ])
        expect(parseCustomThemes({ id: 'aurora' })).toEqual([])
    })

    it('work without storage', () => {
        const storage = new FailingStorage()

        expect(() => writeCustomThemes(toThemeDefinitions([aurora]), storage)).not.toThrow()
        expect(readCustomThemes(storage)).toEqual([])
        expect(readCustomThemes(null)).toEqual([])
    })
})
