<script lang="ts">
import type { RouteLocationRaw } from 'vue-router'
import type { PlaylistCover as PlaylistCoverModel } from '@/api/generated/models'
import type { MenuTarget } from '@/menus/targets'
import type { IconSlotName } from '@/themes/iconSlots'

export type TileKind =
    | 'album'
    | 'artist'
    | 'track'
    | 'genre'
    | 'year'
    | 'playlist'
    | 'smart-playlist'
    | 'recently-played'

export interface Tile {
    readonly id: string
    readonly kind: TileKind
    readonly title: string
    readonly subtitle?: string
    readonly to: RouteLocationRaw
    /** Cover of an album or a track; tiles without cover show the icon of their kind. */
    readonly cover?: {
        readonly kind?: 'album' | 'track'
        readonly id: string
        readonly available: boolean
    }
    /** Cover of a playlist: its own picture or the covers of some of its tracks. */
    readonly playlistCover?: {
        readonly id: string
        readonly cover: PlaylistCoverModel | null | undefined
    }
    /** The element of the context menu of the tile (song, album, artist or playlist). */
    readonly menu?: MenuTarget
    /** Icon slot of a small badge on the cover, e.g. the symbol of smart playlists. */
    readonly badge?: IconSlotName
}
</script>

<script setup lang="ts">
import BaseCover from '@/components/base/BaseCover.vue'
import BaseIcon from '@/components/base/BaseIcon.vue'
import BaseLink from '@/components/base/BaseLink.vue'
import LibraryCover from './LibraryCover.vue'
import PlaylistCover from '@/components/playlists/PlaylistCover.vue'
import ItemMenuButton from '@/components/menus/ItemMenuButton.vue'
import { useContextMenuTrigger } from '@/composables/useContextMenuTrigger'
import { useContextMenuStore } from '@/stores/contextMenu'

/**
 * Tiles of albums, artists, songs, genres, years or playlists that lead to their pages. Tiles with
 * a menu open it with a right click, a long press, the button "More options" or the keyboard (menu
 * key or Shift+F10 on the tile).
 */
defineProps<{
    tiles: readonly Tile[]
    /** Accessible name of the list. */
    label: string
}>()

const contextMenu = useContextMenuStore()
const trigger = useContextMenuTrigger<Tile>((tile, x, y) => {
    if (tile.menu) {
        contextMenu.open(tile.menu, x, y)
    }
})

function onContextmenu(event: MouseEvent, tile: Tile): void {
    if (tile.menu) {
        trigger.onContextmenu(event, tile)
    }
}

function onKeydown(event: KeyboardEvent, tile: Tile): void {
    if (tile.menu) {
        trigger.onKeydown(event, tile)
    }
}

function onPointerdown(event: PointerEvent, tile: Tile): void {
    if (tile.menu) {
        trigger.onPointerdown(event, tile)
    }
}
</script>

<template>
    <ul data-part="tile-list" :aria-label="label">
        <li
            v-for="tile in tiles"
            :key="tile.id"
            data-part="tile-item"
            @contextmenu="onContextmenu($event, tile)"
            @keydown="onKeydown($event, tile)"
            @pointerdown="onPointerdown($event, tile)"
            @pointermove="trigger.onPointermove"
            @pointerup="trigger.cancel"
            @pointercancel="trigger.cancel"
            @pointerleave="trigger.cancel"
            @click.capture="trigger.onClickCapture"
        >
            <BaseLink part="tile" :to="tile.to" :data-kind="tile.kind">
                <LibraryCover
                    v-if="tile.cover"
                    :kind="tile.cover.kind ?? 'album'"
                    :id="tile.cover.id"
                    :available="tile.cover.available"
                />
                <PlaylistCover
                    v-else-if="tile.playlistCover"
                    :id="tile.playlistCover.id"
                    :cover="tile.playlistCover.cover"
                    :icon="tile.kind === 'smart-playlist' ? 'smart-playlist' : 'playlist'"
                />
                <BaseCover v-else :placeholder="false">
                    <BaseIcon :name="tile.kind" />
                </BaseCover>
                <span v-if="tile.badge" data-part="tile-badge">
                    <BaseIcon :name="tile.badge" />
                </span>
                <span data-part="tile-title">{{ tile.title }}</span>
                <span v-if="tile.subtitle" data-part="tile-subtitle">{{ tile.subtitle }}</span>
            </BaseLink>
            <ItemMenuButton
                v-if="tile.menu"
                @open="(x: number, y: number) => contextMenu.open(tile.menu!, x, y)"
            />
        </li>
    </ul>
</template>
