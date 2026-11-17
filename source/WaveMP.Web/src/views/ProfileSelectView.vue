<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { ProblemCodeObject } from '@/api/generated/models'
import { problemCodeOf } from '@/api/problems'
import ProfileSelect from '@/components/profiles/ProfileSelect.vue'
import type { ProfileTileData } from '@/components/profiles/ProfileTile.vue'
import { lockoutMinutes, retryAfterSecondsOf } from '@/profiles/pin'
import { safeRedirect } from '@/router/guards'
import { useProfilesStore } from '@/stores/profiles'
import { useSessionStore } from '@/stores/session'
import { toStoredProfile } from '@/session/storage'

/**
 * Profile selection: shown on start without a selected profile and after switching profiles. A
 * profile with a PIN asks for it with a digit pad before it is selected.
 */
const { t } = useI18n()
const route = useRoute()
const router = useRouter()
const profilesStore = useProfilesStore()
const session = useSessionStore()

const busy = ref(false)
const error = ref<string | null>(null)
/** Profile whose PIN is being entered. */
const pinProfile = ref<ProfileTileData | null>(null)
const pinError = ref<string | null>(null)

const profiles = computed<ProfileTileData[]>(() =>
    profilesStore.profiles.map(toStoredProfile).filter((profile) => profile !== null),
)
const state = computed(() => {
    switch (profilesStore.status) {
        case 'ready':
            return 'ready'
        case 'error':
            return 'error'
        default:
            return 'loading'
    }
})
const currentId = computed(() => (session.isAuthenticated ? session.profile?.id : null))

function askForPin(profile: ProfileTileData, message: string | null = null): void {
    error.value = null
    pinProfile.value = { ...profile, hasPin: true }
    pinError.value = message
}

function closePin(): void {
    pinProfile.value = null
    pinError.value = null
}

async function selectProfile(profile: ProfileTileData, pin?: string): Promise<void> {
    if (busy.value) {
        return
    }

    busy.value = true
    error.value = null
    try {
        await session.select(profile.id, pin)
        await router.replace(safeRedirect(route.query.redirect))
    } catch (failure) {
        switch (problemCodeOf(failure)) {
            case ProblemCodeObject.ProfilePin_required:
                // The PIN was set after the profiles were loaded.
                askForPin(profile)
                void profilesStore.load()
                break
            case ProblemCodeObject.ProfilePin_invalid:
                askForPin(profile, t('pin.wrong'))
                break
            case ProblemCodeObject.ProfilePin_locked:
                askForPin(
                    profile,
                    t('pin.locked', {
                        minutes: lockoutMinutes(retryAfterSecondsOf(failure)),
                    }),
                )
                break
            case ProblemCodeObject.ProfileNot_found:
                closePin()
                error.value = t('profiles.profileGone')
                void profilesStore.load()
                break
            default:
                if (pinProfile.value) {
                    pinError.value = t('profiles.selectError')
                } else {
                    error.value = t('profiles.selectError')
                }
        }
    } finally {
        busy.value = false
    }
}

function choose(profile: ProfileTileData): void {
    if (profile.hasPin && profile.id !== currentId.value) {
        askForPin(profile)
        return
    }

    void selectProfile(profile)
}

function submitPin(pin: string): void {
    if (pinProfile.value) {
        void selectProfile(pinProfile.value, pin)
    }
}

onMounted(() => {
    void profilesStore.load()
})
</script>

<template>
    <ProfileSelect
        :profiles="profiles"
        :state="state"
        :current-id="currentId"
        :error="error"
        :busy="busy"
        :pin-profile="pinProfile"
        :pin-error="pinError"
        @select="choose"
        @retry="profilesStore.load()"
        @pin="submitPin"
        @cancel-pin="closePin"
    />
</template>
