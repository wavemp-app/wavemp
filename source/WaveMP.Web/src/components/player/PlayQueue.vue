<script setup lang="ts">
import { computed, ref, useId } from 'vue'
import { useI18n } from 'vue-i18n'
import BaseButton from '@/components/base/BaseButton.vue'
import LibraryCover from '@/components/library/LibraryCover.vue'
import ItemMenuButton from '@/components/menus/ItemMenuButton.vue'
import { useContextMenuTrigger } from '@/composables/useContextMenuTrigger'
import { useFormat } from '@/composables/useFormat'
import { useKeepInView } from '@/composables/useKeepInView'
import { artistLabelKey, durationSeconds } from '@/library/presentation'
import type { Track } from '@/api/generated/models'
import { visibleQueueEntries, type QueueItem } from '@/player/queue'
import { useContextMenuStore } from '@/stores/contextMenu'

/**
 * The queue: the last played entries, the current one and all that follow, each with its cover.
 * Every entry plays when it is chosen, and its buttons move it up and down or remove it. The
 * current entry cannot be removed, and clearing keeps it. The context menu of an entry (right
 * click, long press, the button "More options" or the keyboard) also plays it next. Where the
 * queue scrolls itself (beside the stage on the desktop), it keeps the current entry in view.
 * Entries autoplay added carry the state autoplay; the songs autoplay plays next follow in the slot
 * below the list.
 */
const props = defineProps<{
    items: readonly QueueItem[]
    currentIndex: number
}>()

const emit = defineEmits<{
    play: [key: string]
    move: [key: string, offset: -1 | 1]
    remove: [key: string]
    clear: []
}>()

const { t } = useI18n()
const format = useFormat()
const titleId = useId()
const queue = ref<HTMLElement | null>(null)

const entries = computed(() => visibleQueueEntries(props.items, props.currentIndex))
const firstNumber = computed(() => (entries.value[0]?.index ?? 0) + 1)

useKeepInView(queue, '[data-part="queue-item"][aria-current="true"]', () => [
    props.currentIndex,
    props.items[props.currentIndex]?.key,
])

const contextMenu = useContextMenuStore()

function openMenu(entry: { item: QueueItem; index: number }, x: number, y: number): void {
    contextMenu.open(
        {
            type: 'queue-entry',
            key: entry.item.key,
            track: entry.item.track,
            index: entry.index,
            count: props.items.length,
            currentIndex: props.currentIndex,
        },
        x,
        y,
    )
}

const trigger = useContextMenuTrigger(openMenu)

/** The data-state tokens of an entry: current or played, and autoplay for songs autoplay added. */
function stateOf(item: QueueItem, state: 'played' | 'current' | undefined): string | undefined {
    return [state, item.autoplay ? 'autoplay' : undefined].filter(Boolean).join(' ') || undefined
}

function artistName(track: Track): string {
    const key = artistLabelKey(track.artist)
    return key ? t(key) : (track.artist?.name ?? '')
}

function duration(track: Track): string {
    const seconds = durationSeconds(track.durationMilliseconds)
    return seconds === null ? '' : format.duration(seconds)
}
</script>

<template>
    <section ref="queue" data-part="queue" :aria-labelledby="titleId">
        <h2 :id="titleId" data-part="section-title">{{ t('nowPlaying.queue') }}</h2>
        <BaseButton
            data-item="clear-queue"
            icon="clear-queue"
            :label="t('nowPlaying.clear')"
            :disabled="items.length <= 1"
            @click="emit('clear')"
        />
        <ol data-part="queue-list" :start="firstNumber">
            <li
                v-for="{ item, index, state } in entries"
                :key="item.key"
                data-part="queue-item"
                :data-state="stateOf(item, state)"
                :aria-current="state === 'current' ? 'true' : undefined"
                @contextmenu="trigger.onContextmenu($event, { item, index })"
                @keydown="trigger.onKeydown($event, { item, index })"
                @pointerdown="trigger.onPointerdown($event, { item, index })"
                @pointermove="trigger.onPointermove"
                @pointerup="trigger.cancel"
                @pointercancel="trigger.cancel"
                @pointerleave="trigger.cancel"
                @click.capture="trigger.onClickCapture"
            >
                <button
                    type="button"
                    data-part="queue-item-info"
                    :aria-label="t('nowPlaying.playItem', { title: item.track.title ?? '' })"
                    @click="emit('play', item.key)"
                >
                    <LibraryCover
                        kind="track"
                        :id="item.track.id"
                        :available="item.track.hasCover ?? false"
                    />
                    <span data-part="queue-item-title">{{ item.track.title }}</span>
                    <span data-part="queue-item-artist">{{ artistName(item.track) }}</span>
                </button>
                <span data-part="queue-item-duration">{{ duration(item.track) }}</span>
                <div data-part="queue-item-actions" role="group">
                    <BaseButton
                        variant="icon"
                        icon="move-up"
                        data-item="move-up"
                        :label="t('nowPlaying.moveUp')"
                        :disabled="index === 0"
                        @click="emit('move', item.key, -1)"
                    />
                    <BaseButton
                        variant="icon"
                        icon="move-down"
                        data-item="move-down"
                        :label="t('nowPlaying.moveDown')"
                        :disabled="index === items.length - 1"
                        @click="emit('move', item.key, 1)"
                    />
                    <BaseButton
                        variant="icon"
                        icon="remove-from-queue"
                        data-item="remove"
                        :label="t('nowPlaying.remove')"
                        :disabled="index === currentIndex"
                        @click="emit('remove', item.key)"
                    />
                    <ItemMenuButton
                        @open="(x: number, y: number) => openMenu({ item, index }, x, y)"
                    />
                </div>
            </li>
        </ol>
        <slot />
    </section>
</template>
