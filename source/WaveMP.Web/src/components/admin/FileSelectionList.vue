<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { TrackFileSummary } from '@/api/generated/models'
import { categoryStateKey, summaryStates, summaryStateTokens } from '@/admin/fileSelections'
import BaseLink from '@/components/base/BaseLink.vue'
import { useFormat } from '@/composables/useFormat'

/**
 * Songs of the file selection: title (opens the files of the song), artist and album, the folder
 * and base name of the files, the categories with a selection (or a selected file that is gone)
 * and the number of audio files, videos and covers.
 */
defineProps<{
    items: readonly TrackFileSummary[]
    /** Accessible name of the list. */
    label: string
    busy?: boolean
}>()

const { t } = useI18n()
const format = useFormat()

function location(item: TrackFileSummary): string {
    return [item.directoryPath, item.baseName].filter((part) => !!part).join('/')
}

function context(item: TrackFileSummary): string {
    return [item.artist?.name, item.album?.title].filter((part) => !!part).join(' · ')
}

function counts(item: TrackFileSummary): { id: string; text: string }[] {
    return [
        {
            id: 'audio',
            text: t('adminFiles.counts.audio', { count: format.number(item.audioCount ?? 0) }),
        },
        {
            id: 'video',
            text: t('adminFiles.counts.video', { count: format.number(item.videoCount ?? 0) }),
        },
        {
            id: 'cover',
            text: t('adminFiles.counts.cover', { count: format.number(item.coverCount ?? 0) }),
        },
    ]
}
</script>

<template>
    <ul data-part="file-selection-list" :aria-label="label" :aria-busy="busy ? 'true' : undefined">
        <li
            v-for="item in items"
            :key="item.trackId ?? ''"
            data-part="file-selection-item"
            :data-state="summaryStateTokens(item) || undefined"
        >
            <div data-part="file-selection-info">
                <BaseLink
                    part="file-selection-title"
                    :to="{ name: 'admin-track-files', params: { id: item.trackId ?? '' } }"
                >
                    {{ item.title }}
                </BaseLink>
                <span v-if="context(item)" data-part="label">{{ context(item) }}</span>
                <span data-part="file-path">{{ location(item) }}</span>
                <span data-part="file-meta">
                    <span
                        v-for="state in summaryStates(item)"
                        :key="state.category"
                        data-part="badge"
                        :data-state="`${state.state.toLowerCase()} ${state.category}`"
                    >
                        <span data-part="label">
                            {{ t(categoryStateKey(state.category, state.state)) }}
                        </span>
                    </span>
                    <span
                        v-for="count in counts(item)"
                        :key="count.id"
                        data-part="label"
                        :data-field="count.id"
                    >
                        {{ count.text }}
                    </span>
                </span>
            </div>
        </li>
    </ul>
</template>
