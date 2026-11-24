// Layout options of the theme API (THEMING.md, section "Layout options"): optional parts of the
// desktop layout that a theme switches on with CSS variables on :root. The defaults in
// src/styles/tokens.css keep the DOM and the layout every theme had before the options existed, so
// a theme that sets none of them stays exactly as it is. The app reads the values from the
// computed style of the root element whenever the stylesheet of a theme is loaded or switched and
// when the desktop breakpoint is crossed (a theme may set them inside its desktop media query).
// Below the breakpoint, no option applies: phones always keep the fixed mobile base.

import { shallowRef } from 'vue'

/** Where the breadcrumb stands: in the toolbar (default) or in the header. */
export type BreadcrumbPlacement = 'toolbar' | 'header'

/** Whether the play queue is docked as a pane beside the content. */
export type QueuePanePlacement = 'none' | 'docked'

/** Whether the view options of lists of songs are offered. */
export type ViewOptionsState = 'off' | 'on'

export interface LayoutOptions {
    readonly breadcrumb: BreadcrumbPlacement
    readonly queuePane: QueuePanePlacement
    readonly viewOptions: ViewOptionsState
}

/** The variables of the theme API that hold the layout options. */
export const layoutOptionVariables = {
    breadcrumb: '--wmp-layout-breadcrumb',
    queuePane: '--wmp-layout-queue-pane',
    viewOptions: '--wmp-layout-view-options',
} as const satisfies Record<keyof LayoutOptions, string>

/** The values each option accepts; the first one is its default. */
const layoutOptionValues = {
    breadcrumb: ['toolbar', 'header'],
    queuePane: ['none', 'docked'],
    viewOptions: ['off', 'on'],
} as const satisfies {
    [K in keyof LayoutOptions]: readonly [LayoutOptions[K], ...LayoutOptions[K][]]
}

/** The options of a theme that sets none: the layout as it has always been. */
export const defaultLayoutOptions: LayoutOptions = Object.freeze({
    breadcrumb: 'toolbar',
    queuePane: 'none',
    viewOptions: 'off',
})

function parseValue<K extends keyof LayoutOptions>(
    key: K,
    read: (name: string) => string,
): LayoutOptions[K] {
    const value = read(layoutOptionVariables[key]).trim().toLowerCase()
    const allowed: readonly string[] = layoutOptionValues[key]
    return (allowed.includes(value) ? value : defaultLayoutOptions[key]) as LayoutOptions[K]
}

/**
 * The layout options from the values of their variables; `read` returns the value of a variable
 * ('' when it is not set). An empty or unknown value means the default of that option.
 */
export function parseLayoutOptions(read: (name: string) => string): LayoutOptions {
    return {
        breadcrumb: parseValue('breadcrumb', read),
        queuePane: parseValue('queuePane', read),
        viewOptions: parseValue('viewOptions', read),
    }
}

/** The layout options the active theme sets, read from the computed style of the root element. */
export function readLayoutOptions(doc: Document = document): LayoutOptions {
    const view = doc.defaultView
    if (!view) {
        return defaultLayoutOptions
    }

    const style = view.getComputedStyle(doc.documentElement)
    return parseLayoutOptions((name) => style.getPropertyValue(name))
}

function sameOptions(first: LayoutOptions, second: LayoutOptions): boolean {
    return (
        first.breadcrumb === second.breadcrumb &&
        first.queuePane === second.queuePane &&
        first.viewOptions === second.viewOptions
    )
}

/** The layout options of the active theme, as the theme sets them (on any width). */
export const layoutOptions = shallowRef<LayoutOptions>(defaultLayoutOptions)

/** Reads the layout options of the active theme again; components follow only real changes. */
export function syncLayoutOptions(doc: Document = document): void {
    const options = readLayoutOptions(doc)
    if (!sameOptions(options, layoutOptions.value)) {
        layoutOptions.value = options
    }
}

/** The options that apply: those of the theme on the desktop, the defaults on smaller screens. */
export function effectiveLayoutOptions(options: LayoutOptions, desktop: boolean): LayoutOptions {
    return desktop ? options : defaultLayoutOptions
}

/**
 * Whether the shell shows the docked play queue pane: where the options dock it, on every page but
 * Now Playing, which shows the queue itself.
 */
export function showsQueuePane(
    options: LayoutOptions,
    routeName: string | symbol | null | undefined,
): boolean {
    return options.queuePane === 'docked' && routeName !== 'now-playing'
}
