<script setup lang="ts">
import { computed, nextTick, ref, useId } from 'vue'
import { useI18n } from 'vue-i18n'
import type { LayoutTemplateError } from '@/api/generated/models'
import {
    insertToken,
    presetOf,
    templateErrorKey,
    templateMaxLength,
    templatePlaceholders,
    templatePresets,
    type TemplatePreset,
} from '@/admin/organize'
import BaseSegmentedControl from '@/components/base/BaseSegmentedControl.vue'
import BaseTextField from '@/components/base/BaseTextField.vue'

/**
 * Editor of the layout template: the presets Folders and Flat as a segmented control (Custom for
 * any other text), the text field that holds the template, and a row of placeholder tokens that
 * insert their placeholder at the cursor. The text stays the source of truth; the problems the
 * server found are shown under the field with their position.
 */
const props = withDefaults(
    defineProps<{
        errors?: readonly LayoutTemplateError[]
        disabled?: boolean
    }>(),
    { errors: () => [], disabled: false },
)

const template = defineModel<string>({ default: '' })

const { t } = useI18n()
/** The placeholder the file name needs; named in the hint and in the error missing_title. */
const titleToken = '{Title}'
const root = ref<HTMLElement | null>(null)
const presetsLabelId = useId()
/** Custom was chosen while the text still equals a preset; typing keeps it. */
const customChosen = ref(false)

const preset = computed<TemplatePreset>({
    get: () => (customChosen.value ? 'custom' : presetOf(template.value)),
    set: (value) => {
        if (value === 'custom') {
            customChosen.value = true
            void focusInput()
            return
        }

        customChosen.value = false
        template.value = templatePresets[value]
    },
})

const presetOptions = computed(() => [
    { value: 'folders', label: t('organize.template.presets.folders') },
    { value: 'flat', label: t('organize.template.presets.flat') },
    { value: 'custom', label: t('organize.template.presets.custom') },
])

const error = computed(() => {
    const first = props.errors[0]
    if (!first) {
        return undefined
    }

    return t(templateErrorKey(first.code), {
        placeholder: first.placeholder ?? '',
        max: first.max ?? templateMaxLength,
        position: (first.position ?? 0) + 1,
        title: titleToken,
    })
})

function input(): HTMLInputElement | null {
    return root.value?.querySelector<HTMLInputElement>('input[data-part="input"]') ?? null
}

async function focusInput(cursor?: number): Promise<void> {
    await nextTick()
    const element = input()
    element?.focus()
    if (element && cursor !== undefined) {
        element.setSelectionRange(cursor, cursor)
    }
}

function insert(token: string): void {
    if (props.disabled) {
        return
    }

    const element = input()
    const result = insertToken(
        template.value,
        element?.selectionStart,
        element?.selectionEnd,
        token,
    )
    if (result.text.length > templateMaxLength) {
        return
    }

    template.value = result.text
    void focusInput(result.cursor)
}
</script>

<template>
    <div ref="root" data-part="template-field" :data-state="preset">
        <div data-part="template-presets">
            <span :id="presetsLabelId" data-part="label">
                {{ t('organize.template.presetsLabel') }}
            </span>
            <BaseSegmentedControl
                v-model="preset"
                item="template-preset"
                :options="presetOptions"
                :labelledby="presetsLabelId"
                :disabled="disabled"
            />
        </div>
        <BaseTextField
            v-model="template"
            data-field="layout-template"
            :label="t('organize.template.label')"
            :hint="t('organize.template.hint', { title: titleToken })"
            :error="error"
            :maxlength="templateMaxLength"
            :disabled="disabled"
            autocomplete="off"
        />
        <div data-part="template-tokens" role="group" :aria-label="t('organize.template.tokens')">
            <button
                v-for="placeholder in templatePlaceholders"
                :key="placeholder.name"
                type="button"
                data-part="template-token"
                :data-item="placeholder.name"
                :disabled="disabled"
                :aria-label="
                    t('organize.template.insert', {
                        placeholder: t(`organize.placeholders.${placeholder.name}`),
                    })
                "
                :title="placeholder.token"
                @mousedown.prevent
                @click="insert(placeholder.token)"
            >
                <span data-part="label" data-field="name">
                    {{ t(`organize.placeholders.${placeholder.name}`) }}
                </span>
                <span data-part="label" data-field="token">{{ placeholder.token }}</span>
            </button>
        </div>
    </div>
</template>
