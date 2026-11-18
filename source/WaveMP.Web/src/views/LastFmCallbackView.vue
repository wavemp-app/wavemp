<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { ScrobbleServiceObject } from '@/api/generated/models'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseLink from '@/components/base/BaseLink.vue'
import { connectErrorKey } from '@/scrobbling/presentation'
import { useScrobblingStore } from '@/stores/scrobbling'

/**
 * Last.fm sends the user back here after the authorization, with a token in the query. The page
 * hands the token to the server, which exchanges it for a session of the profile, and then goes
 * back to the settings.
 */
const { t } = useI18n()
const route = useRoute()
const router = useRouter()
const scrobbling = useScrobblingStore()

const state = ref<'connecting' | 'failed'>('connecting')
const error = ref('')

const token = computed(() => {
    const value = route.query.token
    return typeof value === 'string' ? value.trim() : ''
})

onMounted(async () => {
    if (token.value === '') {
        state.value = 'failed'
        error.value = t('scrobbling.errors.lastFmCanceled')
        return
    }

    try {
        await scrobbling.connect(ScrobbleServiceObject.LastFm, token.value)
        await router.replace({ name: 'settings' })
    } catch (failure) {
        state.value = 'failed'
        error.value = t(connectErrorKey(failure))
    }
})
</script>

<template>
    <div
        data-part="view"
        data-view="settings-scrobbling-lastfm"
        :data-state="state"
        :aria-busy="state === 'connecting' ? 'true' : undefined"
    >
        <p v-if="state === 'connecting'" data-part="form-note" data-field="lastfm-status">
            {{ t('scrobbling.lastFmConnecting') }}
        </p>
        <template v-else>
            <BaseAlert tone="error" :message="error" />
            <p data-part="form-note">
                <BaseLink :to="{ name: 'settings' }" data-item="back-to-settings">{{
                    t('scrobbling.backToSettings')
                }}</BaseLink>
            </p>
        </template>
    </div>
</template>
