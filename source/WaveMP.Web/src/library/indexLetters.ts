// The letter a text is filed under in an index A to Z, the same rule as IndexLetters of the server
// (WaveMP.Core.Library), whose tests use the same table of cases.

/** The entry for digits and special characters. */
export const otherIndexLetter = '#'

/** The entries of an index in their order: A to Z, then the entry for everything else. */
export const indexLetters: readonly string[] = [
    ...Array.from({ length: 26 }, (_, index) => String.fromCharCode(65 + index)),
    otherIndexLetter,
]

/**
 * The first character of the text in Unicode NFD (so accents do not count), an ASCII letter in
 * upper case; everything else (digits, punctuation, other scripts, letters without a decomposition
 * to A to Z such as the sharp s, an empty text) is filed under "#". Independent of the language.
 */
export function indexLetterOf(text: string | null | undefined): string {
    if (!text) {
        return otherIndexLetter
    }

    // A character outside the basic plane never decomposes to A to Z; a lone surrogate is none.
    const code = text.charCodeAt(0)
    if (code >= 0xd800 && code <= 0xdfff) {
        return otherIndexLetter
    }

    const first = text.charAt(0).normalize('NFD').charAt(0)
    return /^[A-Za-z]$/.test(first) ? first.toUpperCase() : otherIndexLetter
}
