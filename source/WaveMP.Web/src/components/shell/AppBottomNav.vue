<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute } from 'vue-router'
import BaseTabs from '@/components/base/BaseTabs.vue'
import { mainNavigation, type MainTab } from '@/navigation/libraryTree'

/**
 * Bottom navigation of the mobile base; the same entries as the tabs of the header. Entries with a
 * short label show it and keep their full label as tooltip and accessible name.
 */
defineProps<{
    /** Active entry; by default the tab of the current route. */
    activeTab?: MainTab
}>()

const { t } = useI18n()
const route = useRoute()

const items = computed(() =>
    mainNavigation.map((item) => ({
        id: item.id,
        label: t(item.shortLabelKey ?? item.labelKey),
        fullLabel: item.shortLabelKey ? t(item.labelKey) : undefined,
        icon: item.icon,
        to: item.to,
    })),
)
</script>

<template>
    <BaseTabs
        part="bottom-nav"
        :items="items"
        :label="t('nav.main')"
        :active="activeTab ?? route.meta.tab"
    />
</template>
