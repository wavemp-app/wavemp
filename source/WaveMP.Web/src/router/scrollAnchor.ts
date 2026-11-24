// Keeps the entry a person looks at in place while entries are inserted above it, e.g. when a list
// that starts in the middle loads its previous page. The content area of the app shell scrolls
// (themes may let the window scroll instead); overflow-anchor is not used, Safari lacks it.

import { contentAreaId } from './scroll'

/**
 * Notes where the first entry (selector, inside the container) that is visible at the top of the
 * content area is, and returns a function that scrolls back so it is there again after the change.
 * An entry that was rendered anew is found again by its id. Does nothing without such an entry.
 */
export function captureScrollAnchor(
    container: Element | null | undefined,
    selector: string,
    scroller: HTMLElement | null = document.getElementById(contentAreaId),
): () => void {
    if (!container) {
        return () => {}
    }

    const top = Math.max(scroller?.getBoundingClientRect().top ?? 0, 0)
    const anchor = Array.from(container.querySelectorAll<HTMLElement>(selector)).find(
        (element) => element.getBoundingClientRect().bottom > top,
    )
    if (!anchor) {
        return () => {}
    }

    const before = anchor.getBoundingClientRect().top
    const id = anchor.id
    const document = container.ownerDocument

    return () => {
        const element = anchor.isConnected ? anchor : id ? document.getElementById(id) : null
        if (!element) {
            return
        }

        const shift = element.getBoundingClientRect().top - before
        if (scroller && shift !== 0) {
            scroller.scrollTop += shift
        }

        // What the content area could not take (it does not scroll in every theme) the window takes.
        const rest = element.getBoundingClientRect().top - before
        if (Math.abs(rest) >= 1) {
            document.defaultView?.scrollBy(0, rest)
        }
    }
}
