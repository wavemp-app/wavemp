import { describe, expect, it } from 'vitest'
import {
    appendDigit,
    isCompletePin,
    lockoutMinutes,
    pinPadKeys,
    removeLastDigit,
    retryAfterSecondsOf,
    sanitizePin,
} from '../pin'

describe('sanitizePin', () => {
    it('keeps only the digits 0 to 9, at most eight', () => {
        expect(sanitizePin('12a3 4')).toBe('1234')
        expect(sanitizePin('1234567890')).toBe('12345678')
        expect(sanitizePin('١٢٣٤')).toBe('')
        expect(sanitizePin('')).toBe('')
    })
})

describe('appendDigit and removeLastDigit', () => {
    it('adds digits until the PIN is full', () => {
        expect(appendDigit('', '1')).toBe('1')
        expect(appendDigit('123', '4')).toBe('1234')
        expect(appendDigit('12345678', '9')).toBe('12345678')
    })

    it('ignores everything but single digits', () => {
        expect(appendDigit('12', 'a')).toBe('12')
        expect(appendDigit('12', '34')).toBe('12')
        expect(appendDigit('12', 'delete')).toBe('12')
    })

    it('removes the last digit', () => {
        expect(removeLastDigit('1234')).toBe('123')
        expect(removeLastDigit('')).toBe('')
    })
})

describe('isCompletePin', () => {
    it('accepts four to eight digits', () => {
        expect(isCompletePin('1234')).toBe(true)
        expect(isCompletePin('12345678')).toBe(true)
        expect(isCompletePin('123')).toBe(false)
        expect(isCompletePin('123456789')).toBe(false)
        expect(isCompletePin('12a4')).toBe(false)
    })
})

describe('pinPadKeys', () => {
    it('are laid out like a phone keypad', () => {
        expect(pinPadKeys).toEqual([
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
        ])
    })
})

describe('retryAfterSecondsOf', () => {
    it('reads the seconds of the Retry-After header', () => {
        expect(retryAfterSecondsOf({ responseHeaders: { 'retry-after': ['297'] } })).toBe(297)
        expect(retryAfterSecondsOf({ responseHeaders: { 'Retry-After': '60' } })).toBe(60)
    })

    it('is undefined without a usable header', () => {
        expect(retryAfterSecondsOf(undefined)).toBeUndefined()
        expect(retryAfterSecondsOf(new Error('offline'))).toBeUndefined()
        expect(retryAfterSecondsOf({ responseHeaders: {} })).toBeUndefined()
        expect(
            retryAfterSecondsOf({ responseHeaders: { 'retry-after': ['soon'] } }),
        ).toBeUndefined()
    })
})

describe('lockoutMinutes', () => {
    it('rounds up to whole minutes, at least one', () => {
        expect(lockoutMinutes(300)).toBe(5)
        expect(lockoutMinutes(241)).toBe(5)
        expect(lockoutMinutes(1)).toBe(1)
        expect(lockoutMinutes(0)).toBe(1)
        expect(lockoutMinutes(undefined)).toBe(1)
    })
})
