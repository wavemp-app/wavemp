import { describe, expect, it } from 'vitest'
import { decodeLyricsFile, hasTimestamps } from '../files'

function bytes(...values: number[]): ArrayBuffer {
    return new Uint8Array(values).buffer
}

describe('lyrics files', () => {
    it('reads UTF-8 with and without byte order mark', () => {
        const text = new TextEncoder().encode('[00:01.00]Grüße')
        expect(decodeLyricsFile(text.slice().buffer)).toBe('[00:01.00]Grüße')

        const withMark = new Uint8Array([0xef, 0xbb, 0xbf, ...text])
        expect(decodeLyricsFile(withMark.buffer)).toBe('[00:01.00]Grüße')
    })

    it('reads older files that are no valid UTF-8 as Windows-1252', () => {
        // "Grüße" in Windows-1252
        expect(decodeLyricsFile(bytes(0x47, 0x72, 0xfc, 0xdf, 0x65))).toBe('Grüße')
    })

    it('tells lyrics with times from plain text', () => {
        expect(hasTimestamps('[ar:Band]\n[00:12.34]Line')).toBe(true)
        expect(hasTimestamps('  [1:02]Line')).toBe(true)
        expect(hasTimestamps('[ar:Band]\nLine\n[Chorus]')).toBe(false)
        expect(hasTimestamps('')).toBe(false)
    })
})
