// The themes admins uploaded (GET /api/v1/themes) as entries of the theme registry. Their files are
// served by the server without a session at addresses named after their content. The list is
// remembered in localStorage, so a profile with an uploaded theme sees it from the first paint;
// the server stays the source of truth and the list is loaded again after a profile is selected.
// Only addresses of theme files of the server itself are taken, from the API and from storage.
// Themes the server blocked (they break a security rule of its version) are not taken.

import { ThemeStatusObject, ThemeVariantObject, type Theme } from '@/api/generated/models'
import { isValidThemeId } from './choice'
import { themes as shippedThemes, type ThemeDefinition, type ThemeVariant } from './themes'

/** Key in localStorage; the version changes if the stored format changes. */
export const customThemesStorageKey = 'wavemp.customThemes.v1'

/** Address of the template for own themes (a ZIP file, see THEMING.md). */
export const themeTemplateUrl = '/api/v1/theme-template'

/** The address of a file of an uploaded theme, as the server names it. */
const fileAddressPattern =
    /^\/api\/v1\/themes\/([a-z0-9-]+)\/files\/[0-9a-f]{32}\.(css|svg|png|webp)$/

const shippedIds = new Set<string>(shippedThemes.map((theme) => theme.id))

function defaultStorage(): Storage | null {
    try {
        return window.localStorage
    } catch {
        return null
    }
}

function isRecord(value: unknown): value is Record<string, unknown> {
    return typeof value === 'object' && value !== null
}

/** Whether the value is the address of a file of the theme with the extension. */
function isFileOf(value: unknown, id: string, extensions: readonly string[]): value is string {
    if (typeof value !== 'string') {
        return false
    }

    const match = fileAddressPattern.exec(value)
    return match?.[1] === id && extensions.includes(match[2] ?? '')
}

function isVariant(value: unknown): value is ThemeVariant {
    return value === 'light' || value === 'dark'
}

/** The variants as the registry takes them: one or two different ones, or null. */
function toVariants(values: readonly unknown[]): readonly [ThemeVariant, ...ThemeVariant[]] | null {
    const [first, ...rest] = values
    if (!isVariant(first) || values.length > 2 || !rest.every(isVariant)) {
        return null
    }

    return new Set(values).size === values.length ? [first, ...rest] : null
}

function fromApiVariant(value: unknown): ThemeVariant | null {
    if (value === ThemeVariantObject.Light) {
        return 'light'
    }

    return value === ThemeVariantObject.Dark ? 'dark' : null
}

/**
 * Checks a theme for the registry: a valid id that is not one of a shipped theme, a name, the CSS
 * file and the previews as addresses of files of this theme. Returns null otherwise.
 */
function toDefinition(
    id: unknown,
    name: unknown,
    stylesheet: unknown,
    variants: readonly unknown[],
    previews: Readonly<Record<string, unknown>>,
): ThemeDefinition | null {
    const checkedVariants = toVariants(variants)
    if (
        !isValidThemeId(id) ||
        shippedIds.has(id) ||
        typeof name !== 'string' ||
        name.trim() === '' ||
        !isFileOf(stylesheet, id, ['css']) ||
        checkedVariants === null
    ) {
        return null
    }

    const checkedPreviews: Partial<Record<ThemeVariant, string>> = {}
    for (const variant of checkedVariants) {
        const preview = previews[variant]
        if (isFileOf(preview, id, ['svg', 'png', 'webp'])) {
            checkedPreviews[variant] = preview
        }
    }

    return {
        id,
        name,
        stylesheet,
        variants: checkedVariants,
        previews: checkedPreviews,
        custom: true,
    }
}

/** An uploaded theme of the API as entry of the registry, or null if the answer is not valid. */
export function toThemeDefinition(theme: Theme): ThemeDefinition | null {
    const previews: Record<string, unknown> = {}
    for (const preview of theme.previews ?? []) {
        const variant = fromApiVariant(preview.variant)
        if (variant) {
            previews[variant] = preview.url
        }
    }

    return toDefinition(
        theme.id,
        theme.name,
        theme.stylesheetUrl,
        (theme.variants ?? []).map(fromApiVariant),
        previews,
    )
}

/** Whether the server blocked the theme because it breaks a security rule; it serves no files then. */
export function isBlockedTheme(theme: Theme): boolean {
    return theme.status === ThemeStatusObject.Blocked
}

/**
 * The valid uploaded themes of an answer of the API, each id once. Blocked themes are left out, so
 * they cannot be chosen and profiles that chose one see the default theme.
 */
export function toThemeDefinitions(list: readonly Theme[]): ThemeDefinition[] {
    const definitions: ThemeDefinition[] = []
    for (const theme of list) {
        const definition = isBlockedTheme(theme) ? null : toThemeDefinition(theme)
        if (definition && !definitions.some((known) => known.id === definition.id)) {
            definitions.push(definition)
        }
    }

    return definitions
}

/** The remembered uploaded themes; invalid entries are left out. */
export function parseCustomThemes(value: unknown): ThemeDefinition[] {
    if (!Array.isArray(value)) {
        return []
    }

    const definitions: ThemeDefinition[] = []
    for (const item of value) {
        if (!isRecord(item) || !Array.isArray(item.variants)) {
            continue
        }

        const definition = toDefinition(
            item.id,
            item.name,
            item.stylesheet,
            item.variants,
            isRecord(item.previews) ? item.previews : {},
        )
        if (definition && !definitions.some((known) => known.id === definition.id)) {
            definitions.push(definition)
        }
    }

    return definitions
}

export function readCustomThemes(storage: Storage | null = defaultStorage()): ThemeDefinition[] {
    try {
        const text = storage?.getItem(customThemesStorageKey)
        return text ? parseCustomThemes(JSON.parse(text)) : []
    } catch {
        return []
    }
}

export function writeCustomThemes(
    list: readonly ThemeDefinition[],
    storage: Storage | null = defaultStorage(),
): void {
    try {
        if (list.length === 0) {
            storage?.removeItem(customThemesStorageKey)
        } else {
            const stored = list.map(({ id, name, stylesheet, variants, previews }) => ({
                id,
                name,
                stylesheet,
                variants,
                previews,
            }))
            storage?.setItem(customThemesStorageKey, JSON.stringify(stored))
        }
    } catch {
        // Without storage the first paint shows the default theme until the list is loaded.
    }
}
