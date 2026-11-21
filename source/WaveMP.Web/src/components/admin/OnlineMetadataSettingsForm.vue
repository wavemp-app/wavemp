<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { ProblemCodeObject, type OnlineMetadataSettings } from '@/api/generated/models'
import { problemCodeOf, validationErrorsOf } from '@/api/problems'
import { changedSettings } from '@/admin/settingsChanges'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseCheckbox from '@/components/base/BaseCheckbox.vue'
import BaseTextField from '@/components/base/BaseTextField.vue'
import { validationMessagesByField } from '@/i18n/validation'
import { getOnlineMetadataSettings, updateOnlineMetadataSettings } from '@/library/onlineMetadata'

/**
 * Settings of the online search for album info: the switch (off by default, then WaveMP sends no
 * request to MusicBrainz or the Cover Art Archive), the addresses of both services (own mirrors
 * allowed) and the pause between two requests to MusicBrainz. Saving sends only the changed values,
 * so settings changed elsewhere since the page was loaded stay as they are.
 */
type Field = 'musicBrainzUrl' | 'coverArtArchiveUrl' | 'interval'

/** Limits of the API (UpdateOnlineMetadataSettings). */
const intervalLimits = { min: 0, max: 60_000 } as const

const { t } = useI18n()

const status = ref<'loading' | 'error' | 'ready'>('loading')
/** The settings the form shows as the server sent them; the base of the changes to save. */
const loaded = ref<OnlineMetadataSettings | null>(null)
const enabled = ref(false)
const musicBrainzUrl = ref('')
const coverArtArchiveUrl = ref('')
const interval = ref('')
const saving = ref(false)
const saved = ref(false)
const failed = ref(false)
const invalid = ref<Partial<Record<Field, string>>>({})

function show(settings: OnlineMetadataSettings | undefined): void {
    loaded.value = settings ?? null
    enabled.value = settings?.enabled ?? false
    musicBrainzUrl.value = settings?.musicBrainzUrl ?? ''
    coverArtArchiveUrl.value = settings?.coverArtArchiveUrl ?? ''
    interval.value = String(settings?.musicBrainzRequestIntervalMilliseconds ?? 1000)
}

async function load(): Promise<void> {
    status.value = 'loading'
    try {
        show(await getOnlineMetadataSettings())
        status.value = 'ready'
    } catch {
        status.value = 'error'
    }
}

/** The interval as whole number within the limits, otherwise null. */
function intervalValue(): number | null {
    const text = interval.value.trim()
    if (!/^\d+$/.test(text)) {
        return null
    }
    const value = Number(text)
    return value >= intervalLimits.min && value <= intervalLimits.max ? value : null
}

async function save(): Promise<void> {
    if (saving.value) {
        return
    }

    saved.value = false
    failed.value = false
    invalid.value = {}
    const milliseconds = intervalValue()
    if (milliseconds === null) {
        invalid.value = { interval: t('onlineMetadataSettings.errors.interval', intervalLimits) }
        return
    }

    saving.value = true
    try {
        // Only the changed values: an unchanged field must not put back a value that was changed
        // elsewhere since the form was loaded. The answer shows all settings of the server again.
        show(
            await updateOnlineMetadataSettings(
                changedSettings(loaded.value, {
                    enabled: enabled.value,
                    musicBrainzUrl: musicBrainzUrl.value.trim(),
                    coverArtArchiveUrl: coverArtArchiveUrl.value.trim(),
                    musicBrainzRequestIntervalMilliseconds: milliseconds,
                }),
            ),
        )
        saved.value = true
    } catch (error) {
        const messages = validationMessagesByField<Field>(
            validationErrorsOf(error),
            {
                musicBrainzUrl: 'musicBrainzUrl',
                coverArtArchiveUrl: 'coverArtArchiveUrl',
                musicBrainzRequestIntervalMilliseconds: 'interval',
            },
            (key, parameters) => t(key, parameters),
            (field) =>
                field === 'interval'
                    ? { keys: { out_of_range: 'onlineMetadataSettings.errors.interval' } }
                    : {
                          keys: {
                              invalid_format: 'onlineMetadataSettings.errors.url',
                              max_length: 'onlineMetadataSettings.errors.url',
                          },
                      },
        )
        if (
            problemCodeOf(error) === ProblemCodeObject.RequestInvalid &&
            Object.keys(messages).length > 0
        ) {
            invalid.value = messages
        } else {
            failed.value = true
        }
    } finally {
        saving.value = false
    }
}

onMounted(load)
</script>

<template>
    <template v-if="status === 'error'">
        <BaseAlert tone="error" :message="t('onlineMetadataSettings.loadError')" />
        <div data-part="view-actions">
            <BaseButton data-item="retry" :label="t('library.retry')" @click="load" />
        </div>
    </template>
    <form
        v-else
        data-part="form"
        data-item="online-metadata-settings"
        :aria-busy="status === 'loading' ? 'true' : undefined"
        novalidate
        @submit.prevent="save"
    >
        <BaseCheckbox
            v-model="enabled"
            data-field="online-metadata-enabled"
            :label="t('onlineMetadataSettings.enabled')"
            :hint="t('onlineMetadataSettings.enabledHint')"
            :disabled="saving || status !== 'ready'"
        />
        <BaseTextField
            v-model="musicBrainzUrl"
            data-field="musicbrainz-url"
            type="url"
            :label="t('onlineMetadataSettings.musicBrainzUrl')"
            :hint="t('onlineMetadataSettings.musicBrainzUrlHint')"
            :error="invalid.musicBrainzUrl"
            :maxlength="2048"
            :disabled="saving || status !== 'ready'"
            autocomplete="off"
        />
        <BaseTextField
            v-model="coverArtArchiveUrl"
            data-field="cover-art-archive-url"
            type="url"
            :label="t('onlineMetadataSettings.coverArtArchiveUrl')"
            :error="invalid.coverArtArchiveUrl"
            :maxlength="2048"
            :disabled="saving || status !== 'ready'"
            autocomplete="off"
        />
        <BaseTextField
            v-model="interval"
            data-field="musicbrainz-interval"
            inputmode="numeric"
            :label="t('onlineMetadataSettings.interval')"
            :hint="t('onlineMetadataSettings.intervalHint')"
            :error="invalid.interval"
            :maxlength="5"
            :disabled="saving || status !== 'ready'"
            autocomplete="off"
        />
        <BaseAlert v-if="saved" tone="success" :message="t('onlineMetadataSettings.saved')" />
        <BaseAlert v-if="failed" tone="error" :message="t('onlineMetadataSettings.saveError')" />
        <div data-part="view-actions">
            <BaseButton
                type="submit"
                variant="primary"
                :label="t('onlineMetadataSettings.save')"
                :disabled="saving || status !== 'ready'"
            />
        </div>
    </form>
</template>
