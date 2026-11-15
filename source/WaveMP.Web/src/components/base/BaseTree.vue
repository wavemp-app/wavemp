<script setup lang="ts">
import { computed, nextTick, ref, useId, watch } from 'vue'
import BaseTreeItem, { type TreeNode } from './BaseTreeItem.vue'
import { nextFocus, visibleItems } from './tree'
import { indicatorVariables, useIndicator } from '@/composables/useIndicator'

/**
 * Tree view with a roving tabindex. The tree holds an indicator part and tells themes the position
 * of the item of the current page among the visible items (--wmp-active-index, --wmp-item-count,
 * --wmp-indicator-x, -y, -width and -height), so a theme can slide a highlight or a line to it.
 */
const props = withDefaults(
    defineProps<{
        nodes: readonly TreeNode[]
        /** Accessible name of the tree. */
        label: string
        /** Id of the node of the current page (aria-current). */
        active?: string
        /** Shows the label of every item as tooltip, e.g. while a theme shows only the icons. */
        tooltips?: boolean
    }>(),
    { active: undefined, tooltips: false },
)

/** Ids of the expanded nodes. */
const expanded = defineModel<string[]>('expanded', { default: () => [] })

const treeId = useId()
const expandedSet = computed(() => new Set(expanded.value))
const focusedId = ref<string | undefined>(undefined)
const tree = ref<HTMLElement | null>(null)

const visible = computed(() => visibleItems(props.nodes, expandedSet.value))
const activeIndex = computed(() => visible.value.findIndex((item) => item.id === props.active))
const indicator = useIndicator(tree, '[role="treeitem"][aria-current="page"]', () => [
    activeIndex.value,
    visible.value.length,
])

// Roving tabindex: exactly one item is in the tab order, by default the active one.
const tabStop = computed(() => {
    const candidates = [focusedId.value, props.active]
    return (
        candidates.find((id) => id && visible.value.some((item) => item.id === id)) ??
        visible.value[0]?.id
    )
})

watch(
    () => props.active,
    () => {
        focusedId.value = undefined
    },
)

function elementId(nodeId: string): string {
    return `${treeId}-${nodeId}`
}

function setExpanded(nodeId: string, value: boolean): void {
    const others = expanded.value.filter((id) => id !== nodeId)
    expanded.value = value ? [...others, nodeId] : others
}

function toggle(nodeId: string): void {
    setExpanded(nodeId, !expandedSet.value.has(nodeId))
}

async function focusItem(nodeId: string): Promise<void> {
    focusedId.value = nodeId
    await nextTick()
    document.getElementById(elementId(nodeId))?.focus()
}

function onKeydown(event: KeyboardEvent): void {
    const current = focusedId.value ?? tabStop.value
    if (!current) {
        return
    }

    const action = nextFocus(event.key, current, props.nodes, expandedSet.value)
    if (!action) {
        return
    }

    event.preventDefault()
    if (action.type === 'focus') {
        void focusItem(action.id)
    } else {
        setExpanded(action.id, action.type === 'expand')
    }
}

defineExpose({ focusItem, tabStop })
</script>

<template>
    <ul
        ref="tree"
        data-part="tree"
        role="tree"
        :aria-label="label"
        :style="indicatorVariables(visible.length, activeIndex, indicator)"
        @keydown="onKeydown"
    >
        <li data-part="indicator" role="none" aria-hidden="true"></li>
        <BaseTreeItem
            v-for="node in nodes"
            :key="node.id"
            :node="node"
            :level="1"
            :expanded="expandedSet"
            :active="active"
            :tab-stop="tabStop"
            :element-id="elementId"
            :tooltips="tooltips"
            @toggle="toggle"
            @focus-item="(id: string) => (focusedId = id)"
        />
    </ul>
</template>
