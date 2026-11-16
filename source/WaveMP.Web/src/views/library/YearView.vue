<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import BaseCover from '@/components/base/BaseCover.vue'
import BaseIcon from '@/components/base/BaseIcon.vue'
import DetailHeader from '@/components/library/DetailHeader.vue'
import TrackCollection from '@/components/library/TrackCollection.vue'
import { useFormat } from '@/composables/useFormat'
import { useDetailTitle } from '@/composables/useDetailTitle'
import type { PagedListStatus } from '@/composables/usePagedList'
import { useViewRoute } from '@/composables/useViewRoute'
import { parseSort } from '@/library/sorting'

/** The songs of a year, grouped by album and sortable. */
const props = defineProps<{
    year: string
}>()

const { t } = useI18n()
const route = useViewRoute()
const format = useFormat()
const status = ref<PagedListStatus>('idle')

const value = computed(() => Number(props.year))
const title = computed(() => format.year(value.value))
const filter = computed(() => ({ year: value.value }))
const sortState = computed(() => parseSort('tracks', route.value.query, false))

useDetailTitle(() => title.value)
</script>

<template>
    <div
        data-part="view"
        data-view="library-year"
        :aria-busy="status === 'loading' ? 'true' : undefined"
    >
        <DetailHeader
            :title="title"
            :back="{ name: 'library-section', params: { section: 'years' } }"
        >
            <template #media>
                <BaseCover :placeholder="false">
                    <BaseIcon name="year" />
                </BaseCover>
            </template>
        </DetailHeader>
        <TrackCollection
            :label="t('library.songsTitle')"
            :filter="filter"
            :sort="sortState.sort"
            :direction="sortState.direction"
            @status="status = $event"
        />
    </div>
</template>
