import { onBeforeUnmount, toValue, watch, type MaybeRefOrGetter, type Ref } from 'vue'

/** How close the two fingers must come, as a share of their distance at the start. */
export const pinchInRatio = 0.7

/** The page counts as not zoomed up to this scale of the visual viewport. */
const unzoomedScale = 1.01

interface Point {
    readonly clientX: number
    readonly clientY: number
}

function distance(first: Point, second: Point): number {
    return Math.hypot(first.clientX - second.clientX, first.clientY - second.clientY)
}

/**
 * Calls onPinch once when two fingers that both touched the element move together (to 0.7 of their
 * distance at the start), e.g. to open the overview of the groups of a list. Only while the page is
 * not zoomed (scale of the visual viewport 1): the listeners are passive and never prevent the
 * gesture, and the element keeps its touch-action, so the browser still zooms the page with two
 * fingers (spreading them, and pinching a zoomed page back). A third finger cancels the gesture.
 */
export function usePinchIn(
    target: Ref<HTMLElement | null | undefined>,
    onPinch: () => void,
    enabled: MaybeRefOrGetter<boolean> = true,
): void {
    let start = 0
    let fired = false
    let element: HTMLElement | null = null

    function zoomed(): boolean {
        const scale = element?.ownerDocument.defaultView?.visualViewport?.scale ?? 1
        return scale > unzoomedScale
    }

    function reset(): void {
        start = 0
        fired = false
    }

    function onTouchstart(event: TouchEvent): void {
        const touches = event.touches
        if (touches.length !== 2) {
            reset()
            return
        }

        const [first, second] = [touches[0]!, touches[1]!]
        const inside = [first, second].every(
            (touch) => touch.target instanceof Node && !!element?.contains(touch.target),
        )
        if (!inside || !toValue(enabled) || zoomed()) {
            reset()
            return
        }

        start = distance(first, second)
        fired = false
    }

    function onTouchmove(event: TouchEvent): void {
        if (start <= 0 || fired || event.touches.length !== 2) {
            return
        }

        const now = distance(event.touches[0]!, event.touches[1]!)
        if (now <= start * pinchInRatio && toValue(enabled) && !zoomed()) {
            fired = true
            onPinch()
        }
    }

    function onTouchend(event: TouchEvent): void {
        if (event.touches.length < 2) {
            reset()
        }
    }

    const options: AddEventListenerOptions = { passive: true }

    function detach(): void {
        element?.removeEventListener('touchstart', onTouchstart, options)
        element?.removeEventListener('touchmove', onTouchmove, options)
        element?.removeEventListener('touchend', onTouchend, options)
        element?.removeEventListener('touchcancel', onTouchend, options)
        element = null
        reset()
    }

    watch(
        target,
        (next) => {
            detach()
            element = next ?? null
            element?.addEventListener('touchstart', onTouchstart, options)
            element?.addEventListener('touchmove', onTouchmove, options)
            element?.addEventListener('touchend', onTouchend, options)
            element?.addEventListener('touchcancel', onTouchend, options)
        },
        { immediate: true },
    )

    onBeforeUnmount(detach)
}
