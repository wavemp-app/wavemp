<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { UserListItem } from './UserList.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseDialog from '@/components/base/BaseDialog.vue'

/** Asks before the PIN of a profile is removed. */
defineProps<{
    user: UserListItem | null
    busy?: boolean
}>()

const emit = defineEmits<{
    confirm: []
}>()

const open = defineModel<boolean>('open', { default: false })

const { t } = useI18n()
</script>

<template>
    <BaseDialog v-model:open="open" :title="t('adminUsers.resetPinDialog.title')">
        <p>{{ t('adminUsers.resetPinDialog.text', { name: user?.name ?? '' }) }}</p>
        <template #footer>
            <BaseButton :label="t('actions.cancel')" @click="open = false" />
            <BaseButton
                variant="primary"
                data-item="confirm-reset-pin"
                :label="t('adminUsers.resetPinDialog.confirm')"
                :disabled="busy"
                @click="emit('confirm')"
            />
        </template>
    </BaseDialog>
</template>
