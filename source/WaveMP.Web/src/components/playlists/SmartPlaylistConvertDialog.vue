<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseDialog from '@/components/base/BaseDialog.vue'

/**
 * Asks before a smart playlist becomes a normal playlist: the playlist keeps the songs of now and
 * no longer follows the rules; the smart playlist is gone.
 */
withDefaults(
    defineProps<{
        smartPlaylist: { readonly name?: string | null } | null
        busy?: boolean
        error?: string | null
    }>(),
    { busy: false, error: null },
)

const emit = defineEmits<{
    confirm: []
}>()

const open = defineModel<boolean>('open', { default: false })

const { t } = useI18n()
</script>

<template>
    <BaseDialog v-model:open="open" :title="t('smartPlaylists.convertDialog.title')">
        <BaseAlert v-if="error" tone="error" :message="error" />
        <p>{{ t('smartPlaylists.convertDialog.text', { name: smartPlaylist?.name ?? '' }) }}</p>
        <template #footer>
            <BaseButton :label="t('actions.cancel')" @click="open = false" />
            <BaseButton
                data-item="confirm-convert"
                variant="primary"
                :label="t('smartPlaylists.convertDialog.confirm')"
                :disabled="busy"
                @click="emit('confirm')"
            />
        </template>
    </BaseDialog>
</template>
