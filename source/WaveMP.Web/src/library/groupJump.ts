// When the keyboard may open the overview of the groups of a list, and how the list scrolls to a
// chosen group. The overview opens with Ctrl+Minus or Escape only while nothing else of the app
// takes the key: elsewhere Ctrl+Minus zooms the page and Escape closes what is open, as before.

import { contentAreaId } from '@/router/scroll'

/** What the app shell has open, besides dialogs and menus (which the shortcuts leave alone). */
export interface ShellOpenState {
    /** The full-screen player of phones. */
    readonly fullPlayerExpanded: boolean
    /** The mini player in place of the app. */
    readonly compactInline: boolean
}

/** Whether the document has an element for the selector; false where the browser lacks it. */
function has(document: Document, selector: string): boolean {
    try {
        return document.querySelector(selector) !== null
    } catch {
        return false
    }
}

/**
 * Whether a key of the shortcut of the overview belongs to something else: a key pressed in another
 * document (the floating mini player), the open drawer, the full-screen player, the mini player in
 * place of the app, an open dialog or menu. Escape then closes that, Ctrl+Minus zooms the page.
 */
export function groupOverviewBlocked(
    event: Pick<Event, 'target'>,
    shell: ShellOpenState,
    appDocument: Document = document,
): boolean {
    const target = event.target as Node | null
    const eventDocument = target?.ownerDocument ?? (target as Document | null) ?? appDocument
    if (eventDocument !== appDocument) {
        return true
    }

    if (shell.fullPlayerExpanded || shell.compactInline) {
        return true
    }

    return (
        has(appDocument, "[data-part='sidebar'][data-state~='open']") ||
        has(appDocument, 'dialog[open]') ||
        has(appDocument, '[role="menu"]:popover-open')
    )
}

/** Whether the person asked for reduced motion. */
export function prefersReducedMotion(view: Window | null = globalThis.window ?? null): boolean {
    return view?.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false
}

function seconds(value: string): number {
    const number = Number.parseFloat(value)
    if (Number.isNaN(number)) {
        return 0
    }

    return value.trim().endsWith('ms') ? number / 1000 : number
}

/** The longest transition of the element (duration and delay) in milliseconds. */
export function transitionTime(element: Element): number {
    const style = element.ownerDocument.defaultView?.getComputedStyle(element)
    if (!style) {
        return 0
    }

    const durations = style.transitionDuration.split(',').map(seconds)
    const delays = style.transitionDelay.split(',').map(seconds)
    return (
        Math.max(0, ...durations.map((duration, index) => duration + (delays[index] ?? 0))) * 1000
    )
}

/**
 * Resolves when the transitions the element started have ended (at the latest after their time),
 * e.g. when a list zoomed out by the overview is back at its size, so it scrolls to the right
 * place. At once without transitions.
 */
export function afterTransition(element: Element | null | undefined): Promise<void> {
    const time = element ? transitionTime(element) : 0
    if (!element || time <= 0) {
        return Promise.resolve()
    }

    return new Promise((resolve) => {
        const done = () => {
            element.removeEventListener('transitionend', onEnd)
            clearTimeout(timer)
            resolve()
        }
        const onEnd = (event: Event) => {
            if (event.target === element) {
                done()
            }
        }
        const timer = setTimeout(done, time + 50)
        element.addEventListener('transitionend', onEnd)
    })
}

/**
 * The position (data-position) of the first group of the list that is visible at the top of the
 * content area; undefined without groups.
 */
export function firstVisiblePosition(
    groups: readonly HTMLElement[],
    scroller: HTMLElement | null = document.getElementById(contentAreaId),
): number | undefined {
    const top = Math.max(scroller?.getBoundingClientRect().top ?? 0, 0)
    const visible = groups.find((element) => element.getBoundingClientRect().bottom > top + 1)
    const position = Number(visible?.dataset.position)
    return Number.isFinite(position) ? position : undefined
}

/** Scrolls the group to the top of the content area, smoothly unless instant. */
export function scrollToGroup(element: HTMLElement, smooth: boolean): void {
    element.scrollIntoView?.({ block: 'start', behavior: smooth ? 'smooth' : 'auto' })
}
