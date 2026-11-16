<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterLink, useRoute } from 'vue-router'
import BaseIcon from '@/components/base/BaseIcon.vue'
import { indicatorVariables, useIndicator } from '@/composables/useIndicator'
import type { IconSlotName } from '@/themes/iconSlots'

interface AdminNavItem {
    readonly id: string
    readonly labelKey: string
    readonly icon: IconSlotName
}

/**
 * Navigation between the pages of the administration. Like the tabs, it holds an indicator part
 * and tells themes the position of the current page (--wmp-active-index, --wmp-item-count,
 * --wmp-indicator-x, -y, -width and -height).
 */
const { t } = useI18n()
const route = useRoute()

const items: readonly AdminNavItem[] = [
    { id: 'admin-library', labelKey: 'admin.nav.library', icon: 'admin-library' },
    { id: 'admin-jobs', labelKey: 'admin.nav.jobs', icon: 'admin-jobs' },
    { id: 'admin-cache', labelKey: 'admin.nav.cache', icon: 'admin-cache' },
    { id: 'admin-transcoding', labelKey: 'admin.nav.transcoding', icon: 'admin-transcoding' },
    { id: 'admin-backups', labelKey: 'admin.nav.backups', icon: 'admin-backups' },
    { id: 'admin-users', labelKey: 'admin.nav.profiles', icon: 'manage-profiles' },
]

const active = computed(() => route.name)
const activeIndex = computed(() => items.findIndex((item) => item.id === active.value))
const navigation = ref<HTMLElement | null>(null)
const indicator = useIndicator(navigation, '[aria-current="page"]', () => activeIndex.value)
</script>

<template>
    <nav
        ref="navigation"
        data-part="admin-nav"
        :aria-label="t('admin.nav.label')"
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
                data-part="admin-nav-item"
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
