<script setup lang="ts">
import { onMounted, ref, useId } from 'vue'
import { useI18n } from 'vue-i18n'
import type { Backup } from '@/api/generated/models'
import AdminNav from '@/components/admin/AdminNav.vue'
import BackupDeleteDialog from '@/components/admin/BackupDeleteDialog.vue'
import BackupList from '@/components/admin/BackupList.vue'
import BackupSettingsForm from '@/components/admin/BackupSettingsForm.vue'
import BackupStatus from '@/components/admin/BackupStatus.vue'
import BaseAlert, { type AlertTone } from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import { usePolling } from '@/composables/usePolling'
import { useBackupsStore } from '@/stores/backups'

/**
 * Admin page of the database backups: last and next run of the daily export, a manual export, the
 * list of the exports and the settings. Restoring is done outside the app (see the README).
 */
const { t } = useI18n()
const store = useBackupsStore()

const starting = ref(false)
const deleting = ref<string | null>(null)
const pendingDelete = ref<Backup | null>(null)
const dialogOpen = ref(false)
const message = ref<{ text: string; tone: AlertTone } | null>(null)
const statusTitleId = useId()
const backupsTitleId = useId()
const settingsTitleId = useId()

const polling = usePolling(async () => {
    await store.load()
    if (!store.active) {
        polling.stop()
    }
}, 2000)

async function create(): Promise<void> {
    starting.value = true
    message.value = null
    const result = await store.create()
    starting.value = false
    if (result === 'alreadyRunning') {
        message.value = { text: t('adminBackups.alreadyRunning'), tone: 'info' }
    } else if (result === 'error') {
        message.value = { text: t('adminBackups.createError'), tone: 'error' }
    }

    await store.load()
    if (store.active) {
        polling.start()
    }
}

function askDelete(backup: Backup): void {
    pendingDelete.value = backup
    dialogOpen.value = true
}

async function confirmDelete(): Promise<void> {
    const backup = pendingDelete.value
    if (!backup?.id) {
        return
    }

    deleting.value = backup.id
    message.value = null
    const result = await store.remove(backup.id)
    deleting.value = null
    dialogOpen.value = false
    pendingDelete.value = null
    if (result === 'deleted') {
        message.value = { text: t('adminBackups.deleted'), tone: 'success' }
    } else if (result === 'error') {
        message.value = { text: t('adminBackups.deleteError'), tone: 'error' }
    }
}

onMounted(async () => {
    await store.load()
    if (store.active) {
        polling.start()
    }
})
</script>

<template>
    <div
        data-part="view"
        data-view="admin-backups"
        :aria-busy="store.status === 'loading' || undefined"
    >
        <AdminNav />
        <section data-part="section" data-item="status" :aria-labelledby="statusTitleId">
            <h2 :id="statusTitleId" data-part="section-title">
                {{ t('adminBackups.statusTitle') }}
            </h2>
            <template v-if="store.status === 'error'">
                <BaseAlert tone="error" :message="t('adminBackups.loadError')" />
                <div data-part="view-actions">
                    <BaseButton
                        data-item="retry"
                        :label="t('library.retry')"
                        @click="store.load()"
                    />
                </div>
            </template>
            <BackupStatus
                v-if="store.job"
                :job="store.job"
                :backups="store.backups"
                :starting="starting"
                @create="create"
            />
            <BaseAlert v-if="message" :tone="message.tone" :message="message.text" />
        </section>
        <section data-part="section" data-item="backups" :aria-labelledby="backupsTitleId">
            <h2 :id="backupsTitleId" data-part="section-title">
                {{ t('adminBackups.backupsTitle') }}
            </h2>
            <BackupList
                v-if="store.status !== 'loading' && store.job"
                :backups="store.backups"
                :deleting="deleting"
                @delete="askDelete"
            />
        </section>
        <section data-part="section" data-item="settings" :aria-labelledby="settingsTitleId">
            <h2 :id="settingsTitleId" data-part="section-title">
                {{ t('adminBackups.settingsTitle') }}
            </h2>
            <BackupSettingsForm @saved="store.load()" />
        </section>
        <BackupDeleteDialog
            v-model:open="dialogOpen"
            :backup="pendingDelete"
            :busy="deleting !== null"
            @confirm="confirmDelete"
        />
    </div>
</template>
