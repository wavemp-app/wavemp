import { describe, expect, it } from 'vitest'
import {
    canonicalLocale,
    defaultLocale,
    languageName,
    localeFromFileName,
    matchLocale,
    messagesByLocale,
} from '../tags'

describe('canonicalLocale', () => {
    it('returns the canonical form of a tag', () => {
        expect(canonicalLocale('de')).toBe('de')
        expect(canonicalLocale('PT-br')).toBe('pt-BR')
        expect(canonicalLocale('zh-hans')).toBe('zh-Hans')
        expect(canonicalLocale('es-419')).toBe('es-419')
        expect(canonicalLocale(' en ')).toBe('en')
    })

    it('treats underscores as hyphens', () => {
        expect(canonicalLocale('pt_BR')).toBe('pt-BR')
    })

    it('rejects what the API does not accept', () => {
        expect(canonicalLocale(null)).toBeNull()
        expect(canonicalLocale(undefined)).toBeNull()
        expect(canonicalLocale('')).toBeNull()
        expect(canonicalLocale('english')).toBeNull()
        expect(canonicalLocale('de-')).toBeNull()
        expect(canonicalLocale('de-u-co-phonebk')).toBeNull()
        expect(canonicalLocale('de-abcdefgh-abcdefgh-abcdefgh-abcdefgh')).toBeNull()
    })
})

describe('localeFromFileName', () => {
    it('takes the language from the name of a translation file', () => {
        expect(localeFromFileName('../locales/en.json')).toBe('en')
        expect(localeFromFileName('../locales/pt-BR.json')).toBe('pt-BR')
        expect(localeFromFileName('C:\\locales\\zh_Hant.json')).toBe('zh-Hant')
    })

    it('ignores other files', () => {
        expect(localeFromFileName('../locales/README.md')).toBeNull()
        expect(localeFromFileName('../locales/messages.json')).toBeNull()
    })
})

describe('messagesByLocale', () => {
    it('has one language per translation file with English first', () => {
        const messages = messagesByLocale({
            '../locales/fr.json': 'fr',
            '../locales/de.json': 'de',
            '../locales/en.json': 'en',
            '../locales/pt-BR.json': 'pt-BR',
        })

        expect(Object.keys(messages)).toEqual(['en', 'de', 'fr', 'pt-BR'])
        expect(messages['pt-BR']).toBe('pt-BR')
    })

    it('leaves out files without a language in their name', () => {
        const messages = messagesByLocale({
            '../locales/en.json': 'en',
            '../locales/template.json': 'template',
        })

        expect(Object.keys(messages)).toEqual(['en'])
    })
})

describe('matchLocale', () => {
    const supported = ['en', 'de', 'pt-BR', 'zh-Hans', 'zh-Hant']

    it('matches the tag case-insensitively', () => {
        expect(matchLocale(['PT-br'], supported)).toBe('pt-BR')
        expect(matchLocale(['zh-hant'], supported)).toBe('zh-Hant')
    })

    it('shortens the tag one subtag at a time', () => {
        expect(matchLocale(['de-AT'], supported)).toBe('de')
        expect(matchLocale(['zh-Hant-TW'], supported)).toBe('zh-Hant')
    })

    it('takes another variant of the same language', () => {
        expect(matchLocale(['pt-PT'], supported)).toBe('pt-BR')
        expect(matchLocale(['pt'], supported)).toBe('pt-BR')
    })

    it('keeps the order of preference', () => {
        expect(matchLocale(['fr', 'de', 'en'], supported)).toBe('de')
    })

    it('falls back to the default language', () => {
        expect(matchLocale(['fr', 'not a tag'], supported)).toBe(defaultLocale)
        expect(matchLocale([], supported)).toBe(defaultLocale)
    })
})

describe('languageName', () => {
    it('names every language in that language', () => {
        expect(languageName('en')).toBe('English')
        expect(languageName('de')).toBe('Deutsch')
    })

    it('starts with a capital letter', () => {
        expect(languageName('fr')).toBe('Français')
        expect(languageName('es')).toBe('Español')
    })

    it('shows the tag of unknown languages', () => {
        expect(languageName('xx')).toBe('xx')
    })
})
