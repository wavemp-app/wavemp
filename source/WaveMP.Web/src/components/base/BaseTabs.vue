<script setup lang="ts">
import { computed, ref } from 'vue'
import { RouterLink, type RouteLocationRaw } from 'vue-router'
import BaseIcon from './BaseIcon.vue'
import { indicatorVariables, useIndicator } from '@/composables/useIndicator'
import type { IconSlotName } from '@/themes/iconSlots'
import type { PartName } from '@/themes/parts'

export interface TabItem {
    readonly id: string
    readonly label: string
    readonly icon: IconSlotName
    readonly to: RouteLocationRaw
}

/**
 * Main navigation as tabs (header) or bottom navigation (mobile). The navigation holds an
 * indicator part and tells themes the position of the active entry (--wmp-active-index,
 * --wmp-item-count, --wmp-indicator-x, -y, -width and -height), so a theme can slide a line or a
 * highlight between the entries with CSS only.
 */
const props = withDefaults(
    defineProps<{
        items: readonly TabItem[]
        /** Accessible name of the navigation. */
        label: string
        /** Id of the active item. */
        active?: string
        part?: Extract<PartName, 'tabs' | 'bottom-nav'>
    }>(),
    { active: undefined, part: 'tabs' },
)

const navigation = ref<HTMLElement | null>(null)
const activeIndex = computed(() => props.items.findIndex((item) => item.id === props.active))
const indicator = useIndicator(navigation, '[aria-current="page"]', () => [
    activeIndex.value,
    props.items.length,
])
</script>

<template>
    <nav
        ref="navigation"
        :data-part="part"
        :aria-label="label"
        :style="indicatorVariables(items.length, activeIndex, indicator)"
    >
        <span data-part="indicator" aria-hidden="true"></span>
        <RouterLink
            v-for="item in items"
            :key="item.id"
            v-slot="{ href, navigate }"
            :to="item.to"
            custom
        >
            <a
                :data-part="part === 'tabs' ? 'tab' : 'bottom-nav-item'"
                :href="href"
                :aria-current="item.id === active ? 'page' : undefined"
                :data-state="item.id === active ? 'active' : undefined"
                @click="navigate"
            >
                <BaseIcon :name="item.icon" />
                <span data-part="label">{{ item.label }}</span>
                <span v-if="part === 'tabs'" data-part="indicator" aria-hidden="true">
                    <BaseIcon name="tab-indicator" />
                </span>
            </a>
        </RouterLink>
    </nav>
</template>
