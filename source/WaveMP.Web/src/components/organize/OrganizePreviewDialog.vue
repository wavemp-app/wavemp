<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import {
    OrganizeItemFilterObject,
    OrganizeOperationStatusObject,
    type OrganizeItem,
    type OrganizeItemFilter,
    type OrganizeOperation,
} from '@/api/generated/models'
import { problemCodeOf } from '@/api/problems'
import { organizeProblemKey, type BlockedReason } from '@/admin/organize'
import { listOrganizeItems } from '@/admin/organizeApi'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseDialog from '@/components/base/BaseDialog.vue'
import { useOrganizeStore } from '@/stores/organize'
import OrganizePreviewPanel from './OrganizePreviewPanel.vue'

/**
 * Shows an organize operation before anything moves (mode preview) or what a finished one changed
 * (mode changes): the numbers of songs, conflicts and warnings (organize-summary), the filter All,
 * Conflicts, Warnings as a segmented control, and the manifest of the items. A preview with
 * conflicts can only be applied with "Skip songs with conflicts" checked. "Move files" applies the
 * preview; while the media folder cannot be written or another run is busy, it stays disabled with
 * the reason. Cancel throws the preview away.
 */
const props = withDefaults(
    defineProps<{
        operation: OrganizeOperation
        mode?: 'preview' | 'changes'
        /** Why nothing can be organized now, or null. */
        blocked?: BlockedReason | null
    }>(),
    { mode: 'preview', blocked: null },
)

const emit = defineEmits<{
    applied: [operation: OrganizeOperation]
    discarded: []
}>()

const open = defineModel<boolean>('open', { default: false })

/** Items per page of the manifest. */
const pageSize = 100

const { t } = useI18n()
const organize = useOrganizeStore()

const filter = ref<OrganizeItemFilter>(OrganizeItemFilterObject.All)
const items = ref<OrganizeItem[]>([])
const total = ref(0)
const loading = ref(false)
const loadFailed = ref(false)
const skipConflicts = ref(false)
const applying = ref(false)
const error = ref<string | null>(null)
let request = 0
/** The preview was applied; closing the dialog then keeps it. */
let applied = false

const conflicts = computed(() => props.operation.conflictItems ?? 0)
const songs = computed(() => props.operation.totalItems ?? 0)
const isPreview = computed(() => props.mode === 'preview')
const expired = computed(() => props.operation.status === OrganizeOperationStatusObject.Expired)
const nothingToDo = computed(
    () => songs.value === 0 || (skipConflicts.value && conflicts.value >= songs.value),
)

/** Why "Move files" cannot be used, as message key; null when it can. */
const applyBlockedKey = computed(() => {
    if (props.blocked) {
        return `organize.blocked.${props.blocked}`
    }
    if (expired.value) {
        return 'organize.problems.previewExpired'
    }
    if (conflicts.value > 0 && !skipConflicts.value) {
        return 'organize.preview.conflictsBlock'
    }
    return null
})

const canApply = computed(
    () =>
        isPreview.value &&
        props.operation.status === OrganizeOperationStatusObject.Previewed &&
        !applyBlockedKey.value &&
        !nothingToDo.value &&
        !applying.value,
)

async function load(reset = true): Promise<void> {
    const id = props.operation.id
    if (!id) {
        return
    }

    const own = ++request
    loading.value = true
    loadFailed.value = false
    try {
        const page = await listOrganizeItems(id, {
            filter: filter.value,
            offset: reset ? 0 : items.value.length,
            limit: pageSize,
        })
        if (own !== request) {
            return
        }

        items.value = reset ? (page?.items ?? []) : [...items.value, ...(page?.items ?? [])]
        total.value = page?.total ?? items.value.length
    } catch {
        if (own === request) {
            loadFailed.value = true
        }
    } finally {
        if (own === request) {
            loading.value = false
        }
    }
}

watch(
    () => [open.value, props.operation.id] as const,
    ([isOpen]) => {
        if (isOpen) {
            filter.value = OrganizeItemFilterObject.All
            skipConflicts.value = false
            error.value = null
            applied = false
            void load()
        }
    },
    { immediate: true },
)

watch(filter, () => void load())

async function apply(): Promise<void> {
    const id = props.operation.id
    if (!id || !canApply.value) {
        return
    }

    applying.value = true
    error.value = null
    try {
        const operation = await organize.apply(id, skipConflicts.value)
        applied = true
        open.value = false
        if (operation) {
            emit('applied', operation)
        }
    } catch (failure) {
        error.value = t(organizeProblemKey(problemCodeOf(failure), 'organize.problems.applyFailed'))
    } finally {
        applying.value = false
    }
}

/** Closes the dialog; a preview that was not applied is thrown away. */
function cancel(): void {
    open.value = false
}

watch(open, async (isOpen, wasOpen) => {
    const id = props.operation.id
    if (
        !isOpen &&
        wasOpen &&
        isPreview.value &&
        id &&
        !applying.value &&
        !applied &&
        props.operation.status === OrganizeOperationStatusObject.Previewed
    ) {
        await organize.discard(id).catch(() => {})
        emit('discarded')
    }
})
</script>

<template>
    <BaseDialog
        v-model:open="open"
        data-item="organize-preview"
        :title="isPreview ? t('organize.preview.title') : t('organize.preview.changesTitle')"
    >
        <OrganizePreviewPanel
            v-model:filter="filter"
            v-model:skip-conflicts="skipConflicts"
            :operation="operation"
            :items="items"
            :total="total"
            :mode="mode"
            :loading="loading"
            :load-failed="loadFailed"
            :disabled="applying"
            @more="load(false)"
        />
        <BaseAlert v-if="isPreview && applyBlockedKey" tone="info" :message="t(applyBlockedKey)" />
        <BaseAlert v-if="error" tone="error" :message="error" />
        <template #footer>
            <template v-if="isPreview">
                <BaseButton data-item="cancel" :label="t('actions.cancel')" @click="cancel" />
                <BaseButton
                    data-item="apply"
                    variant="primary"
                    icon="organize-apply"
                    :label="t('organize.preview.apply')"
                    :disabled="!canApply"
                    @click="apply"
                />
            </template>
            <BaseButton v-else data-item="close" :label="t('dialog.close')" @click="open = false" />
        </template>
    </BaseDialog>
</template>
