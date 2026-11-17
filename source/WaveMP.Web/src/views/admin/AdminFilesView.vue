<script setup lang="ts">
import { computed, onActivated, onBeforeUnmount, ref, useId, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import {
    FileSelectionFilterObject,
    FileSelectionSortObject,
    type FileSelectionFilter,
    type FileSelectionSort,
} from '@/api/generated/models'
import { fileSelectionRevision, listFileSelections } from '@/admin/fileSelectionApi'
import AdminNav from '@/components/admin/AdminNav.vue'
import FileSelectionFilters from '@/components/admin/FileSelectionFilters.vue'
import FileSelectionList from '@/components/admin/FileSelectionList.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import LoadMore from '@/components/library/LoadMore.vue'
import { usePagedList } from '@/composables/usePagedList'
import { searchDelay } from '@/composables/useSearchField'
import { normalizeQuery } from '@/library/search'

/**
 * Admin page: every song with the number of its audio files, videos and covers and whether the
 * admin selected a file, filtered (e.g. songs with several audio files) and searched. A song opens
 * the page where its files are selected. Kept alive in the background, so back returns to the same
 * list; after a selection changed, the list loads again.
 */
const { t } = useI18n()
const titleId = useId()

const filter = ref<FileSelectionFilter>(FileSelectionFilterObject.All)
const sort = ref<FileSelectionSort>(FileSelectionSortObject.Path)
const search = ref('')
const query = ref('')
let timer: ReturnType<typeof setTimeout> | undefined

const list = usePagedList((offset, limit) =>
    listFileSelections({
        offset,
        limit,
        filter: filter.value,
        sort: sort.value,
        ...(query.value ? { q: query.value } : {}),
    }),
)
let loadedRevision = fileSelectionRevision.value

function reload(): Promise<void> {
    loadedRevision = fileSelectionRevision.value
    return list.reload()
}

watch([filter, sort], () => void reload())
watch(search, (value) => {
    clearTimeout(timer)
    timer = setTimeout(() => {
        const next = normalizeQuery(value)
        if (next !== query.value) {
            query.value = next
            void reload()
        }
    }, searchDelay)
})

void reload()
onActivated(() => {
    if (loadedRevision !== fileSelectionRevision.value) {
        void reload()
    }
})
onBeforeUnmount(() => clearTimeout(timer))

const filtered = computed(
    () => filter.value !== FileSelectionFilterObject.All || query.value !== '',
)
</script>

<template>
    <div data-part="view" data-view="admin-files">
        <AdminNav />
        <section data-part="section" data-item="songs" :aria-labelledby="titleId">
            <h2 :id="titleId" data-part="section-title">{{ t('adminFiles.songsTitle') }}</h2>
            <FileSelectionFilters
                v-model:search="search"
                v-model:filter="filter"
                v-model:sort="sort"
            />
            <div v-if="list.status.value === 'error'" data-part="empty-state">
                <p data-part="empty-state-title">{{ t('adminFiles.loadError') }}</p>
                <BaseButton :label="t('library.retry')" @click="reload" />
            </div>
            <div
                v-else-if="list.status.value === 'ready' && list.items.value.length === 0"
                data-part="empty-state"
            >
                <p data-part="empty-state-title">
                    {{ filtered ? t('adminFiles.noMatches') : t('adminFiles.empty') }}
                </p>
            </div>
            <template v-else-if="list.items.value.length > 0">
                <FileSelectionList
                    :items="list.items.value"
                    :label="t('adminFiles.listLabel')"
                    :busy="list.status.value === 'loading'"
                />
                <LoadMore
                    v-if="list.hasMore.value"
                    :loading="list.loadingMore.value"
                    @load="list.loadMore()"
                />
            </template>
        </section>
    </div>
</template>
