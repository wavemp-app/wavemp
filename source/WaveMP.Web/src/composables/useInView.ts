import { onBeforeUnmount, ref, watch, type ComponentPublicInstance, type Ref } from 'vue'

type Target = Element | ComponentPublicInstance | null | undefined

function elementOf(target: Target): Element | null {
    if (!target) {
        return null
    }

    if (target instanceof Element) {
        return target
    }

    const element: unknown = target.$el
    return element instanceof Element ? element : null
}

/**
 * Whether the element (or the root element of a component) is in or near the viewport, e.g. to
 * load covers and further pages only when they are needed; in the viewport of the window the
 * element is in. Without IntersectionObserver the element counts as visible.
 */
export function useInView(target: Ref<Target>, rootMargin = '200px'): Ref<boolean> {
    const supported = typeof IntersectionObserver !== 'undefined'
    const visible = ref(!supported)
    let observer: IntersectionObserver | null = null

    function disconnect(): void {
        observer?.disconnect()
        observer = null
    }

    watch(
        () => elementOf(target.value),
        (element) => {
            disconnect()
            if (!element || !supported) {
                return
            }

            // The observer of the window the element is in: an element in another window (the
            // floating mini player) never intersects the viewport of the app.
            const Observer = element.ownerDocument.defaultView?.IntersectionObserver
            observer = new (Observer ?? IntersectionObserver)(
                (entries) => {
                    visible.value = entries.some((entry) => entry.isIntersecting)
                },
                { rootMargin },
            )
            observer.observe(element)
        },
        { immediate: true, flush: 'post' },
    )

    onBeforeUnmount(disconnect)
    return visible
}
