<script setup lang="ts">
import { reactive } from 'vue'
import { useI18n } from 'vue-i18n'
import {
    ProblemCodeObject,
    type LibraryIssue,
    type LibraryIssueTrack,
} from '@/api/generated/models'
import { problemCodeOf } from '@/api/problems'
import {
    artistSplitChanges,
    issueActions,
    issueDetail,
    issueLocation,
    issueTypeLabelKey,
    issueTypeToken,
    severityToken,
    suggestedArtists,
    trackContext,
    trackLocation,
    type ArtistSplitAnswer,
} from '@/admin/libraryIssues'
import { durationSeconds } from '@/admin/presentation'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseLink from '@/components/base/BaseLink.vue'
import ArtistNames from '@/components/library/ArtistNames.vue'
import { useFormat } from '@/composables/useFormat'
import { updateTrackMetadata } from '@/library/api'

/**
 * Problems of the library check: the kind as badge, the song (its title opens the page of the
 * song) with artist and album, where its files are, what is wrong and, for a possible duplicate,
 * the other song. The links below lead to where the problem is fixed: the metadata editor, the
 * online search or the files of the song. For the hint that an artist name may stand for several
 * artists it shows the suggested split and answers it at once: "Split" writes the suggested artists
 * into the tags of the audio file, "Keep as one" writes the name as one artist; the hint goes away
 * with the next check.
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

type AnswerState = 'saving' | ArtistSplitAnswer | 'failed' | 'not-writable'

/** The answers given on this page, by problem. */
const answers = reactive(new Map<string, AnswerState>())

function answerOf(issue: LibraryIssue): AnswerState | undefined {
    return answers.get(issue.id ?? '')
}

async function answer(issue: LibraryIssue, choice: ArtistSplitAnswer): Promise<void> {
    const id = issue.id ?? ''
    const trackId = issue.track?.id
    if (!trackId || answers.get(id) === 'saving') {
        return
    }

    answers.set(id, 'saving')
    try {
        await updateTrackMetadata(trackId, artistSplitChanges(issue, choice))
        answers.set(id, choice)
    } catch (failure) {
        answers.set(
            id,
            problemCodeOf(failure) === ProblemCodeObject.TrackNot_writable
                ? 'not-writable'
                : 'failed',
        )
    }
}

function answerMessage(state: AnswerState | undefined): string | null {
    switch (state) {
        case 'split':
            return t('adminChecks.artistSplit.split')
        case 'keep':
            return t('adminChecks.artistSplit.kept')
        case 'not-writable':
            return t('adminChecks.artistSplit.notWritable')
        case 'failed':
            return t('adminChecks.artistSplit.failed')
        default:
            return null
    }
}

function answered(issue: LibraryIssue): boolean {
    const state = answerOf(issue)
    return state === 'saving' || state === 'split' || state === 'keep'
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
                <p
                    v-if="suggestedArtists(issue).length > 0"
                    data-part="artist-split"
                    :data-state="answerOf(issue)"
                >
                    <span data-part="label">{{ t('adminChecks.artistSplit.suggestion') }}</span
                    >{{ ' '
                    }}<span data-part="artist-split-names">
                        <ArtistNames :artists="suggestedArtists(issue)" :links="false" />
                    </span>
                </p>
                <BaseAlert
                    v-if="answerMessage(answerOf(issue))"
                    :tone="answered(issue) ? 'success' : 'error'"
                    :message="answerMessage(answerOf(issue))!"
                />
            </div>
            <div
                v-if="issueActions(issue).length > 0"
                data-part="library-issue-actions"
                role="group"
                :aria-label="t('adminChecks.actionsLabel', { title: issue.track?.title ?? '' })"
            >
                <template v-if="suggestedArtists(issue).length > 0">
                    <BaseButton
                        data-item="split-artists"
                        variant="primary"
                        :label="t('adminChecks.artistSplit.splitAction')"
                        :disabled="answered(issue)"
                        @click="answer(issue, 'split')"
                    />
                    <BaseButton
                        data-item="keep-artist"
                        :label="t('adminChecks.artistSplit.keepAction')"
                        :disabled="answered(issue)"
                        @click="answer(issue, 'keep')"
                    />
                </template>
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
