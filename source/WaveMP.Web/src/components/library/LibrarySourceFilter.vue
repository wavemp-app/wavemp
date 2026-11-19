<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import BaseSelect, { type SelectOption } from '@/components/base/BaseSelect.vue'
import { sourceOf, withSource } from '@/collection/presentation'
import { useMediaSourcesStore } from '@/stores/mediaSources'

/**
 * Filter of the library by media folder, only shown when the library has more than one. The
 * number of folders is open, so it is a select instead of a segmented control. The choice is part
 * of the query of the route (?source=<id>), so it combines with search, sort order and "my music"
 * and can be linked.
 */
const { t } = useI18n()
const route = useRoute()
const router = useRouter()
const sources = useMediaSourcesStore()

const allValue = ''
const selected = computed(() => sourceOf(route.query) ?? allValue)

const options = computed<SelectOption[]>(() => [
    { value: allValue, label: t('library.sourceFilter.all') },
    ...sources.sources.flatMap((source) =>
        source.id ? [{ value: source.id, label: source.name ?? '' }] : [],
    ),
])

function choose(value: string): void {
    if (value !== selected.value) {
        void router.replace({ query: withSource(route.query, value || undefined) })
    }
}

onMounted(() => {
    void sources.ensureLoaded()
})
</script>

<template>
    <div v-if="sources.hasSeveral" data-part="source-filter">
        <BaseSelect
            :label="t('library.sourceFilter.label')"
            :options="options"
            :model-value="selected"
            @update:model-value="choose"
        />
    </div>
</template>
