import { describe, expect, it } from 'vitest'
import { createWaveMpI18n, setLocale, type MessageSchema } from '../index'
import { localeMessages } from '../messages'
import {
    appLocale,
    browserLocales,
    defaultLocale,
    isSupportedLocale,
    resolveLocale,
    supportedLocales,
} from '../locales'

describe('supportedLocales', () => {
    it('are the languages of the translation files with English first', () => {
        expect(supportedLocales).toEqual(Object.keys(localeMessages))
        expect(supportedLocales[0]).toBe('en')
        expect(supportedLocales).toContain('de')
        expect(defaultLocale).toBe('en')
    })

    it('are checked exactly', () => {
        expect(isSupportedLocale('de')).toBe(true)
        expect(isSupportedLocale('de-AT')).toBe(false)
    })
})

describe('resolveLocale', () => {
    it('matches exact tags case-insensitively', () => {
        expect(resolveLocale(['DE'])).toBe('de')
        expect(resolveLocale(['en'])).toBe('en')
    })

    it('matches the primary subtag of regional tags', () => {
        expect(resolveLocale(['de-AT'])).toBe('de')
        expect(resolveLocale(['en-GB'])).toBe('en')
        expect(resolveLocale(['de_CH'])).toBe('de')
    })

    it('takes the first supported language in order of preference', () => {
        expect(resolveLocale(['xx-XX', 'de-DE', 'en-US'])).toBe('de')
        expect(resolveLocale(['en-US', 'de-DE'])).toBe('en')
    })

    it('falls back to English', () => {
        expect(resolveLocale(['xx-XX', 'yy'])).toBe('en')
        expect(resolveLocale([])).toBe('en')
        expect(resolveLocale([''])).toBe('en')
    })

    it('offers a language as soon as it has a translation file', () => {
        expect(resolveLocale(['fr-FR'], ['en', 'de'])).toBe('en')
        expect(resolveLocale(['fr-FR'], ['en', 'de', 'fr'])).toBe('fr')
    })
})

describe('appLocale', () => {
    it('uses the language of the profile once one is selected', () => {
        expect(appLocale('de', ['en-US'])).toBe('de')
        expect(appLocale('de-AT', ['en-US'])).toBe('de')
    })

    it('shows English for a profile language without translations', () => {
        expect(appLocale('xx', ['de-DE'])).toBe('en')
    })

    it('uses the browser languages before a profile is selected', () => {
        expect(appLocale(null, ['de-DE', 'en'])).toBe('de')
        expect(appLocale(undefined, [])).toBe('en')
    })
})

describe('browserLocales', () => {
    it('prefers the list of languages', () => {
        expect(browserLocales({ languages: ['de-DE', 'en'], language: 'fr' })).toEqual([
            'de-DE',
            'en',
        ])
    })

    it('falls back to the single language', () => {
        expect(browserLocales({ languages: [], language: 'de' })).toEqual(['de'])
        expect(browserLocales({ languages: [], language: '' })).toEqual([])
    })
})

describe('setLocale', () => {
    it('switches the messages and the document language', () => {
        const i18n = createWaveMpI18n('en')
        const doc = document.implementation.createHTMLDocument('test')

        expect(i18n.global.t('nav.library')).toBe('Library')

        setLocale(i18n.global, 'de', doc)

        expect(i18n.global.locale.value).toBe('de')
        expect(i18n.global.t('nav.library')).toBe('Bibliothek')
        expect(doc.documentElement.lang).toBe('de')
    })

    it('formats named placeholders', () => {
        const i18n = createWaveMpI18n('de')

        expect(i18n.global.t('placeholders.album', { number: 2 })).toBe('Platzhalter-Album 2')
    })
})

describe('incomplete translations', () => {
    // A community translation with a single message, like a new file from Weblate.
    const messages = {
        ...localeMessages,
        xx: { nav: { library: 'Xibliothek' } } as unknown as MessageSchema,
    }

    it('use their own messages', () => {
        const i18n = createWaveMpI18n('xx', messages)

        expect(i18n.global.t('nav.library')).toBe('Xibliothek')
    })

    it('fall back to English for missing messages', () => {
        const i18n = createWaveMpI18n('xx', messages)

        expect(i18n.global.t('nav.playlists')).toBe('Playlists')
        expect(i18n.global.t('placeholders.album', { number: 2 })).toBe('Placeholder Album 2')
    })

    it('fall back from a region to the language, then to English', () => {
        const i18n = createWaveMpI18n('xx-YY', messages)

        expect(i18n.global.t('nav.library')).toBe('Xibliothek')
        expect(i18n.global.t('nav.playlists')).toBe('Playlists')
    })
})
