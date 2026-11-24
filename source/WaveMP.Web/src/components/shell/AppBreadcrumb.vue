<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterLink, useRoute } from 'vue-router'
import BaseIcon from '@/components/base/BaseIcon.vue'
import { buildBreadcrumb, type RouteInfo } from '@/navigation/breadcrumb'
import { usePageTitleStore } from '@/stores/pageTitle'

// The breadcrumb of the current page. It stands in the toolbar, or in the header where the theme
// switches on the layout option for it (THEMING.md, section "Layout options").
const props = defineProps<{
    /** Location the breadcrumb shows; by default the current route. */
    location?: RouteInfo
}>()

const { t } = useI18n()
const route = useRoute()
const pageTitle = usePageTitleStore()

const crumbs = computed(() => buildBreadcrumb(props.location ?? route, pageTitle.title))
</script>

<template>
    <nav data-part="breadcrumb" :aria-label="t('breadcrumb.label')">
        <ol data-part="breadcrumb-list">
            <li
                v-for="(crumb, index) in crumbs"
                :key="crumb.id"
                data-part="breadcrumb-item"
                :data-state="crumb.to ? undefined : 'active'"
            >
                <RouterLink v-if="crumb.to" v-slot="{ href, navigate }" :to="crumb.to" custom>
                    <a data-part="breadcrumb-link" :href="href" @click="navigate">
                        <BaseIcon v-if="index === 0" name="breadcrumb-root" />
                        <span data-part="label">{{ crumb.label ?? t(crumb.labelKey) }}</span>
                    </a>
                </RouterLink>
                <span v-else data-part="breadcrumb-link" aria-current="page">
                    <BaseIcon v-if="index === 0" name="breadcrumb-root" />
                    <span data-part="label">{{ crumb.label ?? t(crumb.labelKey) }}</span>
                </span>
                <span
                    v-if="index < crumbs.length - 1"
                    data-part="breadcrumb-separator"
                    aria-hidden="true"
                >
                    <BaseIcon name="breadcrumb-separator" />
                </span>
            </li>
        </ol>
    </nav>
</template>
