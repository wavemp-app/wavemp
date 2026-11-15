import {
    onBeforeUnmount,
    onMounted,
    ref,
    watch,
    type CSSProperties,
    type Ref,
    type WatchSource,
} from 'vue'

/** Position and size of the active element within its container, in CSS pixels. */
export interface IndicatorBox {
    readonly x: number
    readonly y: number
    readonly width: number
    readonly height: number
}

const emptyBox: IndicatorBox = { x: 0, y: 0, width: 0, height: 0 }

function round(value: number): number {
    return Math.round(value * 100) / 100
}

/**
 * The box of the active element relative to its container (including the scrolled distance of the
 * container); empty without an active element or while the active element is not rendered, e.g.
 * inside a collapsed group of a tree.
 */
export function indicatorBox(container: Element, active: Element | null): IndicatorBox {
    if (!active) {
        return emptyBox
    }

    const outer = container.getBoundingClientRect()
    const inner = active.getBoundingClientRect()
    if (inner.width === 0 && inner.height === 0) {
        return emptyBox
    }

    return {
        x: round(inner.left - outer.left + container.scrollLeft),
        y: round(inner.top - outer.top + container.scrollTop),
        width: round(inner.width),
        height: round(inner.height),
    }
}

/**
 * The runtime variables of the theme API for a group with one active element (e.g. the checked
 * option of a segmented control, the current tab): number of items, index of the active one and
 * its box. A theme moves a data-part="indicator" of the group to it with CSS only.
 */
export function indicatorVariables(count: number, index: number, box: IndicatorBox): CSSProperties {
    return {
        '--wmp-item-count': count,
        '--wmp-active-index': index,
        '--wmp-indicator-x': `${box.x}px`,
        '--wmp-indicator-y': `${box.y}px`,
        '--wmp-indicator-width': `${box.width}px`,
        '--wmp-indicator-height': `${box.height}px`,
    }
}

/**
 * Measures the active element of a group (e.g. the checked option of a segmented control), so
 * themes can move a data-part="indicator" to it with CSS only (--wmp-indicator-x, -y, -width and
 * -height). It measures again when the active element changes and when the container or its
 * children change their size. Without ResizeObserver it measures on changes only.
 */
export function useIndicator(
    container: Ref<HTMLElement | null>,
    activeSelector: string,
    changes: WatchSource<unknown>,
): Ref<IndicatorBox> {
    const box = ref<IndicatorBox>(emptyBox)
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
