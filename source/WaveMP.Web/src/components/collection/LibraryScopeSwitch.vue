<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import BaseSegmentedControl, {
    type SegmentOption,
} from '@/components/base/BaseSegmentedControl.vue'
import { isMyMusic, withMyMusic } from '@/collection/presentation'

/**
 * Switch between the whole library and the "my music" of the profile (rated and favorite songs,
 * the songs of favorite albums and artists and of the own playlists). The choice is part of the
 * query of the route (?myMusic=true), so it combines with search and sort order and can be linked.
 */
const { t } = useI18n()
const route = useRoute()
const router = useRouter()
const myMusic = computed(() => isMyMusic(route.query))

const options = computed<SegmentOption[]>(() => [
    { value: 'all-music', label: t('collection.scope.all') },
    { value: 'my-music', label: t('collection.scope.myMusic') },
])

function choose(value: string): void {
    const wanted = value === 'my-music'
    if (wanted !== myMusic.value) {
        void router.replace({ query: withMyMusic(route.query, wanted) })
    }
}
</script>

<template>
    <div data-part="scope-switch">
        <BaseSegmentedControl
            item="scope"
            :label="t('collection.scope.label')"
            :options="options"
            :model-value="myMusic ? 'my-music' : 'all-music'"
            @update:model-value="choose"
        />
    </div>
</template>
