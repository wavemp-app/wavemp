<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { CoverPriority, LibrarySettings } from '@/api/generated/models'
import { problemCodeOf, validationErrorsOf } from '@/api/problems'
import { customIntervalKey, formatList, intervalOptions, parseList } from '@/admin/presentation'
import { changedSettings } from '@/admin/settingsChanges'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseCheckbox from '@/components/base/BaseCheckbox.vue'
import type { SegmentOption } from '@/components/base/BaseSegmentedControl.vue'
import BaseSegmentedField from '@/components/base/BaseSegmentedField.vue'
import BaseSelect, { type SelectOption } from '@/components/base/BaseSelect.vue'
import BaseTextField from '@/components/base/BaseTextField.vue'
import { useFormat } from '@/composables/useFormat'
import { useSettingsLock } from '@/composables/useSettingsLock'
import {
    coverPriorities,
    coverPriorityOf,
    coverRuleOptionKey,
    coverRuleToken,
} from '@/library/coverRules'
import { validationMessagesByField, type ValidationMessageOptions } from '@/i18n/validation'
import { useCoversStore } from '@/stores/covers'
import { useLibrarySettingsStore } from '@/stores/librarySettings'

type Field = 'interval' | 'audio' | 'video' | 'images' | 'coverNames' | 'coverPriority'

/**
 * Settings of the library: scan interval, file watcher, extension lists, album cover names, the
 * global cover rule, the download of lyrics (off by default, because it sends the titles of the
 * library to an online service) and whether Subsonic apps may download the original files. A
 * changed cover rule loads the covers of albums and songs again. Saving sends only the changed
 * values, so settings changed elsewhere since the page was loaded stay as they are.
 */
const { t } = useI18n()
const format = useFormat()
const store = useLibrarySettingsStore()
const covers = useCoversStore()

const interval = ref('')
const fileWatcher = ref(true)
const audio = ref('')
const video = ref('')
const images = ref('')
const coverNames = ref('')
const coverPriority = ref<string>('AlbumFirst')
const downloadLyrics = ref(false)
const subsonicDownloads = ref(false)
const saving = ref(false)
const saved = ref(false)
const failed = ref(false)
const errors = ref<Partial<Record<Field, string>>>({})
/** Locked until the settings are loaded, so a loaded value never replaces what was typed. */
const locked = useSettingsLock(saving, () => !!store.settings)

const fields: Record<string, Field> = {
    scanIntervalMinutes: 'interval',
    audioExtensions: 'audio',
    videoExtensions: 'video',
    imageExtensions: 'images',
    albumCoverNames: 'coverNames',
    coverPriority: 'coverPriority',
}

const extensionKeys = {
    invalid_format: 'adminLibrary.errors.extensionFormat',
    duplicate: 'adminLibrary.errors.extensionDuplicate',
    required: 'adminLibrary.errors.extensionsRequired',
} as const

/** Texts of the form for single codes; all other codes get the general validation messages. */
const messageKeys: Record<Field, ValidationMessageOptions['keys']> = {
    interval: { out_of_range: 'adminLibrary.errors.interval' },
    audio: extensionKeys,
    video: extensionKeys,
    images: extensionKeys,
    coverNames: { invalid_format: 'adminLibrary.errors.coverNames' },
    coverPriority: {},
}

function show(settings: LibrarySettings | null): void {
    if (!settings) {
        return
    }

    interval.value = String(settings.scanIntervalMinutes ?? 360)
    fileWatcher.value = settings.fileWatcherEnabled ?? true
    audio.value = formatList(settings.audioExtensions)
    video.value = formatList(settings.videoExtensions)
    images.value = formatList(settings.imageExtensions)
    coverNames.value = formatList(settings.albumCoverNames)
    coverPriority.value = coverPriorityOf(settings.coverPriority)
    downloadLyrics.value = settings.downloadLyrics ?? false
    subsonicDownloads.value = settings.subsonicDownloads ?? false
}

const coverPriorityOptions = computed<SegmentOption[]>(() =>
    coverPriorities.map((priority) => ({
        value: priority,
        item: coverRuleToken(priority),
        label: t(coverRuleOptionKey(priority)),
    })),
)

const intervalSelectOptions = computed<SelectOption[]>(() => {
    const options = intervalOptions.map((option) => ({
        value: String(option.minutes),
        label: t(option.labelKey),
    }))
    if (interval.value && !options.some((option) => option.value === interval.value)) {
        options.push({
            value: interval.value,
            label: t(customIntervalKey, { minutes: interval.value }),
        })
    }

    return options
})

async function save(): Promise<void> {
    if (saving.value) {
        return
    }

    saving.value = true
    saved.value = false
    failed.value = false
    errors.value = {}
    const previousPriority = store.settings?.coverPriority
    try {
        // Only the changed values: an unchanged field must not put back a value that was changed
        // elsewhere since the form was loaded. The answer shows all settings of the server again.
        const result = await store.update(
            changedSettings(store.settings, {
                scanIntervalMinutes: Number(interval.value),
                fileWatcherEnabled: fileWatcher.value,
                audioExtensions: parseList(audio.value),
                videoExtensions: parseList(video.value),
                imageExtensions: parseList(images.value),
                albumCoverNames: parseList(coverNames.value),
                coverPriority: coverPriorityOf(coverPriority.value as CoverPriority),
                downloadLyrics: downloadLyrics.value,
                subsonicDownloads: subsonicDownloads.value,
            }),
        )
        if (result && result.coverPriority !== previousPriority) {
            // Albums and songs without a rule of their own may show another cover now.
            covers.invalidate()
        }

        show(result)
        saved.value = true
    } catch (error) {
        const messages = validationMessagesByField(
            validationErrorsOf(error),
            fields,
            (key, parameters) => t(key, parameters),
            (field) => ({ keys: messageKeys[field], formatLimit: format.number }),
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
        await store.load()
    }

    show(store.settings)
})
</script>

<template>
    <template v-if="store.status === 'error' && !store.settings">
        <BaseAlert tone="error" :message="t('adminLibrary.settingsLoadError')" />
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
        <BaseSelect
            v-model="interval"
            :label="t('adminLibrary.interval')"
            :options="intervalSelectOptions"
            :hint="t('adminLibrary.intervalHint')"
            :error="errors.interval"
            :disabled="locked"
        />
        <BaseCheckbox
            v-model="fileWatcher"
            :label="t('adminLibrary.fileWatcher')"
            :hint="t('adminLibrary.fileWatcherHint')"
            :disabled="locked"
        />
        <BaseTextField
            v-model="audio"
            :label="t('adminLibrary.audioExtensions')"
            :hint="t('adminLibrary.listHint')"
            :error="errors.audio"
            :disabled="locked"
        />
        <BaseTextField
            v-model="video"
            :label="t('adminLibrary.videoExtensions')"
            :hint="t('adminLibrary.listHint')"
            :error="errors.video"
            :disabled="locked"
        />
        <BaseTextField
            v-model="images"
            :label="t('adminLibrary.imageExtensions')"
            :hint="t('adminLibrary.listHint')"
            :error="errors.images"
            :disabled="locked"
        />
        <BaseTextField
            v-model="coverNames"
            :label="t('adminLibrary.albumCoverNames')"
            :hint="t('adminLibrary.coverNamesHint')"
            :error="errors.coverNames"
            :disabled="locked"
        />
        <BaseSegmentedField
            v-model="coverPriority"
            data-field="cover-priority"
            item="cover-priority"
            :label="t('adminLibrary.coverPriority')"
            :options="coverPriorityOptions"
            :hint="t('adminLibrary.coverPriorityHint')"
            :error="errors.coverPriority"
            :disabled="locked"
        />
        <BaseCheckbox
            v-model="downloadLyrics"
            data-field="download-lyrics"
            :label="t('adminLibrary.downloadLyrics')"
            :hint="t('adminLibrary.downloadLyricsHint')"
            :disabled="locked"
        />
        <BaseCheckbox
            v-model="subsonicDownloads"
            data-field="subsonic-downloads"
            :label="t('adminLibrary.subsonicDownloads')"
            :hint="t('adminLibrary.subsonicDownloadsHint')"
            :disabled="locked"
        />
        <BaseAlert v-if="saved" tone="success" :message="t('adminLibrary.saved')" />
        <BaseAlert v-if="failed" tone="error" :message="t('adminLibrary.saveError')" />
        <div data-part="view-actions">
            <BaseButton
                type="submit"
                variant="primary"
                :label="t('adminLibrary.save')"
                :disabled="locked"
            />
        </div>
    </form>
</template>
