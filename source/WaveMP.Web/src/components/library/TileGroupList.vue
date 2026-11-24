<script setup lang="ts">
import { useId } from 'vue'
import { useI18n } from 'vue-i18n'
import TileList, { type Tile } from './TileList.vue'
import BaseIcon from '@/components/base/BaseIcon.vue'
import { useFormat } from '@/composables/useFormat'
import { otherIndexLetter } from '@/library/indexLetters'
import type { TileGroup } from '@/library/tileGrouping'

/**
 * Tiles in groups, each a section with a heading and the tiles of the group: albums by album
 * artist, letter or year, artists by letter. Each group carries the position of its first entry
 * in the whole list (data-position), also when the list is loaded from the middle. With
 * groupJump (at least two groups) the headings are buttons that open the overview of the groups.
 */
withDefaults(
    defineProps<{
        groups: readonly TileGroup<Tile>[]
        /** Accessible name of all groups together. */
        label: string
        /** data-state tokens of the groups, e.g. "zoomed-out" while the overview is open. */
        state?: string
        /** The headings open the overview of the groups (btn-group-jump). */
        groupJump?: boolean
        /** Whether the overview of the groups is open. */
        groupJumpOpen?: boolean
    }>(),
    { state: undefined, groupJump: false, groupJumpOpen: false },
)

const emit = defineEmits<{
    /** The heading of a group was chosen: open the overview of the groups. */
    'group-jump': [position: number, opener: HTMLElement]
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
    <div data-part="tile-groups" role="group" :aria-label="label" :data-state="state">
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
                    :aria-label="groupJump ? undefined : accessibleTitle(group)"
                >
                    <button
                        v-if="groupJump"
                        type="button"
                        data-part="btn-group-jump"
                        aria-haspopup="dialog"
                        :aria-expanded="groupJumpOpen ? 'true' : 'false'"
                        :aria-label="accessibleTitle(group)"
                        :title="t('list.groupJump')"
                        @click="
                            emit('group-jump', group.position, $event.currentTarget as HTMLElement)
                        "
                    >
                        <span data-part="label">{{ title(group) }}</span>
                        <BaseIcon name="group-jump" />
                    </button>
                    <template v-else>{{ title(group) }}</template>
                </h2>
            </div>
            <TileList :tiles="group.items" :label="accessibleTitle(group) ?? title(group)" />
        </section>
    </div>
</template>
