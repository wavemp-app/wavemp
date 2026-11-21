<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { BackupSettings } from '@/api/generated/models'
import { problemCodeOf, validationErrorsOf } from '@/api/problems'
import { backupRetentionOptions, isTimeOfDay } from '@/admin/presentation'
import { changedSettings } from '@/admin/settingsChanges'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseSelect, { type SelectOption } from '@/components/base/BaseSelect.vue'
import BaseTextField from '@/components/base/BaseTextField.vue'
import { useFormat } from '@/composables/useFormat'
import { useSettingsLock } from '@/composables/useSettingsLock'
import { validationMessagesByField } from '@/i18n/validation'
import { useBackupSettingsStore } from '@/stores/backupSettings'

/**
 * Settings of the database backups: the time of the daily export and how many exports are kept.
 * Saving sends only the changed values, so settings changed elsewhere since the page was loaded
 * stay as they are.
 */
const props = defineProps<{
    /** IANA id of the time zone of the server, in which the time of the daily export is set. */
    timeZone?: string | null
}>()

const emit = defineEmits<{
    saved: []
}>()

const { t } = useI18n()
const format = useFormat()
const store = useBackupSettingsStore()

const scheduleTime = ref('')
const retention = ref('')
const saving = ref(false)
const saved = ref(false)
const failed = ref(false)
const invalid = ref<Partial<Record<'scheduleTime' | 'retention', string>>>({})
/** Locked until the settings are loaded, so a loaded value never replaces what was typed. */
const locked = useSettingsLock(saving, () => !!store.settings)

function show(settings: BackupSettings | null): void {
    if (settings?.scheduleTime) {
        scheduleTime.value = settings.scheduleTime
    }
    if (settings?.retentionCount) {
        retention.value = String(settings.retentionCount)
    }
}

function retentionLabel(count: number): string {
    return format.number(count)
}

const options = computed<SelectOption[]>(() => {
    const offered = backupRetentionOptions.map((count) => ({
        value: String(count),
        label: retentionLabel(count),
    }))
    if (retention.value && !offered.some((option) => option.value === retention.value)) {
        offered.push({ value: retention.value, label: retentionLabel(Number(retention.value)) })
    }

    return offered
})

async function save(): Promise<void> {
    if (saving.value) {
        return
    }

    saved.value = false
    failed.value = false
    invalid.value = {}
    // Browsers without a time input send what the user typed; it is checked before it is sent.
    if (!isTimeOfDay(scheduleTime.value)) {
        invalid.value = { scheduleTime: t('adminBackups.errors.scheduleTime') }
        return
    }

    saving.value = true
    try {
        // Only the changed values: an unchanged field must not put back a value that was changed
        // elsewhere since the form was loaded. The answer shows all settings of the server again.
        show(
            await store.update(
                changedSettings(store.settings, {
                    scheduleTime: scheduleTime.value,
                    retentionCount: Number(retention.value),
                }),
            ),
        )
        saved.value = true
        emit('saved')
    } catch (error) {
        const messages = validationMessagesByField(
            validationErrorsOf(error),
            { scheduleTime: 'scheduleTime', retentionCount: 'retention' },
            (key, parameters) => t(key, parameters),
            (field) =>
                field === 'scheduleTime'
                    ? { keys: { invalid_format: 'adminBackups.errors.scheduleTime' } }
                    : { keys: { out_of_range: 'adminBackups.errors.retention' } },
        )
        if (problemCodeOf(error) === 'request.invalid' && Object.keys(messages).length > 0) {
            invalid.value = messages
        } else {
            failed.value = true
        }
    } finally {
        saving.value = false
    }
}

watch(() => store.settings, show)

onMounted(async () => {
    if (!store.settings) {
        await store.load()
    }

    show(store.settings)
})
</script>

<template>
    <template v-if="store.status === 'error' && !store.settings">
        <BaseAlert tone="error" :message="t('adminBackups.settingsLoadError')" />
        <div data-part="view-actions">
            <BaseButton data-item="retry" :label="t('library.retry')" @click="store.load()" />
        </div>
    </template>
    <form
        v-else
        data-part="form"
        :aria-busy="store.status === 'loading' && !store.settings ? 'true' : undefined"
        @submit.prevent="save"
    >
        <BaseTextField
            v-model="scheduleTime"
            data-field="backup-time"
            type="time"
            :label="t('adminBackups.scheduleTime')"
            :hint="
                props.timeZone
                    ? t('adminBackups.scheduleTimeHintZone', { timeZone: props.timeZone })
                    : t('adminBackups.scheduleTimeHint')
            "
            :error="invalid.scheduleTime"
            :disabled="locked"
            required
        />
        <BaseSelect
            v-model="retention"
            data-field="backup-retention"
            :label="t('adminBackups.retention')"
            :options="options"
            :hint="t('adminBackups.retentionHint')"
            :error="invalid.retention"
            :disabled="locked"
        />
        <BaseAlert v-if="saved" tone="success" :message="t('adminBackups.saved')" />
        <BaseAlert v-if="failed" tone="error" :message="t('adminBackups.saveError')" />
        <div data-part="view-actions">
            <BaseButton
                type="submit"
                variant="primary"
                :label="t('adminBackups.save')"
                :disabled="locked"
            />
        </div>
    </form>
</template>
