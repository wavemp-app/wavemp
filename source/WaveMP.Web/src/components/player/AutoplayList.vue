<script setup lang="ts">
import { computed, useId } from 'vue'
import { useI18n } from 'vue-i18n'
import BaseButton from '@/components/base/BaseButton.vue'
import LibraryCover from '@/components/library/LibraryCover.vue'
import ItemMenuButton from '@/components/menus/ItemMenuButton.vue'
import { useContextMenuTrigger } from '@/composables/useContextMenuTrigger'
import { useFormat } from '@/composables/useFormat'
import { artistLine, durationSeconds } from '@/library/presentation'
import type { Track } from '@/api/generated/models'
import type { AutoplayEntry } from '@/player/autoplay'
import { useContextMenuStore } from '@/stores/contextMenu'

/**
 * Autoplay below the queue: the songs that play after the list has ended, in their order. A song
 * plays at once when it is chosen (the songs before it are skipped), its button removes it, and
 * the button of the area turns autoplay off. The context menu of a song (right click, long press,
 * "More options" or the keyboard) is the menu of a song; "Play" plays it like choosing it.
 */
const props = defineProps<{
    entries: readonly AutoplayEntry[]
    loading: boolean
}>()

const emit = defineEmits<{
    play: [key: string]
    remove: [key: string]
    turnOff: []
}>()

const { t } = useI18n()
const format = useFormat()
const titleId = useId()
const hintId = useId()

const state = computed(() => {
    if (props.entries.length > 0) {
        return 'ready'
    }

    return props.loading ? 'loading' : 'empty'
})

const contextMenu = useContextMenuStore()

function openMenu(entry: AutoplayEntry, x: number, y: number): void {
    contextMenu.open(
        { type: 'song', tracks: [entry.track], playFrom: () => emit('play', entry.key) },
        x,
        y,
    )
}

const trigger = useContextMenuTrigger(openMenu)

function artistName(track: Track): string {
    return artistLine(track, t)
}

function duration(track: Track): string {
    const seconds = durationSeconds(track.durationMilliseconds)
    return seconds === null ? '' : format.duration(seconds)
}
</script>

<template>
    <section
        data-part="autoplay"
        :data-state="state"
        :aria-labelledby="titleId"
        :aria-describedby="hintId"
        :aria-busy="loading ? 'true' : undefined"
    >
        <h3 :id="titleId" data-part="section-title">{{ t('autoplay.title') }}</h3>
        <BaseButton
            data-item="turn-off-autoplay"
            icon="autoplay"
            :label="t('autoplay.turnOff')"
            @click="emit('turnOff')"
        />
        <p :id="hintId" data-part="autoplay-hint">{{ t('autoplay.hint') }}</p>
        <ol v-if="entries.length > 0" data-part="autoplay-list">
            <li
                v-for="entry in entries"
                :key="entry.key"
                data-part="autoplay-item"
                :data-state="entry.track.hasAudio === false ? 'video-only' : undefined"
                @contextmenu="trigger.onContextmenu($event, entry)"
                @keydown="trigger.onKeydown($event, entry)"
                @pointerdown="trigger.onPointerdown($event, entry)"
                @pointermove="trigger.onPointermove"
                @pointerup="trigger.cancel"
                @pointercancel="trigger.cancel"
                @pointerleave="trigger.cancel"
                @click.capture="trigger.onClickCapture"
            >
                <button
                    type="button"
                    data-part="autoplay-item-info"
                    :aria-label="t('autoplay.playItem', { title: entry.track.title ?? '' })"
                    @click="emit('play', entry.key)"
                >
                    <LibraryCover
                        kind="track"
                        :id="entry.track.id"
                        :available="entry.track.hasCover ?? false"
                    />
                    <span data-part="autoplay-item-title">{{ entry.track.title }}</span>
                    <span data-part="autoplay-item-artist">{{ artistName(entry.track) }}</span>
                </button>
                <span data-part="autoplay-item-duration">{{ duration(entry.track) }}</span>
                <div data-part="autoplay-item-actions" role="group">
                    <BaseButton
                        variant="icon"
                        icon="remove-from-queue"
                        data-item="remove"
                        :label="t('autoplay.remove')"
                        @click="emit('remove', entry.key)"
                    />
                    <ItemMenuButton @open="(x: number, y: number) => openMenu(entry, x, y)" />
                </div>
            </li>
        </ol>
        <p v-else data-part="autoplay-status" role="status">
            {{ loading ? t('autoplay.loading') : t('autoplay.empty') }}
        </p>
    </section>
</template>
