<script setup lang="ts">
import { computed, nextTick, ref, useId } from 'vue'
import { useI18n } from 'vue-i18n'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseIcon from '@/components/base/BaseIcon.vue'
import { layoutSize, roomAbove } from '@/components/base/menu'
import type { TransportDevices } from '@/components/shell/transport'
import { indicatorVariables, useIndicator } from '@/composables/useIndicator'
import { useMediaQuery } from '@/composables/useMediaQuery'
import {
    commandStatusKeys,
    deviceStatusKeys,
    devicesControlTokens,
    type DeviceEntry,
} from '@/devices/presentation'
import {
    deviceTabStop,
    nextDeviceIndex,
    pickerPosition,
    roomBelow,
    type PickerPlacement,
} from '@/devices/picker'
import { mobileMediaQuery } from '@/themes/breakpoints'

/**
 * The devices of the profile in the transport bar: the button shows where the music plays and
 * opens the device picker, a dialog on the Popover API (light dismiss and Escape come from the
 * browser) above the button or, on phones, as a sheet at the bottom of the screen. Its list is a
 * radio group of the open devices with the one that plays checked; choosing another device hands
 * playback over to it. The arrow keys only move the focus, because choosing acts at once. Devices
 * that do not allow remote control are greyed out (aria-disabled). Below the list, the switch
 * "Allow remote control" of this device, for the running page only. Controlled through props and
 * events only.
 */
const props = defineProps<{
    devices?: TransportDevices
}>()

const emit = defineEmits<{
    /** Hands playback over to a device (this one: play here). */
    transfer: [deviceId: string]
    /** Allows or forbids remote control of this device; emitted within the click. */
    remoteControl: [allowed: boolean]
    /** The notice of a command that was not accepted was read. */
    dismissStatus: []
}>()

const { t } = useI18n()
const phone = useMediaQuery(mobileMediaQuery)

const pickerId = useId()
const titleId = useId()
const hintId = useId()
const root = ref<HTMLElement | null>(null)
const picker = ref<HTMLElement | null>(null)
const list = ref<HTMLElement | null>(null)
const open = ref(false)
const placement = ref<PickerPlacement>('above')
const x = ref<number | null>(null)
const y = ref<number | null>(null)
const maxHeight = ref<number | null>(null)
/** The device that has the focus in the list (roving tab stop); null: the default tab stop. */
const focused = ref<number | null>(null)

const entries = computed<readonly DeviceEntry[]>(() => props.devices?.entries ?? [])
const checked = computed(() => entries.value.findIndex((entry) => entry.active))
const tabStop = computed(() => {
    const index = focused.value
    return index !== null && index < entries.value.length ? index : deviceTabStop(entries.value)
})
const remote = computed(() => props.devices?.role === 'remote')
const tokens = computed(() =>
    props.devices
        ? devicesControlTokens(props.devices.role, props.devices.connection, open.value)
        : '',
)
const label = computed(() =>
    remote.value && props.devices?.playingOn
        ? t('devices.buttonRemote', { device: props.devices.playingOn })
        : t('devices.button'),
)
const statusText = computed(() => {
    const status = props.devices?.status
    return status ? t(commandStatusKeys[status]) : ''
})

const indicator = useIndicator(list, '[aria-checked="true"]', () => [
    checked.value,
    entries.value.length,
    open.value,
])

/** A length in pixels for a runtime variable; without a value the variable stays unset. */
function pixels(value: number | null): string | undefined {
    return value === null ? undefined : `${value}px`
}

function anchorRect(): DOMRect | undefined {
    return root.value
        ?.querySelector<HTMLElement>('[data-part="btn-devices"]')
        ?.getBoundingClientRect()
}

/** Before the picker opens: where it goes, so it never shows at the wrong place. */
function onBeforeToggle(event: Event): void {
    if ((event as ToggleEvent).newState !== 'open') {
        return
    }

    placement.value = phone.value ? 'sheet' : 'above'
    const anchor = anchorRect()
    if (placement.value === 'sheet' || !anchor) {
        x.value = null
        y.value = null
        maxHeight.value = null
        return
    }

    maxHeight.value = Math.max(
        roomAbove(anchor),
        roomBelow(anchor, { width: window.innerWidth, height: window.innerHeight }),
    )
    x.value = anchor.left
    y.value = anchor.top
}

async function onToggle(event: Event): Promise<void> {
    const isOpen = (event as ToggleEvent).newState === 'open'
    open.value = isOpen
    if (!isOpen) {
        focused.value = null
        if (props.devices?.status) {
            emit('dismissStatus')
        }
        // The focus goes back to the button, unless it went somewhere else on purpose.
        const active = document.activeElement
        if (!active || active === document.body || picker.value?.contains(active)) {
            root.value?.querySelector<HTMLElement>('[data-part="btn-devices"]')?.focus()
        }
        return
    }

    await nextTick()
    const element = picker.value
    const anchor = anchorRect()
    if (element && anchor && placement.value !== 'sheet') {
        const layout = pickerPosition(anchor, layoutSize(element), {
            width: window.innerWidth,
            height: window.innerHeight,
        })
        placement.value = layout.placement
        x.value = layout.x
        y.value = layout.y
        maxHeight.value = layout.maxHeight
    }

    focusDevice(tabStop.value)
}

function focusDevice(index: number): void {
    focused.value = index
    deviceElements()[index]?.focus()
}

function deviceElements(): HTMLElement[] {
    return [...(list.value?.querySelectorAll<HTMLElement>('[data-part="device-item"]') ?? [])]
}

function onListKeydown(event: KeyboardEvent): void {
    const current = deviceElements().indexOf(event.target as HTMLElement)
    const next = nextDeviceIndex(event.key, current, entries.value.length)
    if (next === null) {
        return
    }

    event.preventDefault()
    focusDevice(next)
}

function choose(entry: DeviceEntry, index: number): void {
    focused.value = index
    if (!entry.selectable || entry.active) {
        return
    }

    emit('transfer', entry.id)
}

function toggleRemoteControl(): void {
    emit('remoteControl', !(props.devices?.remoteControl ?? false))
}
</script>

<template>
    <div v-if="devices" ref="root" data-part="devices" :data-state="tokens">
        <BaseButton
            part="btn-devices"
            variant="icon"
            icon="devices"
            :label="label"
            :state="devices.role"
            aria-haspopup="dialog"
            :aria-expanded="open ? 'true' : 'false'"
            :aria-controls="pickerId"
            :popovertarget="pickerId"
        />
        <div
            :id="pickerId"
            ref="picker"
            data-part="device-picker"
            role="dialog"
            popover="auto"
            :aria-labelledby="titleId"
            :data-state="open ? 'open' : 'closed'"
            :data-placement="placement"
            :style="{
                '--wmp-menu-x': pixels(x),
                '--wmp-menu-y': pixels(y),
                '--wmp-menu-max-height': pixels(maxHeight),
            }"
            @beforetoggle="onBeforeToggle"
            @toggle="onToggle"
        >
            <h2 :id="titleId" data-part="device-picker-title">{{ t('devices.title') }}</h2>
            <div
                ref="list"
                data-part="device-list"
                role="radiogroup"
                :aria-labelledby="titleId"
                :style="indicatorVariables(entries.length, checked, indicator)"
                @keydown="onListKeydown"
            >
                <span data-part="indicator" aria-hidden="true"></span>
                <button
                    v-for="(entry, index) in entries"
                    :key="entry.id"
                    type="button"
                    data-part="device-item"
                    role="radio"
                    :data-state="entry.tokens"
                    :aria-checked="entry.active ? 'true' : 'false'"
                    :aria-disabled="entry.selectable ? undefined : 'true'"
                    :tabindex="index === tabStop ? 0 : -1"
                    @click="choose(entry, index)"
                >
                    <BaseIcon :name="entry.icon" />
                    <span data-part="label">{{ entry.name }}</span>
                    <span data-part="device-meta">{{ t(deviceStatusKeys[entry.status]) }}</span>
                </button>
            </div>
            <BaseAlert v-if="statusText" tone="error" :message="statusText" />
            <p v-if="devices.connection === 'offline'" data-part="device-hint" data-kind="offline">
                {{ t('devices.offline') }}
            </p>
            <p v-else-if="devices.replaced" data-part="device-hint" data-kind="replaced">
                {{ t('devices.replaced') }}
            </p>
            <button
                type="button"
                data-part="remote-control-toggle"
                role="switch"
                :aria-checked="devices.remoteControl ? 'true' : 'false'"
                :aria-describedby="hintId"
                :data-state="devices.remoteControl ? 'on' : 'off'"
                @click="toggleRemoteControl"
            >
                <BaseIcon name="remote-control" />
                <span data-part="label">{{ t('devices.remoteControl') }}</span>
                <span data-part="switch-track" aria-hidden="true">
                    <span data-part="switch-thumb"></span>
                </span>
            </button>
            <p :id="hintId" data-part="device-hint" data-kind="remote-control">
                {{ t('devices.remoteControlHint') }}
            </p>
            <p v-if="devices.appleMobile" data-part="device-hint" data-kind="apple">
                {{ t('devices.remoteControlApple') }}
            </p>
        </div>
    </div>
</template>
