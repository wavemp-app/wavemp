<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type {
    LayoutTemplateError,
    LayoutTemplatePath,
    OrganizingSettings,
} from '@/api/generated/models'
import { templateSamples } from '@/admin/organize'
import {
    getOrganizingSettings,
    previewLayoutTemplate,
    updateOrganizingSettings,
} from '@/admin/organizeApi'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseLink from '@/components/base/BaseLink.vue'
import { useSettingsLock } from '@/composables/useSettingsLock'
import TemplateField from './TemplateField.vue'
import TemplatePreviewList from './TemplatePreviewList.vue'

/**
 * Settings of the file layout: the layout template with its live preview. The preview follows
 * the typed text 300 ms after the last change and shows where songs of the library would go, or
 * translated example songs while the library has none. Saving the template moves no file; the
 * page Organize applies it.
 */
const { t } = useI18n()

/** Wait after the last change before the preview is asked for. */
const previewDelayMs = 300

const status = ref<'loading' | 'error' | 'ready'>('loading')
const loaded = ref<OrganizingSettings | null>(null)
const template = ref('')
const saving = ref(false)
const saved = ref(false)
const failed = ref(false)
const errors = ref<LayoutTemplateError[]>([])
const paths = ref<LayoutTemplatePath[]>([])
const previewValid = ref(true)
const previewing = ref(false)
const previewFailed = ref(false)
const folderBased = ref(true)
/** The library had no songs for the preview, so the example songs are shown. */
const useSamples = ref(false)
const locked = useSettingsLock(saving, () => status.value === 'ready')

let timer: ReturnType<typeof setTimeout> | undefined
let request = 0
/** The text of the last preview asked for. */
let requested: string | null = null

const changed = computed(() => template.value.trim() !== (loaded.value?.layoutTemplate ?? ''))

function show(settings: OrganizingSettings | undefined): void {
    loaded.value = settings ?? null
    template.value = settings?.layoutTemplate ?? ''
    folderBased.value = settings?.isFolderBased ?? true
}

async function load(): Promise<void> {
    status.value = 'loading'
    try {
        show(await getOrganizingSettings())
        status.value = 'ready'
        await preview()
    } catch {
        status.value = 'error'
    }
}

/** Asks for the preview of the current text; an older answer that arrives late is ignored. */
async function preview(): Promise<void> {
    const text = template.value
    const own = ++request
    requested = text
    previewing.value = true
    previewFailed.value = false
    try {
        let result = await previewLayoutTemplate({
            template: text,
            ...(useSamples.value ? { samples: templateSamples(t) } : {}),
        })
        if (result?.isValid && (result.paths ?? []).length === 0 && !useSamples.value) {
            useSamples.value = true
            result = await previewLayoutTemplate({ template: text, samples: templateSamples(t) })
        }

        if (own !== request) {
            return
        }

        previewValid.value = !!result?.isValid
        errors.value = result?.errors ?? []
        paths.value = result?.paths ?? []
        if (result?.isValid) {
            folderBased.value = !!result.isFolderBased
        }
    } catch {
        if (own === request) {
            previewFailed.value = true
        }
    } finally {
        if (own === request) {
            previewing.value = false
        }
    }
}

watch(template, () => {
    saved.value = false
    clearTimeout(timer)
    if (status.value !== 'ready' || template.value === requested) {
        return
    }

    timer = setTimeout(() => void preview(), previewDelayMs)
})

async function save(): Promise<void> {
    if (saving.value || !previewValid.value) {
        return
    }

    saved.value = false
    failed.value = false
    saving.value = true
    try {
        show(await updateOrganizingSettings({ layoutTemplate: template.value.trim() }))
        saved.value = true
    } catch {
        failed.value = true
        await preview()
    } finally {
        saving.value = false
    }
}

onMounted(load)
onBeforeUnmount(() => clearTimeout(timer))
</script>

<template>
    <template v-if="status === 'error'">
        <BaseAlert tone="error" :message="t('organize.settings.loadError')" />
        <div data-part="view-actions">
            <BaseButton data-item="retry" :label="t('library.retry')" @click="load" />
        </div>
    </template>
    <form
        v-else
        data-part="form"
        data-item="organizing-settings"
        :aria-busy="status === 'loading' ? 'true' : undefined"
        novalidate
        @submit.prevent="save"
    >
        <TemplateField v-model="template" :errors="errors" :disabled="locked" />
        <p
            data-part="form-note"
            data-item="title-cleaning"
            :data-state="folderBased ? 'folders' : 'flat'"
        >
            {{
                folderBased
                    ? t('organize.settings.titleCleaningFolders')
                    : t('organize.settings.titleCleaningFlat')
            }}
        </p>
        <TemplatePreviewList
            v-if="previewValid && paths.length > 0"
            :paths="paths"
            :samples="useSamples"
            :loading="previewing"
        />
        <BaseAlert
            v-if="previewFailed"
            tone="error"
            :message="t('organize.settings.previewError')"
        />
        <p data-part="form-note" data-item="organize-hint">
            {{ t('organize.settings.moveHint') }}
            <BaseLink :to="{ name: 'admin-organize' }">{{
                t('organize.settings.toOrganize')
            }}</BaseLink>
        </p>
        <BaseAlert v-if="saved" tone="success" :message="t('organize.settings.saved')" />
        <BaseAlert v-if="failed" tone="error" :message="t('organize.settings.saveError')" />
        <div data-part="view-actions">
            <BaseButton
                type="submit"
                variant="primary"
                :label="t('organize.settings.save')"
                :disabled="locked || !changed || !previewValid"
            />
        </div>
    </form>
</template>
