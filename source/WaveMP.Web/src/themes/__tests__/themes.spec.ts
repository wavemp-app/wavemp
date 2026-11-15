import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { themeApiVersion } from '../parts'
import {
    applyTheme,
    defaultThemeId,
    resolveThemeId,
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

    it('knows a stylesheet for every theme', () => {
        for (const theme of themes) {
            expect(theme.stylesheet).toBe(`/themes/${theme.id}/theme.css`)
        }
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
        applyTheme('midnight', doc)

        expect(doc.documentElement.dataset.theme).toBe('midnight')
        expect(doc.documentElement.dataset.themeApi).toBe(String(themeApiVersion))
    })

    it('creates the theme link if it is missing', () => {
        applyTheme('midnight', doc)

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

        applyTheme('midnight', doc)

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
