<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import {
    HardwareAccelerationModeObject,
    type HardwareAccelerationMode,
    type TranscodingSettings,
} from '@/api/generated/models'
import { problemCodeOf, validationErrorsOf } from '@/api/problems'
import {
    audioBitrateOptions,
    maxParallelJobOptions,
    maxVideoHeightOptions,
} from '@/admin/transcoding'
import { changedSettings } from '@/admin/settingsChanges'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import type { SegmentOption } from '@/components/base/BaseSegmentedControl.vue'
import BaseSegmentedField from '@/components/base/BaseSegmentedField.vue'
import BaseSelect, { type SelectOption } from '@/components/base/BaseSelect.vue'
import { useFormat } from '@/composables/useFormat'
import { useSettingsLock } from '@/composables/useSettingsLock'
import { validationMessagesByField } from '@/i18n/validation'
import { useTranscodingStore } from '@/stores/transcoding'

type Field = 'hardwareAcceleration' | 'maxParallelJobs' | 'maxVideoHeight' | 'audioBitrate'

/**
 * Settings of the transcoding: hardware acceleration, parallel transcodes and the quality. Saving
 * sends only the changed values, so settings changed elsewhere since the page was loaded stay as
 * they are.
 */
const emit = defineEmits<{
    saved: []
}>()

const { t } = useI18n()
const format = useFormat()
const store = useTranscodingStore()

const hardware = ref<string>(HardwareAccelerationModeObject.Auto)
const maxParallelJobs = ref('')
const maxVideoHeight = ref('')
const audioBitrate = ref('')
const saving = ref(false)
const saved = ref(false)
const failed = ref(false)
const errors = ref<Partial<Record<Field, string>>>({})
/** Locked until the settings are loaded, so a loaded value never replaces what was typed. */
const locked = useSettingsLock(saving, () => !!store.settings)

function show(settings: TranscodingSettings | null): void {
    if (!settings) {
        return
    }

    hardware.value = settings.hardwareAcceleration ?? HardwareAccelerationModeObject.Auto
    maxParallelJobs.value = String(settings.maxParallelJobs ?? '')
    maxVideoHeight.value = String(settings.maxVideoHeight ?? '')
    audioBitrate.value = String(settings.audioBitrateKbps ?? '')
}

/** The offered values, plus the current one if the server holds another. */
function optionsOf(values: readonly number[], current: string, label: (value: number) => string) {
    const offered: SelectOption[] = values.map((value) => ({
        value: String(value),
        label: label(value),
    }))
    if (current && !offered.some((option) => option.value === current)) {
        offered.push({ value: current, label: label(Number(current)) })
    }

    return offered
}

const hardwareOptions = computed<SegmentOption[]>(() => [
    {
        value: HardwareAccelerationModeObject.Auto,
        item: 'auto',
        label: t('adminTranscoding.hardwareModes.auto'),
    },
    {
        value: HardwareAccelerationModeObject.Off,
        item: 'off',
        label: t('adminTranscoding.hardwareModes.off'),
    },
])
const parallelOptions = computed(() =>
    optionsOf(maxParallelJobOptions, maxParallelJobs.value, (value) => format.number(value)),
)
const heightOptions = computed(() =>
    optionsOf(maxVideoHeightOptions, maxVideoHeight.value, (value) =>
        t('adminTranscoding.height', { height: format.number(value) }),
    ),
)
const bitrateOptions = computed(() =>
    optionsOf(audioBitrateOptions, audioBitrate.value, (value) =>
        t('adminTranscoding.bitrate', { bitrate: format.number(value) }),
    ),
)

async function save(): Promise<void> {
    if (saving.value) {
        return
    }

    saving.value = true
    saved.value = false
    failed.value = false
    errors.value = {}
    try {
        // Only the changed values: an unchanged field must not put back a value that was changed
        // elsewhere since the form was loaded. The answer shows all settings of the server again.
        show(
            await store.updateSettings(
                changedSettings(store.settings, {
                    hardwareAcceleration: hardware.value as HardwareAccelerationMode,
                    maxParallelJobs: Number(maxParallelJobs.value),
                    maxVideoHeight: Number(maxVideoHeight.value),
                    audioBitrateKbps: Number(audioBitrate.value),
                }),
            ),
        )
        saved.value = true
        emit('saved')
    } catch (error) {
        const messages = validationMessagesByField<Field>(
            validationErrorsOf(error),
            {
                hardwareAcceleration: 'hardwareAcceleration',
                maxParallelJobs: 'maxParallelJobs',
                maxVideoHeight: 'maxVideoHeight',
                audioBitrateKbps: 'audioBitrate',
            },
            (key, parameters) => t(key, parameters),
        )
        if (problemCodeOf(error) === 'request.invalid' && Object.keys(messages).length > 0) {
            errors.value = messages
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
        await store.loadSettings()
    }

    show(store.settings)
})
</script>

<template>
    <template v-if="store.settingsStatus === 'error' && !store.settings">
        <BaseAlert tone="error" :message="t('adminTranscoding.settingsLoadError')" />
        <div data-part="view-actions">
            <BaseButton
                data-item="retry"
                :label="t('library.retry')"
                @click="store.loadSettings()"
            />
        </div>
    </template>
    <form
        v-else
        data-part="form"
        :aria-busy="store.settingsStatus === 'loading' && !store.settings ? 'true' : undefined"
        @submit.prevent="save"
    >
        <BaseSegmentedField
            v-model="hardware"
            data-field="hardware-acceleration"
            item="hardware-acceleration"
            :label="t('adminTranscoding.hardwareMode')"
            :options="hardwareOptions"
            :hint="t('adminTranscoding.hardwareModeHint')"
            :error="errors.hardwareAcceleration"
            :disabled="locked"
        />
        <BaseSelect
            v-model="maxParallelJobs"
            :label="t('adminTranscoding.maxParallelJobs')"
            :options="parallelOptions"
            :hint="t('adminTranscoding.maxParallelJobsHint')"
            :error="errors.maxParallelJobs"
            :disabled="locked"
        />
        <BaseSelect
            v-model="maxVideoHeight"
            :label="t('adminTranscoding.maxVideoHeight')"
            :options="heightOptions"
            :hint="t('adminTranscoding.maxVideoHeightHint')"
            :error="errors.maxVideoHeight"
            :disabled="locked"
        />
        <BaseSelect
            v-model="audioBitrate"
            :label="t('adminTranscoding.audioBitrate')"
            :options="bitrateOptions"
            :hint="t('adminTranscoding.audioBitrateHint')"
            :error="errors.audioBitrate"
            :disabled="locked"
        />
        <BaseAlert v-if="saved" tone="success" :message="t('adminTranscoding.saved')" />
        <BaseAlert v-if="failed" tone="error" :message="t('adminTranscoding.saveError')" />
        <div data-part="view-actions">
            <BaseButton
                type="submit"
                variant="primary"
                :label="t('adminTranscoding.save')"
                :disabled="locked"
            />
        </div>
    </form>
</template>
