<script setup lang="ts">
import { computed, useId } from 'vue'
import BaseSegmentedControl, { type SegmentOption } from './BaseSegmentedControl.vue'

/**
 * A field of a form whose few, mutually exclusive options show as a segmented control, with
 * label, optional help text and error; v-model is the value of the checked option.
 */
const props = withDefaults(
    defineProps<{
        label: string
        options: readonly SegmentOption[]
        /** data-item of the segmented control. */
        item?: string
        disabled?: boolean
        hint?: string
        error?: string
    }>(),
    { item: undefined, disabled: false, hint: undefined, error: undefined },
)

const value = defineModel<string>({ default: '' })

const labelId = useId()
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
    <div data-part="field" :data-state="error ? 'invalid' : undefined">
        <span :id="labelId" data-part="label">{{ label }}</span>
        <BaseSegmentedControl
            v-model="value"
            :options="options"
            :labelledby="labelId"
            :describedby="describedBy"
            :item="item"
            :disabled="disabled"
            :invalid="!!error"
        />
        <span v-if="hint" :id="hintId" data-part="field-hint">{{ hint }}</span>
        <span v-if="error" :id="errorId" data-part="field-error">{{ error }}</span>
    </div>
</template>
