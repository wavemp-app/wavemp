<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { ProblemCodeObject } from '@/api/generated/models'
import { problemCodeOf } from '@/api/problems'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseDialog from '@/components/base/BaseDialog.vue'
import BaseTextField from '@/components/base/BaseTextField.vue'
import { isCompletePin, pinMaxLength } from '@/profiles/pin'
import { usePinStore } from '@/stores/pin'
import { useSessionStore } from '@/stores/session'

/**
 * The PIN of the current profile: set, change or remove it. A PIN from ADMIN_PIN can only be
 * changed on the server.
 */
const { t } = useI18n()
const pinStore = usePinStore()
const session = useSessionStore()

const pin = ref('')
const confirmation = ref('')
const pinError = ref<string | undefined>(undefined)
const confirmationError = ref<string | undefined>(undefined)
const busy = ref(false)
const message = ref<{ tone: 'error' | 'success'; text: string } | null>(null)
const removeOpen = ref(false)

const state = computed(() => {
    if (pinStore.status !== 'ready') {
        return pinStore.status === 'error' ? 'error' : 'loading'
    }

    if (pinStore.isManagedByEnvironment) {
        return 'managed'
    }

    return pinStore.isSet ? 'on' : 'off'
})

const statusText = computed(() => {
    switch (state.value) {
        case 'managed':
            return t('pinSettings.statusManaged')
        case 'on':
            return t('pinSettings.statusOn')
        case 'off':
            return t('pinSettings.statusOff')
        default:
            return ''
    }
})

function failureText(failure: unknown, fallback: string): string {
    switch (problemCodeOf(failure)) {
        case ProblemCodeObject.PinManaged_by_environment:
            return t('pinSettings.statusManaged')
        case ProblemCodeObject.RequestInvalid:
            return t('pinSettings.invalid')
        default:
            return fallback
    }
}

async function save(): Promise<void> {
    if (busy.value) {
        return
    }

    message.value = null
    pinError.value = isCompletePin(pin.value) ? undefined : t('pinSettings.invalid')
    confirmationError.value =
        pinError.value || pin.value === confirmation.value ? undefined : t('pinSettings.mismatch')
    if (pinError.value || confirmationError.value) {
        return
    }

    busy.value = true
    try {
        await pinStore.set(pin.value)
        pin.value = ''
        confirmation.value = ''
        message.value = { tone: 'success', text: t('pinSettings.saved') }
        // The lock symbol of the profile follows.
        await session.refresh()
    } catch (failure) {
        message.value = { tone: 'error', text: failureText(failure, t('pinSettings.saveError')) }
    } finally {
        busy.value = false
    }
}

async function remove(): Promise<void> {
    if (busy.value) {
        return
    }

    busy.value = true
    message.value = null
    try {
        await pinStore.remove()
        removeOpen.value = false
        message.value = { tone: 'success', text: t('pinSettings.removed') }
        await session.refresh()
    } catch (failure) {
        removeOpen.value = false
        message.value = { tone: 'error', text: failureText(failure, t('pinSettings.removeError')) }
    } finally {
        busy.value = false
    }
}

onMounted(() => {
    void pinStore.load()
})
</script>

<template>
    <div
        data-part="view"
        data-view="settings-pin"
        :data-state="state"
        :aria-busy="state === 'loading' ? 'true' : undefined"
    >
        <BaseAlert v-if="message" :tone="message.tone" :message="message.text" />
        <template v-if="state === 'error'">
            <BaseAlert tone="error" :message="t('pinSettings.loadError')" />
            <div data-part="view-actions">
                <BaseButton
                    data-item="retry"
                    :label="t('library.retry')"
                    @click="pinStore.load()"
                />
            </div>
        </template>
        <p v-else-if="statusText" data-part="form-note" data-field="pin-status">
            {{ statusText }}
        </p>
        <form
            v-if="state === 'on' || state === 'off'"
            data-part="form"
            novalidate
            @submit.prevent="save"
        >
            <BaseTextField
                v-model="pin"
                data-field="pin"
                type="password"
                inputmode="numeric"
                autocomplete="new-password"
                :maxlength="pinMaxLength"
                :label="state === 'on' ? t('pinSettings.newPin') : t('pinSettings.pin')"
                :hint="t('pinSettings.pinHint')"
                :error="pinError"
                :disabled="busy"
            />
            <BaseTextField
                v-model="confirmation"
                data-field="pin-confirmation"
                type="password"
                inputmode="numeric"
                autocomplete="new-password"
                :maxlength="pinMaxLength"
                :label="t('pinSettings.confirmation')"
                :error="confirmationError"
                :disabled="busy"
            />
            <p data-part="form-note">{{ t('pinSettings.otherDevices') }}</p>
            <div data-part="view-actions">
                <BaseButton
                    type="submit"
                    variant="primary"
                    data-item="save-pin"
                    :label="state === 'on' ? t('pinSettings.change') : t('pinSettings.set')"
                    :disabled="busy"
                />
                <BaseButton
                    v-if="state === 'on'"
                    data-item="remove-pin"
                    :label="t('pinSettings.remove')"
                    :disabled="busy"
                    @click="removeOpen = true"
                />
            </div>
        </form>
        <BaseDialog v-model:open="removeOpen" :title="t('pinSettings.removeDialog.title')">
            <p>{{ t('pinSettings.removeDialog.text') }}</p>
            <template #footer>
                <BaseButton :label="t('actions.cancel')" @click="removeOpen = false" />
                <BaseButton
                    variant="primary"
                    data-item="confirm-remove-pin"
                    :label="t('pinSettings.removeDialog.confirm')"
                    :disabled="busy"
                    @click="remove"
                />
            </template>
        </BaseDialog>
    </div>
</template>
