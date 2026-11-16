<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { Backup } from '@/api/generated/models'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseDialog from '@/components/base/BaseDialog.vue'

/** Asks before an export of the database is deleted. */
defineProps<{
    backup: Backup | null
    busy?: boolean
}>()

const emit = defineEmits<{
    confirm: []
}>()

const open = defineModel<boolean>('open', { default: false })

const { t } = useI18n()
</script>

<template>
    <BaseDialog v-model:open="open" :title="t('adminBackups.deleteDialog.title')">
        <p>{{ t('adminBackups.deleteDialog.text', { name: backup?.fileName ?? '' }) }}</p>
        <template #footer>
            <BaseButton :label="t('actions.cancel')" @click="open = false" />
            <BaseButton
                variant="primary"
                :label="t('adminBackups.deleteDialog.confirm')"
                :disabled="busy"
                @click="emit('confirm')"
            />
        </template>
    </BaseDialog>
</template>
