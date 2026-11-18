<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { ScrobbleService, ScrobblingService } from '@/api/generated/models'
import BaseButton from '@/components/base/BaseButton.vue'
import { useFormat } from '@/composables/useFormat'
import {
    scrobbleServicePresentation,
    scrobbleServices,
    scrobbleState,
    scrobbleStateKey,
} from '@/scrobbling/presentation'

/**
 * The scrobbling services with the state of their connection, each with Connect or Disconnect.
 * Services the server did not list are shown as unavailable.
 */
const props = withDefaults(
    defineProps<{
        services: readonly ScrobblingService[]
        /** Service that is being connected or disconnected; disables the buttons. */
        busy?: ScrobbleService | null
        /** Disables Connect, e.g. while the states load. */
        disabled?: boolean
        loading?: boolean
    }>(),
    { busy: null, disabled: false, loading: false },
)

const emit = defineEmits<{
    connect: [service: ScrobblingService]
    disconnect: [service: ScrobblingService]
}>()

const { t } = useI18n()
const format = useFormat()

const items = computed(() =>
    scrobbleServices.map((service) => {
        const item = props.services.find((entry) => entry.service === service) ?? {
            service,
            isAvailable: false,
        }
        const name = t(scrobbleServicePresentation(service).nameKey)
        return {
            service,
            item,
            name,
            state: scrobbleState(item),
            token: scrobbleServicePresentation(service).token,
        }
    }),
)

function dateTime(date: Date | null | undefined): string {
    return date ? format.date(date, { dateStyle: 'medium', timeStyle: 'short' }) : ''
}
</script>

<template>
    <ul
        data-part="scrobble-list"
        :aria-label="t('scrobbling.list')"
        :aria-busy="loading ? 'true' : undefined"
    >
        <li
            v-for="entry in items"
            :key="entry.service"
            data-part="scrobble-item"
            :data-item="entry.token"
            :data-state="busy === entry.service ? `${entry.state} busy` : entry.state"
        >
            <div data-part="scrobble-info">
                <span data-part="scrobble-name">{{ entry.name }}</span>
                <span data-part="scrobble-meta">
                    <span data-part="badge" :data-state="entry.state">
                        <span data-part="label">{{ t(scrobbleStateKey(entry.state)) }}</span>
                    </span>
                    <span v-if="entry.item.accountName" data-part="label" data-field="account">{{
                        t('scrobbling.account', { name: entry.item.accountName })
                    }}</span>
                    <span
                        v-if="entry.item.lastScrobbledAt"
                        data-part="label"
                        data-field="last-scrobbled"
                        >{{
                            t('scrobbling.lastScrobbled', {
                                time: dateTime(entry.item.lastScrobbledAt),
                            })
                        }}</span
                    >
                    <span v-if="entry.item.queuedScrobbles" data-part="label" data-field="queued">{{
                        t('scrobbling.queued', { count: format.number(entry.item.queuedScrobbles) })
                    }}</span>
                </span>
            </div>
            <p
                v-if="entry.state === 'authorization-failed'"
                data-part="form-note"
                data-field="authorization-failed"
            >
                {{ t('scrobbling.authorizationFailedHint') }}
            </p>
            <p
                v-else-if="entry.state === 'unavailable'"
                data-part="form-note"
                data-field="unavailable"
            >
                {{ t('scrobbling.unavailableHint') }}
            </p>
            <div data-part="scrobble-actions">
                <BaseButton
                    v-if="entry.state === 'not-connected' || entry.state === 'authorization-failed'"
                    data-item="connect"
                    :variant="entry.state === 'authorization-failed' ? 'primary' : 'default'"
                    :label="
                        entry.state === 'authorization-failed'
                            ? t('scrobbling.reconnect', { service: entry.name })
                            : t('scrobbling.connect', { service: entry.name })
                    "
                    :disabled="busy !== null || disabled"
                    @click="emit('connect', entry.item)"
                />
                <BaseButton
                    v-if="entry.state === 'connected' || entry.state === 'authorization-failed'"
                    data-item="disconnect"
                    :label="t('scrobbling.disconnect', { service: entry.name })"
                    :disabled="busy !== null"
                    @click="emit('disconnect', entry.item)"
                />
            </div>
        </li>
    </ul>
</template>
