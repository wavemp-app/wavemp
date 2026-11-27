<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { ArtistCredit } from '@/api/generated/models'
import BaseLink from '@/components/base/BaseLink.vue'
import { artistParts } from '@/library/artists'

/**
 * The artists of a song or album, each one on its own and, with an id, a link to its page: the
 * main artists first, then the featured ones, e.g. "Simon & Garfunkel, AC/DC feat. K.Flay". The
 * component has no element of its own: the links (part link, or artist-link in lists and the
 * player) and the artist-separators between them stand directly in the element around it, so
 * a theme styles them like the other links there. Without artists it shows the fallback text.
 * In the rows of a list (in-row) the links are no tab stops and a click on them does not choose
 * or play the row; the menu of the row leads to the artist as well.
 */
const props = withDefaults(
    defineProps<{
        artists: readonly ArtistCredit[]
        /** Text without artists, e.g. "Unknown artist". */
        fallback?: string
        /** Part of the links: link in details, artist-link in lists and the player. */
        part?: 'link' | 'artist-link'
        /** False shows the names as text, without links. */
        links?: boolean
        inRow?: boolean
    }>(),
    { fallback: '', part: 'link', links: true, inRow: false },
)

const { t } = useI18n()

const parts = computed(() => artistParts(props.artists))

function role(artist: ArtistCredit): 'main' | 'featured' {
    return artist.role === 'Featured' ? 'featured' : 'main'
}

/** In a row, the link keeps its events from the row (choose, play, long press). */
function isolate(event: Event): void {
    if (props.inRow) {
        event.stopPropagation()
    }
}

function separatorText(separator: 'list' | 'featuring'): string {
    return separator === 'featuring' ? t('artists.featuring') : t('artists.separator')
}
</script>

<template>
    <template v-for="({ artist, separator }, index) in parts" :key="artist.id ?? `${index}`">
        <span v-if="separator" data-part="artist-separator" :data-kind="separator">{{
            separatorText(separator)
        }}</span>
        <BaseLink
            v-if="links && artist.id"
            :part="part"
            :data-role="role(artist)"
            :to="{ name: 'library-artist', params: { id: artist.id } }"
            :tabindex="inRow ? -1 : undefined"
            @click="isolate"
            @dblclick="isolate"
            @pointerdown="isolate"
            >{{ artist.name }}</BaseLink
        >
        <span v-else data-part="artist-name" :data-role="role(artist)">{{ artist.name }}</span>
    </template>
    <span v-if="parts.length === 0 && fallback" data-part="artist-name" data-state="unknown">{{
        fallback
    }}</span>
</template>
