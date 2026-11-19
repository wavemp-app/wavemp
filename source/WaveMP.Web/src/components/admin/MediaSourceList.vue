<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { MediaSource } from '@/api/generated/models'
import BaseButton from '@/components/base/BaseButton.vue'
import { useFormat } from '@/composables/useFormat'
import { mediaSourceStates } from '@/admin/presentation'

/**
 * The media folders of the library with their path, state and size, and the actions to edit,
 * switch on or off and remove them. The states are tokens of data-state on the item (active or
 * inactive, unavailable, watched, media-path), so themes can mark them.
 */
defineProps<{
    sources: readonly MediaSource[]
    /** Id of the folder whose change is being saved; its buttons are disabled meanwhile. */
    busyId?: string | null
}>()

const emit = defineEmits<{
    edit: [source: MediaSource]
    toggle: [source: MediaSource]
    delete: [source: MediaSource]
}>()

const { t } = useI18n()
const format = useFormat()
</script>

<template>
    <ul data-part="source-list" :aria-label="t('adminSources.list')">
        <li
            v-for="source in sources"
            :key="source.id ?? ''"
            data-part="source-item"
            :data-state="mediaSourceStates(source).join(' ')"
        >
            <div data-part="source-info">
                <span data-part="source-name">{{ source.name }}</span>
                <span data-part="source-path" translate="no">{{ source.path }}</span>
                <span data-part="source-meta">
                    <span data-part="badge" :data-state="source.isActive ? 'active' : 'inactive'">
                        <span data-part="label">{{
                            source.isActive
                                ? t('adminSources.states.active')
                                : t('adminSources.states.inactive')
                        }}</span>
                    </span>
                    <span v-if="!source.isAvailable" data-part="badge" data-state="unavailable">
                        <span data-part="label">{{ t('adminSources.states.unavailable') }}</span>
                    </span>
                    <span v-if="source.isWatched" data-part="badge" data-state="watched">
                        <span data-part="label">{{ t('adminSources.states.watched') }}</span>
                    </span>
                    <span v-if="source.followsMediaPath" data-part="badge" data-state="media-path">
                        <span data-part="label">{{ t('adminSources.states.mediaPath') }}</span>
                    </span>
                    <span data-part="label">{{
                        t('adminSources.counts', {
                            tracks: format.number(source.trackCount ?? 0),
                            files: format.number(source.fileCount ?? 0),
                        })
                    }}</span>
                </span>
            </div>
            <div data-part="source-actions">
                <BaseButton
                    data-item="toggle"
                    :label="
                        source.isActive ? t('adminSources.deactivate') : t('adminSources.activate')
                    "
                    :aria-label="
                        source.isActive
                            ? t('adminSources.deactivateNamed', { name: source.name })
                            : t('adminSources.activateNamed', { name: source.name })
                    "
                    :disabled="busyId === source.id"
                    @click="emit('toggle', source)"
                />
                <BaseButton
                    variant="icon"
                    icon="edit"
                    data-item="edit"
                    :label="t('adminSources.edit', { name: source.name })"
                    :disabled="busyId === source.id"
                    @click="emit('edit', source)"
                />
                <BaseButton
                    variant="icon"
                    icon="delete"
                    data-item="delete"
                    :label="t('adminSources.delete', { name: source.name })"
                    :disabled="busyId === source.id"
                    @click="emit('delete', source)"
                />
            </div>
        </li>
    </ul>
</template>
