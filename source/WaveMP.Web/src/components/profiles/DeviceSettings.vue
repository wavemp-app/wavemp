<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { Device } from '@/api/generated/models'
import { fetchAllPages } from '@/api/paging'
import { problemCodeOf, validationErrorsOf } from '@/api/problems'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseDialog from '@/components/base/BaseDialog.vue'
import KnownDeviceList from '@/components/profiles/KnownDeviceList.vue'
import { forgetDevice, listDevices, renameDevice } from '@/devices/api'
import { validationMessage } from '@/i18n/validation'
import { useDevicesStore } from '@/stores/devices'

/**
 * The devices of the current profile on the settings page: every browser and app that opened
 * WaveMP with it, open ones first (KnownDeviceList). A device can be renamed (an empty name brings
 * back the name the device reports) and one that is not open can be forgotten after a
 * confirmation. The list follows the devices that open, close or get a new name while it is shown.
 */
const { t } = useI18n()
const devices = useDevicesStore()

const status = ref<'loading' | 'ready' | 'error'>('loading')
const items = ref<Device[]>([])
/** The error of the field of a device, by its id. */
const errors = reactive<Record<string, string | undefined>>({})
const busy = ref<string | null>(null)
const message = ref<{ tone: 'error' | 'success'; text: string } | null>(null)
const forgetting = ref<Device | null>(null)
const forgetOpen = computed({
    get: () => forgetting.value !== null,
    set: (open: boolean) => {
        if (!open) {
            forgetting.value = null
        }
    },
})

async function load(quiet = false): Promise<void> {
    if (!quiet) {
        status.value = 'loading'
    }
    try {
        items.value = await fetchAllPages((offset, limit) => listDevices(offset, limit))
        status.value = 'ready'
    } catch {
        if (!quiet) {
            status.value = 'error'
        }
    }
}

async function rename(device: Device, name: string): Promise<void> {
    const id = device.id ?? ''
    if (busy.value !== null) {
        return
    }

    busy.value = id
    errors[id] = undefined
    message.value = null
    try {
        const updated = await renameDevice(id, name)
        if (updated) {
            items.value = items.value.map((item) => (item.id === id ? updated : item))
        }
        message.value = { tone: 'success', text: t('devices.settings.renamed') }
    } catch (error) {
        const fieldError = validationErrorsOf(error)[0]
        if (problemCodeOf(error) === 'device.not_found') {
            message.value = { tone: 'error', text: t('devices.settings.errors.notFound') }
            void load(true)
        } else if (fieldError) {
            errors[id] = validationMessage(fieldError, (key, parameters) => t(key, parameters))
        } else {
            message.value = { tone: 'error', text: t('devices.settings.errors.renameFailed') }
        }
    } finally {
        busy.value = null
    }
}

async function forget(): Promise<void> {
    const device = forgetting.value
    if (!device || busy.value !== null) {
        return
    }

    const id = device.id ?? ''
    busy.value = id
    message.value = null
    try {
        await forgetDevice(id)
        items.value = items.value.filter((item) => item.id !== id)
        message.value = {
            tone: 'success',
            text: t('devices.settings.forgotten', { device: device.name ?? '' }),
        }
    } catch (error) {
        const code = problemCodeOf(error)
        const key =
            code === 'device.online'
                ? 'devices.settings.errors.online'
                : code === 'device.not_found'
                  ? 'devices.settings.errors.notFound'
                  : 'devices.settings.errors.forgetFailed'
        message.value = { tone: 'error', text: t(key) }
        if (code) {
            void load(true)
        }
    } finally {
        busy.value = null
        forgetting.value = null
    }
}

// Devices that open, close or get a new name elsewhere show up here as well.
watch(
    () => devices.devices,
    () => {
        if (status.value === 'ready') {
            void load(true)
        }
    },
)

onMounted(() => {
    void load()
})
</script>

<template>
    <BaseAlert v-if="message" :tone="message.tone" :message="message.text" />
    <template v-if="status === 'error'">
        <BaseAlert tone="error" :message="t('devices.settings.loadError')" />
        <div data-part="view-actions">
            <BaseButton data-item="retry" :label="t('library.retry')" @click="load()" />
        </div>
    </template>
    <p v-else-if="status === 'loading'" data-part="form-note" aria-busy="true">
        {{ t('devices.settings.loading') }}
    </p>
    <KnownDeviceList
        v-else
        :devices="items"
        :current-id="devices.self.id"
        :busy="busy"
        :errors="errors"
        @rename="rename"
        @forget="forgetting = $event"
    />
    <BaseDialog
        v-model:open="forgetOpen"
        data-item="forget-device"
        :title="t('devices.settings.forgetDialog.title')"
    >
        <p>{{ t('devices.settings.forgetDialog.text', { device: forgetting?.name ?? '' }) }}</p>
        <template #footer>
            <BaseButton :label="t('actions.cancel')" @click="forgetting = null" />
            <BaseButton
                variant="primary"
                data-item="confirm-forget-device"
                :label="t('devices.settings.forgetDialog.confirm')"
                :disabled="busy !== null"
                @click="forget"
            />
        </template>
    </BaseDialog>
</template>
