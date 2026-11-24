<script setup lang="ts">
import { useId } from 'vue'
import { useI18n } from 'vue-i18n'
import TileList, { type Tile } from './TileList.vue'
import { useFormat } from '@/composables/useFormat'
import { otherIndexLetter } from '@/library/indexLetters'
import type { TileGroup } from '@/library/tileGrouping'

/**
 * Tiles in groups, each a section with a heading and the tiles of the group: albums by album
 * artist, letter or year, artists by letter. Each group carries the position of its first entry
 * in the whole list (data-position), also when the list is loaded from the middle.
 */
defineProps<{
    groups: readonly TileGroup<Tile>[]
    /** Accessible name of all groups together. */
    label: string
}>()

const { t } = useI18n()
const format = useFormat()
const listId = useId()

function titleId(group: TileGroup<Tile>): string {
    return `${listId}-group-${group.id}`
}

function title(group: TileGroup<Tile>): string {
    switch (group.kind) {
        case 'artist':
            return group.artist?.name || t('library.unknownArtist')
        case 'year':
            return group.year === undefined ? t('library.unknownYear') : format.year(group.year)
        default:
            return group.letter ?? otherIndexLetter
    }
}

/** "#" is read out as what it stands for. */
function accessibleTitle(group: TileGroup<Tile>): string | undefined {
    return group.kind === 'letter' && group.letter === otherIndexLetter
        ? t('library.otherLetter')
        : undefined
}
</script>

<template>
    <div data-part="tile-groups" role="group" :aria-label="label">
        <section
            v-for="group in groups"
            :key="group.id"
            data-part="tile-group"
            :data-kind="group.kind"
            :data-position="group.position"
            :aria-labelledby="titleId(group)"
        >
            <div data-part="tile-group-header">
                <h2
                    :id="titleId(group)"
                    data-part="tile-group-title"
                    :aria-label="accessibleTitle(group)"
                >
                    {{ title(group) }}
                </h2>
            </div>
            <TileList :tiles="group.items" :label="accessibleTitle(group) ?? title(group)" />
        </section>
    </div>
</template>
