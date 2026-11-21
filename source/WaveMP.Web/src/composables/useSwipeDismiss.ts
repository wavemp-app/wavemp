import { computed, onBeforeUnmount, onMounted, ref, watch, type Ref } from 'vue'
import {
    SwipeTracker,
    swipeCloseDuration,
    swipeOutcome,
    swipeProgress,
    swipeSnapDuration,
    swipeStartThreshold,
    type SwipePhase,
} from '@/components/shell/swipe'
import { useMediaQuery } from './useMediaQuery'

export interface SwipeDismissOptions {
    /** Whether the element can be swiped away right now. */
    enabled: () => boolean
    /** The element was swiped away; called when it has left the screen. */
    onDismiss: () => void
}

/** Where a drag never starts: controls that take the finger themselves. */
const excludedTargets =
    "[data-part='slider'], input, select, textarea, [data-part='visualization-controls']"

/**
 * Swiping an element down to close it (the full-screen player on mobile), with a finger (touch
 * events, so the drag can keep the page from scrolling) or a mouse or pen (pointer events). A drag
 * starts when the finger moves down from the top of the element (not scrolled) by
 * swipeStartThreshold pixels, more down than sideways, and not on a slider or a field. While it
 * drags, offset follows the finger; when it lets go, the element closes or snaps back (swipe.ts).
 * The phase, the offset, the progress and the duration of the movement after the finger let go are
 * for the theme API; a click after a drag is swallowed, so the drag does not also tap a button.
 */
export function useSwipeDismiss(element: Ref<HTMLElement | null>, options: SwipeDismissOptions) {
    const phase = ref<SwipePhase>('idle')
    const offset = ref(0)
    const height = ref(0)
    /** Duration of the movement after the finger let go, in milliseconds. */
    const duration = ref(0)
    const progress = computed(() => swipeProgress(offset.value, height.value))
    const reducedMotion = useMediaQuery('(prefers-reduced-motion: reduce)')

    const tracker = new SwipeTracker()
    let pressed: { x: number; y: number } | null = null
    let pointerId: number | null = null
    let touchId: number | null = null
    let timer: ReturnType<typeof setTimeout> | undefined
    let swallowClick = false
    let swallowTimer: ReturnType<typeof setTimeout> | undefined

    function clearTimer(): void {
        clearTimeout(timer)
        timer = undefined
    }

    function reset(): void {
        clearTimer()
        followTouch(null)
        pressed = null
        pointerId = null
        touchId = null
        phase.value = 'idle'
        offset.value = 0
        duration.value = 0
    }

    function canStart(target: EventTarget | null): boolean {
        const root = element.value
        if (!root || !options.enabled() || phase.value === 'dismissing') {
            return false
        }

        if (root.scrollTop > 0) {
            return false
        }

        return !(target instanceof Element && target.closest(excludedTargets))
    }

    function press(x: number, y: number): void {
        pressed = { x, y }
    }

    /** The finger moved; true while it drags the element. */
    function drag(x: number, y: number, time: number): boolean {
        if (phase.value !== 'dragging') {
            if (!pressed) {
                return false
            }

            const dx = x - pressed.x
            const dy = y - pressed.y
            if (Math.abs(dx) < swipeStartThreshold && Math.abs(dy) < swipeStartThreshold) {
                return false
            }

            // Sideways or upwards: not a swipe; the page scrolls or the control takes it.
            if (dy <= Math.abs(dx)) {
                pressed = null
                return false
            }

            clearTimer()
            phase.value = 'dragging'
            height.value = element.value?.clientHeight ?? 0
            duration.value = 0
            tracker.begin(y, time)
        }

        offset.value = tracker.move(y, time)
        return true
    }

    function swallowNextClick(): void {
        swallowClick = true
        clearTimeout(swallowTimer)
        swallowTimer = setTimeout(() => (swallowClick = false), 400)
    }

    function release(time: number): void {
        followTouch(null)
        pressed = null
        pointerId = null
        touchId = null
        if (phase.value !== 'dragging') {
            return
        }

        swallowNextClick()
        const velocity = tracker.velocity(time)
        if (swipeOutcome(offset.value, velocity, height.value) === 'close') {
            const ms = reducedMotion.value
                ? 0
                : swipeCloseDuration(offset.value, velocity, height.value)
            phase.value = 'dismissing'
            duration.value = ms
            offset.value = height.value
            timer = setTimeout(() => {
                timer = undefined
                options.onDismiss()
                reset()
            }, ms)
        } else {
            snapBack()
        }
    }

    function snapBack(): void {
        const ms = reducedMotion.value ? 0 : swipeSnapDuration
        phase.value = 'settling'
        duration.value = ms
        offset.value = 0
        timer = setTimeout(() => {
            timer = undefined
            phase.value = 'idle'
            duration.value = 0
        }, ms)
    }

    function cancel(): void {
        followTouch(null)
        pressed = null
        pointerId = null
        touchId = null
        if (phase.value === 'dragging') {
            snapBack()
        }
    }

    // --- Touch ------------------------------------------------------------------------------

    function touchOf(list: TouchList): Touch | undefined {
        return [...list].find((touch) => touch.identifier === touchId)
    }

    function onTouchstart(event: TouchEvent): void {
        const touch = event.touches[0]
        if (event.touches.length !== 1 || !touch) {
            cancel()
            return
        }

        if (canStart(event.target)) {
            touchId = touch.identifier
            press(touch.clientX, touch.clientY)
            followTouch(event.target)
        }
    }

    function onTouchmove(event: TouchEvent): void {
        const touch = touchId === null ? undefined : touchOf(event.touches)
        if (!touch) {
            return
        }

        // Pulling down from the top scrolls nothing: the browser does not take the gesture over
        // (or refresh the page) before the drag starts, and the element moves instead.
        const dragging = drag(touch.clientX, touch.clientY, event.timeStamp)
        const down = pressed !== null && touch.clientY > pressed.y
        if ((dragging || down) && event.cancelable) {
            event.preventDefault()
        }
    }

    function onTouchend(event: TouchEvent): void {
        if (touchId !== null && touchOf(event.changedTouches)) {
            release(event.timeStamp)
        }
    }

    function onTouchcancel(event: TouchEvent): void {
        if (touchId !== null && touchOf(event.changedTouches)) {
            cancel()
        }
    }

    const touchListeners: readonly (readonly [string, EventListener, AddEventListenerOptions])[] = [
        ['touchmove', onTouchmove as EventListener, { passive: false }],
        ['touchend', onTouchend as EventListener, { passive: true }],
        ['touchcancel', onTouchcancel as EventListener, { passive: true }],
    ]

    let touchTarget: EventTarget | null = null

    /**
     * The moves and the end of a touch go to the element it started on, also when that element
     * leaves the page meanwhile (another song replaces the cover); so they are followed there,
     * not where they would bubble to.
     */
    function followTouch(target: EventTarget | null): void {
        if (touchTarget === target) {
            return
        }

        for (const [name, listener, listenerOptions] of touchListeners) {
            touchTarget?.removeEventListener(name, listener, listenerOptions)
        }
        touchTarget = target
        for (const [name, listener, listenerOptions] of touchListeners) {
            touchTarget?.addEventListener(name, listener, listenerOptions)
        }
    }

    // --- Mouse and pen ----------------------------------------------------------------------

    function onPointerdown(event: PointerEvent): void {
        if (event.pointerType === 'touch' || event.button !== 0 || !canStart(event.target)) {
            return
        }

        pointerId = event.pointerId
        press(event.clientX, event.clientY)
    }

    function onPointermove(event: PointerEvent): void {
        if (event.pointerId !== pointerId) {
            return
        }

        const wasDragging = phase.value === 'dragging'
        if (drag(event.clientX, event.clientY, event.timeStamp) && !wasDragging) {
            element.value?.setPointerCapture?.(event.pointerId)
        }
    }

    function onPointerup(event: PointerEvent): void {
        if (event.pointerId === pointerId) {
            release(event.timeStamp)
        }
    }

    function onPointercancel(event: PointerEvent): void {
        if (event.pointerId === pointerId) {
            cancel()
        }
    }

    /** A picture dragged with the mouse would start the drag and drop of the browser. */
    function onDragstart(event: DragEvent): void {
        if (pressed || phase.value === 'dragging') {
            event.preventDefault()
        }
    }

    function onClick(event: MouseEvent): void {
        if (swallowClick) {
            swallowClick = false
            event.preventDefault()
            event.stopPropagation()
        }
    }

    const listeners: readonly (readonly [string, EventListener, AddEventListenerOptions?])[] = [
        ['touchstart', onTouchstart as EventListener, { passive: true }],
        ['pointerdown', onPointerdown as EventListener],
        ['pointermove', onPointermove as EventListener],
        ['pointerup', onPointerup as EventListener],
        ['pointercancel', onPointercancel as EventListener],
        ['dragstart', onDragstart as EventListener],
        ['click', onClick as EventListener, { capture: true }],
    ]

    let attached: HTMLElement | null = null

    function attach(target: HTMLElement | null): void {
        if (attached) {
            for (const [name, listener, listenerOptions] of listeners) {
                attached.removeEventListener(name, listener, listenerOptions)
            }
        }

        attached = target
        if (target) {
            for (const [name, listener, listenerOptions] of listeners) {
                target.addEventListener(name, listener, listenerOptions)
            }
        }
    }

    onMounted(() => attach(element.value))
    watch(element, (target) => attach(target))

    // Closed in another way (the button, a key, navigating): the gesture ends.
    watch(
        () => options.enabled(),
        (enabled) => {
            if (!enabled) {
                reset()
            }
        },
    )

    onBeforeUnmount(() => {
        attach(null)
        followTouch(null)
        clearTimer()
        clearTimeout(swallowTimer)
    })

    return { phase, offset, progress, duration }
}
