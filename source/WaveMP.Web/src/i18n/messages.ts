import type en from '@/locales/en.json'
import { messagesByLocale } from './tags'

/** Structure of all messages; English is the source language. */
export type MessageSchema = typeof en

/**
 * Every translation file in src/locales, precompiled at build time. A new file (e.g. one added by
 * Weblate) adds its language to the app without further changes. Translations other than English
 * may be incomplete; their missing messages fall back to English.
 */
const files = import.meta.glob<MessageSchema>('../locales/*.json', {
    eager: true,
    import: 'default',
})

/** Messages per language, English first. */
export const localeMessages: Record<string, MessageSchema> = messagesByLocale(files)
