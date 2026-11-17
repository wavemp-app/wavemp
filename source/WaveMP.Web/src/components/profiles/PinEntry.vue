<script setup lang="ts">
import { ref, useId } from 'vue'
import { useI18n } from 'vue-i18n'
import PinPad from './PinPad.vue'
import ProfileAvatar from './ProfileAvatar.vue'
import type { ProfileTileData } from './ProfileTile.vue'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import { isCompletePin } from '@/profiles/pin'

/**
 * PIN entry of the profile selection for a profile with a PIN: the profile, the digit pad and a
 * way back to the profiles. The entered PIN is passed on and cleared, so a wrong one is not kept.
 */
const props = withDefaults(
    defineProps<{
        profile: ProfileTileData
        /** Message about a wrong PIN or a lockout. */
        error?: string | null
        busy?: boolean
    }>(),
    { error: null, busy: false },
)

const emit = defineEmits<{
    submit: [pin: string]
    cancel: []
}>()

const { t } = useI18n()
const titleId = useId()
const pin = ref('')

function submit(): void {
    if (props.busy || !isCompletePin(pin.value)) {
        return
    }

    const entered = pin.value
    pin.value = ''
    emit('submit', entered)
}
</script>

<template>
    <form
        data-part="pin-entry"
        :data-state="busy ? 'busy' : error ? 'error' : undefined"
        :aria-busy="busy ? 'true' : undefined"
        :aria-labelledby="titleId"
        @submit.prevent="submit"
    >
        <ProfileAvatar :initials="profile.initials" :color="profile.color" />
        <h2 :id="titleId" data-part="pin-entry-title">
            {{ t('pin.title', { name: profile.name }) }}
        </h2>
        <BaseAlert v-if="error" tone="error" :message="error" />
        <PinPad v-model="pin" :label="t('pin.field')" :disabled="busy" autofocus />
        <div data-part="pin-entry-actions">
            <BaseButton data-item="back" :label="t('pin.back')" @click="emit('cancel')" />
        </div>
    </form>
</template>
