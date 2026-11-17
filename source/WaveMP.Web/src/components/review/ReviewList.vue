<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { TrackReview } from '@/api/generated/models'
import BaseIcon from '@/components/base/BaseIcon.vue'
import BaseLink from '@/components/base/BaseLink.vue'
import LibraryCover from '@/components/library/LibraryCover.vue'
import ReviewTagHints from './ReviewTagHints.vue'
import { useFormat } from '@/composables/useFormat'
import { isReviewed } from '@/review/inbox'

/**
 * Songs of the review inbox: a checkbox for the bulk action, the cover, the title (opens the song
 * in the editor of the inbox), artist and album, the folder and base name of the files, when the
 * song was added and which tags its file lacks. Songs reviewed since the list was loaded stay in
 * the list, marked as reviewed, so the list keeps its position.
 */
const props = defineProps<{
    items: readonly TrackReview[]
    /** Songs marked as reviewed in this tab. */
    reviewed: ReadonlySet<string>
    /** Songs selected for the bulk action. */
    selected: ReadonlySet<string>
    /** Accessible name of the list. */
    label: string
    busy?: boolean
}>()

const emit = defineEmits<{
    select: [trackId: string, selected: boolean]
}>()

const { t } = useI18n()
const format = useFormat()

function idOf(item: TrackReview): string {
    return item.trackId ?? ''
}

function context(item: TrackReview): string {
    return [item.artist?.name, item.album?.title].filter((part) => !!part).join(' · ')
}

function location(item: TrackReview): string {
    return [item.directoryPath, item.baseName].filter((part) => !!part).join('/')
}

function stateOf(item: TrackReview): string {
    return [
        isReviewed(item, props.reviewed) ? 'reviewed' : 'unreviewed',
        ...(props.selected.has(idOf(item)) ? ['selected'] : []),
        ...((item.missingTags?.length ?? 0) > 0 ? ['missing-tags'] : []),
    ].join(' ')
}

function onChange(item: TrackReview, event: Event): void {
    emit('select', idOf(item), (event.target as HTMLInputElement).checked)
}
</script>

<template>
    <ul data-part="review-list" :aria-label="label" :aria-busy="busy ? 'true' : undefined">
        <li
            v-for="item in items"
            :key="idOf(item)"
            data-part="review-item"
            :data-state="stateOf(item)"
        >
            <input
                type="checkbox"
                data-part="checkbox"
                :checked="selected.has(idOf(item))"
                :disabled="isReviewed(item, reviewed)"
                :aria-label="t('reviewInbox.select', { title: item.title ?? '' })"
                @change="onChange(item, $event)"
            />
            <LibraryCover kind="track" :id="item.trackId" :available="!!item.hasCover" />
            <div data-part="review-info">
                <BaseLink
                    part="review-title"
                    :to="{ name: 'admin-review-track', params: { id: idOf(item) } }"
                >
                    {{ item.title }}
                </BaseLink>
                <span v-if="context(item)" data-part="label" data-field="context">
                    {{ context(item) }}
                </span>
                <span data-part="file-path">{{ location(item) }}</span>
                <span data-part="review-meta">
                    <span v-if="isReviewed(item, reviewed)" data-part="badge" data-state="reviewed">
                        <BaseIcon name="review-done" />
                        <span data-part="label">{{ t('reviewInbox.reviewed') }}</span>
                    </span>
                    <span v-if="item.firstSeenAt" data-part="label" data-field="first-seen-at">
                        {{ t('reviewInbox.added', { date: format.date(item.firstSeenAt) }) }}
                    </span>
                    <ReviewTagHints :tags="item.missingTags" />
                </span>
            </div>
        </li>
    </ul>
</template>
