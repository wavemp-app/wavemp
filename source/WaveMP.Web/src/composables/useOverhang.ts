import { onBeforeUnmount, readonly, ref, watch, type Ref } from 'vue'

/** The part of a box the overhang needs: its top edge and its height, in CSS pixels. */
export interface OverhangBox {
    readonly top: number
    readonly height: number
}

/**
 * How far an element reaches above the top edge of another one, in whole CSS pixels (rounded up):
 * 0 without the element, while it is not rendered (no height, e.g. display: none) or when it
 * starts at or below that edge.
 */
export function overhang(element: OverhangBox | null, base: OverhangBox | null): number {
    if (!element || !base || element.height <= 0) {
        return 0
    }

    return Math.max(0, Math.ceil(base.top - element.top))
}

function box(element: Element | null): OverhangBox | null {
    if (!element) {
        return null
    }

    const rect = element.getBoundingClientRect()
    return { top: rect.top, height: rect.height }
}

/**
 * Measures how far an element that floats above another one (e.g. a notice positioned above the
 * transport bar) reaches over its top edge, so the area there can make room for it. It measures
 * again when either element appears, goes or changes its size; without ResizeObserver it measures
 * when the elements change only.
 */
export function useOverhang(
    element: Ref<HTMLElement | null>,
    base: Ref<HTMLElement | null>,
): Readonly<Ref<number>> {
    const pixels = ref(0)
    let observer: ResizeObserver | null = null

    function measure(): void {
        pixels.value = overhang(box(element.value), box(base.value))
    }

    function disconnect(): void {
        observer?.disconnect()
        observer = null
    }

    watch(
        [element, base],
        ([shown, below]) => {
            disconnect()
            measure()
            if (typeof ResizeObserver === 'undefined') {
                return
            }

            observer = new ResizeObserver(measure)
            for (const observed of [shown, below]) {
                if (observed) {
                    observer.observe(observed)
                }
            }
        },
        { immediate: true, flush: 'post' },
    )

    onBeforeUnmount(disconnect)

    return readonly(pixels)
}
