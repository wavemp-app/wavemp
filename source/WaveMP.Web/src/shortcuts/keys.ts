// Key combinations of the keyboard shortcuts. A combination is written as text, for example
// "space", "m", "?" or "mod+arrowleft": the modifiers first, then the key, all in lower case and
// joined with "+". "mod" is the main modifier of the platform: Ctrl, and Command on Apple devices.
// Characters are matched by the character the key produces (event.key), so "?" works with every
// keyboard layout; Shift belongs to such a character and is not part of its combination.

/** Modifiers in the order they are written. */
export const modifiers = ['mod', 'alt', 'shift'] as const

export type Modifier = (typeof modifiers)[number]

/** Names of the keys that are not a single character, by the values of event.key. */
const namedKeys: Readonly<Record<string, string>> = {
    ' ': 'space',
    Spacebar: 'space',
    Left: 'arrowleft',
    Right: 'arrowright',
    Up: 'arrowup',
    Down: 'arrowdown',
    Esc: 'escape',
}

/** Keys that are only modifiers themselves. */
const modifierKeys = new Set([
    'control',
    'meta',
    'alt',
    'altgraph',
    'shift',
    'os',
    'hyper',
    'super',
])

/** The parts of a key event a combination is made of. */
export interface KeyInput {
    readonly key: string
    readonly ctrlKey: boolean
    readonly metaKey: boolean
    readonly altKey: boolean
    readonly shiftKey: boolean
}

/** Whether the device is an Apple one, whose main modifier is Command instead of Ctrl. */
export function isApplePlatform(
    agent: { platform?: string; userAgent?: string } | undefined = typeof navigator === 'undefined'
        ? undefined
        : navigator,
): boolean {
    return /Mac|iPhone|iPad|iPod/i.test(`${agent?.platform ?? ''} ${agent?.userAgent ?? ''}`)
}

/** Whether a key name is a single character, e.g. "m" or "?". */
function isCharacter(name: string): boolean {
    return [...name].length === 1
}

/**
 * The combination of a key event, e.g. "mod+arrowleft"; null for a key without a name, a modifier
 * on its own and a modifier no combination uses (Ctrl on Apple devices, the Windows key elsewhere).
 */
export function comboOf(input: KeyInput, apple = isApplePlatform()): string | null {
    const key = input.key
    if (key === '' || key === 'Unidentified' || key === 'Dead') {
        return null
    }

    const name = namedKeys[key] ?? key.toLowerCase()
    if (modifierKeys.has(name) || (apple ? input.ctrlKey : input.metaKey)) {
        return null
    }

    const parts: string[] = []
    if (apple ? input.metaKey : input.ctrlKey) {
        parts.push('mod')
    }

    if (input.altKey) {
        parts.push('alt')
    }

    if (input.shiftKey && !isCharacter(name)) {
        parts.push('shift')
    }

    parts.push(name)
    return parts.join('+')
}

/**
 * A combination as written in a definition, e.g. "Mod+ArrowLeft", in its normal form
 * ("mod+arrowleft"). Throws for an unknown modifier or a missing key.
 */
export function normalizeCombo(combo: string): string {
    const tokens = combo.split('+').map((token) => token.trim().toLowerCase())
    const key = tokens.pop() ?? ''
    if (key === '') {
        throw new Error(`The shortcut "${combo}" has no key.`)
    }

    const used = new Set(tokens)
    for (const token of used) {
        if (!(modifiers as readonly string[]).includes(token)) {
            throw new Error(`Unknown modifier "${token}" in the shortcut "${combo}".`)
        }
    }

    return [
        ...modifiers.filter(
            (modifier) => used.has(modifier) && !(modifier === 'shift' && isCharacter(key)),
        ),
        key,
    ].join('+')
}

/** One key of a combination as the overview shows it. */
export interface ComboKey {
    /** The key, e.g. "mod", "space", "arrowleft" or "m" (data-key). */
    readonly key: string
    /** i18n key of the name of a named key. */
    readonly labelKey?: string
    /** The text of any other key, e.g. "M" or "?". */
    readonly text?: string
}

const labelKeys: Readonly<Record<string, string>> = {
    alt: 'shortcuts.keys.alt',
    shift: 'shortcuts.keys.shift',
    space: 'shortcuts.keys.space',
    arrowleft: 'shortcuts.keys.left',
    arrowright: 'shortcuts.keys.right',
    arrowup: 'shortcuts.keys.up',
    arrowdown: 'shortcuts.keys.down',
    escape: 'shortcuts.keys.escape',
    enter: 'shortcuts.keys.enter',
}

/** The keys of a combination for the overview, with the modifier of the platform. */
export function comboKeys(combo: string, apple = isApplePlatform()): ComboKey[] {
    return normalizeCombo(combo)
        .split('+')
        .map((token) => {
            if (token === 'mod') {
                return {
                    key: token,
                    labelKey: apple ? 'shortcuts.keys.command' : 'shortcuts.keys.ctrl',
                }
            }

            const labelKey = labelKeys[token]
            if (labelKey) {
                return { key: token, labelKey }
            }

            // Characters in upper case like on the keys, other keys (F1, Home ...) by their name.
            return {
                key: token,
                text: isCharacter(token)
                    ? token.toUpperCase()
                    : token.charAt(0).toUpperCase() + token.slice(1),
            }
        })
}
