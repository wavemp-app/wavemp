import { expect, type Locator, type Page } from '@playwright/test'

// Groups with one active entry (tabs, bottom navigation, administration, tree, color picker,
// segmented controls) carry an indicator part and the box of the active entry as runtime
// variables, so a theme can highlight it with CSS only (THEMING.md, "Active element of a group").

/** Shows the indicators like a theme would; the base hides them. */
export async function showIndicators(page: Page): Promise<void> {
    await page.addStyleTag({
        content: `:is([data-part='tabs'], [data-part='bottom-nav'], [data-part='admin-nav'],
            [data-part='tree'], [data-part='color-picker'], [data-part='segmented-control'])
            > [data-part='indicator'] { display: block; }`,
    })
}

export interface IndicatorState {
    readonly count: number
    readonly index: number
    /** Index of the active entry among the entries of the group, -1 without one. */
    readonly expectedIndex: number
    readonly entries: number
    /** Distances of the indicator from the box of the active entry, in pixels. */
    readonly offset: { x: number; y: number; width: number; height: number } | null
}

/**
 * Reads the runtime variables of a group and compares the box of its (shown) indicator with the
 * box of the active entry.
 */
export async function indicatorState(
    group: Locator,
    entrySelector: string,
    activeSelector: string,
): Promise<IndicatorState> {
    return group.evaluate(
        (element, selectors) => {
            const style = (element as HTMLElement).style
            const entries = [...element.querySelectorAll(selectors.entry)].filter(
                (entry) => entry.getClientRects().length > 0,
            )
            const active = element.querySelector(selectors.active)
            const indicator = element.firstElementChild
            let offset = null
            if (active && indicator?.getAttribute('data-part') === 'indicator') {
                const a = active.getBoundingClientRect()
                const i = indicator.getBoundingClientRect()
                offset = {
                    x: Math.abs(a.left - i.left),
                    y: Math.abs(a.top - i.top),
                    width: Math.abs(a.width - i.width),
                    height: Math.abs(a.height - i.height),
                }
            }
            return {
                count: Number(style.getPropertyValue('--wmp-item-count')),
                index: Number(style.getPropertyValue('--wmp-active-index')),
                expectedIndex: active ? entries.indexOf(active) : -1,
                entries: entries.length,
                offset,
            }
        },
        { entry: entrySelector, active: activeSelector },
    )
}

/** Expects the indicator of the group to lie exactly on its active entry. */
export async function expectIndicatorOnActive(
    group: Locator,
    entrySelector: string,
    activeSelector: string,
): Promise<void> {
    await expect
        .poll(async () => {
            const state = await indicatorState(group, entrySelector, activeSelector)
            const offset = state.offset
            return {
                count: state.count === state.entries,
                index: state.index === state.expectedIndex && state.index >= 0,
                box:
                    offset !== null &&
                    Math.max(offset.x, offset.y, offset.width, offset.height) <= 1,
            }
        })
        .toEqual({ count: true, index: true, box: true })
}

/** Selectors of the groups of the theme API: the entries and the active entry. */
export const indicatorGroups = {
    tabs: { entry: "[data-part='tab']", active: "[data-part='tab'][aria-current='page']" },
    'bottom-nav': {
        entry: "[data-part='bottom-nav-item']",
        active: "[data-part='bottom-nav-item'][aria-current='page']",
    },
    'admin-nav': {
        entry: "[data-part='admin-nav-item']",
        active: "[data-part='admin-nav-item'][aria-current='page']",
    },
    tree: {
        entry: "[data-part='tree-item']",
        active: "[data-part='tree-item'][aria-current='page']",
    },
    'segmented-control': {
        entry: "[data-part='segment']",
        active: "[data-part='segment'][aria-checked='true']",
    },
} as const
