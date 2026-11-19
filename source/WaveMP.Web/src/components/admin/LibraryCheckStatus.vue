<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { Job, LibraryIssueSummary } from '@/api/generated/models'
import { activityOf, errorLabelKey, runDateTimeFormat } from '@/admin/presentation'
import BaseButton from '@/components/base/BaseButton.vue'
import { useFormat } from '@/composables/useFormat'

/**
 * State of the library check: whether it runs, when it ran last, how many songs it checked, the
 * number of problems and hints it found, the error of its last run and the button that checks the
 * library now.
 */
const props = withDefaults(
    defineProps<{
        summary: LibraryIssueSummary | null
        job: Job | null
        /** Whether the start of a check is being requested. */
        starting?: boolean
    }>(),
    { starting: false },
)

const emit = defineEmits<{
    run: []
}>()

const { t } = useI18n()
const format = useFormat()

const activity = computed(() =>
    activityOf(props.job?.state === 'Idle' ? null : (props.job?.state ?? null)),
)
const errorKey = computed(() =>
    activity.value === 'idle' ? errorLabelKey(props.job?.lastRun?.errorCode) : null,
)

const statistics = computed(() => {
    const checkedAt = props.summary?.checkedAt
    return [
        {
            id: 'checked-at',
            labelKey: 'adminChecks.stats.checkedAt',
            text: checkedAt ? format.date(checkedAt, runDateTimeFormat) : t('adminChecks.never'),
        },
        {
            id: 'tracks',
            labelKey: 'adminChecks.stats.tracks',
            text: format.number(props.summary?.trackCount ?? 0),
        },
        {
            id: 'problems',
            labelKey: 'adminChecks.stats.problems',
            text: format.number(props.summary?.problemCount ?? 0),
        },
        {
            id: 'hints',
            labelKey: 'adminChecks.stats.hints',
            text: format.number(props.summary?.hintCount ?? 0),
        },
    ]
})
</script>

<template>
    <div data-part="library-check-status" :data-state="activity">
        <dl data-part="stat-list" :aria-label="t('adminChecks.stats.label')">
            <div v-if="activity !== 'idle'" data-part="stat-item" data-item="status">
                <dt data-part="label">{{ t('adminChecks.stats.status') }}</dt>
                <dd data-part="stat-value">
                    <span data-part="badge" :data-state="activity">
                        <span data-part="label">{{
                            activity === 'running'
                                ? t('adminChecks.states.running')
                                : t('adminChecks.states.queued')
                        }}</span>
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
        <span v-if="errorKey" data-part="label" data-item="error">{{ t(errorKey) }}</span>
        <span data-part="label">{{ t('adminChecks.hint') }}</span>
        <BaseButton
            data-item="run-check"
            icon="admin-checks"
            :label="activity === 'idle' ? t('adminChecks.checkNow') : t('adminChecks.checking')"
            :disabled="starting || activity !== 'idle'"
            @click="emit('run')"
        />
    </div>
</template>
