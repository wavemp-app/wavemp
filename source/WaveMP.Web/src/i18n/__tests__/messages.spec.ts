// Checks the message files (read as raw JSON; importing them yields precompiled functions), also
// the ones from Weblate, and that every message key used in the source code exists.
import { readdirSync, readFileSync, statSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import { supportedLocales } from '../locales'
import { localeFromFileName } from '../tags'

// Not new URL(..., import.meta.url): Vite rewrites that pattern into an asset URL.
const sourceDirectory = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..')
const localesDirectory = path.join(sourceDirectory, 'locales')

type Messages = { [key: string]: string | Messages }

function readMessages(locale: string): Messages {
    return JSON.parse(readFileSync(path.join(localesDirectory, `${locale}.json`), 'utf8'))
}

/** Flattens nested messages to "a.b.c" keys. */
function flatten(messages: Messages, prefix = ''): Map<string, string> {
    const result = new Map<string, string>()
    for (const [key, value] of Object.entries(messages)) {
        const fullKey = prefix ? `${prefix}.${key}` : key
        if (typeof value === 'string') {
            result.set(fullKey, value)
        } else {
            for (const [nestedKey, nestedValue] of flatten(value, fullKey)) {
                result.set(nestedKey, nestedValue)
            }
        }
    }
    return result
}

function placeholders(message: string): string[] {
    return [...message.matchAll(/\{\s*([A-Za-z0-9_]+)\s*\}/g)].map((match) => match[1] ?? '').sort()
}

function sourceFiles(directory: string): string[] {
    return readdirSync(directory).flatMap((name) => {
        const file = path.join(directory, name)
        if (statSync(file).isDirectory()) {
            return name === '__tests__' || name === 'generated' ? [] : sourceFiles(file)
        }
        return /\.(vue|ts)$/.test(name) ? [file] : []
    })
}

const english = flatten(readMessages('en'))

/**
 * Languages maintained together with the code; they always have every message. Translations from
 * Weblate may be incomplete, their missing messages fall back to English.
 */
const completeLocales = ['en', 'de']

const files = readdirSync(localesDirectory).sort()

describe('message files', () => {
    it('are the supported languages', () => {
        expect(files).toEqual(supportedLocales.map((locale) => `${locale}.json`).sort())
    })

    it('are named after their language tag in its canonical form', () => {
        // e.g. pt-BR.json; Weblate creates the files with the BCP 47 language code style.
        const invalid = files.filter((file) => localeFromFileName(file) + '.json' !== file)
        expect(invalid).toEqual([])
    })

    it('include the complete languages', () => {
        expect(supportedLocales).toEqual(expect.arrayContaining(completeLocales))
    })

    for (const locale of supportedLocales) {
        describe(locale, () => {
            const messages = flatten(readMessages(locale))

            if (completeLocales.includes(locale)) {
                it('has exactly the keys of the English messages', () => {
                    expect([...messages.keys()].sort()).toEqual([...english.keys()].sort())
                })
            } else {
                it('has only keys of the English messages', () => {
                    const unknown = [...messages.keys()].filter((key) => !english.has(key))
                    expect(unknown).toEqual([])
                })
            }

            it('has no empty messages', () => {
                const empty = [...messages].filter(([, value]) => value.trim() === '')
                expect(empty).toEqual([])
            })

            it('uses the same named placeholders as English', () => {
                for (const [key, value] of messages) {
                    expect(placeholders(value), key).toEqual(placeholders(english.get(key) ?? ''))
                }
            })

            it('contains no markup and no special message syntax', () => {
                // HTML would need v-html; @, | and $ have a special meaning in vue-i18n messages.
                const invalid = [...messages].filter(([, value]) => /[<>@|$]/.test(value))
                expect(invalid).toEqual([])
            })

            it('uses camelCase keys', () => {
                const invalid = [...messages.keys()].filter(
                    (key) => !key.split('.').every((part) => /^[a-z][A-Za-z0-9]*$/.test(part)),
                )
                expect(invalid).toEqual([])
            })
        })
    }
})

describe('message keys in the source code', () => {
    it('exist in the English messages', () => {
        // t('key'), and route meta or definitions that name message keys (titleKey: 'key').
        const keyPattern = /\bt\(\s*'([A-Za-z0-9.]+)'|\b[a-z]*Key:\s*'([A-Za-z0-9.]+)'/g
        const missing: string[] = []
        for (const file of sourceFiles(sourceDirectory)) {
            const content = readFileSync(file, 'utf8')
            for (const match of content.matchAll(keyPattern)) {
                const key = match[1] ?? match[2] ?? ''
                if (!english.has(key)) {
                    missing.push(`${path.relative(sourceDirectory, file)}: ${key}`)
                }
            }
        }
        expect(missing).toEqual([])
    })
})
