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
 * change and full screen, and the choice of the module. The buttons keep their places whatever the
 * preset is called; on a narrow stage the module choice takes a row of its own below them. The
 * preset list opens above its button on the desktop (towards the picture, the bottom of the page
 * is near) and as a sheet at the bottom edge of the screen on phones; the menu lies beside the
 * toolbar, so it inherits nothing from the look of the toolbar. Controlled through props and events
 * only; StageVisualization draws and owns the presets.
 */
const props = withDefaults(
    defineProps<{
        /** The presets of the module, in their order, with their labels. */
        presets?: readonly PresetChoice[]
        /** Id of the shown preset; empty before one is shown. */
        preset?: string
        /** The visualization settings of the profile. */
        settings: VisualizationSettings
        /** Whether the visualization is shown in full screen. */
        fullscreen?: boolean
        /** Whether the browser can show the visualization in full screen. */
        fullscreenAvailable?: boolean
        /** Whether the visualization is shown, so it may go to full screen. */
        active?: boolean
    }>(),
    { presets: () => [], preset: '', fullscreen: false, fullscreenAvailable: false, active: true },
)

const emit = defineEmits<{
    /** A preset was chosen: from the list or with the previous and next buttons. */
    selectPreset: [id: string]
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

const ready = computed(() => props.presets.length > 0)

const current = computed(() => props.presets.find((choice) => choice.id === props.preset))

const presetLabel = computed(() => current.value?.label ?? t('nowPlaying.visualizationPreset'))

const menuItems = computed<MenuItem[]>(() =>
    props.presets.map((choice) => ({
        id: choice.id,
        label: choice.label,
        checked: choice.id === props.preset,
    })),
)

const moduleOptions = computed<SegmentOption[]>(() =>
    visualizationModuleChoices.map((choice) => ({
        value: choice.id,
        item: choice.id.toLowerCase(),
        label: t(choice.labelKey),
    })),
)

function pickerElement(): HTMLElement | null {
    const element = (picker.value as { $el?: unknown } | null)?.$el
    return element instanceof HTMLElement ? element : null
}

async function togglePresets(): Promise<void> {
    if (open.value) {
        menu.value?.close()
        return
    }

    const element = pickerElement()
    if (!element || !ready.value) {
        return
    }

    const placement = presetMenuPlacement(phone.value, element.getBoundingClientRect())
    if (placement.scrollIntoView) {
        // Too little room above the button: the page brings it down first, the list still opens
        // above it.
        element.scrollIntoView?.({ block: 'end', inline: 'nearest' })
        await nextTick()
    }

    await menu.value?.openAnchored(element.getBoundingClientRect(), placement.placement)
}

function selectPreset(id: string): void {
    if (props.presets.some((choice) => choice.id === id) && id !== props.preset) {
        emit('selectPreset', id)
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
        :data-state="ready ? 'ready' : 'loading'"
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
        <BaseSegmentedControl
            item="visualization-module"
            :label="t('nowPlaying.visualizationModule')"
            :options="moduleOptions"
            :model-value="settings.module"
            @update:model-value="selectModule"
        />
    </div>
    <!-- Beside the toolbar, not in it: the menu takes nothing over from the look of the toolbar. -->
    <BaseContextMenu
        ref="menu"
        data-kind="visualization-presets"
        :items="menuItems"
        :label="t('nowPlaying.visualizationPresets')"
        @select="selectPreset"
    />
</template>
