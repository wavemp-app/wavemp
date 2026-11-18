// Why the server rejected a theme package. The server sends a code per problem plus the file, the
// line, the member of manifest.json and the limits it concerns (ThemeProblem, 422 theme.invalid);
// the web app translates them. Pure functions only.

import {
    ThemeErrorCodeObject,
    type ThemeErrorCode,
    type ThemeProblem,
} from '@/api/generated/models'

export interface ThemePackageError {
    /** The code, or null for a code this web app does not know. */
    readonly code: ThemeErrorCode | null
    readonly path?: string
    readonly line?: number
    readonly field?: string
    readonly file?: string
    readonly min?: number
    readonly max?: number
}

const codes = new Set<string>(Object.values(ThemeErrorCodeObject))

/** Codes whose limits are sizes in bytes. */
const sizeLimits = new Set<string>([
    ThemeErrorCodeObject.PackageToo_large,
    ThemeErrorCodeObject.FileToo_large,
])

function text(value: unknown): string | undefined {
    return typeof value === 'string' && value !== '' ? value : undefined
}

function number(value: unknown): number | undefined {
    return typeof value === 'number' && Number.isFinite(value) ? value : undefined
}

/** The problems of a rejected theme package, empty for other errors. */
export function themeErrorsOf(error: unknown): ThemePackageError[] {
    const errors = (error as ThemeProblem | null | undefined)?.errors
    if (!Array.isArray(errors)) {
        return []
    }

    return errors.map((item) => {
        const value = item as Record<string, unknown>
        const code = typeof value.code === 'string' && codes.has(value.code) ? value.code : null
        return {
            code: code as ThemeErrorCode | null,
            path: text(value.path),
            line: number(value.line),
            field: text(value.field),
            file: text(value.file),
            min: number(value.min),
            max: number(value.max),
        }
    })
}

/**
 * Message key of a problem: the code in camelCase below adminThemes.errorCodes, e.g.
 * adminThemes.errorCodes.css.externalUrl for css.external_url.
 */
export function themeErrorMessageKey(error: ThemePackageError): string {
    if (!error.code) {
        return 'adminThemes.errorCodes.unknown'
    }

    const key = error.code.replace(/_([a-z])/g, (_, letter: string) => letter.toUpperCase())
    return `adminThemes.errorCodes.${key}`
}

/**
 * The placeholders of the message of a problem: {path}, {line}, {field}, {file}, {min} and {max};
 * sizes in bytes are formatted with formatSize, other numbers with formatNumber.
 */
export function themeErrorParameters(
    error: ThemePackageError,
    formatSize: (bytes: number) => string,
    formatNumber: (value: number) => string = String,
): Record<string, string> {
    const formatLimit = error.code && sizeLimits.has(error.code) ? formatSize : formatNumber
    const parameters: Record<string, string> = {}
    for (const name of ['path', 'field', 'file'] as const) {
        const value = error[name]
        if (value !== undefined) {
            parameters[name] = value
        }
    }
    if (error.line !== undefined) {
        parameters.line = formatNumber(error.line)
    }
    if (error.min !== undefined) {
        parameters.min = formatLimit(error.min)
    }
    if (error.max !== undefined) {
        parameters.max = formatLimit(error.max)
    }

    return parameters
}

/** Message key of the place of a problem, or null if it concerns the package as a whole. */
export function themeErrorLocationKey(error: ThemePackageError): string | null {
    if (error.path === undefined) {
        return null
    }

    return error.line === undefined
        ? 'adminThemes.errorLocation.file'
        : 'adminThemes.errorLocation.line'
}
