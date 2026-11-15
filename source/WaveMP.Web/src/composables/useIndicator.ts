import { onBeforeUnmount, onMounted, ref, watch, type Ref, type WatchSource } from 'vue'

/** Position and width of the active element within its container, in CSS pixels. */
export interface IndicatorBox {
    readonly x: number
    readonly width: number
}

/** The box of the active element relative to its container; zero without an active element. */
export function indicatorBox(container: Element, active: Element | null): IndicatorBox {
    if (!active) {
        return { x: 0, width: 0 }
    }

    const outer = container.getBoundingClientRect()
    const inner = active.getBoundingClientRect()
    return {
        x: Math.round((inner.left - outer.left + container.scrollLeft) * 100) / 100,
        width: Math.round(inner.width * 100) / 100,
    }
}

/**
 * Measures the active element of a group (e.g. the checked option of a segmented control), so
 * themes can move a data-part="indicator" to it with CSS only (--wmp-indicator-x and
 * --wmp-indicator-width). It measures again when the active element changes and when the container
 * or its children change their size. Without ResizeObserver it measures on changes only.
 */
export function useIndicator(
    container: Ref<HTMLElement | null>,
    activeSelector: string,
    changes: WatchSource<unknown>,
): Ref<IndicatorBox> {
    const box = ref<IndicatorBox>({ x: 0, width: 0 })
    let observer: ResizeObserver | null = null

    function measure(): void {
        const element = container.value
        if (element) {
            box.value = indicatorBox(element, element.querySelector(activeSelector))
        }
    }

    watch(changes, measure, { flush: 'post' })

    onMounted(() => {
        measure()
        const element = container.value
        if (element && typeof ResizeObserver !== 'undefined') {
            observer = new ResizeObserver(measure)
            observer.observe(element)
            for (const child of element.children) {
                observer.observe(child)
            }
        }
    })

    onBeforeUnmount(() => {
        observer?.disconnect()
        observer = null
    })

    return box
}
