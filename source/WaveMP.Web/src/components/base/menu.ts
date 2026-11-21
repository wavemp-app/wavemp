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
