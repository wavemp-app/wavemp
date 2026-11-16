<script setup lang="ts">
import type { RouteLocationRaw } from 'vue-router'
import BackButton from './BackButton.vue'

/**
 * Head of a detail page (album, artist, genre, year, track, playlist): the back button to the
 * previous page (or to the overview given as back, for pages opened directly), a picture (slot
 * media), the name, the details (default slot, meta-items) and actions such as buttons for admins
 * (slot actions).
 */
defineProps<{
    title: string
    /** The overview the page belongs to; shows the back button. */
    back?: RouteLocationRaw
}>()
</script>

<template>
    <BackButton v-if="back" :fallback="back" />
    <div data-part="detail-header">
        <slot name="media" />
        <div data-part="detail-info">
            <h2 data-part="detail-title">{{ title }}</h2>
            <slot />
            <div v-if="$slots.actions" data-part="detail-actions">
                <slot name="actions" />
            </div>
        </div>
    </div>
</template>
