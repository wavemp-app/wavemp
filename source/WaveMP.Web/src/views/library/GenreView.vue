<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import BaseCover from '@/components/base/BaseCover.vue'
import BaseIcon from '@/components/base/BaseIcon.vue'
import DetailHeader from '@/components/library/DetailHeader.vue'
import TrackCollection from '@/components/library/TrackCollection.vue'
import { useDetailTitle } from '@/composables/useDetailTitle'
import type { PagedListStatus } from '@/composables/usePagedList'
import { useViewRoute } from '@/composables/useViewRoute'
import { parseSort } from '@/library/sorting'

/** The songs of a genre, grouped by album and sortable. */
const props = defineProps<{
    genre: string
}>()

const { t } = useI18n()
const route = useViewRoute()
const status = ref<PagedListStatus>('idle')

const filter = computed(() => ({ genre: props.genre }))
const sortState = computed(() => parseSort('tracks', route.value.query, false))

useDetailTitle(() => props.genre)
</script>

<template>
    <div
        data-part="view"
        data-view="library-genre"
        :aria-busy="status === 'loading' ? 'true' : undefined"
    >
        <DetailHeader
            :title="genre"
            :back="{ name: 'library-section', params: { section: 'genres' } }"
        >
            <template #media>
                <BaseCover :placeholder="false">
                    <BaseIcon name="genre" />
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
