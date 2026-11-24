<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import BaseIcon from '@/components/base/BaseIcon.vue'
import type { PathSegment, Route } from '@/admin/organize'

/**
 * Where a file goes: the old path (path-from) and the new path (path-to), each broken into its
 * folders and file name (path-segment, with data-state same, changed or new, and data-kind folder
 * or file) and the separators between them (path-separator), so a theme can mark what changes.
 * Without an old path (a cover written from the server, or a template preview) only the new path
 * shows; without a new path (a file an undo removes) only the old one. The route carries the state
 * move, create, remove or unchanged.
 */
const props = defineProps<{
    route: Route
}>()

const { t } = useI18n()

const state = computed(() => {
    const { from, to } = props.route
    if (!from) {
        return 'create'
    }
    if (!to) {
        return 'remove'
    }
    const unchanged = from.length === to.length && to.every((segment) => segment.state === 'same')
    return unchanged ? 'unchanged' : 'move'
})

function isLast(segments: readonly PathSegment[], index: number): boolean {
    return index === segments.length - 1
}
</script>

<template>
    <div data-part="path-route" :data-state="state">
        <p v-if="route.from" data-part="path-from">
            <span data-part="label">{{ t('organize.route.from') }}</span>
            <template v-for="(segment, index) in route.from" :key="`from-${index}`">
                <span
                    data-part="path-segment"
                    :data-kind="segment.kind"
                    :data-state="segment.state"
                >
                    {{ segment.name }}
                </span>
                <span
                    v-if="!isLast(route.from, index)"
                    data-part="path-separator"
                    aria-hidden="true"
                >
                    <BaseIcon name="path-separator" />
                </span>
            </template>
            <span v-if="route.from.length === 0" data-part="path-segment" data-kind="root">
                {{ t('organize.route.root') }}
            </span>
        </p>
        <p v-if="route.to" data-part="path-to">
            <BaseIcon v-if="route.from" name="path-arrow" />
            <span data-part="label">{{ t('organize.route.to') }}</span>
            <template v-for="(segment, index) in route.to" :key="`to-${index}`">
                <span
                    data-part="path-segment"
                    :data-kind="segment.kind"
                    :data-state="segment.state"
                >
                    {{ segment.name }}
                </span>
                <span v-if="!isLast(route.to, index)" data-part="path-separator" aria-hidden="true">
                    <BaseIcon name="path-separator" />
                </span>
            </template>
            <span v-if="route.to.length === 0" data-part="path-segment" data-kind="root">
                {{ t('organize.route.root') }}
            </span>
        </p>
    </div>
</template>
