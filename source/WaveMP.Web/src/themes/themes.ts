// Registry of the themes shipped with WaveMP. A theme is exactly one CSS file plus graphics under
// /assets/web/themes/<id>/, served at /themes/<id>/theme.css and loaded with a <link> element.
// Prepared for steps 30 and 31 (theme selection per user, uploaded themes): applyTheme() switches
// the theme without reloading the page.

import { themeApiVersion } from './parts'

export interface ThemeDefinition {
    readonly id: string
    readonly stylesheet: string
}

export const themes = [
    { id: 'midnight', stylesheet: '/themes/midnight/theme.css' },
] as const satisfies readonly ThemeDefinition[]

export type ThemeId = (typeof themes)[number]['id']

export const defaultThemeId: ThemeId = 'midnight'

/** Id of the <link> element that loads the theme (set in index.html for the first paint). */
export const themeLinkId = 'wavemp-theme'

export function isThemeId(value: string): value is ThemeId {
    return themes.some((theme) => theme.id === value)
}

/** Returns the given theme if it exists, otherwise the default theme. */
export function resolveThemeId(candidate?: string | null): ThemeId {
    return candidate && isThemeId(candidate) ? candidate : defaultThemeId
}

/**
 * Activates a theme: sets data-theme and data-theme-api on <html> and points the theme <link>
 * at its stylesheet. There is always exactly one theme link.
 */
export function applyTheme(id: ThemeId, doc: Document = document): void {
    const theme = themes.find((candidate) => candidate.id === id) ?? themes[0]
    const root = doc.documentElement
    root.dataset.theme = theme.id
    root.dataset.themeApi = String(themeApiVersion)

    const links = [...doc.querySelectorAll<HTMLLinkElement>(`link#${themeLinkId}`)]
    let link = links.shift()
    for (const duplicate of links) {
        duplicate.remove()
    }

    if (!link) {
        link = doc.createElement('link')
        link.id = themeLinkId
        link.rel = 'stylesheet'
        doc.head.append(link)
    }

    if (link.getAttribute('href') !== theme.stylesheet) {
        link.setAttribute('href', theme.stylesheet)
    }

    // The theme color is known once the stylesheet is loaded; it may already be (index.html).
    link.addEventListener('load', () => syncThemeColor(doc), { once: true })
    if (link.sheet) {
        syncThemeColor(doc)
    }
}

/** Name of the meta element browsers color their bars and the status bar with. */
export const themeColorMetaName = 'theme-color'

/** Variable of the theme API with the theme color (THEMING.md, section "Areas"). */
export const themeColorVariable = '--wmp-theme-color'

/**
 * Hands the theme color of the active theme (--wmp-theme-color) to the browser: <meta
 * name="theme-color"> colors the status bar and the bars of the browser, so they blend into the
 * header. Keeps the current value while the theme sets none.
 */
export function syncThemeColor(doc: Document = document): void {
    const view = doc.defaultView
    if (!view) {
        return
    }

    const color = view
        .getComputedStyle(doc.documentElement)
        .getPropertyValue(themeColorVariable)
        .trim()
    if (color === '') {
        return
    }

    let meta = doc.head.querySelector<HTMLMetaElement>(`meta[name="${themeColorMetaName}"]`)
    if (!meta) {
        meta = doc.createElement('meta')
        meta.name = themeColorMetaName
        doc.head.append(meta)
    }

    if (meta.content !== color) {
        meta.content = color
    }
}
