import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { themeApiVersion } from '../parts'
import {
    applyTheme,
    defaultThemeId,
    hasVariants,
    resolveThemeId,
    resolveThemeVariant,
    themeById,
    themePreview,
    syncThemeColor,
    themeColorVariable,
    themeLinkId,
    themes,
} from '../themes'

describe('resolveThemeId', () => {
    it('returns known themes', () => {
        expect(resolveThemeId('midnight')).toBe('midnight')
    })

    it('falls back to the default theme', () => {
        expect(defaultThemeId).toBe('midnight')
        expect(resolveThemeId('unknown')).toBe('midnight')
        expect(resolveThemeId('')).toBe('midnight')
        expect(resolveThemeId(null)).toBe('midnight')
        expect(resolveThemeId(undefined)).toBe('midnight')
    })

    it('knows the shipped themes', () => {
        expect(themes.map((theme) => theme.id)).toEqual([
            'midnight',
            'meadow',
            'daylight',
            'accent',
            'pinstripe',
            'clear-glass',
        ])
        expect(resolveThemeId('clear-glass')).toBe('clear-glass')
        expect(new Set(themes.map((theme) => theme.name)).size).toBe(themes.length)
    })

    it('knows a stylesheet for every theme', () => {
        for (const theme of themes) {
            expect(theme.stylesheet).toBe(`/themes/${theme.id}/theme.css`)
        }
    })
})

describe('resolveThemeVariant', () => {
    const accent = themeById('accent')
    const midnight = themeById('midnight')

    it('follows the mode of the device for the appearance system', () => {
        expect(resolveThemeVariant(accent, 'system', true)).toBe('dark')
        expect(resolveThemeVariant(accent, 'system', false)).toBe('light')
    })

    it('shows the chosen variant of a theme with both variants', () => {
        expect(resolveThemeVariant(accent, 'light', true)).toBe('light')
        expect(resolveThemeVariant(accent, 'dark', false)).toBe('dark')
    })

    it('shows the only variant of a theme with one variant', () => {
        expect(hasVariants(midnight)).toBe(false)
        expect(resolveThemeVariant(midnight, 'light', false)).toBe('dark')
        expect(resolveThemeVariant(midnight, 'system', false)).toBe('dark')
        expect(resolveThemeVariant(themeById('daylight'), 'dark', true)).toBe('light')
    })

    it('knows a preview picture per variant', () => {
        expect(themePreview(accent, 'dark')).toBe('/themes/accent/preview-dark.webp')
        expect(themeById('unknown').id).toBe('midnight')
    })
})

describe('applyTheme', () => {
    let doc: Document

    beforeEach(() => {
        doc = document.implementation.createHTMLDocument('test')
    })

    function themeLinks(): HTMLLinkElement[] {
        return [...doc.querySelectorAll<HTMLLinkElement>(`link#${themeLinkId}`)]
    }

    it('sets the theme and the theme API version on the root element', () => {
        applyTheme('midnight', undefined, doc)

        expect(doc.documentElement.dataset.theme).toBe('midnight')
        expect(doc.documentElement.dataset.themeApi).toBe(String(themeApiVersion))
    })

    it('sets the variant of the theme on the root element', () => {
        applyTheme('accent', 'dark', doc)
        expect(doc.documentElement.dataset.theme).toBe('accent')
        expect(doc.documentElement.dataset.themeVariant).toBe('dark')

        applyTheme('accent', 'light', doc)
        expect(doc.documentElement.dataset.themeVariant).toBe('light')

        // A variant the theme does not have, or none, shows its first variant.
        applyTheme('midnight', 'light', doc)
        expect(doc.documentElement.dataset.themeVariant).toBe('dark')
        applyTheme('clear-glass', undefined, doc)
        expect(doc.documentElement.dataset.themeVariant).toBe('light')
    })

    it('shows the default theme for an unknown id', () => {
        applyTheme('uploaded-1', 'dark', doc)

        expect(doc.documentElement.dataset.theme).toBe('midnight')
        expect(themeLinks()[0]?.getAttribute('href')).toBe('/themes/midnight/theme.css')
    })

    it('switches the stylesheet without a second link', () => {
        applyTheme('midnight', undefined, doc)
        applyTheme('pinstripe', undefined, doc)

        const links = themeLinks()
        expect(links).toHaveLength(1)
        expect(links[0]?.getAttribute('href')).toBe('/themes/pinstripe/theme.css')
    })

    it('creates the theme link if it is missing', () => {
        applyTheme('midnight', undefined, doc)

        const links = themeLinks()
        expect(links).toHaveLength(1)
        expect(links[0]?.rel).toBe('stylesheet')
        expect(links[0]?.getAttribute('href')).toBe('/themes/midnight/theme.css')
    })

    it('updates an existing link and keeps exactly one', () => {
        for (const href of ['/themes/old/theme.css', '/themes/other/theme.css']) {
            const link = doc.createElement('link')
            link.id = themeLinkId
            link.rel = 'stylesheet'
            link.href = href
            doc.head.append(link)
        }

        applyTheme('midnight', undefined, doc)

        const links = themeLinks()
        expect(links).toHaveLength(1)
        expect(links[0]?.getAttribute('href')).toBe('/themes/midnight/theme.css')
    })
})

describe('syncThemeColor', () => {
    afterEach(() => {
        document.documentElement.style.removeProperty(themeColorVariable)
        document.head.querySelectorAll('meta[name="theme-color"]').forEach((meta) => meta.remove())
    })

    function themeColorMeta(): HTMLMetaElement | null {
        return document.head.querySelector<HTMLMetaElement>('meta[name="theme-color"]')
    }

    it('hands the theme color of the theme to the browser', () => {
        const meta = document.createElement('meta')
        meta.name = 'theme-color'
        meta.content = '#000000'
        document.head.append(meta)
        document.documentElement.style.setProperty(themeColorVariable, '#43484f')

        syncThemeColor(document)

        expect(themeColorMeta()?.content).toBe('#43484f')
    })

    it('creates the meta element if it is missing', () => {
        document.documentElement.style.setProperty(themeColorVariable, 'rgb(1 2 3)')

        syncThemeColor(document)

        expect(themeColorMeta()?.content).toBe('rgb(1 2 3)')
    })

    it('keeps the current color while the theme sets none', () => {
        const meta = document.createElement('meta')
        meta.name = 'theme-color'
        meta.content = '#43484f'
        document.head.append(meta)

        syncThemeColor(document)

        expect(themeColorMeta()?.content).toBe('#43484f')
    })
})
