import { onBeforeUnmount, ref, watch, type ComponentPublicInstance, type Ref } from 'vue'

type Target = Element | ComponentPublicInstance | null | undefined

type Listener = () => void

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

/** One ResizeObserver per window for all elements, so long lists do not create one per cover. */
const observers = new WeakMap<object, { type: typeof ResizeObserver; observer: ResizeObserver }>()
const listeners = new WeakMap<Element, Set<Listener>>()

function observerOf(element: Element): ResizeObserver | null {
    // The observer of the window the element is in, e.g. the floating mini player.
    const view =
        element.ownerDocument.defaultView ?? (typeof window === 'undefined' ? null : window)
    const Observer = (view as { ResizeObserver?: typeof ResizeObserver } | null)?.ResizeObserver
    if (!view || typeof Observer !== 'function') {
        return null
    }

    const known = observers.get(view)
    if (known?.type === Observer) {
        return known.observer
    }

    const observer = new Observer((entries) => {
        for (const entry of entries) {
            listeners.get(entry.target)?.forEach((listener) => listener())
        }
    })
    observers.set(view, { type: Observer, observer })
    return observer
}

/**
 * The width an element is laid out with in CSS pixels: its layout width, which transforms (e.g.
 * the opening animation of a player) do not change; 0 while it is not laid out.
 */
export function layoutWidthOf(element: Element | null): number {
    if (!element) {
        return 0
    }

    if (element instanceof HTMLElement) {
        return element.offsetWidth
    }

    return element.getBoundingClientRect().width
}

/**
 * The larger side an element is laid out with in CSS pixels: its layout width, or its layout
 * height where that is larger (e.g. a picture that fills a tile higher than wide); 0 while it is
 * not laid out.
 */
export function layoutSizeOf(element: Element | null): number {
    if (!element) {
        return 0
    }

    if (element instanceof HTMLElement) {
        return Math.max(element.offsetWidth, element.offsetHeight)
    }

    const box = element.getBoundingClientRect()
    return Math.max(box.width, box.height)
}

/** The pixel density of the screen the element is shown on; 1 where it is unknown. */
export function pixelRatioOf(element: Element | null): number {
    const view =
        element?.ownerDocument.defaultView ?? (typeof window === 'undefined' ? null : window)
    const ratio = view?.devicePixelRatio
    return typeof ratio === 'number' && Number.isFinite(ratio) && ratio > 0 ? ratio : 1
}

/**
 * Counts the changes of the size an element (or the root element of a component) is shown with:
 * of its layout width (ResizeObserver) and of the pixel density of the screen, which changes when
 * the window moves to another screen or the page is zoomed. Components read it in a watcher and
 * measure again with layoutWidthOf() and pixelRatioOf(), e.g. to load a larger picture when a
 * cover grows, like the cover of the mini player that becomes the full-screen player on phones.
 * Without ResizeObserver or matchMedia the count does not change.
 */
export function useShownSize(target: Ref<Target>): Readonly<Ref<number>> {
    const changes = ref(0)
    let observed: Element | null = null
    let resolution: MediaQueryList | null = null

    function changed(): void {
        changes.value++
    }

    function unobserve(): void {
        if (!observed) {
            return
        }

        const own = listeners.get(observed)
        own?.delete(changed)
        if (own && own.size === 0) {
            listeners.delete(observed)
            observerOf(observed)?.unobserve(observed)
        }

        observed = null
    }

    function observe(element: Element | null): void {
        unobserve()
        const observer = element ? observerOf(element) : null
        if (!element || !observer) {
            return
        }

        observed = element
        let own = listeners.get(element)
        if (!own) {
            own = new Set()
            listeners.set(element, own)
            observer.observe(element)
        }

        own.add(changed)
    }

    /** Listens for the next change of the pixel density (a query only matches one density). */
    function listenToResolution(): void {
        resolution?.removeEventListener('change', onResolution)
        resolution = null
        if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') {
            return
        }

        resolution = window.matchMedia(`(resolution: ${pixelRatioOf(null)}dppx)`)
        resolution.addEventListener('change', onResolution)
    }

    function onResolution(): void {
        listenToResolution()
        changed()
    }

    watch(() => elementOf(target.value), observe, { immediate: true, flush: 'post' })
    listenToResolution()

    onBeforeUnmount(() => {
        unobserve()
        resolution?.removeEventListener('change', onResolution)
        resolution = null
    })

    return changes
}
