<script setup lang="ts">
import { computed, nextTick, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { Track } from '@/api/generated/models'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseIcon from '@/components/base/BaseIcon.vue'
import { useFormat } from '@/composables/useFormat'
import { useSortableList } from '@/composables/useSortableList'

/**
 * The songs of an own album in their order; the position is the track number the songs get. Each
 * row (own-album-track) has a drag handle, the buttons "Move up" and "Move down" (also Alt+Arrow up
 * and down on a row) and "Remove". The list changes nothing itself: it reports the moves and
 * removals, and the editor saves them.
 */
const props = withDefaults(
    defineProps<{
        tracks: readonly Track[]
        disabled?: boolean
    }>(),
    { disabled: false },
)

const emit = defineEmits<{
    move: [from: number, to: number]
    remove: [index: number]
}>()

const { t } = useI18n()
const format = useFormat()
const list = ref<HTMLElement | null>(null)

useSortableList(list, {
    draggable: '[data-part="own-album-track"]',
    handle: '[data-part="drag-handle"]',
    disabled: computed(() => props.disabled),
    onMove: (from, to) => emit('move', from, to),
})

function context(track: Track): string {
    const artist = track.artist?.name ?? ''
    const album = track.album?.title ?? ''
    return artist && album ? t('ownAlbums.tracks.context', { artist, album }) : artist || album
}

async function move(index: number, offset: -1 | 1): Promise<void> {
    const target = index + offset
    if (props.disabled || target < 0 || target >= props.tracks.length) {
        return
    }

    emit('move', index, target)
    // The focus follows the row to its new place.
    await nextTick()
    const row = list.value?.querySelectorAll<HTMLElement>('[data-part="own-album-track"]')[target]
    const preferred = offset < 0 ? 'move-up' : 'move-down'
    const button =
        row?.querySelector<HTMLElement>(`[data-item="${preferred}"]:not(:disabled)`) ??
        row?.querySelector<HTMLElement>('[data-item="remove"]')
    button?.focus()
}

function onKeydown(event: KeyboardEvent, index: number): void {
    if (!event.altKey || (event.key !== 'ArrowUp' && event.key !== 'ArrowDown')) {
        return
    }

    event.preventDefault()
    void move(index, event.key === 'ArrowUp' ? -1 : 1)
}
</script>

<template>
    <ol
        ref="list"
        data-part="own-album-tracks"
        :aria-label="t('ownAlbums.tracks.label')"
        :aria-disabled="disabled ? 'true' : undefined"
    >
        <li
            v-for="(track, index) in tracks"
            :key="track.id ?? index"
            data-part="own-album-track"
            :data-state="track.hasAudio ? undefined : 'no-audio'"
            @keydown="onKeydown($event, index)"
        >
            <span data-part="drag-handle" :title="t('ownAlbums.tracks.dragHandle')">
                <BaseIcon name="drag-handle" />
                <span data-part="label">{{ t('ownAlbums.tracks.dragHandle') }}</span>
            </span>
            <span data-part="own-album-track-number">{{ format.number(index + 1) }}</span>
            <span data-part="own-album-track-info">
                <span data-part="label" data-field="title">{{ track.title }}</span>
                <span data-part="label" data-field="context">{{ context(track) }}</span>
            </span>
            <span
                data-part="own-album-track-actions"
                role="group"
                :aria-label="t('ownAlbums.tracks.actions', { title: track.title ?? '' })"
            >
                <BaseButton
                    data-item="move-up"
                    variant="icon"
                    icon="move-up"
                    :label="t('ownAlbums.tracks.moveUp')"
                    :disabled="disabled || index === 0"
                    @click="move(index, -1)"
                />
                <BaseButton
                    data-item="move-down"
                    variant="icon"
                    icon="move-down"
                    :label="t('ownAlbums.tracks.moveDown')"
                    :disabled="disabled || index === tracks.length - 1"
                    @click="move(index, 1)"
                />
                <BaseButton
                    data-item="remove"
                    variant="icon"
                    icon="remove-from-playlist"
                    :label="t('ownAlbums.tracks.remove')"
                    :disabled="disabled"
                    @click="emit('remove', index)"
                />
            </span>
        </li>
    </ol>
</template>
