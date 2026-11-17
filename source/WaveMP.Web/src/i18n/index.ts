import { createI18n } from 'vue-i18n'
import { localeMessages, type MessageSchema } from './messages'
import { defaultLocale } from './tags'

export type { MessageSchema } from './messages'

/**
 * The i18n instance of the app. A language without its own message for a key shows the message of
 * the language without region ("de" for "de-AT"), then the English one; that way incomplete
 * translations work.
 */
export function createWaveMpI18n(
    locale: string = defaultLocale,
    messages: Record<string, MessageSchema> = localeMessages,
) {
    return createI18n<[MessageSchema], string, false>({
        legacy: false,
        locale,
        fallbackLocale: defaultLocale,
        messages,
        // Falling back is expected for incomplete translations, not a mistake.
        fallbackWarn: false,
    })
}

export type WaveMpI18n = ReturnType<typeof createWaveMpI18n>

/** The global composer (i18n.global, or useI18n({ useScope: 'global' }) in components). */
export interface LocaleTarget {
    locale: { value: string }
}

/**
 * Switches the language of the web app and of the document. useLocaleSync calls it with the
 * language of the selected profile, or with the browser language before a profile is selected.
 */
export function setLocale(composer: LocaleTarget, locale: string, doc: Document = document): void {
    composer.locale.value = locale
    doc.documentElement.lang = locale
}
