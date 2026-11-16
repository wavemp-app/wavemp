import { defineStore } from 'pinia'
import { ref } from 'vue'
import { apiClient } from '@/api/client'
import type { BackupSettings, UpdateBackupSettings } from '@/api/generated/models'

export type BackupSettingsStatus = 'idle' | 'loading' | 'ready' | 'error'

/** Settings of the database backups. Changes pass errors on to the page, which shows them. */
export const useBackupSettingsStore = defineStore('backupSettings', () => {
    const settings = ref<BackupSettings | null>(null)
    const status = ref<BackupSettingsStatus>('idle')

    async function load(): Promise<void> {
        status.value = 'loading'
        try {
            settings.value = (await apiClient.api.v1.settings.backup.get()) ?? null
            status.value = 'ready'
        } catch {
            status.value = 'error'
        }
    }

    async function update(changes: UpdateBackupSettings): Promise<BackupSettings | null> {
        const saved = (await apiClient.api.v1.settings.backup.patch(changes)) ?? null
        if (saved) {
            settings.value = saved
        }

        return saved
    }

    return { settings, status, load, update }
})
