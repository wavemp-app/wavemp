// Registry of the themes: the themes shipped with WaveMP and the themes admins uploaded. A theme is
// exactly one CSS file plus graphics; shipped themes live under /assets/web/themes/<id>/ and are
// served at /themes/<id>/theme.css, uploaded themes come from the API
// (/api/v1/themes/<id>/files/...). The theme is loaded with a <link> element; applyTheme()
// switches it without reloading the page. Each theme has one or two variants (light and dark in one
// CSS file); the root element tells the shown variant in data-theme-variant, so the CSS of the
// theme selects it with [data-theme-variant='dark']. The id of the theme of a profile is any
// string the API accepts, and an unknown id (e.g. of a deleted theme) shows the default theme.

import { shallowRef } from 'vue'
import { themeApiVersion } from './parts'

/** Variant of a theme: themes with both variants follow the appearance of the profile. */
export type ThemeVariant = 'light' | 'dark'

/** The appearance of a profile: which variant of a theme with both variants it sees. */
export type ThemeAppearance = 'system' | 'light' | 'dark'

export interface ThemeDefinition {
    readonly id: string
    /** Name of the theme; a proper name, the same in every language. */
    readonly name: string
    readonly stylesheet: string
    /** The variants of the theme; the first one is used where the appearance has no say. */
    readonly variants: readonly [ThemeVariant, ...ThemeVariant[]]
    /**
     * The preview pictures of an uploaded theme per variant. Shipped themes have none here; their
     * previews are /themes/<id>/preview-<variant>.webp.
     */
    readonly previews?: Readonly<Partial<Record<ThemeVariant, string>>>
    /** Whether an admin uploaded the theme. */
    readonly custom?: boolean
}

function builtIn(
    id: string,
    name: string,
    variants: readonly [ThemeVariant, ...ThemeVariant[]],
): ThemeDefinition {
    return { id, name, stylesheet: `/themes/${id}/theme.css`, variants }
}

export const themes = [
    builtIn('midnight', 'Midnight', ['dark']),
    builtIn('meadow', 'Meadow', ['light']),
    builtIn('daylight', 'Daylight', ['light']),
    builtIn('accent', 'Accent', ['light', 'dark']),
    builtIn('pinstripe', 'Pinstripe', ['light']),
    builtIn('clear-glass', 'Clear Glass', ['light', 'dark']),
] as const satisfies readonly ThemeDefinition[]

export type ThemeId = (typeof themes)[number]['id']

export const defaultThemeId: ThemeId = 'midnight'

export const defaultThemeAppearance: ThemeAppearance = 'system'

/** Id of the <link> element that loads the theme (set in index.html for the first paint). */
export const themeLinkId = 'wavemp-theme'

/** Media query of the dark mode of the device, which the appearance "system" follows. */
export const darkModeMediaQuery = '(prefers-color-scheme: dark)'

/**
 * The themes admins uploaded, after the shipped ones (see src/themes/custom.ts). Reactive, so the
 * theme of a profile applies as soon as the list is known; remembered for the first paint.
 */
export const customThemes = shallowRef<readonly ThemeDefinition[]>([])

/** Every theme a profile can choose: the shipped themes, then the uploaded ones. */
export function availableThemes(): readonly ThemeDefinition[] {
    return [...themes, ...customThemes.value]
}

/** Whether a theme with the id exists, shipped or uploaded. */
export function isThemeId(value: string): boolean {
    return availableThemes().some((theme) => theme.id === value)
}

/** Returns the given theme if it exists, otherwise the default theme. */
export function resolveThemeId(candidate?: string | null): string {
    return candidate && isThemeId(candidate) ? candidate : defaultThemeId
}

export function themeById(id: string): ThemeDefinition {
    return availableThemes().find((theme) => theme.id === id) ?? themes[0]
}

/** Whether the theme has a light and a dark variant, so the appearance of the profile applies. */
export function hasVariants(theme: ThemeDefinition): boolean {
    return theme.variants.length > 1
}

/**
 * The variant of a theme to show: the variant the appearance asks for if the theme has it ("system"
 * asks for the mode of the device), otherwise the first variant of the theme.
 */
export function resolveThemeVariant(
    theme: ThemeDefinition,
    appearance: ThemeAppearance,
    systemDark: boolean,
): ThemeVariant {
    const wanted: ThemeVariant =
        appearance === 'system' ? (systemDark ? 'dark' : 'light') : appearance
    return theme.variants.includes(wanted) ? wanted : theme.variants[0]
}

/** URL of the preview picture of a variant of a theme (shown on the settings page). */
export function themePreview(theme: ThemeDefinition, variant: ThemeVariant): string {
    if (theme.previews) {
        return theme.previews[variant] ?? theme.previews[theme.variants[0]] ?? ''
    }

    return `/themes/${theme.id}/preview-${variant}.webp`
}

/**
 * Activates a theme: sets data-theme, data-theme-variant and data-theme-api on <html> and points
 * the theme <link> at its stylesheet. There is always exactly one theme link. Without a variant
 * the first variant of the theme is shown.
 */
export function applyTheme(id: string, variant?: ThemeVariant, doc: Document = document): void {
    const theme = themeById(id)
    const shown = variant && theme.variants.includes(variant) ? variant : theme.variants[0]
    const root = doc.documentElement
    root.dataset.theme = theme.id
    root.dataset.themeVariant = shown
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
        // The theme color is known once the new stylesheet is loaded.
        link.addEventListener('load', () => syncThemeColor(doc), { once: true })
    } else if (link.sheet) {
        // Same stylesheet (also another variant of it): the color may have changed at once.
        syncThemeColor(doc)
    } else {
        link.addEventListener('load', () => syncThemeColor(doc), { once: true })
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
