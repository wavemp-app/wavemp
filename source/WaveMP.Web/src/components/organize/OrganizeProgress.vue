<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { OrganizeOperationStatusObject, type OrganizeOperation } from '@/api/generated/models'
import { operationProgress, operationState, operationStatusKey } from '@/admin/organize'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseProgress from '@/components/base/BaseProgress.vue'
import { useFormat } from '@/composables/useFormat'

/**
 * Progress of an organize operation that computes its preview or moves files: its status, the
 * number of songs done of all, the progress bar and the button Stop (the run stops after the song
 * it is moving; what is done stays and can be undone).
 */
const props = defineProps<{
    operation: OrganizeOperation
}>()

const emit = defineEmits<{
    stop: []
}>()

const { t } = useI18n()
const format = useFormat()
const stopping = ref(false)

watch(
    () => props.operation.id,
    () => {
        stopping.value = false
    },
)

const planning = computed(() => props.operation.status === OrganizeOperationStatusObject.Planning)
const done = computed(
    () =>
        (props.operation.doneItems ?? 0) +
        (props.operation.skippedItems ?? 0) +
        (props.operation.failedItems ?? 0),
)
const total = computed(() => props.operation.totalItems ?? 0)
const valueText = computed(() =>
    total.value > 0
        ? t(planning.value ? 'organize.progress.planned' : 'organize.progress.moved', {
              done: format.number(done.value),
              total: format.number(total.value),
          })
        : t(operationStatusKey(props.operation.status)),
)

function stop(): void {
    stopping.value = true
    emit('stop')
}
</script>

<template>
    <div
        data-part="organize-progress"
        :data-state="
            [operationState(operation.status), ...(stopping ? ['stopping'] : [])].join(' ')
        "
        :data-kind="operation.kind ?? undefined"
    >
        <p data-part="label" data-field="status">
            {{
                stopping ? t('organize.progress.stopping') : t(operationStatusKey(operation.status))
            }}
        </p>
        <BaseProgress
            :label="t('organize.progress.label')"
            :value="operationProgress(operation)"
            :value-text="valueText"
        />
        <p v-if="total > 0" data-part="label" data-field="count">{{ valueText }}</p>
        <div data-part="view-actions">
            <BaseButton
                data-item="stop"
                icon="organize-stop"
                :label="t('organize.progress.stop')"
                :disabled="stopping"
                @click="stop"
            />
        </div>
    </div>
</template>
