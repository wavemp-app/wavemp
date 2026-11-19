import { isMenuKey } from '@/components/base/menu'
import { useLongPress } from './useLongPress'

/**
 * The ways to open the context menu of an element: a right click (contextmenu event), a long press
 * with a finger and the menu key or Shift+F10 while the element or an element in it has the focus.
 * The keyboard opens the menu below the element. After a long press, the click that follows when
 * the finger is lifted is swallowed (onClickCapture), so a tile does not open its page as well.
 * The payload (e.g. the key of a queue entry) is handed to open with the point.
 */
export function useContextMenuTrigger<T>(open: (payload: T, x: number, y: number) => void) {
    let pressed = false

    const longPress = useLongPress<T>((payload, x, y) => {
        pressed = true
        open(payload, x, y)
    })

    function onContextmenu(event: MouseEvent, payload: T): void {
        event.preventDefault()
        longPress.cancel()
        open(payload, event.clientX, event.clientY)
    }

    function onKeydown(event: KeyboardEvent, payload: T): void {
        if (!isMenuKey(event) || event.defaultPrevented) {
            return
        }

        event.preventDefault()
        const rect = (
            (event.target as HTMLElement | null) ?? (event.currentTarget as HTMLElement)
        ).getBoundingClientRect()
        open(payload, rect.left, rect.bottom)
    }

    function onPointerdown(event: PointerEvent, payload: T): void {
        pressed = false
        longPress.onPointerdown(event, payload)
    }

    function onClickCapture(event: MouseEvent): void {
        if (pressed) {
            pressed = false
            event.preventDefault()
            event.stopPropagation()
        }
    }

    return {
        onContextmenu,
        onKeydown,
        onPointerdown,
        onPointermove: longPress.onPointermove,
        cancel: longPress.cancel,
        onClickCapture,
    }
}
