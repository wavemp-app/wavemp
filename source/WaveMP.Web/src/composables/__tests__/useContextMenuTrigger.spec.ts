import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { longPressDelay } from '../useLongPress'
import { useContextMenuTrigger } from '../useContextMenuTrigger'

function pointer(type: string, x: number, y: number, pointerType = 'touch'): PointerEvent {
    return { type, clientX: x, clientY: y, pointerType } as PointerEvent
}

describe('useContextMenuTrigger', () => {
    beforeEach(() => {
        vi.useFakeTimers()
    })

    afterEach(() => {
        vi.useRealTimers()
    })

    it('opens on a right click at its point instead of the menu of the browser', () => {
        const open = vi.fn()
        const trigger = useContextMenuTrigger<string>(open)
        const event = { clientX: 12, clientY: 34, preventDefault: vi.fn() } as unknown as MouseEvent

        trigger.onContextmenu(event, 'tile')

        expect(event.preventDefault).toHaveBeenCalled()
        expect(open).toHaveBeenCalledWith('tile', 12, 34)
    })

    it('opens with the menu key and Shift+F10 below the focused element', () => {
        const open = vi.fn()
        const trigger = useContextMenuTrigger<string>(open)
        const target = {
            getBoundingClientRect: () => ({ left: 5, bottom: 40 }),
        } as unknown as HTMLElement
        const key = (init: Partial<KeyboardEvent>) =>
            ({ preventDefault: vi.fn(), target, defaultPrevented: false, ...init }) as KeyboardEvent

        trigger.onKeydown(key({ key: 'ContextMenu' }), 'a')
        trigger.onKeydown(key({ key: 'F10', shiftKey: true }), 'b')
        trigger.onKeydown(key({ key: 'F10' }), 'c')
        trigger.onKeydown(key({ key: 'ContextMenu', defaultPrevented: true }), 'd')

        expect(open.mock.calls).toEqual([
            ['a', 5, 40],
            ['b', 5, 40],
        ])
    })

    it('opens on a long press and swallows the click that follows', () => {
        const open = vi.fn()
        const trigger = useContextMenuTrigger<string>(open)

        trigger.onPointerdown(pointer('pointerdown', 20, 30), 'tile')
        vi.advanceTimersByTime(longPressDelay)
        expect(open).toHaveBeenCalledWith('tile', 20, 30)

        const click = { preventDefault: vi.fn(), stopPropagation: vi.fn() } as unknown as MouseEvent
        trigger.onClickCapture(click)
        expect(click.preventDefault).toHaveBeenCalled()
        expect(click.stopPropagation).toHaveBeenCalled()

        const next = { preventDefault: vi.fn(), stopPropagation: vi.fn() } as unknown as MouseEvent
        trigger.onClickCapture(next)
        expect(next.preventDefault).not.toHaveBeenCalled()
    })

    it('lets a short tap through', () => {
        const open = vi.fn()
        const trigger = useContextMenuTrigger<string>(open)

        trigger.onPointerdown(pointer('pointerdown', 20, 30), 'tile')
        vi.advanceTimersByTime(longPressDelay / 2)
        trigger.cancel()
        vi.advanceTimersByTime(longPressDelay)

        const click = { preventDefault: vi.fn(), stopPropagation: vi.fn() } as unknown as MouseEvent
        trigger.onClickCapture(click)
        expect(open).not.toHaveBeenCalled()
        expect(click.preventDefault).not.toHaveBeenCalled()
    })
})
