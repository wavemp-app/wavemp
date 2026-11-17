/**
 * Language tags (BCP 47, e.g. "en", "pt-BR", "zh-Hans") of the translations and of the profiles.
 * Pure functions without the message files, so they can be tested with any list of languages.
 */

/** Source language of the web app; every other language falls back to it. */
export const defaultLocale = 'en'

/**
 * Tags the API accepts for the language of a profile (see UpdateUserSettings.language in the
 * OpenAPI document): language, optional script, region and variants, separated by hyphens.
 */
const languageTagPattern =
    /^[A-Za-z]{2,3}(-[A-Za-z]{4})?(-([A-Za-z]{2}|[0-9]{3}))?(-([A-Za-z0-9]{5,8}|[0-9][A-Za-z0-9]{3}))*$/

/** Longest tag the API accepts. */
const languageTagMaxLength = 35

/**
 * The tag in its canonical form ("pt-BR", "zh-Hans"), or null if it is not a language tag the API
 * accepts. Underscores count as hyphens, so "pt_BR" (as in some translation tools) is "pt-BR".
 */
export function canonicalLocale(tag: string | null | undefined): string | null {
    const candidate = tag?.trim().replaceAll('_', '-') ?? ''
    if (candidate.length > languageTagMaxLength || !languageTagPattern.test(candidate)) {
        return null
    }

    try {
        return Intl.getCanonicalLocales(candidate)[0] ?? null
    } catch {
        return null
    }
}

/** The language of a translation file, from its name ("../locales/pt-BR.json" is "pt-BR"). */
export function localeFromFileName(file: string): string | null {
    const match = /([^/\\]+)\.json$/.exec(file)
    return match ? canonicalLocale(match[1]) : null
}

/**
 * Messages per language from the translation files, keyed by their paths. Files without a valid
 * language in their name are left out; the default language is always first, the others follow in
 * the order of their tags.
 */
export function messagesByLocale<T>(files: Record<string, T>): Record<string, T> {
    const entries = Object.entries(files)
        .map(([file, messages]) => [localeFromFileName(file), messages] as const)
        .filter((entry): entry is readonly [string, T] => entry[0] !== null)
        .sort(([a], [b]) =>
            a === defaultLocale ? -1 : b === defaultLocale ? 1 : a.localeCompare(b),
        )

    return Object.fromEntries(entries)
}

/**
 * Picks the language of the app from preferred language tags (e.g. "de-AT"), in their order: the
 * tag itself, then the tag shortened by one subtag at a time ("zh-Hant-TW", "zh-Hant", "zh"), then
 * another variant of the same language ("pt-BR" for "pt-PT"). Case does not matter. Falls back to
 * the default language.
 */
export function matchLocale(preferred: readonly string[], supported: readonly string[]): string {
    const byKey = new Map(supported.map((locale) => [locale.toLowerCase(), locale]))

    for (const tag of preferred) {
        const subtags = canonicalLocale(tag)?.toLowerCase().split('-')
        if (!subtags) {
            continue
        }

        for (let length = subtags.length; length > 0; length--) {
            const match = byKey.get(subtags.slice(0, length).join('-'))
            if (match) {
                return match
            }
        }

        const sameLanguage = supported.find(
            (locale) => locale.toLowerCase().split('-')[0] === subtags[0],
        )
        if (sameLanguage) {
            return sameLanguage
        }
    }

    return defaultLocale
}

/**
 * Name of a language in that language ("Deutsch" for "de"), so everybody finds their own language
 * without understanding the current one. Starts with a capital letter like the other entries of a
 * list; unknown languages show their tag.
 */
export function languageName(locale: string): string {
    try {
        const name = new Intl.DisplayNames([locale], { type: 'language', fallback: 'code' }).of(
            locale,
        )
        if (!name || name.toLowerCase() === locale.toLowerCase()) {
            return locale
        }

        return name.charAt(0).toLocaleUpperCase(locale) + name.slice(1)
    } catch {
        return locale
    }
}
