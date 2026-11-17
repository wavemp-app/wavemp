<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import ProfileAvatar from './ProfileAvatar.vue'
import BaseIcon from '@/components/base/BaseIcon.vue'

export interface ProfileTileData {
    readonly id: string
    readonly name: string
    readonly initials: string
    readonly color: string
    readonly isBuiltIn: boolean
    /** Selecting the profile needs its PIN. */
    readonly hasPin?: boolean
    /** Version of the avatar; null or missing without one. */
    readonly avatarVersion?: string | null
}

/** Tile of the profile selection: avatar and name; selecting it selects the profile. */
defineProps<{
    profile: ProfileTileData
    /** data-state tokens: built-in, secondary, current, protected. */
    state?: string
    disabled?: boolean
}>()

const emit = defineEmits<{
    select: []
}>()

const { t } = useI18n()
</script>

<template>
    <button
        type="button"
        data-part="profile-tile"
        :data-state="state"
        :title="profile.isBuiltIn ? t('profiles.builtIn') : undefined"
        :aria-current="state?.split(' ').includes('current') ? 'true' : undefined"
        :disabled="disabled"
        @click="emit('select')"
    >
        <ProfileAvatar
            :initials="profile.initials"
            :color="profile.color"
            :user-id="profile.id"
            :avatar-version="profile.avatarVersion"
        />
        <span data-part="label">{{ profile.name }}</span>
        <span
            v-if="profile.hasPin"
            data-part="profile-lock"
            role="img"
            :aria-label="t('profiles.protected')"
            :title="t('profiles.protected')"
        >
            <BaseIcon name="profile-lock" />
        </span>
    </button>
</template>
