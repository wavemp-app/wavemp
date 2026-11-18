// The theme a profile chose: the id of the theme and the appearance (which variant of a theme with
// a light and a dark variant it sees). Stored on the server with the settings of the profile
// (PATCH /api/v1/me/settings, group "theme"); the id stays as the server keeps it, also an id this
// web app does not know (it then shows the default theme). Pure functions only.

import {
    ThemeAppearanceObject,
    type ThemeAppearance as ApiThemeAppearance,
    type ThemeSettings,
    type UpdateThemeSettings,
} from '@/api/generated/models'
import { defaultThemeAppearance, defaultThemeId, type ThemeAppearance } from './themes'

export interface ThemeChoice {
    readonly id: string
    readonly appearance: ThemeAppearance
}

/** A change of the theme of a profile; missing members stay as they are. */
export interface ThemeChange {
    readonly id?: string
    readonly appearance?: ThemeAppearance
}

export const defaultThemeChoice: ThemeChoice = {
    id: defaultThemeId,
    appearance: defaultThemeAppearance,
}

/** Pattern of a theme id the API accepts (ThemeIds.Pattern of the service). */
export const themeIdPattern = /^[a-z0-9]+(?:-[a-z0-9]+)*$/

/** Longest theme id the API accepts. */
export const themeIdMaxLength = 64

export function isValidThemeId(value: unknown): value is string {
    return (
        typeof value === 'string' && value.length <= themeIdMaxLength && themeIdPattern.test(value)
    )
}

const appearances: Readonly<Record<ApiThemeAppearance, ThemeAppearance>> = {
    [ThemeAppearanceObject.System]: 'system',
    [ThemeAppearanceObject.Light]: 'light',
    [ThemeAppearanceObject.Dark]: 'dark',
}

export function isThemeAppearance(value: unknown): value is ThemeAppearance {
    return value === 'system' || value === 'light' || value === 'dark'
}

/** The appearance of an answer of the API, or null for an unknown value. */
export function fromApiAppearance(value: unknown): ThemeAppearance | null {
    return typeof value === 'string' && value in appearances
        ? appearances[value as ApiThemeAppearance]
        : null
}

export function toApiAppearance(appearance: ThemeAppearance): ApiThemeAppearance {
    switch (appearance) {
        case 'light':
            return ThemeAppearanceObject.Light
        case 'dark':
            return ThemeAppearanceObject.Dark
        default:
            return ThemeAppearanceObject.System
    }
}

/** The theme of the settings of the API, or null if the answer holds no valid one. */
export function toThemeChoice(settings: ThemeSettings | null | undefined): ThemeChoice | null {
    const appearance = fromApiAppearance(settings?.appearance)
    if (!isValidThemeId(settings?.id) || appearance === null) {
        return null
    }

    return { id: settings.id, appearance }
}

/** A remembered theme (localStorage), or null if it is not valid. */
export function parseThemeChoice(value: unknown): ThemeChoice | null {
    if (typeof value !== 'object' || value === null) {
        return null
    }

    const { id, appearance } = value as Record<string, unknown>
    return isValidThemeId(id) && isThemeAppearance(appearance) ? { id, appearance } : null
}

/** The theme after a change, as the server applies it. */
export function applyThemeChange(choice: ThemeChoice, change: ThemeChange): ThemeChoice {
    return {
        id: change.id ?? choice.id,
        appearance: change.appearance ?? choice.appearance,
    }
}

/** The change as the API takes it. */
export function toUpdateThemeSettings(change: ThemeChange): UpdateThemeSettings {
    return {
        ...(change.id === undefined ? {} : { id: change.id }),
        ...(change.appearance === undefined
            ? {}
            : { appearance: toApiAppearance(change.appearance) }),
    }
}
