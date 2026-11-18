<script setup lang="ts">
import { onMounted, ref, useId } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import {
    ScrobbleServiceObject,
    type ScrobbleService,
    type ScrobblingService,
} from '@/api/generated/models'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseDialog from '@/components/base/BaseDialog.vue'
import BaseTextField from '@/components/base/BaseTextField.vue'
import ScrobbleList from '@/components/profiles/ScrobbleList.vue'
import {
    connectErrorKey,
    lastFmAuthorizationUrl,
    lastFmCallbackRoute,
    listenBrainzTokenUrl,
    scrobbleServicePresentation,
} from '@/scrobbling/presentation'
import { useScrobblingStore } from '@/stores/scrobbling'

/**
 * The scrobbling services of the current profile: the state of each connection with Connect and
 * Disconnect. Last.fm is connected through its web authorization, ListenBrainz with the personal
 * token of the user.
 */
const { t } = useI18n()
const router = useRouter()
const scrobbling = useScrobblingStore()
const formId = useId()

const busy = ref<ScrobbleService | null>(null)
const message = ref<{ tone: 'error' | 'success'; text: string } | null>(null)
const tokenOpen = ref(false)
const token = ref('')
const tokenError = ref<string | undefined>(undefined)

function nameOf(item: ScrobblingService): string {
    return item.service ? t(scrobbleServicePresentation(item.service).nameKey) : ''
}

function connect(item: ScrobblingService): void {
    message.value = null
    if (item.service === ScrobbleServiceObject.LastFm) {
        if (!item.authorizationUrl) {
            message.value = { tone: 'error', text: t('scrobbling.errors.notConfigured') }
            return
        }

        const callback = new URL(
            router.resolve({ name: lastFmCallbackRoute }).href,
            window.location.origin,
        ).toString()
        window.location.assign(lastFmAuthorizationUrl(item.authorizationUrl, callback))
        return
    }

    token.value = ''
    tokenError.value = undefined
    tokenOpen.value = true
}

async function saveToken(): Promise<void> {
    if (busy.value) {
        return
    }

    const trimmed = token.value.trim()
    tokenError.value = trimmed === '' ? t('scrobbling.tokenRequired') : undefined
    if (tokenError.value) {
        return
    }

    busy.value = ScrobbleServiceObject.ListenBrainz
    try {
        await scrobbling.connect(ScrobbleServiceObject.ListenBrainz, trimmed)
        tokenOpen.value = false
        token.value = ''
        message.value = {
            tone: 'success',
            text: t('scrobbling.connected', {
                service: t('scrobbling.services.listenBrainz'),
            }),
        }
    } catch (failure) {
        tokenError.value = t(connectErrorKey(failure))
    } finally {
        busy.value = null
    }
}

async function disconnect(item: ScrobblingService): Promise<void> {
    if (busy.value || !item.service) {
        return
    }

    busy.value = item.service
    message.value = null
    try {
        await scrobbling.disconnect(item.service)
        message.value = {
            tone: 'success',
            text: t('scrobbling.disconnected', { service: nameOf(item) }),
        }
    } catch {
        message.value = { tone: 'error', text: t('scrobbling.errors.disconnectFailed') }
    } finally {
        busy.value = null
    }
}

onMounted(() => {
    void scrobbling.load()
})
</script>

<template>
    <BaseAlert v-if="message" :tone="message.tone" :message="message.text" />
    <template v-if="scrobbling.status === 'error'">
        <BaseAlert tone="error" :message="t('scrobbling.loadError')" />
        <div data-part="view-actions">
            <BaseButton data-item="retry" :label="t('library.retry')" @click="scrobbling.load()" />
        </div>
    </template>
    <ScrobbleList
        v-else
        :services="scrobbling.services"
        :busy="busy"
        :loading="scrobbling.status === 'loading'"
        :disabled="scrobbling.status !== 'ready'"
        @connect="connect"
        @disconnect="disconnect"
    />
    <BaseDialog
        v-model:open="tokenOpen"
        data-item="scrobbling-token"
        :title="t('scrobbling.tokenDialog.title')"
    >
        <form :id="formId" data-part="form" novalidate @submit.prevent="saveToken">
            <p data-part="form-note">
                {{ t('scrobbling.tokenDialog.hint') }}
                <a
                    data-part="link"
                    data-item="listenbrainz-token"
                    :href="listenBrainzTokenUrl"
                    target="_blank"
                    rel="noopener noreferrer"
                    >{{ t('scrobbling.tokenDialog.openSettings') }}</a
                >
            </p>
            <BaseTextField
                v-model="token"
                data-field="scrobbling-token"
                type="password"
                :label="t('scrobbling.tokenDialog.token')"
                :error="tokenError"
                :maxlength="256"
                autocomplete="off"
                required
            />
        </form>
        <template #footer>
            <BaseButton :label="t('actions.cancel')" @click="tokenOpen = false" />
            <BaseButton
                type="submit"
                variant="primary"
                :form="formId"
                :label="t('scrobbling.tokenDialog.connect')"
                :disabled="busy !== null"
            />
        </template>
    </BaseDialog>
</template>
