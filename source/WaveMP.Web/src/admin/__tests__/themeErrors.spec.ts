import { describe, expect, it } from 'vitest'
import en from '@/locales/en.json'
import { ThemeErrorCodeObject } from '@/api/generated/models'
import {
    themeErrorLocationKey,
    themeErrorMessageKey,
    themeErrorParameters,
    themeErrorsOf,
    toThemePackageErrors,
} from '../themeErrors'

type Messages = { [key: string]: unknown }

function hasMessage(key: string): boolean {
    let node: unknown = en
    for (const part of key.split('.')) {
        node = (node as Messages | undefined)?.[part]
    }
    return node !== undefined
}

describe('theme errors', () => {
    it('are read from the problem of a rejected package', () => {
        const problem = {
            code: 'theme.invalid',
            errors: [
                { code: 'css.external_url', path: 'theme.css', line: 3 },
                { code: 'manifest.unsupported_api_version', field: 'themeApi', min: 5, max: 6 },
                { code: 'something.new', path: 7 },
            ],
        }

        expect(themeErrorsOf(problem)).toEqual([
            { code: 'css.external_url', path: 'theme.css', line: 3 },
            { code: 'manifest.unsupported_api_version', field: 'themeApi', min: 5, max: 6 },
            { code: null },
        ])
    })

    it('are read from the problems of an outdated theme', () => {
        expect(
            toThemePackageErrors([
                { code: 'svg.not_cleaned', path: 'images/glass.svg' },
                { code: 'manifest.unsupported_api_version', field: 'themeApi', min: 6, max: 7 },
            ]),
        ).toEqual([
            { code: 'svg.not_cleaned', path: 'images/glass.svg' },
            { code: 'manifest.unsupported_api_version', field: 'themeApi', min: 6, max: 7 },
        ])
        expect(toThemePackageErrors(undefined)).toEqual([])
    })

    it('are empty for other errors', () => {
        expect(themeErrorsOf(new Error('network'))).toEqual([])
        expect(themeErrorsOf(null)).toEqual([])
    })

    it('have a message for every code', () => {
        for (const code of Object.values(ThemeErrorCodeObject)) {
            expect(hasMessage(themeErrorMessageKey({ code })), code).toBe(true)
        }
        expect(themeErrorMessageKey({ code: 'css.at_rule_not_allowed' })).toBe(
            'adminThemes.errorCodes.css.atRuleNotAllowed',
        )
        expect(themeErrorMessageKey({ code: null })).toBe('adminThemes.errorCodes.unknown')
    })

    it('format sizes as sizes and other limits as numbers', () => {
        const size = (bytes: number) => `${bytes / 1024} KiB`
        const number = (value: number) => `#${value}`

        expect(
            themeErrorParameters(
                { code: 'file.too_large', path: 'a.png', max: 2048 },
                size,
                number,
            ),
        ).toEqual({ path: 'a.png', max: '2 KiB' })
        expect(
            themeErrorParameters(
                { code: 'manifest.unsupported_api_version', field: 'themeApi', min: 5, max: 6 },
                size,
                number,
            ),
        ).toEqual({ field: 'themeApi', min: '#5', max: '#6' })
        expect(
            themeErrorParameters(
                { code: 'css.missing_file', path: 'theme.css', line: 4, file: 'x.svg' },
                size,
                number,
            ),
        ).toEqual({ path: 'theme.css', line: '#4', file: 'x.svg' })
    })

    it('name their place in the package', () => {
        expect(themeErrorLocationKey({ code: 'css.import', path: 'theme.css', line: 1 })).toBe(
            'adminThemes.errorLocation.line',
        )
        expect(themeErrorLocationKey({ code: 'svg.script', path: 'a.svg' })).toBe(
            'adminThemes.errorLocation.file',
        )
        expect(themeErrorLocationKey({ code: 'package.too_large', max: 1 })).toBeNull()
    })
})
