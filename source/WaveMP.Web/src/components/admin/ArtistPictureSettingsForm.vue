<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { ProblemCodeObject, type ArtistPictureSettings } from '@/api/generated/models'
import { problemCodeOf, validationErrorsOf } from '@/api/problems'
import { changedSettings } from '@/admin/settingsChanges'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseCheckbox from '@/components/base/BaseCheckbox.vue'
import BaseTextField from '@/components/base/BaseTextField.vue'
import { useSettingsLock } from '@/composables/useSettingsLock'
import { validationMessagesByField } from '@/i18n/validation'
import { getArtistPictureSettings, updateArtistPictureSettings } from '@/library/artistPictureApi'

/**
 * Settings of the online lookup of artist pictures: the switch (off by default, then WaveMP sends
 * no request for artist pictures to any service) and the optional personal key of fanart.tv. The
 * key can only be written: the form never shows it, it only tells whether one is saved, and can
 * remove it. A note says what the lookup sends where. Saving sends only the changed values.
 */
type Field = 'fanartTvApiKey'

/** The format of a fanart.tv key the API accepts (UpdateArtistPictureSettings). */
const keyFormat = /^[A-Za-z0-9]{1,64}$/

const { t } = useI18n()

const status = ref<'loading' | 'error' | 'ready'>('loading')
/** The settings the form shows as the server sent them; the base of the changes to save. */
const loaded = ref<ArtistPictureSettings | null>(null)
const enabled = ref(false)
const key = ref('')
const saving = ref(false)
const saved = ref<'saved' | 'keyRemoved' | null>(null)
const failed = ref(false)
const invalid = ref<Partial<Record<Field, string>>>({})
/** Locked until the settings are loaded, so a loaded value never replaces what was typed. */
const locked = useSettingsLock(saving, () => status.value === 'ready')

function show(settings: ArtistPictureSettings | undefined): void {
    loaded.value = settings ?? null
    enabled.value = settings?.onlineLookupEnabled ?? false
    key.value = ''
}

async function load(): Promise<void> {
    status.value = 'loading'
    try {
        show(await getArtistPictureSettings())
        status.value = 'ready'
    } catch {
        status.value = 'error'
    }
}

async function send(
    changes: { onlineLookupEnabled?: boolean; fanartTvApiKey?: string },
    done: 'saved' | 'keyRemoved',
): Promise<void> {
    saving.value = true
    try {
        show(await updateArtistPictureSettings(changes))
        saved.value = done
    } catch (error) {
        const messages = validationMessagesByField<Field>(
            validationErrorsOf(error),
            { fanartTvApiKey: 'fanartTvApiKey' },
            (messageKey, parameters) => t(messageKey, parameters),
            () => ({
                keys: {
                    invalid_format: 'artistPictureSettings.errors.key',
                    max_length: 'artistPictureSettings.errors.key',
                },
            }),
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

async function save(): Promise<void> {
    if (saving.value) {
        return
    }

    saved.value = null
    failed.value = false
    invalid.value = {}
    const typed = key.value.trim()
    if (typed !== '' && !keyFormat.test(typed)) {
        invalid.value = { fanartTvApiKey: t('artistPictureSettings.errors.key') }
        return
    }

    // Only the changed values; a key is only sent when one was typed.
    const changes: { onlineLookupEnabled?: boolean; fanartTvApiKey?: string } = changedSettings(
        loaded.value,
        { onlineLookupEnabled: enabled.value },
    )
    if (typed !== '') {
        changes.fanartTvApiKey = typed
    }

    await send(changes, 'saved')
}

async function removeKey(): Promise<void> {
    if (saving.value) {
        return
    }

    saved.value = null
    failed.value = false
    invalid.value = {}
    await send({ fanartTvApiKey: '' }, 'keyRemoved')
}

onMounted(load)
</script>

<template>
    <template v-if="status === 'error'">
        <BaseAlert tone="error" :message="t('artistPictureSettings.loadError')" />
        <div data-part="view-actions">
            <BaseButton data-item="retry" :label="t('library.retry')" @click="load" />
        </div>
    </template>
    <form
        v-else
        data-part="form"
        data-item="artist-picture-settings"
        :aria-busy="status === 'loading' ? 'true' : undefined"
        novalidate
        @submit.prevent="save"
    >
        <BaseCheckbox
            v-model="enabled"
            data-field="artist-pictures-online"
            :label="t('artistPictureSettings.enabled')"
            :hint="t('artistPictureSettings.enabledHint')"
            :disabled="locked"
        />
        <BaseTextField
            v-model="key"
            data-field="fanart-tv-key"
            type="password"
            :label="t('artistPictureSettings.key')"
            :placeholder="
                loaded?.fanartTvApiKeySet ? t('artistPictureSettings.keySaved') : undefined
            "
            :hint="t('artistPictureSettings.keyHint')"
            :error="invalid.fanartTvApiKey"
            :maxlength="64"
            :disabled="locked"
            autocomplete="new-password"
        />
        <p
            data-part="form-note"
            data-item="fanart-tv-key-state"
            :data-state="loaded?.fanartTvApiKeySet ? 'saved' : 'missing'"
        >
            {{
                loaded?.fanartTvApiKeySet
                    ? t('artistPictureSettings.keyState.saved')
                    : t('artistPictureSettings.keyState.missing')
            }}
        </p>
        <p data-part="form-note" data-item="artist-pictures-privacy">
            {{ t('artistPictureSettings.privacy') }}
        </p>
        <BaseAlert
            v-if="saved"
            tone="success"
            :message="
                saved === 'keyRemoved'
                    ? t('artistPictureSettings.keyRemoved')
                    : t('artistPictureSettings.saved')
            "
        />
        <BaseAlert v-if="failed" tone="error" :message="t('artistPictureSettings.saveError')" />
        <div data-part="view-actions">
            <BaseButton
                v-if="loaded?.fanartTvApiKeySet"
                data-item="remove-fanart-tv-key"
                icon="delete"
                :label="t('artistPictureSettings.removeKey')"
                :disabled="locked"
                @click="removeKey"
            />
            <BaseButton
                type="submit"
                variant="primary"
                :label="t('artistPictureSettings.save')"
                :disabled="locked"
            />
        </div>
    </form>
</template>
