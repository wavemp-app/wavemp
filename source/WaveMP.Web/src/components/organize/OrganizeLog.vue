<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import {
    OrganizeOperationKindObject,
    OrganizeOperationStatusObject,
    type OrganizeOperation,
} from '@/api/generated/models'
import { operationErrorKey, operationState, operationStatusKey } from '@/admin/organize'
import { runDateTimeFormat } from '@/admin/presentation'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseIcon from '@/components/base/BaseIcon.vue'
import { useFormat } from '@/composables/useFormat'
import type { IconSlotName } from '@/themes/iconSlots'

/**
 * The log of the organization: every preview, run and undo, newest first (organize-entry with the
 * status in data-state and the kind in data-kind). Each entry names what it organized, when, its
 * status as badge and its numbers; "Show changes" lists its items, "Open preview" opens a preview
 * that can still be applied, and "Undo" computes the preview that puts everything back.
 */
defineProps<{
    operations: readonly OrganizeOperation[]
    hasMore: boolean
    loading: boolean
    /** Whether undoing is possible now (the media folders can be written, nothing runs). */
    canUndo: boolean
}>()

const emit = defineEmits<{
    show: [operation: OrganizeOperation]
    undo: [operation: OrganizeOperation]
    more: []
}>()

const { t } = useI18n()
const format = useFormat()

const kindIcons: Readonly<Record<string, IconSlotName>> = {
    [OrganizeOperationKindObject.OwnAlbum]: 'own-album',
    [OrganizeOperationKindObject.Library]: 'admin-organize',
    [OrganizeOperationKindObject.Undo]: 'organize-undo',
}

function title(operation: OrganizeOperation): string {
    if (operation.kind === OrganizeOperationKindObject.Undo) {
        return t('organize.log.undoOf', { title: operation.displayTitle ?? '' })
    }
    return operation.displayTitle || t(`organize.log.kinds.${kindKey(operation.kind)}`)
}

function kindKey(kind: string | null | undefined): string {
    return kind === OrganizeOperationKindObject.OwnAlbum
        ? 'ownAlbum'
        : kind === OrganizeOperationKindObject.Undo
          ? 'undo'
          : 'library'
}

function isOpenPreview(operation: OrganizeOperation): boolean {
    return operation.status === OrganizeOperationStatusObject.Previewed
}

function hasChanges(operation: OrganizeOperation): boolean {
    return operation.status !== OrganizeOperationStatusObject.Planning
}
</script>

<template>
    <div v-if="operations.length === 0 && !loading" data-part="empty-state">
        <p data-part="empty-state-text">{{ t('organize.log.empty') }}</p>
    </div>
    <ul
        v-else
        data-part="organize-log"
        :aria-label="t('organize.log.label')"
        :aria-busy="loading ? 'true' : undefined"
    >
        <li
            v-for="operation in operations"
            :key="operation.id ?? ''"
            data-part="organize-entry"
            :data-kind="kindKey(operation.kind)"
            :data-state="
                [
                    operationState(operation.status),
                    ...(operation.undoneById ? ['undone'] : []),
                ].join(' ')
            "
        >
            <BaseIcon :name="kindIcons[operation.kind ?? ''] ?? 'admin-organize'" />
            <div data-part="organize-entry-info">
                <span data-part="organize-entry-title">{{ title(operation) }}</span>
                <span data-part="organize-entry-meta">
                    <span
                        data-part="badge"
                        :data-state="`status ${operationState(operation.status)}`"
                    >
                        <span data-part="label">{{ t(operationStatusKey(operation.status)) }}</span>
                    </span>
                    <span v-if="operation.undoneById" data-part="badge" data-state="undone">
                        <span data-part="label">{{ t('organize.log.undone') }}</span>
                    </span>
                    <span v-if="operation.createdAt" data-part="label" data-field="created-at">
                        {{ format.date(operation.createdAt, runDateTimeFormat) }}
                    </span>
                    <span data-part="label" data-field="songs">
                        {{
                            t('organize.log.songs', {
                                count: format.number(operation.totalItems ?? 0),
                            })
                        }}
                    </span>
                    <span v-if="(operation.doneItems ?? 0) > 0" data-part="label" data-field="done">
                        {{
                            t('organize.log.done', {
                                count: format.number(operation.doneItems ?? 0),
                            })
                        }}
                    </span>
                    <span
                        v-if="(operation.conflictItems ?? 0) > 0"
                        data-part="label"
                        data-field="conflicts"
                    >
                        {{
                            t('organize.log.conflicts', {
                                count: format.number(operation.conflictItems ?? 0),
                            })
                        }}
                    </span>
                </span>
                <span v-if="operation.errorCode" data-part="label" data-field="error">
                    {{ t(operationErrorKey(operation.errorCode) ?? '') }}
                </span>
            </div>
            <div
                data-part="organize-entry-actions"
                role="group"
                :aria-label="t('organize.log.actions', { title: title(operation) })"
            >
                <BaseButton
                    v-if="isOpenPreview(operation)"
                    data-item="open-preview"
                    icon="organize-preview"
                    :label="t('organize.log.openPreview')"
                    @click="emit('show', operation)"
                />
                <BaseButton
                    v-else-if="hasChanges(operation)"
                    data-item="show-changes"
                    icon="info"
                    :label="t('organize.log.showChanges')"
                    @click="emit('show', operation)"
                />
                <BaseButton
                    v-if="operation.canUndo"
                    data-item="undo"
                    icon="organize-undo"
                    :label="t('organize.log.undo')"
                    :disabled="!canUndo"
                    @click="emit('undo', operation)"
                />
            </div>
        </li>
    </ul>
    <div v-if="hasMore" data-part="view-actions">
        <BaseButton
            data-item="more"
            :label="t('organize.log.more')"
            :disabled="loading"
            @click="emit('more')"
        />
    </div>
</template>
