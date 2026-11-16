<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { Backup, Job } from '@/api/generated/models'
import {
    activityOf,
    dailyScheduleKey,
    errorLabelKey,
    runDateTimeFormat,
    statusPresentation,
} from '@/admin/presentation'
import BaseButton from '@/components/base/BaseButton.vue'
import { useFormat } from '@/composables/useFormat'

/**
 * State of the database backups: last and next run of the daily export, its time, the number and
 * size of the kept exports, the error of the last run and the button that exports now.
 */
const props = withDefaults(
    defineProps<{
        job: Job
        backups: readonly Backup[]
        /** Whether the start of an export is being requested. */
        starting?: boolean
    }>(),
    { starting: false },
)

const emit = defineEmits<{
    create: []
}>()

const { t } = useI18n()
const format = useFormat()

const activity = computed(() => activityOf(props.job.state === 'Idle' ? null : props.job.state))
const lastRun = computed(() => props.job.lastRun ?? null)
const lastStatus = computed(() =>
    activity.value !== 'idle'
        ? statusPresentation(activity.value === 'running' ? 'Running' : 'Queued')
        : lastRun.value
          ? statusPresentation(lastRun.value.status)
          : null,
)
const errorKey = computed(() =>
    activity.value === 'idle' ? errorLabelKey(lastRun.value?.errorCode) : null,
)

function dateTime(date: Date | null | undefined): string {
    return date ? format.date(date, runDateTimeFormat) : t('adminBackups.none')
}

const statistics = computed(() => [
    {
        id: 'last-run',
        labelKey: 'adminBackups.stats.lastRun',
        text: lastRun.value
            ? dateTime(lastRun.value.startedAt ?? lastRun.value.queuedAt)
            : t('adminBackups.never'),
    },
    { id: 'next-run', labelKey: 'adminBackups.stats.nextRun', text: dateTime(props.job.nextRunAt) },
    {
        id: 'schedule',
        labelKey: 'adminBackups.stats.schedule',
        text: props.job.dailyAt
            ? t(dailyScheduleKey('adminBackups', props.job.timeZone), {
                  time: props.job.dailyAt,
                  timeZone: props.job.timeZone,
              })
            : t('adminBackups.none'),
    },
    {
        id: 'backups',
        labelKey: 'adminBackups.stats.backups',
        text: format.number(props.backups.length),
    },
    {
        id: 'size',
        labelKey: 'adminBackups.stats.size',
        text: format.fileSize(
            props.backups.reduce((sum, backup) => sum + (backup.sizeBytes ?? 0), 0),
        ),
    },
])
</script>

<template>
    <div data-part="backup-status" :data-state="activity">
        <dl data-part="stat-list" :aria-label="t('adminBackups.stats.label')">
            <div v-if="lastStatus" data-part="stat-item" data-item="status">
                <dt data-part="label">{{ t('adminBackups.stats.status') }}</dt>
                <dd data-part="stat-value">
                    <span data-part="badge" :data-state="lastStatus.token">
                        <span data-part="label">{{ t(lastStatus.labelKey) }}</span>
                    </span>
                </dd>
            </div>
            <div
                v-for="item in statistics"
                :key="item.id"
                data-part="stat-item"
                :data-item="item.id"
            >
                <dt data-part="label">{{ t(item.labelKey) }}</dt>
                <dd data-part="stat-value">{{ item.text }}</dd>
            </div>
        </dl>
        <span v-if="errorKey" data-part="label">{{ t(errorKey) }}</span>
        <span data-part="label">{{ t('adminBackups.hint') }}</span>
        <BaseButton
            icon="backup"
            :label="t('adminBackups.createNow')"
            :disabled="starting || activity !== 'idle'"
            @click="emit('create')"
        />
    </div>
</template>
