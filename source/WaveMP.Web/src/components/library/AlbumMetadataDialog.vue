<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseDialog from '@/components/base/BaseDialog.vue'
import BackButton from './BackButton.vue'
import CoverRuleFields from './CoverRuleFields.vue'

/**
 * Editor of an album for admins in a dialog: the exceptions of the album from the cover rules of
 * the library settings (see CoverRuleFields), that is what the album shows without a cover of its
 * own and the rule of its songs. Saving writes what changed and closes the dialog, also if nothing
 * changed.
 */
const props = defineProps<{
    albumId: string
}>()

const emit = defineEmits<{
    saved: []
}>()

const open = defineModel<boolean>('open', { default: false })

const { t } = useI18n()
const coverRule = ref<InstanceType<typeof CoverRuleFields> | null>(null)
const finishing = ref(false)

const canSave = computed(
    () => !finishing.value && !coverRule.value?.saving && coverRule.value?.state === 'ready',
)

async function submit(): Promise<void> {
    if (finishing.value || !coverRule.value) {
        return
    }

    finishing.value = true
    try {
        const changed = coverRule.value.dirty
        if (await coverRule.value.save()) {
            open.value = false
            if (changed) {
                emit('saved')
            }
        }
    } finally {
        finishing.value = false
    }
}
</script>

<template>
    <BaseDialog v-model:open="open" :title="t('albumMetadata.title')">
        <template #header-start>
            <BackButton variant="icon" :history="false" @close="open = false" />
        </template>
        <CoverRuleFields ref="coverRule" owner="album" :id="props.albumId" :active="open" />
        <template #footer>
            <BaseButton :label="t('actions.cancel')" @click="open = false" />
            <BaseButton
                variant="primary"
                :label="t('albumMetadata.save')"
                :disabled="!canSave"
                @click="submit"
            />
        </template>
    </BaseDialog>
</template>
