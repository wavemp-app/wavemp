<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import AdminNav from '@/components/admin/AdminNav.vue'
import JobList from '@/components/admin/JobList.vue'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import { usePolling } from '@/composables/usePolling'
import { analyzeAllSongs } from '@/library/api'
import { useJobsStore } from '@/stores/jobs'

/**
 * Admin page of the background jobs: last and next run of every job, a manual start and, for the
 * song analysis, a new analysis of the whole library.
 */
const { t } = useI18n()
const jobs = useJobsStore()

const starting = ref<string | null>(null)
const message = ref<string | null>(null)
const notice = ref<string | null>(null)

const polling = usePolling(async () => {
    await jobs.load()
    if (!jobs.anyActive) {
        polling.stop()
    }
}, 2000)

async function run(id: string): Promise<void> {
    starting.value = id
    message.value = null
    notice.value = null
    const result = await jobs.run(id)
    starting.value = null
    if (result === 'alreadyRunning') {
        message.value = t('adminJobs.alreadyRunning')
    } else if (result === 'error') {
        message.value = t('adminJobs.runError')
    }

    await jobs.load()
    if (jobs.anyActive) {
        polling.start()
    }
}

/** Lets the song analysis analyze every file again (corrections of admins stay). */
async function renew(id: string): Promise<void> {
    starting.value = id
    message.value = null
    notice.value = null
    try {
        const started = await analyzeAllSongs()
        notice.value = t('adminJobs.analyzeAllStarted', { count: started?.renewed ?? 0 })
    } catch {
        message.value = t('adminJobs.analyzeAllError')
    } finally {
        starting.value = null
    }

    await jobs.load()
    if (jobs.anyActive) {
        polling.start()
    }
}

onMounted(async () => {
    await jobs.load()
    if (jobs.anyActive) {
        polling.start()
    }
})
</script>

<template>
    <div
        data-part="view"
        data-view="admin-jobs"
        :aria-busy="jobs.status === 'loading' ? 'true' : undefined"
    >
        <AdminNav />
        <template v-if="jobs.status === 'error'">
            <BaseAlert tone="error" :message="t('adminJobs.loadError')" />
            <div data-part="view-actions">
                <BaseButton data-item="retry" :label="t('library.retry')" @click="jobs.load()" />
            </div>
        </template>
        <BaseAlert v-if="message" tone="error" :message="message" />
        <BaseAlert v-if="notice" tone="success" :message="notice" />
        <JobList :jobs="jobs.jobs" :starting="starting" @run="run" @renew="renew" />
    </div>
</template>
