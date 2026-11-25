<script setup lang="ts">
import { reactive, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { Device } from '@/api/generated/models'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseIcon from '@/components/base/BaseIcon.vue'
import BaseTextField from '@/components/base/BaseTextField.vue'
import { useFormat } from '@/composables/useFormat'
import { knownDeviceMeta, knownDeviceTokens, nameMaxLength, renameTarget } from '@/devices/known'
import { deviceIconSlot } from '@/devices/presentation'

/**
 * The devices of the profile on the settings page: an icon of its kind, a field with its name
 * (saved on Enter and when the field is left) and what it does; a device that is not open can be
 * forgotten. Controlled through props and events only; the names being typed stay here until the
 * device gets a new name.
 */
const props = withDefaults(
    defineProps<{
        devices: readonly Device[]
        /** The id of this device. */
        currentId: string
        /** The device whose change is being saved. */
        busy?: string | null
        /** The error of the field of a device, by its id. */
        errors?: Readonly<Record<string, string | undefined>>
    }>(),
    { busy: null, errors: () => ({}) },
)

const emit = defineEmits<{
    /** The new name of a device, trimmed (the name it reports when the field was emptied). */
    rename: [device: Device, name: string]
    /** Asks to forget a device that is not open. */
    forget: [device: Device]
}>()

const { t } = useI18n()
const format = useFormat()

/** The names in the fields, by device id. */
const names = reactive<Record<string, string>>({})

// A device with a new name (here or elsewhere) shows it; names being typed stay otherwise.
watch(
    () => props.devices.map((device) => [device.id ?? '', device.name ?? ''] as const),
    (now, before) => {
        const old = new Map(before ?? [])
        for (const [id, name] of now) {
            if (old.get(id) !== name) {
                names[id] = name
            }
        }
    },
    { immediate: true },
)

function idOf(device: Device): string {
    return device.id ?? ''
}

/**
 * The name last sent per device: Enter both commits the field (change) and submits its form, which
 * sends the name once. Typing or the end of the task that sent it allows sending it again (retry).
 */
const sent: Record<string, string> = {}

function forgetSent(id: string): void {
    window.setTimeout(() => {
        delete sent[id]
    })
}

/** Saves the name in the field; a field without a change shows the name of the device again. */
function submit(device: Device): void {
    const id = idOf(device)
    const target = renameTarget(device, names[id] ?? '')
    if (target === null) {
        names[id] = device.name ?? ''
        return
    }

    if (sent[id] === target) {
        return
    }

    sent[id] = target
    forgetSent(id)
    emit('rename', device, target)
}

function meta(device: Device): string {
    const line = knownDeviceMeta(device, props.currentId)
    switch (line.kind) {
        case 'current':
            return t('devices.settings.current')
        case 'online':
            return t('devices.settings.online')
        default:
            return line.lastActiveAt
                ? t('devices.settings.lastActive', {
                      date: format.date(line.lastActiveAt, {
                          dateStyle: 'medium',
                          timeStyle: 'short',
                      }),
                  })
                : t('devices.status.offline')
    }
}
</script>

<template>
    <p v-if="devices.length === 0" data-part="form-note">{{ t('devices.settings.empty') }}</p>
    <ul v-else data-part="known-devices" :aria-label="t('devices.settings.title')">
        <li
            v-for="device in devices"
            :key="idOf(device)"
            data-part="known-device"
            :data-state="knownDeviceTokens(device, currentId, busy === idOf(device))"
        >
            <BaseIcon :name="deviceIconSlot(device.type)" />
            <div data-part="known-device-info">
                <form data-part="form" data-item="rename-device" @submit.prevent="submit(device)">
                    <BaseTextField
                        v-model="names[idOf(device)]"
                        data-field="device-name"
                        :label="t('devices.settings.name')"
                        :placeholder="device.reportedName ?? undefined"
                        :maxlength="nameMaxLength"
                        autocomplete="off"
                        :error="errors[idOf(device)]"
                        :disabled="busy === idOf(device)"
                        @input="delete sent[idOf(device)]"
                        @change="submit(device)"
                    />
                </form>
                <span data-part="device-meta">{{ meta(device) }}</span>
            </div>
            <div
                v-if="!device.online"
                data-part="known-device-actions"
                role="group"
                :aria-label="device.name ?? ''"
            >
                <BaseButton
                    data-item="forget-device"
                    icon="delete"
                    :label="t('devices.settings.forget')"
                    :disabled="busy !== null"
                    @click="emit('forget', device)"
                />
            </div>
        </li>
    </ul>
</template>
