<script setup lang="ts">
import { inject } from 'vue'
import { RouterView, type RouteLocationNormalizedLoaded } from 'vue-router'
import { historyEntriesKey } from '@/router/entries'

/**
 * The page of the current route in the content area of the app shell. Pages with lists
 * (meta.keepAlive) stay alive in the background once per history entry: back and forward return
 * to them with their loaded data, filters and scroll position, without loading again. Every
 * other navigation opens the page anew. The other pages are rendered as usual.
 */
const props = withDefaults(
    defineProps<{
        /** Number of pages kept alive; the one used longest ago is dropped first. */
        max?: number
    }>(),
    { max: 10 },
)

const entries = inject(historyEntriesKey, null)

/** One page per history entry and path; changes of the query alone keep the page. */
function pageKey(route: RouteLocationNormalizedLoaded): string {
    return `${entries?.current.value ?? 0}|${route.path}`
}
</script>

<template>
    <RouterView v-slot="{ Component, route }">
        <KeepAlive :max="props.max">
            <component :is="Component" v-if="route.meta.keepAlive" :key="pageKey(route)" />
        </KeepAlive>
        <component :is="Component" v-if="!route.meta.keepAlive" />
    </RouterView>
</template>
