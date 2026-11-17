import { localeMessages } from './messages'
import { matchLocale } from './tags'

export { canonicalLocale, defaultLocale, languageName } from './tags'

/**
 * Languages of the web app: one per JSON file in src/locales, English first. Translations come
 * from Weblate; a new file adds its language to the selection.
 */
export const supportedLocales: readonly string[] = Object.keys(localeMessages)

export function isSupportedLocale(value: string): boolean {
    return supportedLocales.includes(value)
}

/**
 * Picks the first supported language from a list of preferred language tags (e.g. "de-AT"),
 * matching the exact tag, a shorter form of it or another variant of its language,
 * case-insensitive. Falls back to English.
 */
export function resolveLocale(
    preferred: readonly string[],
    supported: readonly string[] = supportedLocales,
): string {
    return matchLocale(preferred, supported)
}

/** Preferred languages of the browser; until a profile is selected, they decide the language. */
export function browserLocales(
    nav: Pick<Navigator, 'languages' | 'language'> = navigator,
): string[] {
    if (nav.languages && nav.languages.length > 0) {
        return [...nav.languages]
    }

    return nav.language ? [nav.language] : []
}

/**
 * Language of the app: the language of the selected profile (a language without translations
 * shows English), before a profile is selected the language of the browser.
 */
export function appLocale(
    profileLanguage: string | null | undefined,
    preferred: readonly string[] = browserLocales(),
): string {
    return profileLanguage ? resolveLocale([profileLanguage]) : resolveLocale(preferred)
}
