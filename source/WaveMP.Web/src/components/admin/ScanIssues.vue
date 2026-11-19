<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { ScanIssue } from '@/api/generated/models'
import { issueLabelKey } from '@/admin/presentation'
import { useFormat } from '@/composables/useFormat'

/**
 * Files with problems of a scan, collapsed by default. A media folder the scan skipped has no path;
 * the name of its folder is shown instead. With problems in several folders, every entry names its
 * folder.
 */
const props = withDefaults(
    defineProps<{
        issues: readonly ScanIssue[]
        /** All files with problems, also those that are not listed. */
        failed: number
        /** Shows the list at once. */
        open?: boolean
    }>(),
    { open: false },
)

const { t } = useI18n()
const format = useFormat()

const severalSources = computed(
    () => new Set(props.issues.map((issue) => issue.sourceId ?? '')).size > 1,
)

function issueKey(issue: ScanIssue): string {
    return `${issue.sourceId ?? ''}/${issue.path ?? ''}`
}

function showsSource(issue: ScanIssue): boolean {
    return !!issue.sourceName && (!issue.path || severalSources.value)
}
</script>

<template>
    <details data-part="disclosure" :open="open">
        <summary data-part="disclosure-summary">
            <span data-part="label">{{
                t('adminLibrary.issuesSummary', { count: format.number(failed) })
            }}</span>
            <span v-if="failed > issues.length" data-part="field-hint">{{
                t('adminLibrary.issuesListed', { count: format.number(issues.length) })
            }}</span>
        </summary>
        <ul data-part="issue-list">
            <li v-for="issue in issues" :key="issueKey(issue)" data-part="issue-item">
                <span v-if="showsSource(issue)" data-part="issue-source">{{
                    issue.sourceName
                }}</span>
                <span v-if="issue.path" data-part="issue-path">{{ issue.path }}</span>
                <span data-part="label">{{ t(issueLabelKey(issue.code)) }}</span>
            </li>
        </ul>
    </details>
</template>
