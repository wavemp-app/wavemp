<script setup lang="ts">
import { computed, nextTick, onDeactivated, ref, useId } from 'vue'
import BaseIcon from './BaseIcon.vue'
import {
    clampMenuPosition,
    firstEnabledIndex,
    isMenuKey,
    isTypeaheadKey,
    lastEnabledIndex,
    layoutRect,
    layoutSize,
    menuAbovePosition,
    nextEnabledIndex,
    roomAbove,
    submenuPosition,
    typeaheadIndex,
    type MenuPlacement,
    type Rect,
} from './menu'
import type { IconSlotName } from '@/themes/iconSlots'

export interface MenuItem {
    readonly id: string
    readonly label?: string
    readonly icon?: IconSlotName
    readonly disabled?: boolean
    readonly separator?: boolean
    /** Entry of a choice (role menuitemradio): whether it is the chosen one. */
    readonly checked?: boolean
    /** Entries of a submenu, which opens beside the entry. */
    readonly children?: readonly MenuItem[]
}

/**
 * Context menu on the Popover API: light dismiss and Escape come from the browser. An entry with
 * children opens them as a submenu (a nested popover) beside it: by pointer, with Enter, Space or
 * the right arrow key; the left arrow key and Escape close it again. Typing letters moves to the
 * next entry whose label starts with them. A context menu opens at a point (openAt); a menu of
 * choices opens above the element that opens it or as a sheet at the bottom of the screen
 * (openAnchored), with the chosen entry focused, and scrolls when it is taller than its room.
 */
const props = defineProps<{
    items: readonly MenuItem[]
    /** Accessible name of the menu. */
    label: string
}>()

const emit = defineEmits<{
    select: [id: string]
}>()

const menu = ref<HTMLElement | null>(null)
const submenu = ref<HTMLElement | null>(null)
const isOpen = ref(false)
const x = ref(0)
const y = ref(0)
const submenuId = ref<string | null>(null)
const submenuX = ref(0)
const submenuY = ref(0)
const submenuElementId = useId()
const placement = ref<MenuPlacement>('point')
/** Height the menu may take at most (placement above), in pixels; null without a limit. */
const maxHeight = ref<number | null>(null)
/** Width of the element the menu opens from (placement above), in pixels. */
const anchorWidth = ref<number | null>(null)
let returnFocus: HTMLElement | null = null
let typed = ''
let typedAt = 0

/** Typed letters belong together while they follow each other within this time. */
const typeaheadTimeout = 700

/** A length in pixels for a runtime variable; without a value the variable stays unset. */
function pixels(value: number | null): string | undefined {
    return value === null ? undefined : `${value}px`
}

const hasSubmenus = computed(() => props.items.some((item) => item.children))
const submenuItem = computed(() => props.items.find((item) => item.id === submenuId.value))
const submenuEntries = computed(() => submenuItem.value?.children ?? [])

function entriesOf(items: readonly MenuItem[]): MenuItem[] {
    return items.filter((item) => !item.separator)
}

/** The entries of a menu element itself, without those of its submenu. */
function itemElements(container: HTMLElement | null): HTMLElement[] {
    return [...(container?.querySelectorAll<HTMLElement>(':scope > [data-part="menu-item"]') ?? [])]
}

function focusEntry(container: HTMLElement | null, items: readonly MenuItem[], index: number) {
    const id = entriesOf(items)[index]?.id
    itemElements(container)
        .find((element) => element.dataset.item === id)
        ?.focus()
}

/** Opens the menu at a point in viewport coordinates (e.g. of a contextmenu event). */
async function openAt(pointX: number, pointY: number): Promise<void> {
    const element = menu.value
    if (!element) {
        return
    }

    if (!isOpen.value) {
        returnFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null
    }

    closeSubmenu()
    placement.value = 'point'
    maxHeight.value = null
    anchorWidth.value = null
    x.value = pointX
    y.value = pointY
    if (!element.matches?.(':popover-open')) {
        element.showPopover?.()
    }
    isOpen.value = true
    await nextTick()

    const position = clampMenuPosition({ x: pointX, y: pointY }, layoutSize(element), {
        width: window.innerWidth,
        height: window.innerHeight,
    })
    x.value = position.x
    y.value = position.y
    focusEntry(element, props.items, firstEnabledIndex(entriesOf(props.items)))
}

/**
 * Opens a menu of choices from the element that opens it (its box in viewport coordinates): above
 * it, at least as wide as it and at most as tall as the room above, or as a sheet at the bottom of
 * the screen. The chosen entry (checked) is focused, otherwise the first one.
 */
async function openAnchored(anchor: Rect, where: 'above' | 'sheet'): Promise<void> {
    const element = menu.value
    if (!element) {
        return
    }

    if (!isOpen.value) {
        returnFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null
    }

    closeSubmenu()
    placement.value = where
    anchorWidth.value = where === 'above' ? anchor.right - anchor.left : null
    maxHeight.value = where === 'above' ? roomAbove(anchor) : null
    x.value = where === 'above' ? anchor.left : 0
    y.value = where === 'above' ? anchor.top : 0
    if (!element.matches?.(':popover-open')) {
        element.showPopover?.()
    }
    isOpen.value = true
    await nextTick()

    if (where === 'above') {
        const layout = menuAbovePosition(anchor, layoutSize(element), {
            width: window.innerWidth,
            height: window.innerHeight,
        })
        x.value = layout.x
        y.value = layout.y
    }

    const entries = entriesOf(props.items)
    const checked = entries.findIndex((entry) => entry.checked && !entry.disabled)
    focusEntry(element, props.items, checked >= 0 ? checked : firstEnabledIndex(entries))
}

/** Moves the focus to the entry whose label starts with the typed letters; true if it took the key. */
function typeahead(
    event: KeyboardEvent,
    container: HTMLElement | null,
    items: readonly MenuItem[],
) {
    if (!isTypeaheadKey(event)) {
        return false
    }

    const now = event.timeStamp
    typed = now - typedAt > typeaheadTimeout ? event.key : typed + event.key
    typedAt = now
    const entries = entriesOf(items)
    const current = entries.findIndex(
        (item) => item.id === (event.target as HTMLElement).dataset.item,
    )
    const next = typeaheadIndex(entries, current, typed, document.documentElement.lang || undefined)
    if (next >= 0) {
        focusEntry(container, items, next)
    }
    return true
}

function close(): void {
    closeSubmenu()
    menu.value?.hidePopover?.()
    isOpen.value = false
}

// A page kept alive in the background takes its menus out of the document, which hides them.
onDeactivated(() => {
    isOpen.value = false
    submenuId.value = null
    returnFocus = null
})

function onToggle(event: Event): void {
    if (event.target !== menu.value) {
        return
    }

    const open = (event as ToggleEvent).newState === 'open'
    isOpen.value = open
    if (!open) {
        submenuId.value = null
        returnFocus?.focus()
        returnFocus = null
    }
}

function onSubmenuToggle(event: Event): void {
    event.stopPropagation()
    if ((event as ToggleEvent).newState !== 'open') {
        submenuId.value = null
    }
}

async function openSubmenu(item: MenuItem, focus: 'first' | 'none'): Promise<void> {
    if (!item.children || item.disabled) {
        return
    }

    submenuId.value = item.id
    await nextTick()
    const element = submenu.value
    const anchor = itemElements(menu.value).find((entry) => entry.dataset.item === item.id)
    if (!element || !anchor || !menu.value) {
        return
    }

    if (!element.matches?.(':popover-open')) {
        element.showPopover?.()
    }
    await nextTick()

    // Measured in the layout, so a menu that is still growing (motion system) is placed right.
    const menuRect = layoutRect({ x: x.value, y: y.value }, layoutSize(menu.value))
    const entryTop = menuRect.top + menu.value.clientTop + anchor.offsetTop - menu.value.scrollTop
    const position = submenuPosition(
        { left: menuRect.left, top: entryTop, right: menuRect.right, bottom: entryTop },
        menuRect,
        layoutSize(element),
        { width: window.innerWidth, height: window.innerHeight },
    )
    submenuX.value = position.x
    submenuY.value = position.y
    if (focus === 'first') {
        const entries = entriesOf(item.children)
        const checked = entries.findIndex((entry) => entry.checked && !entry.disabled)
        focusEntry(element, item.children, checked >= 0 ? checked : firstEnabledIndex(entries))
    }
}

function closeSubmenu(focusParent = false): void {
    const id = submenuId.value
    if (id === null) {
        return
    }

    submenu.value?.hidePopover?.()
    submenuId.value = null
    if (focusParent) {
        itemElements(menu.value)
            .find((element) => element.dataset.item === id)
            ?.focus()
    }
}

function choose(item: MenuItem, event: MouseEvent): void {
    if (item.disabled || item.separator) {
        return
    }

    if (item.children) {
        // A click with the keyboard (detail 0) moves into the submenu; a tap or click opens it.
        if (submenuId.value === item.id && event.detail > 0) {
            closeSubmenu()
        } else {
            void openSubmenu(item, event.detail === 0 ? 'first' : 'none')
        }
        return
    }

    close()
    emit('select', item.id)
}

function chooseChild(item: MenuItem): void {
    if (item.disabled || item.separator) {
        return
    }

    close()
    emit('select', item.id)
}

/** A mouse over an entry opens its submenu, or closes the submenu of another entry. */
function onPointerenter(item: MenuItem, event: PointerEvent): void {
    if (event.pointerType !== 'mouse') {
        return
    }

    if (item.children) {
        if (submenuId.value !== item.id) {
            void openSubmenu(item, 'none')
        }
    } else {
        closeSubmenu()
    }
}

function navigate(event: KeyboardEvent, items: readonly MenuItem[]): number | null {
    const entries = entriesOf(items)
    const current = entries.findIndex(
        (item) => item.id === (event.target as HTMLElement).dataset.item,
    )
    switch (event.key) {
        case 'ArrowDown':
            return nextEnabledIndex(current, 1, entries)
        case 'ArrowUp':
            return nextEnabledIndex(current < 0 ? entries.length : current, -1, entries)
        case 'Home':
            return firstEnabledIndex(entries)
        case 'End':
            return lastEnabledIndex(entries)
        default:
            return null
    }
}

function onSubmenuKeydown(event: KeyboardEvent): void {
    if (event.key === 'ArrowLeft' || event.key === 'Escape') {
        event.preventDefault()
        closeSubmenu(true)
        return
    }

    if (event.key === 'Tab') {
        event.preventDefault()
        close()
        return
    }

    const next = navigate(event, submenuEntries.value)
    if (next !== null) {
        event.preventDefault()
        focusEntry(submenu.value, submenuEntries.value, next)
        return
    }

    if (typeahead(event, submenu.value, submenuEntries.value)) {
        event.preventDefault()
    }
}

function onKeydown(event: KeyboardEvent): void {
    if (submenu.value?.contains(event.target as Node)) {
        event.stopPropagation()
        onSubmenuKeydown(event)
        return
    }

    if (event.key === 'Escape' || event.key === 'Tab' || isMenuKey(event)) {
        event.preventDefault()
        close()
        return
    }

    const item = props.items.find(
        (entry) => entry.id === (event.target as HTMLElement).dataset.item,
    )
    if (event.key === 'ArrowRight' && item?.children) {
        event.preventDefault()
        void openSubmenu(item, 'first')
        return
    }

    const next = navigate(event, props.items)
    if (next !== null) {
        event.preventDefault()
        closeSubmenu()
        focusEntry(menu.value, props.items, next)
        return
    }

    if (typeahead(event, menu.value, props.items)) {
        event.preventDefault()
        closeSubmenu()
    }
}

defineExpose({ openAt, openAnchored, close, isOpen })
</script>

<template>
    <div
        ref="menu"
        data-part="context-menu"
        role="menu"
        popover="auto"
        :aria-label="label"
        :data-state="isOpen ? 'open' : 'closed'"
        :data-placement="placement"
        :style="{
            '--wmp-menu-x': pixels(x),
            '--wmp-menu-y': pixels(y),
            '--wmp-menu-max-height': pixels(maxHeight),
            '--wmp-menu-anchor-width': pixels(anchorWidth),
        }"
        @toggle="onToggle"
        @keydown="onKeydown"
        @contextmenu.prevent
    >
        <template v-for="item in items" :key="item.id">
            <hr v-if="item.separator" data-part="menu-separator" />
            <button
                v-else
                type="button"
                data-part="menu-item"
                :role="item.checked === undefined ? 'menuitem' : 'menuitemradio'"
                :aria-checked="
                    item.checked === undefined ? undefined : item.checked ? 'true' : 'false'
                "
                :aria-haspopup="item.children ? 'menu' : undefined"
                :aria-expanded="
                    item.children ? (submenuId === item.id ? 'true' : 'false') : undefined
                "
                :aria-controls="item.children ? submenuElementId : undefined"
                tabindex="-1"
                :data-item="item.id"
                :disabled="item.disabled"
                @click="choose(item, $event)"
                @pointerenter="onPointerenter(item, $event)"
            >
                <BaseIcon v-if="item.icon" :name="item.icon" />
                <span data-part="label">{{ item.label }}</span>
                <span v-if="item.children" data-part="submenu-indicator" aria-hidden="true">
                    <BaseIcon name="submenu" />
                </span>
            </button>
        </template>
        <div
            v-if="hasSubmenus"
            :id="submenuElementId"
            ref="submenu"
            data-part="context-menu"
            data-kind="submenu"
            role="menu"
            popover="auto"
            :aria-label="submenuItem?.label"
            :data-state="submenuId === null ? 'closed' : 'open'"
            :style="{ '--wmp-menu-x': `${submenuX}px`, '--wmp-menu-y': `${submenuY}px` }"
            @toggle="onSubmenuToggle"
        >
            <template v-for="child in submenuEntries" :key="child.id">
                <hr v-if="child.separator" data-part="menu-separator" />
                <button
                    v-else
                    type="button"
                    data-part="menu-item"
                    :role="child.checked === undefined ? 'menuitem' : 'menuitemradio'"
                    :aria-checked="
                        child.checked === undefined ? undefined : child.checked ? 'true' : 'false'
                    "
                    tabindex="-1"
                    :data-item="child.id"
                    :disabled="child.disabled"
                    @click="chooseChild(child)"
                >
                    <BaseIcon v-if="child.icon" :name="child.icon" />
                    <span data-part="label">{{ child.label }}</span>
                </button>
            </template>
        </div>
    </div>
</template>
