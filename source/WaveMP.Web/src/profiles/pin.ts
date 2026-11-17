// PINs of the profiles: 4 to 8 digits, like the server checks them. The digit pad and the settings
// page only let digits through; the server stays the judge of right and wrong.

export const pinMinLength = 4
export const pinMaxLength = 8

/** Keys of the digit pad in the order of a phone keypad. */
export const pinPadKeys = [
    '1',
    '2',
    '3',
    '4',
    '5',
    '6',
    '7',
    '8',
    '9',
    'delete',
    '0',
    'submit',
] as const

export type PinPadKey = (typeof pinPadKeys)[number]

/** The digits 0 to 9 of a text, at most pinMaxLength of them. */
export function sanitizePin(value: string): string {
    return value.replace(/[^0-9]/g, '').slice(0, pinMaxLength)
}

/** The PIN with one more digit; a full PIN stays as it is. */
export function appendDigit(pin: string, digit: string): string {
    return /^[0-9]$/.test(digit) ? sanitizePin(pin + digit) : pin
}

/** The PIN without its last digit. */
export function removeLastDigit(pin: string): string {
    return pin.slice(0, -1)
}

/** True for 4 to 8 digits. */
export function isCompletePin(pin: string): boolean {
    return new RegExp(`^[0-9]{${pinMinLength},${pinMaxLength}}$`).test(pin)
}

/**
 * Seconds of the Retry-After header of a failed request (a lockout after wrong PINs), or undefined.
 * Kiota passes the headers of the answer with lower-case names.
 */
export function retryAfterSecondsOf(error: unknown): number | undefined {
    const headers = (error as { responseHeaders?: unknown } | null | undefined)?.responseHeaders
    if (typeof headers !== 'object' || headers === null) {
        return undefined
    }

    const entry = Object.entries(headers).find(([name]) => name.toLowerCase() === 'retry-after')
    const raw: unknown = Array.isArray(entry?.[1]) ? entry[1][0] : entry?.[1]
    const seconds = typeof raw === 'string' ? Number(raw.trim()) : Number.NaN
    return Number.isInteger(seconds) && seconds >= 0 ? seconds : undefined
}

/** Whole minutes of a lockout, at least one, for the message after too many wrong PINs. */
export function lockoutMinutes(seconds: number | undefined): number {
    return Math.max(1, Math.ceil((seconds ?? 60) / 60))
}
