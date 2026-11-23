<script setup lang="ts">
import { computed, nextTick, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseContextMenu, { type MenuItem } from '@/components/base/BaseContextMenu.vue'
import BaseSegmentedControl, {
    type SegmentOption,
} from '@/components/base/BaseSegmentedControl.vue'
import { useMediaQuery } from '@/composables/useMediaQuery'
import { mobileMediaQuery } from '@/themes/breakpoints'
import { visualizationModuleChoices } from '@/visualization/modules'
import { adjacentPreset } from '@/visualization/presets'
import type { VisualizationSettings } from '@/visualization/settings'
import { presetMenuPlacement, type PresetChoice } from './visualizationControls'

/**
 * The controls of the visualization on the stage of Now Playing, as a toolbar below the picture,
 * so they never cover it: the previous preset, the preset list, the next preset, the automatic
 * change and full screen, the color schemes of modules that have them and the choice of the module.
 * The buttons keep their places whatever the preset is called; on a narrow stage the module choice
 * and the color schemes take a row of their own below them. The preset list and the color schemes
 * open above their button on the desktop (towards the picture, the bottom of the page is near) and
 * as a sheet at the bottom edge of the screen on phones; the menus lie beside the toolbar, so they
 * inherit nothing from the look of the toolbar. Controlled through props and events only;
 * StageVisualization draws and owns the presets and the color schemes.
 */
const props = withDefaults(
    defineProps<{
        /** The presets of the module, in their order, with their labels. */
        presets?: readonly PresetChoice[]
        /** Id of the shown preset; empty before one is shown. */
        preset?: string
        /** The color schemes of the module, in their order; empty for modules without them. */
        colorSchemes?: readonly PresetChoice[]
        /** Id of the shown color scheme; empty for modules without color schemes. */
        colorScheme?: string
        /** The visualization settings of the profile. */
        settings: VisualizationSettings
        /** Whether the visualization is shown in full screen. */
        fullscreen?: boolean
        /** Whether the browser can show the visualization in full screen. */
        fullscreenAvailable?: boolean
        /** Whether the visualization is shown, so it may go to full screen. */
        active?: boolean
    }>(),
    {
        presets: () => [],
        preset: '',
        colorSchemes: () => [],
        colorScheme: '',
        fullscreen: false,
        fullscreenAvailable: false,
        active: true,
    },
)

const emit = defineEmits<{
    /** A preset was chosen: from the list or with the previous and next buttons. */
    selectPreset: [id: string]
    /** A color scheme was chosen from its list. */
    selectColorScheme: [id: string]
    /** Another module was chosen; the change for the profile. */
    selectModule: [module: VisualizationSettings['module']]
    toggleAutoChange: []
    toggleFullscreen: []
}>()

const { t } = useI18n()
const phone = useMediaQuery(mobileMediaQuery)

const picker = ref<InstanceType<typeof BaseButton> | null>(null)
const menu = ref<InstanceType<typeof BaseContextMenu> | null>(null)
const open = computed(() => menu.value?.isOpen ?? false)

const schemePicker = ref<InstanceType<typeof BaseButton> | null>(null)
const schemeMenu = ref<InstanceType<typeof BaseContextMenu> | null>(null)
const schemesOpen = computed(() => schemeMenu.value?.isOpen ?? false)

const ready = computed(() => props.presets.length > 0)

const hasColorSchemes = computed(() => props.colorSchemes.length > 0)

const stateTokens = computed(() =>
    [ready.value ? 'ready' : 'loading', hasColorSchemes.value ? 'has-color-schemes' : undefined]
        .filter(Boolean)
        .join(' '),
)

const current = computed(() => props.presets.find((choice) => choice.id === props.preset))

const presetLabel = computed(() => current.value?.label ?? t('nowPlaying.visualizationPreset'))

const colorSchemeLabel = computed(
    () =>
        props.colorSchemes.find((choice) => choice.id === props.colorScheme)?.label ??
        t('nowPlaying.colorScheme'),
)

const menuItems = computed<MenuItem[]>(() =>
    props.presets.map((choice) => ({
        id: choice.id,
        label: choice.label,
        checked: choice.id === props.preset,
    })),
)

const schemeItems = computed<MenuItem[]>(() =>
    props.colorSchemes.map((choice) => ({
        id: choice.id,
        label: choice.label,
        checked: choice.id === props.colorScheme,
    })),
)

const moduleOptions = computed<SegmentOption[]>(() =>
    visualizationModuleChoices.map((choice) => ({
        value: choice.id,
        item: choice.id.toLowerCase(),
        label: t(choice.labelKey),
    })),
)

function buttonElement(button: InstanceType<typeof BaseButton> | null): HTMLElement | null {
    const element = (button as { $el?: unknown } | null)?.$el
    return element instanceof HTMLElement ? element : null
}

/** Opens a list above its button (or as a sheet on phones), or closes it when it is open. */
async function toggleList(
    list: InstanceType<typeof BaseContextMenu> | null,
    button: InstanceType<typeof BaseButton> | null,
    available: boolean,
): Promise<void> {
    if (list?.isOpen) {
        list.close()
        return
    }

    const element = buttonElement(button)
    if (!element || !available) {
        return
    }

    const placement = presetMenuPlacement(phone.value, element.getBoundingClientRect())
    if (placement.scrollIntoView) {
        // Too little room above the button: the page brings it down first, the list still opens
        // above it.
        element.scrollIntoView?.({ block: 'end', inline: 'nearest' })
        await nextTick()
    }

    await list?.openAnchored(element.getBoundingClientRect(), placement.placement)
}

function togglePresets(): Promise<void> {
    return toggleList(menu.value, picker.value, ready.value)
}

function toggleColorSchemes(): Promise<void> {
    return toggleList(schemeMenu.value, schemePicker.value, hasColorSchemes.value)
}

function selectPreset(id: string): void {
    if (props.presets.some((choice) => choice.id === id) && id !== props.preset) {
        emit('selectPreset', id)
    }
}

function selectColorScheme(id: string): void {
    if (props.colorSchemes.some((choice) => choice.id === id) && id !== props.colorScheme) {
        emit('selectColorScheme', id)
    }
}

function step(offset: -1 | 1): void {
    const next = adjacentPreset(props.presets, props.preset || null, offset)
    if (next) {
        emit('selectPreset', next.id)
    }
}

function selectModule(value: string): void {
    const choice = visualizationModuleChoices.find((item) => item.id === value)
    if (choice && choice.id !== props.settings.module) {
        emit('selectModule', choice.id)
    }
}
</script>

<template>
    <div
        data-part="visualization-controls"
        role="group"
        :aria-label="t('nowPlaying.visualizationControls')"
        :data-state="stateTokens"
    >
        <BaseButton
            part="btn-preset-previous"
            variant="icon"
            icon="preset-previous"
            :label="t('nowPlaying.previousPreset')"
            :disabled="!ready"
            @click="step(-1)"
        />
        <BaseButton
            ref="picker"
            part="btn-preset-picker"
            :label="presetLabel"
            :title="t('nowPlaying.visualizationPreset')"
            :aria-label="t('nowPlaying.visualizationPresetChoice', { name: presetLabel })"
            dropdown
            aria-haspopup="menu"
            :aria-expanded="open ? 'true' : 'false'"
            :state="open ? 'open' : 'closed'"
            :disabled="!ready"
            @click="togglePresets"
        />
        <BaseButton
            part="btn-preset-next"
            variant="icon"
            icon="preset-next"
            :label="t('nowPlaying.nextPreset')"
            :disabled="!ready"
            @click="step(1)"
        />
        <BaseButton
            part="btn-auto-change"
            variant="icon"
            icon="auto-change"
            :label="t('nowPlaying.autoChange')"
            :pressed="settings.autoChange"
            :state="settings.autoChange ? 'on' : 'off'"
            @click="emit('toggleAutoChange')"
        />
        <BaseButton
            part="btn-fullscreen"
            variant="icon"
            :icon="fullscreen ? 'exit-fullscreen' : 'fullscreen'"
            :label="fullscreen ? t('nowPlaying.exitFullscreen') : t('nowPlaying.fullscreen')"
            :disabled="!fullscreenAvailable || (!active && !fullscreen)"
            @click="emit('toggleFullscreen')"
        />
        <BaseButton
            v-if="hasColorSchemes"
            ref="schemePicker"
            part="btn-color-scheme-picker"
            icon="color-scheme"
            :label="colorSchemeLabel"
            :title="t('nowPlaying.colorScheme')"
            :aria-label="t('nowPlaying.colorSchemeChoice', { name: colorSchemeLabel })"
            dropdown
            aria-haspopup="menu"
            :aria-expanded="schemesOpen ? 'true' : 'false'"
            :state="schemesOpen ? 'open' : 'closed'"
            @click="toggleColorSchemes"
        />
        <BaseSegmentedControl
            item="visualization-module"
            :label="t('nowPlaying.visualizationModule')"
            :options="moduleOptions"
            :model-value="settings.module"
            @update:model-value="selectModule"
        />
    </div>
    <!-- Beside the toolbar, not in it: the menus take nothing over from the look of the toolbar. -->
    <BaseContextMenu
        ref="menu"
        data-kind="visualization-presets"
        :items="menuItems"
        :label="t('nowPlaying.visualizationPresets')"
        @select="selectPreset"
    />
    <BaseContextMenu
        v-if="hasColorSchemes"
        ref="schemeMenu"
        data-kind="visualization-color-schemes"
        :items="schemeItems"
        :label="t('nowPlaying.visualizationColorSchemes')"
        @select="selectColorScheme"
    />
</template>
