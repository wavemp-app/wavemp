<script lang="ts">
export interface ListColumn {
    readonly id: string
    readonly label: string
}

export interface ListRowBase {
    readonly id: string
    /** data-state tokens of the row besides "selected", e.g. "has-video". */
    readonly state?: readonly string[]
}

export interface ListGroup<Row extends ListRowBase> {
    readonly id: string
    /**
     * Title of the group; a group without title has no heading row (e.g. the one group of a plain
     * list), and the list names its rows.
     */
    readonly title?: string
    /**
     * Position of the first row of the group in the whole list (data-position), for lists that
     * load a window of a long list.
     */
    readonly position?: number
    readonly rows: readonly Row[]
}
</script>

<script setup lang="ts" generic="Row extends ListRowBase">
import { computed, nextTick, useId, watch } from 'vue'
import { isMenuKey } from './menu'
import {
    choose,
    chooseAll,
    chooseForMenu,
    prune,
    selectionMode,
    type RowSelection,
    type SelectionMode,
} from './selection'
import { useLongPress } from '@/composables/useLongPress'

const props = withDefaults(
    defineProps<{
        columns: readonly ListColumn[]
        groups: readonly ListGroup<Row>[]
        /** Accessible name of the list. */
        label: string
        /** data-state tokens of the list, e.g. "placeholder". */
        state?: string
        /**
         * Rows have a context menu: right click, the context menu key, Shift+F10 and a long press
         * on touch screens emit row-menu instead of opening the menu of the browser.
         */
        rowMenu?: boolean
        /**
         * A tap on a touch screen activates the row (row-activate) instead of only selecting it,
         * for lists whose rows start something, e.g. playback.
         */
        activateOnTap?: boolean
        /**
         * Several rows can be selected: Ctrl or Cmd with a click or Space adds or removes a row,
         * Shift with a click or an arrow key selects a range, Ctrl+A selects all rows.
         */
        multiSelect?: boolean
    }>(),
    { state: undefined, rowMenu: false, activateOnTap: false, multiSelect: false },
)

const emit = defineEmits<{
    /** The context menu of the row; it applies to the selection, which contains the row. */
    'row-menu': [rowId: string, x: number, y: number]
    /** Double click, Enter or (with activateOnTap) a tap on a touch screen. */
    'row-activate': [rowId: string]
}>()

defineSlots<{
    cell?(props: { row: Row; column: ListColumn }): unknown
    'group-info'?(props: { group: ListGroup<Row> }): unknown
}>()

/** Id of the row with the focus: the selected row, or the one chosen last of several. */
const selected = defineModel<string | undefined>('selected', { default: undefined })

/** Ids of the selected rows in the order of the list (one row without multiSelect). */
const selection = defineModel<readonly string[]>('selection', { default: () => [] })

const listId = useId()
const allRows = computed(() => props.groups.flatMap((group) => group.rows))
const order = computed(() => allRows.value.map((row) => row.id))
const tabStop = computed(() => selected.value ?? allRows.value[0]?.id)
const selectedIds = computed(
    () =>
        new Set(
            props.multiSelect
                ? selection.value
                : selected.value === undefined
                  ? []
                  : [selected.value],
        ),
)
let anchor: string | undefined

function current(): RowSelection {
    return { ids: selection.value, anchor: anchor ?? selected.value }
}

// Rows that leave the list (e.g. removed from a playlist) leave the selection as well.
watch(order, (value) => {
    const pruned = prune(current(), value)
    if (pruned.ids.length !== selection.value.length) {
        selection.value = pruned.ids
        anchor = pruned.anchor
    }
})

function rowElementId(rowId: string): string {
    return `${listId}-row-${rowId}`
}

function titleId(groupId: string): string {
    return `${listId}-group-${groupId}`
}

function isSelected(row: Row): boolean {
    return selectedIds.value.has(row.id)
}

function rowState(row: Row): string | undefined {
    const tokens = [...(row.state ?? []), ...(isSelected(row) ? ['selected'] : [])]
    return tokens.length > 0 ? tokens.join(' ') : undefined
}

async function focusRow(rowId: string): Promise<void> {
    await nextTick()
    document.getElementById(rowElementId(rowId))?.focus()
}

function apply(next: RowSelection, rowId: string, focus: boolean): void {
    anchor = next.anchor
    selection.value = next.ids
    selected.value = rowId
    if (focus) {
        void focusRow(rowId)
    }
}

function select(rowId: string, mode: SelectionMode = 'only', focus = false): void {
    apply(choose(current(), order.value, rowId, props.multiSelect ? mode : 'only'), rowId, focus)
}

function selectForMenu(rowId: string): void {
    const next = props.multiSelect
        ? chooseForMenu(current(), order.value, rowId)
        : choose(current(), order.value, rowId, 'only')
    apply(next, rowId, false)
}

/** Kind of the pointer of the last press, to tell taps from clicks. */
let lastPointerType = ''
let menuOpened = false

const longPress = useLongPress<string>((rowId, x, y) => {
    menuOpened = true
    selectForMenu(rowId)
    emit('row-menu', rowId, x, y)
})

function onClick(event: MouseEvent, rowId: string): void {
    const mode = selectionMode(event)
    select(rowId, mode, props.multiSelect && mode !== 'only')
    if (props.activateOnTap && lastPointerType === 'touch' && !menuOpened) {
        emit('row-activate', rowId)
    }

    menuOpened = false
}

/** Shift and Ctrl with a click choose rows; they must not select the text of the list. */
function onMousedown(event: MouseEvent): void {
    if (props.multiSelect && selectionMode(event) !== 'only') {
        event.preventDefault()
    }
}

function activate(rowId: string): void {
    select(rowId)
    emit('row-activate', rowId)
}

function onContextMenu(event: MouseEvent, rowId: string): void {
    if (!props.rowMenu) {
        return
    }

    event.preventDefault()
    longPress.cancel()
    selectForMenu(rowId)
    emit('row-menu', rowId, event.clientX, event.clientY)
}

function onPointerdown(event: PointerEvent, rowId: string): void {
    lastPointerType = event.pointerType
    menuOpened = false
    if (props.rowMenu) {
        longPress.onPointerdown(event, rowId)
    }
}

function openMenuAtRow(event: KeyboardEvent, rowId: string): void {
    event.preventDefault()
    selectForMenu(rowId)
    const rect = (event.currentTarget as HTMLElement).getBoundingClientRect()
    emit('row-menu', rowId, rect.left, rect.bottom)
}

function onKeydown(event: KeyboardEvent, rowId: string): void {
    if (props.rowMenu && isMenuKey(event)) {
        openMenuAtRow(event, rowId)
        return
    }

    const rows = allRows.value
    const index = rows.findIndex((row) => row.id === rowId)
    const target = {
        ArrowDown: rows[index + 1],
        ArrowUp: rows[index - 1],
        Home: rows[0],
        End: rows[rows.length - 1],
    }[event.key]

    if (event.key === 'Enter') {
        event.preventDefault()
        activate(rowId)
        return
    }

    if (event.key === ' ') {
        event.preventDefault()
        select(rowId, selectionMode(event))
        return
    }

    if (
        props.multiSelect &&
        (event.ctrlKey || event.metaKey) &&
        !event.shiftKey &&
        event.key.toLowerCase() === 'a'
    ) {
        event.preventDefault()
        apply(chooseAll(current(), order.value), rowId, false)
        return
    }

    if (target) {
        event.preventDefault()
        if (props.multiSelect && (event.ctrlKey || event.metaKey) && !event.shiftKey) {
            // Ctrl with an arrow key moves the focus only; Ctrl+Space then adds the row.
            selected.value = target.id
            void focusRow(target.id)
        } else {
            select(target.id, event.shiftKey ? 'range' : 'only', true)
        }
    }
}
</script>

<template>
    <div
        data-part="list"
        role="grid"
        aria-readonly="true"
        :aria-label="label"
        :aria-multiselectable="multiSelect ? 'true' : undefined"
        :data-state="state"
    >
        <div data-part="list-header" role="row">
            <span
                v-for="column in columns"
                :key="column.id"
                data-part="list-column"
                role="columnheader"
                :data-column="column.id"
            >
                <span data-part="label">{{ column.label }}</span>
            </span>
        </div>
        <div
            v-for="group in groups"
            :key="group.id"
            data-part="list-group"
            role="rowgroup"
            :aria-labelledby="group.title !== undefined ? titleId(group.id) : undefined"
            :data-position="group.position"
        >
            <div v-if="group.title !== undefined" data-part="list-group-header" role="row">
                <span
                    :id="titleId(group.id)"
                    data-part="list-group-title"
                    role="rowheader"
                    :aria-colspan="columns.length"
                >
                    {{ group.title }}
                </span>
            </div>
            <div v-if="$slots['group-info']" data-part="list-group-info" role="row">
                <slot name="group-info" :group="group" />
            </div>
            <div data-part="list-group-rows" role="presentation">
                <div
                    v-for="row in group.rows"
                    :id="rowElementId(row.id)"
                    :key="row.id"
                    data-part="list-row"
                    role="row"
                    :aria-selected="isSelected(row) ? 'true' : 'false'"
                    :data-state="rowState(row)"
                    :tabindex="row.id === tabStop ? 0 : -1"
                    @click="onClick($event, row.id)"
                    @mousedown="onMousedown"
                    @dblclick="activate(row.id)"
                    @keydown="onKeydown($event, row.id)"
                    @contextmenu="onContextMenu($event, row.id)"
                    @pointerdown="onPointerdown($event, row.id)"
                    @pointermove="longPress.onPointermove"
                    @pointerup="longPress.cancel"
                    @pointercancel="longPress.cancel"
                    @pointerleave="longPress.cancel"
                >
                    <span
                        v-for="column in columns"
                        :key="column.id"
                        data-part="list-cell"
                        role="gridcell"
                        :data-column="column.id"
                    >
                        <slot name="cell" :row="row" :column="column" />
                    </span>
                </div>
            </div>
        </div>
    </div>
</template>
