<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { Backup } from '@/api/generated/models'
import { backupTriggerPresentation } from '@/admin/presentation'
import BaseButton from '@/components/base/BaseButton.vue'
import { useFormat } from '@/composables/useFormat'

/** The exports of the database in the backup folder, newest first, each with a delete button. */
withDefaults(
    defineProps<{
        backups: readonly Backup[]
        /** Id of the export that is being deleted. */
        deleting?: string | null
    }>(),
    { deleting: null },
)

const emit = defineEmits<{
    delete: [backup: Backup]
}>()

const { t } = useI18n()
const format = useFormat()

function dateTime(date: Date | null | undefined): string {
    return date ? format.date(date, { dateStyle: 'medium', timeStyle: 'short' }) : ''
}
</script>

<template>
    <div v-if="backups.length === 0" data-part="empty-state">
        <p data-part="empty-state-text">{{ t('adminBackups.empty') }}</p>
    </div>
    <ul v-else data-part="backup-list" :aria-label="t('adminBackups.list')">
        <li
            v-for="backup in backups"
            :key="backup.id ?? ''"
            data-part="backup-item"
            :data-kind="backupTriggerPresentation(backup.trigger).token"
            :data-state="deleting === backup.id ? 'busy' : undefined"
        >
            <div data-part="backup-info">
                <span data-part="backup-name">{{ backup.fileName }}</span>
                <span data-part="backup-meta">
                    <span
                        data-part="badge"
                        :data-state="backupTriggerPresentation(backup.trigger).token"
                    >
                        <span data-part="label">{{
                            t(backupTriggerPresentation(backup.trigger).labelKey)
                        }}</span>
                    </span>
                    <span data-part="label">{{ dateTime(backup.createdAt) }}</span>
                    <span data-part="label">{{ format.fileSize(backup.sizeBytes ?? 0) }}</span>
                </span>
            </div>
            <div data-part="backup-actions">
                <BaseButton
                    variant="icon"
                    icon="delete"
                    :label="t('adminBackups.delete', { name: backup.fileName ?? '' })"
                    :disabled="deleting === backup.id"
                    @click="emit('delete', backup)"
                />
            </div>
        </li>
    </ul>
</template>
