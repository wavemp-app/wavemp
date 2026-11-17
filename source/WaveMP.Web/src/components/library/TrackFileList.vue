<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { MediaFile } from '@/api/generated/models'
import { issueLabelKey } from '@/admin/presentation'
import { useFileDetails } from '@/composables/useFileDetails'
import { fileKindKey, fileKindToken } from '@/library/presentation'

/**
 * The files of a track for admins: path, kind, whether it plays (primary), and its technical
 * details. Problems found by the scan are shown with the texts of the scan.
 */
defineProps<{
    files: readonly MediaFile[]
    /** Accessible name of the list. */
    label: string
}>()

const { t } = useI18n()
const details = useFileDetails()

function itemState(file: MediaFile): string {
    return [fileKindToken(file.kind), ...(file.isPrimary ? ['primary'] : [])].join(' ')
}
</script>

<template>
    <ul data-part="file-list" :aria-label="label">
        <li
            v-for="file in files"
            :key="file.id ?? ''"
            data-part="file-item"
            :data-state="itemState(file)"
        >
            <span data-part="file-path">{{ file.relativePath }}</span>
            <span data-part="file-meta">
                <span data-part="badge" :data-state="fileKindToken(file.kind)">
                    <span data-part="label">{{ t(fileKindKey(file.kind)) }}</span>
                </span>
                <span v-if="file.isPrimary" data-part="badge" data-state="primary">
                    <span data-part="label">{{ t('files.primary') }}</span>
                </span>
                <span v-if="file.isLossless" data-part="badge" data-state="lossless">
                    <span data-part="label">{{ t('files.lossless') }}</span>
                </span>
                <span v-if="file.isWritable" data-part="badge" data-state="writable">
                    <span data-part="label">{{ t('files.writable') }}</span>
                </span>
                <span v-if="file.issue" data-part="badge" data-state="issue">
                    <span data-part="label">{{ t(issueLabelKey(file.issue)) }}</span>
                </span>
                <span v-for="(detail, index) in details(file)" :key="index" data-part="label">
                    {{ detail }}
                </span>
            </span>
        </li>
    </ul>
</template>
