// The central registry of the keyboard shortcuts. A feature defines its shortcuts once at startup
// (id, keys, group and the i18n key of the description); define() rejects a shortcut whose keys
// are already taken, by another shortcut or by the app itself (Escape, the context menu keys ...),
// and an id that exists already, so a conflict shows up at startup instead of a shortcut that
// silently does nothing. What a shortcut does is bound separately: the shell binds the defaults,
// a component can bind its own handler while it is shown (the last bound one comes first; a
// handler that returns false hands the key on to the one bound before).

import { comboOf, isApplePlatform, normalizeCombo, type KeyInput } from './keys'

/** Groups of the overview, in their order. */
export const shortcutGroups = ['playback', 'volume', 'view', 'general'] as const

export type ShortcutGroup = (typeof shortcutGroups)[number]

export interface ShortcutDefinition {
    /** Id of the shortcut (data-item in the overview); unique. */
    readonly id: string
    /** Its key combinations, e.g. ["mod+arrowleft"] (see keys.ts). */
    readonly keys: readonly string[]
    readonly group: ShortcutGroup
    /** i18n key of the description in the overview. */
    readonly label: string
    /** Whether holding the key repeats the shortcut (seeking, volume). Default: once per press. */
    readonly repeat?: boolean
    /**
     * The shortcut may also use keys the app reserves (Escape), as a fallback where nothing else
     * takes them: its handlers must return false while something open uses the key itself (the
     * drawer, the full-screen player ...). Open dialogs, menus and fields keep the key anyway.
     */
    readonly fallback?: boolean
}

/** Runs a shortcut; false if it does not apply right now, so the key does what it does otherwise. */
export type ShortcutHandler = (event: KeyboardEvent) => boolean | void

/**
 * Keys the app uses itself: Escape closes menus, dialogs and the mini player, the menu key and
 * Shift+F10 open context menus, Tab and Enter belong to the focus and the buttons.
 */
export const reservedCombos: readonly string[] = [
    'escape',
    'contextmenu',
    'shift+f10',
    'tab',
    'shift+tab',
    'enter',
]

export type ShortcutConflict =
    | { readonly kind: 'combo'; readonly combo: string; readonly ids: readonly string[] }
    | { readonly kind: 'reserved'; readonly combo: string; readonly ids: readonly string[] }
    | { readonly kind: 'id'; readonly ids: readonly string[] }

/** Thrown by define() for shortcuts that conflict with each other or with the app. */
export class ShortcutConflictError extends Error {
    constructor(readonly conflicts: readonly ShortcutConflict[]) {
        super(
            `Conflicting keyboard shortcuts: ${conflicts
                .map((conflict) =>
                    conflict.kind === 'id'
                        ? `id "${conflict.ids[0]}" defined twice`
                        : conflict.kind === 'reserved'
                          ? `"${conflict.combo}" of ${conflict.ids.join(', ')} is reserved by the app`
                          : `"${conflict.combo}" used by ${conflict.ids.join(', ')}`,
                )
                .join('; ')}.`,
        )
        this.name = 'ShortcutConflictError'
    }
}

/**
 * The conflicts among shortcuts: keys used by more than one shortcut (also twice by the same one),
 * reserved keys (except for fallback shortcuts) and ids defined more than once.
 */
export function findShortcutConflicts(
    definitions: readonly ShortcutDefinition[],
): ShortcutConflict[] {
    const conflicts: ShortcutConflict[] = []
    const ids = new Map<string, number>()
    const combos = new Map<string, string[]>()
    const reserved = new Set(reservedCombos.map(normalizeCombo))
    const fallbacks = new Set(
        definitions.filter((definition) => definition.fallback).map((definition) => definition.id),
    )

    for (const definition of definitions) {
        ids.set(definition.id, (ids.get(definition.id) ?? 0) + 1)
        for (const key of definition.keys) {
            const combo = normalizeCombo(key)
            combos.set(combo, [...(combos.get(combo) ?? []), definition.id])
        }
    }

    for (const [id, count] of ids) {
        if (count > 1) {
            conflicts.push({ kind: 'id', ids: [id] })
        }
    }

    for (const [combo, users] of combos) {
        const claiming = users.filter((id) => !fallbacks.has(id))
        if (reserved.has(combo) && claiming.length > 0) {
            conflicts.push({ kind: 'reserved', combo, ids: [...new Set(claiming)] })
        } else if (users.length > 1) {
            conflicts.push({ kind: 'combo', combo, ids: users })
        }
    }

    return conflicts
}

const editableInputTypes = new Set([
    'text',
    'search',
    'email',
    'url',
    'tel',
    'password',
    'number',
    'date',
    'datetime-local',
    'month',
    'time',
    'week',
])

/** Roles of controls that act on Space themselves. */
const spaceRoles =
    'button, input, a[href], summary, [role="button"], [role="link"], [role="checkbox"], ' +
    '[role="switch"], [role="radio"], [role="tab"], [role="option"], [role="menuitem"], ' +
    '[role="menuitemradio"], [role="menuitemcheckbox"], [role="treeitem"], [role="row"], ' +
    '[role="gridcell"], [role="slider"]'

/** Roles of widgets that move or change with the arrow keys. */
const arrowRoles =
    'input[type="range"], input[type="radio"], [role="slider"], [role="radiogroup"], ' +
    '[role="radio"], [role="tablist"], [role="tab"], [role="listbox"], [role="option"], ' +
    '[role="tree"], [role="treeitem"], [role="grid"], [role="row"], [role="gridcell"], ' +
    '[role="menu"], [role="menubar"], [role="spinbutton"], [role="scrollbar"]'

/** The element of an event target, also for targets in the floating mini player window. */
function elementOf(target: EventTarget | null): Element | null {
    const node = target as Node | null
    if (!node || typeof node.nodeType !== 'number') {
        return null
    }

    return node.nodeType === 1 ? (node as Element) : node.parentElement
}

/** Whether the document has an element for the selector; false where the browser lacks it. */
function has(document: Document, selector: string): boolean {
    try {
        return document.querySelector(selector) !== null
    } catch {
        return false
    }
}

/** Whether the element is a field the user types into. */
export function isTypingTarget(element: Element | null): boolean {
    if (!element) {
        return false
    }

    if ((element as HTMLElement).isContentEditable) {
        return true
    }

    const field = element.closest(
        'input, textarea, select, [contenteditable=""], [contenteditable="true"]',
    )
    if (!field) {
        return false
    }

    if (field.tagName === 'INPUT') {
        return editableInputTypes.has(((field as HTMLInputElement).type || 'text').toLowerCase())
    }

    return true
}

/**
 * Whether a shortcut must leave the key to the element that has the focus or to what is open: a
 * field the user types into (all keys), an open modal dialog or menu (all keys), a control that
 * acts on Space itself (Space), and a widget that moves with the arrow keys (arrow keys without a
 * modifier).
 */
export function keyBelongsToTarget(event: KeyboardEvent, combo: string): boolean {
    const element = elementOf(event.target)
    if (isTypingTarget(element)) {
        return true
    }

    const document =
        element?.ownerDocument ?? (typeof window === 'undefined' ? null : window.document)
    if (
        document &&
        (has(document, 'dialog:modal') || has(document, '[role="menu"]:popover-open'))
    ) {
        return true
    }

    if (!element) {
        return false
    }

    if (element.closest('dialog[open], [role="menu"]')) {
        return true
    }

    if (combo === 'space') {
        return element.closest(spaceRoles) !== null
    }

    if (/^(shift\+)?arrow(left|right|up|down)$/.test(combo)) {
        return element.closest(arrowRoles) !== null
    }

    return false
}

export interface ShortcutRegistry {
    /** Adds shortcuts; throws a ShortcutConflictError if they conflict, and then adds none. */
    define(definitions: readonly ShortcutDefinition[]): void
    /** The defined shortcuts in the order they were defined. */
    definitions(): readonly ShortcutDefinition[]
    /** The shortcut of a combination, if any. */
    find(combo: string): ShortcutDefinition | undefined
    /** Binds a handler to a shortcut until the returned function is called. */
    bind(id: string, handler: ShortcutHandler): () => void
    /** Runs the shortcut of a key event; true if one ran (its default action is then prevented). */
    handle(event: KeyboardEvent): boolean
}

export function createShortcutRegistry(apple = isApplePlatform()): ShortcutRegistry {
    const defined: ShortcutDefinition[] = []
    const byCombo = new Map<string, ShortcutDefinition>()
    const handlers = new Map<string, ShortcutHandler[]>()

    function define(definitions: readonly ShortcutDefinition[]): void {
        const conflicts = findShortcutConflicts([...defined, ...definitions])
        if (conflicts.length > 0) {
            throw new ShortcutConflictError(conflicts)
        }

        for (const definition of definitions) {
            defined.push(definition)
            for (const key of definition.keys) {
                byCombo.set(normalizeCombo(key), definition)
            }
        }
    }

    function bind(id: string, handler: ShortcutHandler): () => void {
        if (!defined.some((definition) => definition.id === id)) {
            throw new Error(`The keyboard shortcut "${id}" is not defined.`)
        }

        handlers.set(id, [...(handlers.get(id) ?? []), handler])
        return () => {
            handlers.set(
                id,
                (handlers.get(id) ?? []).filter((bound) => bound !== handler),
            )
        }
    }

    function handle(event: KeyboardEvent): boolean {
        if (event.defaultPrevented || event.isComposing) {
            return false
        }

        const combo = comboOf(event as KeyInput, apple)
        const definition = combo ? byCombo.get(combo) : undefined
        if (!combo || !definition || keyBelongsToTarget(event, combo)) {
            return false
        }

        // A held key repeats only the shortcuts meant for it; the others take the key anyway.
        if (event.repeat && !definition.repeat) {
            event.preventDefault()
            return true
        }

        const bound = handlers.get(definition.id) ?? []
        for (let index = bound.length - 1; index >= 0; index--) {
            if (bound[index]!(event) !== false) {
                event.preventDefault()
                return true
            }
        }

        return false
    }

    return {
        define,
        definitions: () => [...defined],
        find: (combo) => byCombo.get(normalizeCombo(combo)),
        bind,
        handle,
    }
}
