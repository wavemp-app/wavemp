<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseDialog from '@/components/base/BaseDialog.vue'

/** Asks before a playlist or a smart playlist is deleted; its tracks stay in the library. */
const props = withDefaults(
    defineProps<{
        playlist: { readonly name?: string | null } | null
        /** Which texts the dialog shows. */
        kind?: 'playlist' | 'smart-playlist'
        busy?: boolean
        error?: string | null
    }>(),
    { kind: 'playlist', busy: false, error: null },
)

const emit = defineEmits<{
    confirm: []
}>()

const open = defineModel<boolean>('open', { default: false })

const { t } = useI18n()
const keys = computed(() =>
    props.kind === 'smart-playlist' ? 'smartPlaylists.deleteDialog' : 'playlists.deleteDialog',
)
</script>

<template>
    <BaseDialog v-model:open="open" :title="t(`${keys}.title`)">
        <BaseAlert v-if="error" tone="error" :message="error" />
        <p>{{ t(`${keys}.text`, { name: playlist?.name ?? '' }) }}</p>
        <template #footer>
            <BaseButton :label="t('actions.cancel')" @click="open = false" />
            <BaseButton
                variant="primary"
                :label="t(`${keys}.confirm`)"
                :disabled="busy"
                @click="emit('confirm')"
            />
        </template>
    </BaseDialog>
</template>
