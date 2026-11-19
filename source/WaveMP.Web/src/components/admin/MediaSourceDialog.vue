<script setup lang="ts">
import { computed, ref, useId, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { ProblemCodeObject, type MediaSource, type UpdateMediaSource } from '@/api/generated/models'
import { problemCodeOf, statusOf, validationErrorsOf } from '@/api/problems'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseCheckbox from '@/components/base/BaseCheckbox.vue'
import BaseDialog from '@/components/base/BaseDialog.vue'
import BaseTextField from '@/components/base/BaseTextField.vue'
import { validationMessagesByField } from '@/i18n/validation'
import { useMediaSourcesStore } from '@/stores/mediaSources'

/**
 * Adds a media folder or edits one: name, folder in the container and whether it is scanned and
 * watched. Only changed values are sent. The server checks the folder (absolute, existing, apart
 * from the other folders) and scans it after it was added, switched on or moved.
 */
const props = defineProps<{
    source: MediaSource | null
}>()

const emit = defineEmits<{
    saved: [source: MediaSource]
}>()

const open = defineModel<boolean>('open', { default: false })

const { t } = useI18n()
const sources = useMediaSourcesStore()
const formId = useId()

const name = ref('')
const path = ref('')
const active = ref(true)
const nameError = ref<string | undefined>(undefined)
const pathError = ref<string | undefined>(undefined)
const error = ref<string | null>(null)
const saving = ref(false)

const editing = computed(() => props.source !== null)

function reset(): void {
    name.value = props.source?.name ?? ''
    path.value = props.source?.path ?? ''
    active.value = props.source?.isActive ?? true
    nameError.value = undefined
    pathError.value = undefined
    error.value = null
}

watch(open, (value) => {
    if (value) {
        reset()
    }
})

function changes(source: MediaSource): UpdateMediaSource {
    const update: UpdateMediaSource = {}
    if (name.value.trim() !== source.name) {
        update.name = name.value.trim()
    }
    if (path.value.trim() !== source.path) {
        update.path = path.value.trim()
    }
    if (active.value !== source.isActive) {
        update.isActive = active.value
    }
    return update
}

function report(failure: unknown): void {
    switch (problemCodeOf(failure)) {
        case ProblemCodeObject.SourceName_taken:
            nameError.value = t('adminSources.errors.nameTaken')
            return
        case ProblemCodeObject.SourcePath_not_found:
            pathError.value = t('adminSources.errors.pathNotFound')
            return
        case ProblemCodeObject.SourcePath_overlaps:
            pathError.value = t('adminSources.errors.pathOverlaps')
            return
        case ProblemCodeObject.ScanAlready_running:
            error.value = t('adminSources.errors.scanRunning')
            return
        case ProblemCodeObject.SourceNot_found:
            error.value = t('adminSources.errors.notFound')
            return
    }

    if (statusOf(failure) === 400) {
        const messages = validationMessagesByField(
            validationErrorsOf(failure),
            { name: 'name', path: 'path' } as const,
            (key, parameters) => t(key, parameters),
        )
        nameError.value = messages.name
        pathError.value = messages.path && t('adminSources.errors.pathInvalid')
        error.value = t('adminSources.errors.invalid')
    } else {
        error.value = t('adminSources.errors.saveFailed')
    }
}

async function save(): Promise<void> {
    if (saving.value) {
        return
    }

    nameError.value = undefined
    pathError.value = undefined
    error.value = null
    if (name.value.trim() === '') {
        nameError.value = t('adminSources.errors.nameRequired')
    }
    if (path.value.trim() === '') {
        pathError.value = t('adminSources.errors.pathRequired')
    }
    if (nameError.value || pathError.value) {
        return
    }

    saving.value = true
    try {
        let saved: MediaSource | undefined
        if (props.source?.id) {
            const update = changes(props.source)
            if (Object.keys(update).length === 0) {
                open.value = false
                return
            }
            saved = await sources.update(props.source.id, update)
        } else {
            saved = await sources.create({
                name: name.value.trim(),
                path: path.value.trim(),
                isActive: active.value,
            })
        }

        open.value = false
        if (saved) {
            emit('saved', saved)
        }
    } catch (failure) {
        report(failure)
    } finally {
        saving.value = false
    }
}
</script>

<template>
    <BaseDialog
        v-model:open="open"
        :title="editing ? t('adminSources.dialog.editTitle') : t('adminSources.dialog.createTitle')"
    >
        <form :id="formId" data-part="form" novalidate @submit.prevent="save">
            <BaseAlert v-if="error" tone="error" :message="error" />
            <BaseTextField
                v-model="name"
                :label="t('adminSources.dialog.name')"
                :hint="t('adminSources.dialog.nameHint')"
                :error="nameError"
                :maxlength="100"
                autocomplete="off"
                required
            />
            <BaseTextField
                v-model="path"
                :label="t('adminSources.dialog.path')"
                :hint="
                    source?.followsMediaPath
                        ? t('adminSources.dialog.pathMediaPathHint')
                        : t('adminSources.dialog.pathHint')
                "
                :error="pathError"
                :maxlength="1000"
                autocomplete="off"
                spellcheck="false"
                translate="no"
                required
            />
            <BaseCheckbox
                v-model="active"
                :label="t('adminSources.dialog.active')"
                :hint="t('adminSources.dialog.activeHint')"
            />
        </form>
        <template #footer>
            <BaseButton :label="t('actions.cancel')" @click="open = false" />
            <BaseButton
                type="submit"
                variant="primary"
                :form="formId"
                :label="editing ? t('adminSources.dialog.save') : t('adminSources.dialog.add')"
                :disabled="saving"
            />
        </template>
    </BaseDialog>
</template>
