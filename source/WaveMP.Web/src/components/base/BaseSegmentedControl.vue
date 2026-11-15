<script setup lang="ts">
import { computed, nextTick, ref } from 'vue'
import BaseIcon from './BaseIcon.vue'
import { nextSegmentIndex, segmentTabStop } from './segmented'
import { useIndicator } from '@/composables/useIndicator'
import type { IconSlotName } from '@/themes/iconSlots'

export interface SegmentOption {
    readonly value: string
    readonly label: string
    /** data-item of the segment; the value by default. */
    readonly item?: string
    readonly icon?: IconSlotName
    readonly disabled?: boolean
}

/**
 * Mutually exclusive options as one connected group of buttons (segmented control), accessible as
 * a radio group: one tab stop, the arrow keys check the next option. v-model is the value of the
 * checked option. The group holds an indicator part and tells themes the position of the checked
 * option (--wmp-active-index, --wmp-item-count, --wmp-indicator-x, --wmp-indicator-width), so a
 * theme can slide a highlight between the options with CSS only.
 */
const props = withDefaults(
    defineProps<{
        options: readonly SegmentOption[]
        /** Accessible name of the group, unless labelledby names an element with it. */
        label?: string
        labelledby?: string
        describedby?: string
        /** data-item of the group, e.g. "scope" or "stage-view". */
        item?: string
        disabled?: boolean
        invalid?: boolean
    }>(),
    {
        label: undefined,
        labelledby: undefined,
        describedby: undefined,
        item: undefined,
        disabled: false,
        invalid: false,
    },
)

const value = defineModel<string>({ default: '' })

const group = ref<HTMLElement | null>(null)

const checked = computed(() => props.options.findIndex((option) => option.value === value.value))
const disabledOptions = computed(() =>
    props.options.map((option) => props.disabled || !!option.disabled),
)
const tabStop = computed(() => segmentTabStop(checked.value, disabledOptions.value))
const indicator = useIndicator(group, '[aria-checked="true"]', () => [
    checked.value,
    props.options.length,
])

function choose(option: SegmentOption): void {
    if (!props.disabled && !option.disabled) {
        value.value = option.value
    }
}

async function onKeydown(event: KeyboardEvent): Promise<void> {
    const next = nextSegmentIndex(event.key, checked.value, disabledOptions.value)
    const option = next === null ? undefined : props.options[next]
    if (!option) {
        return
    }

    event.preventDefault()
    choose(option)
    await nextTick()
    group.value?.querySelectorAll<HTMLElement>('[data-part="segment"]')[next ?? 0]?.focus()
}
</script>

<template>
    <div
        ref="group"
        data-part="segmented-control"
        role="radiogroup"
        :aria-label="labelledby ? undefined : label"
        :aria-labelledby="labelledby"
        :aria-describedby="describedby"
        :aria-disabled="disabled ? 'true' : undefined"
        :aria-invalid="invalid ? 'true' : undefined"
        :data-item="item"
        :style="{
            '--wmp-item-count': options.length,
            '--wmp-active-index': checked,
            '--wmp-indicator-x': `${indicator.x}px`,
            '--wmp-indicator-width': `${indicator.width}px`,
        }"
        @keydown="onKeydown"
    >
        <span data-part="indicator" aria-hidden="true"></span>
        <button
            v-for="(option, index) in options"
            :key="option.value"
            type="button"
            data-part="segment"
            data-variant="default"
            role="radio"
            :data-item="option.item ?? option.value"
            :aria-checked="index === checked ? 'true' : 'false'"
            :tabindex="index === tabStop ? 0 : -1"
            :disabled="disabledOptions[index]"
            @click="choose(option)"
        >
            <BaseIcon v-if="option.icon" :name="option.icon" />
            <span data-part="label">{{ option.label }}</span>
        </button>
    </div>
</template>
