<script setup lang="ts">
import { computed, ref, useId, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { TrackReviewStatusObject, type Track, type TrackReviewState } from '@/api/generated/models'
import { statusOf } from '@/api/problems'
import AdminNav from '@/components/admin/AdminNav.vue'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseLink from '@/components/base/BaseLink.vue'
import DetailHeader from '@/components/library/DetailHeader.vue'
import LibraryCover from '@/components/library/LibraryCover.vue'
import MetaItem from '@/components/library/MetaItem.vue'
import TrackMetadataForm from '@/components/library/TrackMetadataForm.vue'
import ReviewActions from '@/components/review/ReviewActions.vue'
import ReviewNav from '@/components/review/ReviewNav.vue'
import ReviewTagHints from '@/components/review/ReviewTagHints.vue'
import { useDetailTitle } from '@/composables/useDetailTitle'
import { useFormat } from '@/composables/useFormat'
import { getTrack } from '@/library/api'
import { getTrackReview } from '@/review/api'
import { nextOpenId, reviewPosition } from '@/review/inbox'
import { useReviewStore } from '@/stores/review'

/**
 * Admin page of one song of the review inbox: its metadata in the metadata editor and the
 * actions of the review. "Save and mark as reviewed" writes the changed tags (nothing if nothing
 * changed) and marks the song as reviewed, "Mark as reviewed" marks it without changes, "Skip"
 * leaves it unreviewed. After each action the next unreviewed song of the list opens in its place
 * (the history keeps one entry, so back returns to the list at its position). Previous and next
 * go through the list without changing anything.
 */
const props = defineProps<{
    id: string
}>()

const { t } = useI18n()
const router = useRouter()
const format = useFormat()
const review = useReviewStore()
const formId = useId()
const metadataTitleId = useId()

const track = ref<Track | null>(null)
const reviewState = ref<TrackReviewState | null>(null)
const status = ref<'loading' | 'ready' | 'notFound' | 'error'>('loading')
const busy = ref(false)
const finished = ref(false)
const error = ref<string | null>(null)
const editor = ref<InstanceType<typeof TrackMetadataForm> | null>(null)
let generation = 0

async function load(): Promise<void> {
    const current = ++generation
    status.value = 'loading'
    finished.value = false
    error.value = null
    try {
        const [loadedTrack, loadedState] = await Promise.all([
            getTrack(props.id),
            getTrackReview(props.id),
            review.ensureQueue(props.id).catch(() => undefined),
        ])
        if (current !== generation) {
            return
        }

        track.value = loadedTrack ?? null
        reviewState.value = loadedState ?? null
        status.value = loadedTrack && loadedState ? 'ready' : 'notFound'
    } catch (failure) {
        if (current === generation) {
            status.value = statusOf(failure) === 404 ? 'notFound' : 'error'
        }
    }
}

watch(() => props.id, load, { immediate: true })

useDetailTitle(() => (status.value === 'ready' ? track.value?.title : null))

const position = computed(() => reviewPosition(review.queueIds, props.id))
const queueItem = computed(() => review.queue.find((item) => item.trackId === props.id))
const isReviewed = computed(
    () =>
        reviewState.value?.status === TrackReviewStatusObject.Reviewed ||
        review.reviewed.has(props.id),
)
const reviewText = computed(() => {
    const state = reviewState.value
    if (!isReviewed.value) {
        return t('reviewInbox.unreviewed')
    }

    const date = state?.reviewedAt ? format.date(state.reviewedAt) : null
    if (state?.reviewedBy?.name && date) {
        return t('reviewInbox.reviewedBy', { name: state.reviewedBy.name, date })
    }
    return t('reviewInbox.reviewed')
})
const canSave = computed(() => !!editor.value?.editable && !editor.value.saving)

function open(id: string): void {
    void router.replace({ name: 'admin-review-track', params: { id } })
}

/** The next unreviewed song after this one, loading the next page of the list if needed. */
async function nextOpen(): Promise<string | null> {
    const next = nextOpenId(review.queueIds, props.id, review.reviewed)
    if (next || !review.queueHasMore) {
        return next
    }

    await review.loadMoreQueue()
    return nextOpenId(review.queueIds, props.id, review.reviewed)
}

async function advance(): Promise<void> {
    let next: string | null = null
    try {
        next = await nextOpen()
    } catch {
        error.value = t('reviewInbox.errors.loadNext')
        return
    }

    if (next) {
        open(next)
    } else {
        finished.value = true
    }
}

async function markAndAdvance(): Promise<void> {
    if (busy.value) {
        return
    }

    busy.value = true
    error.value = null
    try {
        await review.markReviewed(props.id)
        await advance()
    } catch {
        error.value = t('reviewInbox.errors.markFailed')
    } finally {
        busy.value = false
    }
}

async function skip(): Promise<void> {
    if (busy.value) {
        return
    }

    busy.value = true
    error.value = null
    try {
        await advance()
    } finally {
        busy.value = false
    }
}

async function goNext(): Promise<void> {
    if (!position.value.next && review.queueHasMore) {
        try {
            await review.loadMoreQueue()
        } catch {
            error.value = t('reviewInbox.errors.loadNext')
            return
        }
    }

    if (position.value.next) {
        open(position.value.next)
    }
}

function goPrevious(): void {
    if (position.value.previous) {
        open(position.value.previous)
    }
}
</script>

<template>
    <div
        data-part="view"
        data-view="admin-review-track"
        :aria-busy="status === 'loading' || busy ? 'true' : undefined"
    >
        <AdminNav />
        <div v-if="status === 'notFound'" data-part="empty-state">
            <p data-part="empty-state-text">{{ t('library.trackNotFound') }}</p>
            <BaseLink :to="{ name: 'admin-review' }">{{ t('reviewInbox.toInbox') }}</BaseLink>
        </div>
        <div v-else-if="status === 'error'" data-part="empty-state">
            <p data-part="empty-state-title">{{ t('reviewInbox.loadTrackError') }}</p>
            <BaseButton :label="t('library.retry')" @click="load" />
        </div>
        <div v-else-if="finished" data-part="empty-state" data-state="finished">
            <p data-part="empty-state-title">{{ t('reviewInbox.finished') }}</p>
            <p data-part="empty-state-text">{{ t('reviewInbox.finishedText') }}</p>
            <BaseLink :to="{ name: 'admin-review' }">{{ t('reviewInbox.toInbox') }}</BaseLink>
        </div>
        <template v-else-if="status === 'ready' && track">
            <DetailHeader :title="track.title ?? ''" :back="{ name: 'admin-review' }">
                <template #media>
                    <LibraryCover kind="track" :id="track.id" :available="!!track.hasCover" />
                </template>
                <MetaItem v-if="track.artist?.name" field="artist" :label="t('list.artist')">
                    {{ track.artist.name }}
                </MetaItem>
                <MetaItem v-if="track.album?.title" field="album" :label="t('list.album')">
                    {{ track.album.title }}
                </MetaItem>
                <MetaItem field="review-status" :label="t('reviewInbox.status')">
                    {{ reviewText }}
                </MetaItem>
                <MetaItem field="track" :label="t('adminFiles.song')">
                    <BaseLink :to="{ name: 'library-track', params: { id: track.id ?? id } }">
                        {{ t('adminFiles.openSong') }}
                    </BaseLink>
                </MetaItem>
                <ReviewTagHints :tags="queueItem?.missingTags" />
            </DetailHeader>
            <BaseAlert v-if="error" tone="error" :message="error" />
            <section data-part="section" data-item="metadata" :aria-labelledby="metadataTitleId">
                <h2 :id="metadataTitleId" data-part="section-title">
                    {{ t('reviewInbox.metadataTitle') }}
                </h2>
                <TrackMetadataForm
                    ref="editor"
                    :track-id="id"
                    :form-id="formId"
                    @saved="markAndAdvance"
                    @unchanged="markAndAdvance"
                />
            </section>
            <ReviewActions
                :form-id="formId"
                :can-save="canSave"
                :busy="busy"
                @mark-reviewed="markAndAdvance"
                @skip="skip"
            />
            <ReviewNav
                :index="position.index"
                :count="review.queue.length"
                :has-previous="!!position.previous"
                :has-next="!!position.next || review.queueHasMore"
                :busy="busy"
                @previous="goPrevious"
                @next="goNext"
            />
        </template>
    </div>
</template>
