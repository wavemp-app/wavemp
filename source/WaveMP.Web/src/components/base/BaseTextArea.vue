<script setup lang="ts">
import { computed, useId } from 'vue'

/**
 * Text field over several lines: the same parts as a text field, the input as textarea with
 * data-variant="multiline".
 */
const props = withDefaults(
    defineProps<{
        label: string
        placeholder?: string
        disabled?: boolean
        required?: boolean
        maxlength?: number
        /** Visible lines before it scrolls. */
        rows?: number
        /** Help text below the input. */
        hint?: string
        /** Error message; marks the field as invalid. */
        error?: string
    }>(),
    {
        placeholder: undefined,
        disabled: false,
        required: false,
        maxlength: undefined,
        rows: 8,
        hint: undefined,
        error: undefined,
    },
)

const value = defineModel<string>({ default: '' })

const hintId = useId()
const errorId = useId()
const describedBy = computed(
    () =>
        [props.error ? errorId : undefined, props.hint ? hintId : undefined]
            .filter(Boolean)
            .join(' ') || undefined,
)
</script>

<template>
    <label data-part="field" data-variant="multiline" :data-state="error ? 'invalid' : undefined">
        <span data-part="label">{{ label }}</span>
        <textarea
            v-model="value"
            data-part="input"
            data-variant="multiline"
            :placeholder="placeholder"
            :disabled="disabled"
            :required="required"
            :maxlength="maxlength"
            :rows="rows"
            spellcheck="false"
            :aria-invalid="error ? 'true' : undefined"
            :aria-describedby="describedBy"
        ></textarea>
        <span v-if="hint" :id="hintId" data-part="field-hint">{{ hint }}</span>
        <span v-if="error" :id="errorId" data-part="field-error">{{ error }}</span>
    </label>
</template>
