// Multi-selection of the rows of a list (BaseList): a click selects one row, Ctrl or Cmd adds or
// removes a row, Shift selects all rows from the anchor (the row chosen last without Shift) to the
// row. The selection keeps the order of the list.

export interface RowSelection {
    /** Ids of the selected rows in the order of the list. */
    readonly ids: readonly string[]
    /** Row a range starts at; undefined before the first choice. */
    readonly anchor: string | undefined
}

export type SelectionMode = 'only' | 'toggle' | 'range'

export const emptySelection: RowSelection = { ids: [], anchor: undefined }

/** The mode of a click or a key press with the modifier keys. */
export function selectionMode(event: {
    readonly ctrlKey?: boolean
    readonly metaKey?: boolean
    readonly shiftKey?: boolean
}): SelectionMode {
    if (event.shiftKey) {
        return 'range'
    }

    return event.ctrlKey || event.metaKey ? 'toggle' : 'only'
}

/** Ids in the order of the list; ids that are not in the list are left out. */
function inOrder(order: readonly string[], ids: ReadonlySet<string>): string[] {
    return order.filter((id) => ids.has(id))
}

/** The selection after choosing a row in the mode. */
export function choose(
    selection: RowSelection,
    order: readonly string[],
    id: string,
    mode: SelectionMode,
): RowSelection {
    if (!order.includes(id)) {
        return selection
    }

    if (mode === 'toggle') {
        const ids = new Set(selection.ids)
        if (ids.has(id)) {
            ids.delete(id)
        } else {
            ids.add(id)
        }
        return { ids: inOrder(order, ids), anchor: id }
    }

    if (mode === 'range' && selection.anchor !== undefined && order.includes(selection.anchor)) {
        const from = order.indexOf(selection.anchor)
        const to = order.indexOf(id)
        return {
            ids: order.slice(Math.min(from, to), Math.max(from, to) + 1),
            anchor: selection.anchor,
        }
    }

    return { ids: [id], anchor: id }
}

/** Selects all rows of the list; the anchor stays. */
export function chooseAll(selection: RowSelection, order: readonly string[]): RowSelection {
    return { ids: [...order], anchor: selection.anchor ?? order[0] }
}

/**
 * The selection for the context menu of a row: a selected row keeps the selection, so the menu
 * applies to all selected rows; any other row becomes the only selected one.
 */
export function chooseForMenu(
    selection: RowSelection,
    order: readonly string[],
    id: string,
): RowSelection {
    return selection.ids.includes(id) ? selection : choose(selection, order, id, 'only')
}

/** The selection without rows that are no longer in the list. */
export function prune(selection: RowSelection, order: readonly string[]): RowSelection {
    const ids = inOrder(order, new Set(selection.ids))
    const anchor =
        selection.anchor !== undefined && order.includes(selection.anchor)
            ? selection.anchor
            : undefined
    return ids.length === selection.ids.length && anchor === selection.anchor
        ? selection
        : { ids, anchor }
}
