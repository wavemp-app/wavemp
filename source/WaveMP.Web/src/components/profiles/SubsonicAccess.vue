<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseCheckbox from '@/components/base/BaseCheckbox.vue'
import { useFormat } from '@/composables/useFormat'
import { useSessionStore } from '@/stores/session'
import { useSubsonicStore } from '@/stores/subsonic'

/**
 * The Subsonic access of the current profile: the switch that allows Subsonic apps to sign in,
 * what the apps need to sign in (address of the server, user name, app password) and the app
 * password, which is shown once after it is created and can be renewed or revoked.
 */
const { t } = useI18n()
const format = useFormat()
const session = useSessionStore()
const subsonic = useSubsonicStore()

type Busy = 'enable' | 'create' | 'revoke' | null

const busy = ref<Busy>(null)
const message = ref<{ tone: 'error' | 'success'; text: string } | null>(null)
const enabled = ref(false)
const newPassword = ref<string | null>(null)
const copied = ref(false)

const access = computed(() => subsonic.access)
const ready = computed(() => subsonic.status === 'ready' && access.value !== null)

/** The address apps take: the server of this web app, without the path of the Subsonic API. */
const serverAddress = window.location.origin

const passwordState = computed(() => {
    const createdAt = access.value?.passwordCreatedAt
    if (!access.value?.hasPassword) {
        return t('subsonic.passwordNone')
    }

    return createdAt
        ? t('subsonic.passwordCreatedAt', { date: format.date(createdAt) })
        : t('subsonic.passwordSet')
})

const details = computed(() => [
    { field: 'subsonic-server', labelKey: 'subsonic.server', value: serverAddress },
    {
        field: 'subsonic-user-name',
        labelKey: 'subsonic.userName',
        value: session.profile?.name ?? '',
    },
    { field: 'subsonic-password-state', labelKey: 'subsonic.password', value: passwordState.value },
])

async function load(): Promise<void> {
    await subsonic.load()
    enabled.value = subsonic.access?.enabled ?? false
}

async function changeEnabled(value: boolean): Promise<void> {
    if (busy.value || value === access.value?.enabled) {
        return
    }

    busy.value = 'enable'
    message.value = null
    try {
        await subsonic.setEnabled(value)
    } catch {
        message.value = { tone: 'error', text: t('subsonic.errors.saveFailed') }
    } finally {
        enabled.value = subsonic.access?.enabled ?? false
        busy.value = null
    }
}

async function createPassword(): Promise<void> {
    if (busy.value) {
        return
    }

    busy.value = 'create'
    message.value = null
    copied.value = false
    try {
        newPassword.value = await subsonic.createPassword()
    } catch {
        message.value = { tone: 'error', text: t('subsonic.errors.createFailed') }
    } finally {
        busy.value = null
    }
}

async function revokePassword(): Promise<void> {
    if (busy.value) {
        return
    }

    busy.value = 'revoke'
    message.value = null
    try {
        await subsonic.revokePassword()
        newPassword.value = null
        message.value = { tone: 'success', text: t('subsonic.revoked') }
    } catch {
        message.value = { tone: 'error', text: t('subsonic.errors.revokeFailed') }
    } finally {
        busy.value = null
    }
}

async function copyPassword(): Promise<void> {
    if (!newPassword.value) {
        return
    }

    try {
        await navigator.clipboard.writeText(newPassword.value)
        copied.value = true
    } catch {
        message.value = { tone: 'error', text: t('subsonic.errors.copyFailed') }
    }
}

onMounted(() => {
    void load()
})
</script>

<template>
    <BaseAlert v-if="message" :tone="message.tone" :message="message.text" />
    <template v-if="subsonic.status === 'error'">
        <BaseAlert tone="error" :message="t('subsonic.loadError')" />
        <div data-part="view-actions">
            <BaseButton data-item="retry" :label="t('library.retry')" @click="load()" />
        </div>
    </template>
    <template v-else>
        <BaseCheckbox
            v-model="enabled"
            data-field="subsonic-enabled"
            :label="t('subsonic.enabled')"
            :hint="t('subsonic.enabledHint')"
            :disabled="!ready || busy !== null"
            @update:model-value="changeEnabled"
        />
        <dl data-part="stat-list" data-item="subsonic-sign-in">
            <div
                v-for="item in details"
                :key="item.field"
                data-part="stat-item"
                :data-field="item.field"
            >
                <dt data-part="label">{{ t(item.labelKey) }}</dt>
                <dd data-part="stat-value">{{ item.value }}</dd>
            </div>
            <div v-if="newPassword" data-part="stat-item" data-field="subsonic-new-password">
                <dt data-part="label">{{ t('subsonic.newPassword') }}</dt>
                <dd data-part="stat-value">{{ newPassword }}</dd>
            </div>
        </dl>
        <p
            v-if="newPassword"
            data-part="form-note"
            data-item="subsonic-password-once"
            role="status"
        >
            {{ copied ? t('subsonic.copied') : t('subsonic.newPasswordHint') }}
        </p>
        <div data-part="view-actions">
            <BaseButton
                v-if="newPassword"
                data-item="copy-subsonic-password"
                :label="t('subsonic.copy')"
                @click="copyPassword"
            />
            <BaseButton
                data-item="create-subsonic-password"
                :label="
                    access?.hasPassword ? t('subsonic.renewPassword') : t('subsonic.createPassword')
                "
                :disabled="!ready || busy !== null"
                @click="createPassword"
            />
            <BaseButton
                v-if="access?.hasPassword"
                data-item="revoke-subsonic-password"
                :label="t('subsonic.revokePassword')"
                :disabled="!ready || busy !== null"
                @click="revokePassword"
            />
        </div>
    </template>
</template>
