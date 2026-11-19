<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { MediaSource } from '@/api/generated/models'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseDialog from '@/components/base/BaseDialog.vue'
import { useFormat } from '@/composables/useFormat'

/** Asks before a media folder is removed from the library together with its songs. */
defineProps<{
    source: MediaSource | null
    busy?: boolean
}>()

const emit = defineEmits<{
    confirm: []
}>()

const open = defineModel<boolean>('open', { default: false })

const { t } = useI18n()
const format = useFormat()
</script>

<template>
    <BaseDialog v-model:open="open" :title="t('adminSources.deleteDialog.title')">
        <p>
            {{
                t('adminSources.deleteDialog.text', {
                    name: source?.name ?? '',
                    count: format.number(source?.trackCount ?? 0),
                })
            }}
        </p>
        <p>{{ t('adminSources.deleteDialog.filesStay') }}</p>
        <template #footer>
            <BaseButton :label="t('actions.cancel')" @click="open = false" />
            <BaseButton
                variant="primary"
                :label="t('adminSources.deleteDialog.confirm')"
                :disabled="busy"
                @click="emit('confirm')"
            />
        </template>
    </BaseDialog>
</template>
