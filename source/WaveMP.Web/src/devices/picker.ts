// Where the device picker opens and which device the keyboard reaches next. Pure functions for
// DevicesControl.vue.

import { roomAbove, type Point, type Rect, type Size } from '@/components/base/menu'

/**
 * Where the device picker opens: above its button (desktop), below it (a theme that moves the
 * player to the top of the window) or as a sheet (phones).
 */
export type PickerPlacement = 'above' | 'below' | 'sheet'

export interface PickerLayout extends Point {
    readonly placement: 'above' | 'below'
    /** Height the picker may take at most, in pixels. */
    readonly maxHeight: number
}

/** The room below an element for a picker that opens below it, with a gap and a margin. */
export function roomBelow(anchor: Rect, viewport: Size, margin = 4): number {
    return Math.max(0, viewport.height - anchor.bottom - 2 * margin)
}

/**
 * Position of the device picker at its button: above it, its bottom edge a small gap above the
 * button, unless it does not fit there and the room below is larger (a theme can move the player
 * to the top of the window); then below it. Its end edge lies at the end of the button (the button
 * sits at the end of the bar), kept within the viewport. A picker taller than its room scrolls.
 */
export function pickerPosition(anchor: Rect, size: Size, viewport: Size, margin = 4): PickerLayout {
    const above = roomAbove(anchor, margin)
    const below = roomBelow(anchor, viewport, margin)
    const placement = size.height <= above || above >= below ? 'above' : 'below'
    const maxHeight = placement === 'above' ? above : below
    const height = Math.min(size.height, maxHeight)
    const maxX = Math.max(margin, viewport.width - size.width - margin)
    return {
        placement,
        x: Math.min(Math.max(anchor.right - size.width, margin), maxX),
        y:
            placement === 'above'
                ? Math.max(margin, anchor.top - margin - height)
                : anchor.bottom + margin,
        maxHeight,
    }
}

/**
 * The device the arrow keys move the focus to in the list of the picker, wrapping around; Home and
 * End reach the first and the last one. Unlike a segmented control, moving does not choose: a
 * device is chosen with Enter, Space or a click, because choosing hands playback over. Devices that
 * cannot be chosen stay reachable, so their line can be read. Returns null for other keys.
 */
export function nextDeviceIndex(key: string, current: number, count: number): number | null {
    if (count <= 0) {
        return null
    }

    const from = current < 0 ? 0 : current
    switch (key) {
        case 'ArrowDown':
        case 'ArrowRight':
            return current < 0 ? 0 : (from + 1) % count
        case 'ArrowUp':
        case 'ArrowLeft':
            return current < 0 ? count - 1 : (from - 1 + count) % count
        case 'Home':
            return 0
        case 'End':
            return count - 1
        default:
            return null
    }
}

/** The device in the tab order of the list: the one that plays, otherwise this device. */
export function deviceTabStop(
    entries: readonly { readonly active: boolean; readonly current: boolean }[],
): number {
    const active = entries.findIndex((entry) => entry.active)
    if (active >= 0) {
        return active
    }

    const current = entries.findIndex((entry) => entry.current)
    return current < 0 ? 0 : current
}
