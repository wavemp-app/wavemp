// Keyboard and position logic of BaseContextMenu and of the elements that open a context menu.

export interface MenuEntry {
    readonly disabled?: boolean
    readonly separator?: boolean
}

export interface Point {
    readonly x: number
    readonly y: number
}

export interface Size {
    readonly width: number
    readonly height: number
}

function isFocusable(entry: MenuEntry | undefined): boolean {
    return !!entry && !entry.disabled && !entry.separator
}

/**
 * Index of the next focusable entry in the given direction, wrapping around at the ends.
 * Disabled entries and separators are skipped. Returns -1 if no entry is focusable.
 */
export function nextEnabledIndex(
    current: number,
    delta: 1 | -1,
    entries: readonly MenuEntry[],
): number {
    const count = entries.length
    for (let offset = 1; offset <= count; offset++) {
        const index = (((current + delta * offset) % count) + count) % count
        if (isFocusable(entries[index])) {
            return index
        }
    }

    return -1
}

export function firstEnabledIndex(entries: readonly MenuEntry[]): number {
    return nextEnabledIndex(-1, 1, entries)
}

export function lastEnabledIndex(entries: readonly MenuEntry[]): number {
    return nextEnabledIndex(entries.length, -1, entries)
}

/** Keeps a menu of the given size at the point within the viewport, with a small margin. */
export function clampMenuPosition(point: Point, size: Size, viewport: Size, margin = 4): Point {
    const maxX = Math.max(margin, viewport.width - size.width - margin)
    const maxY = Math.max(margin, viewport.height - size.height - margin)
    return {
        x: Math.min(Math.max(point.x, margin), maxX),
        y: Math.min(Math.max(point.y, margin), maxY),
    }
}

export interface Rect {
    readonly left: number
    readonly top: number
    readonly right: number
    readonly bottom: number
}

/**
 * Size of a menu in its layout, without the scale it grows from while it opens (motion system):
 * the position is computed for the menu as it ends up, not for a frame of its animation.
 */
export function layoutSize(element: Pick<HTMLElement, 'offsetWidth' | 'offsetHeight'>): Size {
    return { width: element.offsetWidth, height: element.offsetHeight }
}

/** Box of a menu in its layout: placed at a point (its top left corner) with its layout size. */
export function layoutRect(position: Point, size: Size): Rect {
    return {
        left: position.x,
        top: position.y,
        right: position.x + size.width,
        bottom: position.y + size.height,
    }
}

/**
 * Position of a submenu: beside its menu at the height of its entry, on the right if it fits there,
 * otherwise on the left; always kept within the viewport.
 */
export function submenuPosition(
    entry: Rect,
    menu: Rect,
    size: Size,
    viewport: Size,
    margin = 4,
): Point {
    const fitsRight = menu.right + size.width + margin <= viewport.width
    const x = fitsRight ? menu.right : menu.left - size.width
    return clampMenuPosition({ x, y: entry.top }, size, viewport, margin)
}

/** The keys that open a context menu with the keyboard: the menu key and Shift+F10. */
export function isMenuKey(event: Pick<KeyboardEvent, 'key' | 'shiftKey'>): boolean {
    return event.key === 'ContextMenu' || (event.key === 'F10' && event.shiftKey)
}

/**
 * Where a menu opens: at a point (a context menu, the default), above the element that opens it
 * (a menu of choices that grows away from the bottom of the page) or as a sheet at the bottom edge
 * of the screen (phones).
 */
export type MenuPlacement = 'point' | 'above' | 'sheet'

/** A menu above the element that opens it: its position and the height it may take at most. */
export interface MenuAboveLayout extends Point {
    readonly maxHeight: number
}

/** The room above an element for a menu that opens above it, with a gap and a margin at the top. */
export function roomAbove(anchor: Rect, margin = 4): number {
    return Math.max(0, anchor.top - 2 * margin)
}

/**
 * Position of a menu that opens above the element that opens it: its bottom edge a small gap above
 * the element, its start edge at the start of the element and kept within the viewport. A menu
 * taller than the room above scrolls; it never opens below instead, so it always opens the same way.
 */
export function menuAbovePosition(
    anchor: Rect,
    size: Size,
    viewport: Size,
    margin = 4,
): MenuAboveLayout {
    const maxHeight = roomAbove(anchor, margin)
    const height = Math.min(size.height, maxHeight)
    const maxX = Math.max(margin, viewport.width - size.width - margin)
    return {
        x: Math.min(Math.max(anchor.left, margin), maxX),
        y: anchor.top - margin - height,
        maxHeight,
    }
}

export interface TypeaheadEntry extends MenuEntry {
    readonly label?: string
}

/**
 * The entry typing jumps to: the next focusable entry after the current one whose label starts with
 * the typed text, wrapping around at the end. Typing the same letter again moves on to the next
 * entry with that letter; a longer text first tries the current entry, so it stays there while the
 * text still fits it. Returns -1 if no entry fits.
 */
export function typeaheadIndex(
    entries: readonly TypeaheadEntry[],
    current: number,
    typed: string,
    locale?: string,
): number {
    const text = typed.toLocaleLowerCase(locale)
    if (text === '' || entries.length === 0) {
        return -1
    }

    // "aaa" cycles through the entries with "a" like a single "a".
    const repeated = [...text].every((character) => character === text[0])
    const query = repeated ? text[0]! : text
    const start = repeated || current < 0 ? current + 1 : current
    for (let offset = 0; offset < entries.length; offset++) {
        const index = (((start + offset) % entries.length) + entries.length) % entries.length
        const entry = entries[index]!
        if (
            !entry.disabled &&
            !entry.separator &&
            (entry.label ?? '').trim().toLocaleLowerCase(locale).startsWith(query)
        ) {
            return index
        }
    }

    return -1
}

/** Whether a key types a character for the typeahead of a menu (no modifier, no special key). */
export function isTypeaheadKey(
    event: Pick<KeyboardEvent, 'key' | 'ctrlKey' | 'metaKey' | 'altKey'>,
): boolean {
    return (
        event.key.length === 1 &&
        event.key !== ' ' &&
        !event.ctrlKey &&
        !event.metaKey &&
        !event.altKey
    )
}
