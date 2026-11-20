<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterLink, useRoute } from 'vue-router'
import BaseIcon from '@/components/base/BaseIcon.vue'
import { indicatorVariables, useIndicator } from '@/composables/useIndicator'
import type { IconSlotName } from '@/themes/iconSlots'

interface RecapNavItem {
    readonly id: string
    readonly labelKey: string
    readonly icon: IconSlotName
}

/**
 * Navigation between the recap, the statistics and the recaps shared with the profile. Like the
 * navigation of the administration, it holds an indicator part and tells themes the position of
 * the current page (--wmp-active-index, --wmp-item-count, --wmp-indicator-x, -y, -width and
 * -height).
 */
const { t } = useI18n()
const route = useRoute()

const items: readonly RecapNavItem[] = [
    { id: 'recap', labelKey: 'recap.nav.recap', icon: 'recap' },
    { id: 'statistics', labelKey: 'recap.nav.statistics', icon: 'statistics' },
    { id: 'shared-recaps', labelKey: 'recap.nav.shared', icon: 'shared-recaps' },
]

// A shared recap belongs to the list of shared recaps.
const active = computed(() =>
    route.name === 'shared-recap' ? 'shared-recaps' : (route.name as string | undefined),
)
const activeIndex = computed(() => items.findIndex((item) => item.id === active.value))
const navigation = ref<HTMLElement | null>(null)
const indicator = useIndicator(navigation, '[aria-current="page"]', () => activeIndex.value)
</script>

<template>
    <nav
        ref="navigation"
        data-part="recap-nav"
        :aria-label="t('recap.nav.label')"
        :style="indicatorVariables(items.length, activeIndex, indicator)"
    >
        <span data-part="indicator" aria-hidden="true"></span>
        <RouterLink
            v-for="item in items"
            :key="item.id"
            v-slot="{ href, navigate }"
            :to="{ name: item.id }"
            custom
        >
            <a
                data-part="recap-nav-item"
                :data-item="item.id"
                :href="href"
                :aria-current="item.id === active ? 'page' : undefined"
                :data-state="item.id === active ? 'active' : undefined"
                @click="navigate"
            >
                <BaseIcon :name="item.icon" />
                <span data-part="label">{{ t(item.labelKey) }}</span>
            </a>
        </RouterLink>
    </nav>
</template>
