import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { apiClient } from '@/api/client'
import { JobIdObject, ProblemCodeObject, type Backup, type Job } from '@/api/generated/models'
import { fetchAllPages } from '@/api/paging'
import { problemCodeOf } from '@/api/problems'
import { isActive } from '@/admin/presentation'

export type BackupsStatus = 'idle' | 'loading' | 'ready' | 'error'
export type BackupStartResult = 'started' | 'alreadyRunning' | 'error'
export type BackupDeleteResult = 'deleted' | 'notFound' | 'error'

/**
 * Exports of the database for the admin page: the job that creates them (last and next run) and
 * the list of the exports in the backup folder.
 */
export const useBackupsStore = defineStore('backups', () => {
    const job = ref<Job | null>(null)
    const backups = ref<Backup[]>([])
    const status = ref<BackupsStatus>('idle')

    /** Whether an export is queued or running. */
    const active = computed(() => isActive(job.value?.state === 'Idle' ? null : job.value?.state))

    async function load(): Promise<void> {
        if (!job.value) {
            status.value = 'loading'
        }

        try {
            const [loadedJob, loadedBackups] = await Promise.all([
                apiClient.api.v1.jobs.byId(JobIdObject.DatabaseBackup).get(),
                fetchAllPages((offset, limit) =>
                    apiClient.api.v1.backups.get({ queryParameters: { offset, limit } }),
                ),
            ])
            job.value = loadedJob ?? null
            backups.value = loadedBackups
            status.value = 'ready'
        } catch {
            status.value = 'error'
        }
    }

    /** Starts an export now. */
    async function create(): Promise<BackupStartResult> {
        try {
            await apiClient.api.v1.backups.post()
            return 'started'
        } catch (error) {
            return problemCodeOf(error) === ProblemCodeObject.JobAlready_running
                ? 'alreadyRunning'
                : 'error'
        }
    }

    /** Deletes an export; the list is loaded again afterwards. */
    async function remove(id: string): Promise<BackupDeleteResult> {
        let result: BackupDeleteResult
        try {
            await apiClient.api.v1.backups.byId(id).delete()
            result = 'deleted'
        } catch (error) {
            result =
                problemCodeOf(error) === ProblemCodeObject.BackupNot_found ? 'notFound' : 'error'
        }

        await load()
        return result
    }

    return { job, backups, status, active, load, create, remove }
})
