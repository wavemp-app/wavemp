<script setup lang="ts">
import { computed, nextTick, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { colorTabStop, nextColorIndex } from '@/components/base/colorPicker'
import { indicatorVariables, useIndicator } from '@/composables/useIndicator'
import {
    hasVariants,
    resolveThemeVariant,
    themePreview,
    type ThemeAppearance,
    type ThemeDefinition,
} from '@/themes/themes'

/**
 * Choice of the theme (radio group of preview tiles); v-model is the id of the chosen theme. Every
 * tile shows a picture of its theme in the variant the appearance would show, its name and its
 * variants. Like the other radio groups it holds an indicator part and tells themes the position of
 * the chosen tile (--wmp-active-index, --wmp-item-count, --wmp-indicator-x, -y, -width, -height).
 */
const props = withDefaults(
    defineProps<{
        themes: readonly ThemeDefinition[]
        /** Accessible name of the group, unless labelledby names an element with it. */
        label?: string
        labelledby?: string
        describedby?: string
        appearance: ThemeAppearance
        /** Whether the device is in dark mode, for the appearance "system". */
        systemDark: boolean
        disabled?: boolean
    }>(),
    { label: undefined, labelledby: undefined, describedby: undefined, disabled: false },
)

const value = defineModel<string>({ default: '' })

const { t } = useI18n()
const picker = ref<HTMLElement | null>(null)
/** Previews that could not be loaded; their tiles show the name only. */
const missing = ref<ReadonlySet<string>>(new Set())

const selected = computed(() => props.themes.findIndex((theme) => theme.id === value.value))
const tabStop = computed(() => colorTabStop(selected.value))
const indicator = useIndicator(picker, '[aria-checked="true"]', () => [
    selected.value,
    props.themes.length,
])

const tiles = computed(() =>
    props.themes.map((theme) => {
        const variant = resolveThemeVariant(theme, props.appearance, props.systemDark)
        return {
            theme,
            variant,
            preview: themePreview(theme, variant),
            variants: theme.variants.join(' '),
            variantsLabel: hasVariants(theme)
                ? t('settings.themeVariants.both')
                : t(`settings.themeVariants.${theme.variants[0]}`),
        }
    }),
)

function choose(id: string): void {
    if (!props.disabled) {
        value.value = id
    }
}

function previewFailed(preview: string): void {
    missing.value = new Set([...missing.value, preview])
}

async function onKeydown(event: KeyboardEvent): Promise<void> {
    if (props.disabled) {
        return
    }

    const next = nextColorIndex(event.key, selected.value, props.themes.length)
    const theme = next === null ? undefined : props.themes[next]
    if (!theme) {
        return
    }

    event.preventDefault()
    value.value = theme.id
    await nextTick()
    picker.value?.querySelectorAll<HTMLElement>('[data-part="theme-option"]')[next ?? 0]?.focus()
}
</script>

<template>
    <div
        ref="picker"
        data-part="theme-picker"
        role="radiogroup"
        :aria-label="labelledby ? undefined : label"
        :aria-labelledby="labelledby"
        :aria-describedby="describedby"
        :aria-disabled="disabled ? 'true' : undefined"
        :style="indicatorVariables(themes.length, selected, indicator)"
        @keydown="onKeydown"
    >
        <span data-part="indicator" aria-hidden="true"></span>
        <button
            v-for="(tile, index) in tiles"
            :key="tile.theme.id"
            type="button"
            data-part="theme-option"
            role="radio"
            :data-item="tile.theme.id"
            :data-variants="tile.variants"
            :aria-checked="index === selected ? 'true' : 'false'"
            :aria-disabled="disabled ? 'true' : undefined"
            :tabindex="index === tabStop ? 0 : -1"
            @click="choose(tile.theme.id)"
        >
            <span
                data-part="theme-preview"
                :data-kind="tile.variant"
                :data-state="missing.has(tile.preview) ? 'missing' : undefined"
            >
                <img
                    v-if="!missing.has(tile.preview)"
                    data-part="theme-preview-image"
                    :src="tile.preview"
                    alt=""
                    draggable="false"
                    loading="lazy"
                    @error="previewFailed(tile.preview)"
                />
            </span>
            <span data-part="label">{{ tile.theme.name }}</span>
            <span data-part="theme-variants">{{ tile.variantsLabel }}</span>
        </button>
    </div>
</template>
