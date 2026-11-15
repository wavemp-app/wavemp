import { onMounted, watch, type Ref, type WatchSource } from 'vue'

/**
 * Scrolls a scroll container just as far as needed to show an element in it (like
 * scrollIntoView with block "nearest"), but never its ancestors or the page. Does nothing if the
 * container does not scroll itself, e.g. the queue on phones, which lies in the page.
 */
export function scrollIntoContainer(container: HTMLElement, element: Element): void {
    const overflow = getComputedStyle(container).overflowY
    if ((overflow !== 'auto' && overflow !== 'scroll') || !container.contains(element)) {
        return
    }

    const outer = container.getBoundingClientRect()
    const inner = element.getBoundingClientRect()
    if (inner.top < outer.top) {
        container.scrollTop -= outer.top - inner.top
    } else if (inner.bottom > outer.bottom) {
        // An element taller than the container shows its start.
        container.scrollTop += Math.min(inner.bottom - outer.bottom, inner.top - outer.top)
    }
}

/**
 * Keeps the element that matches a selector (e.g. the current entry of the queue) in view of its
 * scroll container: when the component is mounted and whenever the source changes.
 */
export function useKeepInView(
    container: Ref<HTMLElement | null>,
    selector: string,
    changes: WatchSource<unknown>,
): void {
    function reveal(): void {
        const element = container.value
        const target = element?.querySelector(selector)
        if (element && target) {
            scrollIntoContainer(element, target)
        }
    }

    onMounted(reveal)
    watch(changes, reveal, { flush: 'post' })
}
