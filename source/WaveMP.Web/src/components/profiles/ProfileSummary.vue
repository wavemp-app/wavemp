<script setup lang="ts">
import ProfileAvatar from './ProfileAvatar.vue'

/** Avatar and name of a profile, e.g. of the current profile on the settings page. */
defineProps<{
    name: string
    initials: string
    color: string
    /** Id of the profile and version of its avatar; without them the initials show. */
    userId?: string | null
    avatarVersion?: string | null
}>()
</script>

<template>
    <div data-part="profile-summary">
        <ProfileAvatar
            :initials="initials"
            :color="color"
            :user-id="userId"
            :avatar-version="avatarVersion"
        />
        <span data-part="label">{{ name }}</span>
    </div>
</template>
