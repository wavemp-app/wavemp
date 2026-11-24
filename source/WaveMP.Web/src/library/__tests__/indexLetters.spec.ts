import { describe, expect, it } from 'vitest'
import { indexLetterOf, indexLetters, otherIndexLetter } from '../indexLetters'

// The same table of cases as IndexLettersTests of the server.
const cases: readonly [string | null, string][] = [
    ['Abba', 'A'],
    ['abba', 'A'],
    ['zappa', 'Z'],
    ['Élan', 'E'],
    ['Élan', 'E'],
    ['ärzte', 'A'],
    ['Österreich', 'O'],
    ['Ñandú', 'N'],
    ['Ça', 'C'],
    ['Ångström', 'A'],
    ['İstanbul', 'I'],
    ['ı', '#'],
    ['ß', '#'],
    ['Øresund', '#'],
    ['Æon', '#'],
    ['Œuvre', '#'],
    ['ﬁve', '#'],
    ['Ａ', '#'],
    ['2Pac', '#'],
    ["(What's the Story)", '#'],
    ['!!!', '#'],
    [' Space', '#'],
    ['Ωmega', '#'],
    ['Чайф', '#'],
    ['東京', '#'],
    ['😀', '#'],
    ['́', '#'],
    ['\ud800', '#'],
    ['', '#'],
    [null, '#'],
]

describe('indexLetterOf', () => {
    it.each(cases)('files %j under %s', (text, expected) => {
        expect(indexLetterOf(text)).toBe(expected)
    })

    it('files undefined under the entry for everything else', () => {
        expect(indexLetterOf(undefined)).toBe(otherIndexLetter)
    })
})

describe('indexLetters', () => {
    it('lists A to Z and then the entry for everything else', () => {
        expect(indexLetters).toHaveLength(27)
        expect(indexLetters[0]).toBe('A')
        expect(indexLetters[25]).toBe('Z')
        expect(indexLetters[26]).toBe('#')
    })
})
