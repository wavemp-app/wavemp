<script setup lang="ts">
import { computed, onBeforeUnmount, ref, shallowRef, useId, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import {
    TrackReviewSortObject,
    type TrackReview,
    type TrackReviewSort,
} from '@/api/generated/models'
import type { PageLike } from '@/api/paging'
import AdminNav from '@/components/admin/AdminNav.vue'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import LoadMore from '@/components/library/LoadMore.vue'
import ReviewBulkBar from '@/components/review/ReviewBulkBar.vue'
import ReviewFilters from '@/components/review/ReviewFilters.vue'
import ReviewList from '@/components/review/ReviewList.vue'
import { usePagedList } from '@/composables/usePagedList'
import { searchDelay } from '@/composables/useSearchField'
import { normalizeQuery } from '@/library/search'
import { listTrackReviews, type TrackReviewQuery } from '@/review/api'
import {
    missingTagsParameter,
    openIds,
    selectionOf,
    toggled,
    type MissingTagsFilter,
} from '@/review/inbox'
import { useReviewStore } from '@/stores/review'

/**
 * Admin page: the review inbox with every song whose metadata nobody has checked yet, filtered by
 * missing tags, sorted and searched. A song opens in the metadata editor of the inbox, which goes
 * on to the next song after each review. Kept alive in the background, so back returns to the
 * same list at the same position; songs reviewed meanwhile stay in it, marked as reviewed, until
 * the list loads again. Selected songs are marked as reviewed at once.
 */
const { t } = useI18n()
const titleId = useId()
const review = useReviewStore()

const filter = ref<MissingTagsFilter>('all')
const sort = ref<TrackReviewSort>(TrackReviewSortObject.FirstSeenAt)
const search = ref('')
const query = ref('')
const selected = shallowRef<ReadonlySet<string>>(new Set())
const marking = ref(false)
const error = ref<string | null>(null)
let timer: ReturnType<typeof setTimeout> | undefined

function currentQuery(): TrackReviewQuery {
    const missingTags = missingTagsParameter(filter.value)
    return {
        sort: sort.value,
        ...(missingTags === undefined ? {} : { missingTags }),
        ...(query.value ? { q: query.value } : {}),
    }
}

/**
 * Songs reviewed since the list was loaded have left the unreviewed songs of the server, but stay
 * in the list: the next page starts after the songs that are still unreviewed, and the total
 * counts the reviewed ones the list shows.
 */
async function fetchPage(
    offset: number,
    limit: number,
): Promise<PageLike<TrackReview> | undefined> {
    if (offset === 0) {
        return listTrackReviews({ ...currentQuery(), offset, limit })
    }

    const loaded = list.items.value
    const shown = loaded.length - openIds(loaded, review.reviewed).length
    const page = await listTrackReviews({ ...currentQuery(), offset: offset - shown, limit })
    const known = new Set(loaded.map((item) => item.trackId))
    return {
        items: (page?.items ?? []).filter((item) => !known.has(item.trackId)),
        total: (page?.total ?? 0) + shown,
    }
}

const list = usePagedList(fetchPage)

function reload(): Promise<void> {
    selected.value = new Set()
    error.value = null
    void review.refreshCount()
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

// The page of a song goes through the list this page shows.
watch(
    () => list.items.value,
    (items) => review.setQueue(items, currentQuery(), list.total.value),
)

void reload()
onBeforeUnmount(() => clearTimeout(timer))

const selectable = computed(() => openIds(list.items.value, review.reviewed))
const selection = computed(() => selectionOf(selected.value, list.items.value, review.reviewed))
const filtered = computed(() => filter.value !== 'all' || query.value !== '')

function select(trackId: string, on: boolean): void {
    selected.value = toggled(selection.value, trackId, on)
}

function selectAll(on: boolean): void {
    selected.value = on ? new Set(selectable.value) : new Set()
}

async function markSelected(): Promise<void> {
    const ids = [...selection.value]
    if (ids.length === 0 || marking.value) {
        return
    }

    marking.value = true
    error.value = null
    try {
        await review.markManyReviewed(ids)
        selected.value = new Set()
    } catch {
        error.value = t('reviewInbox.errors.markFailed')
    } finally {
        marking.value = false
    }
}
</script>

<template>
    <div data-part="view" data-view="admin-review">
        <AdminNav />
        <section data-part="section" data-item="review" :aria-labelledby="titleId">
            <h2 :id="titleId" data-part="section-title">{{ t('reviewInbox.listTitle') }}</h2>
            <ReviewFilters v-model:search="search" v-model:filter="filter" v-model:sort="sort" />
            <BaseAlert v-if="error" tone="error" :message="error" />
            <div v-if="list.status.value === 'error'" data-part="empty-state">
                <p data-part="empty-state-title">{{ t('reviewInbox.loadError') }}</p>
                <BaseButton :label="t('library.retry')" @click="reload" />
            </div>
            <div
                v-else-if="list.status.value === 'ready' && list.items.value.length === 0"
                data-part="empty-state"
            >
                <p data-part="empty-state-title">
                    {{ filtered ? t('reviewInbox.noMatches') : t('reviewInbox.empty') }}
                </p>
            </div>
            <template v-else-if="list.items.value.length > 0">
                <ReviewBulkBar
                    :selected-count="selection.size"
                    :selectable-count="selectable.length"
                    :busy="marking"
                    @select-all="selectAll"
                    @mark-reviewed="markSelected"
                />
                <ReviewList
                    :items="list.items.value"
                    :reviewed="review.reviewed"
                    :selected="selection"
                    :label="t('reviewInbox.listLabel')"
                    :busy="list.status.value === 'loading' || marking"
                    @select="select"
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
