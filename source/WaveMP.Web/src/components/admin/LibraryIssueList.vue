<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { LibraryIssue, LibraryIssueTrack } from '@/api/generated/models'
import {
    issueActions,
    issueDetail,
    issueLocation,
    issueTypeLabelKey,
    issueTypeToken,
    severityToken,
    trackContext,
    trackLocation,
} from '@/admin/libraryIssues'
import { durationSeconds } from '@/admin/presentation'
import BaseLink from '@/components/base/BaseLink.vue'
import { useFormat } from '@/composables/useFormat'

/**
 * Problems of the library check: the kind as badge, the song (its title opens the page of the
 * song) with artist and album, where its files are, what is wrong and, for a possible duplicate,
 * the other song. The links below lead to where the problem is fixed: the metadata editor, the
 * online search or the files of the song.
 */
defineProps<{
    items: readonly LibraryIssue[]
    /** Accessible name of the list. */
    label: string
    busy?: boolean
}>()

const { t } = useI18n()
const format = useFormat()

function detailText(issue: LibraryIssue): string {
    const detail = issueDetail(issue)
    return t(detail.key, {
        tags: detail.tagKeys.map((key) => t(key)).join(', '),
    })
}

function length(track: LibraryIssueTrack | null | undefined): string | null {
    const seconds = durationSeconds(track?.durationMilliseconds)
    return seconds === null ? null : format.duration(seconds)
}
</script>

<template>
    <ul data-part="library-issue-list" :aria-label="label" :aria-busy="busy ? 'true' : undefined">
        <li
            v-for="issue in items"
            :key="issue.id ?? ''"
            data-part="library-issue-item"
            :data-item="issueTypeToken(issue.type)"
            :data-state="severityToken(issue)"
        >
            <div data-part="library-issue-info">
                <span data-part="library-issue-meta">
                    <span
                        data-part="badge"
                        :data-state="`${issueTypeToken(issue.type)} ${severityToken(issue)}`"
                    >
                        <span data-part="label">{{ t(issueTypeLabelKey(issue.type)) }}</span>
                    </span>
                    <span v-if="length(issue.track)" data-part="label" data-field="duration">
                        {{ length(issue.track) }}
                    </span>
                </span>
                <BaseLink
                    v-if="issue.track?.id"
                    part="library-issue-title"
                    :to="{ name: 'library-track', params: { id: issue.track.id } }"
                >
                    {{ issue.track.title }}
                </BaseLink>
                <span v-if="trackContext(issue.track).length > 0" data-part="label">
                    {{ trackContext(issue.track).join(' · ') }}
                </span>
                <span data-part="file-path">{{ issueLocation(issue) }}</span>
                <p data-part="library-issue-detail">
                    <span data-part="label">{{ detailText(issue) }}</span>
                    <BaseLink
                        v-if="issue.relatedTrack?.id"
                        data-item="related-track"
                        :to="{ name: 'library-track', params: { id: issue.relatedTrack.id } }"
                    >
                        {{ issue.relatedTrack.title }}
                    </BaseLink>
                    <span v-if="issue.relatedTrack" data-part="file-path">
                        {{ trackLocation(issue.relatedTrack) }}
                    </span>
                    <span
                        v-if="length(issue.relatedTrack)"
                        data-part="label"
                        data-field="related-duration"
                    >
                        {{ length(issue.relatedTrack) }}
                    </span>
                </p>
            </div>
            <div
                v-if="issueActions(issue).length > 0"
                data-part="library-issue-actions"
                role="group"
                :aria-label="t('adminChecks.actionsLabel', { title: issue.track?.title ?? '' })"
            >
                <BaseLink
                    v-for="action in issueActions(issue)"
                    :key="action.item"
                    :data-item="action.item"
                    :to="action.to"
                >
                    {{ t(action.labelKey) }}
                </BaseLink>
            </div>
        </li>
    </ul>
</template>
