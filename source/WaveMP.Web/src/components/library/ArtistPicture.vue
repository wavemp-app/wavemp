<script setup lang="ts">
import { computed } from 'vue'
import type { ArtistPicture } from '@/api/generated/models'
import BaseCover from '@/components/base/BaseCover.vue'
import BaseIcon from '@/components/base/BaseIcon.vue'
import { artistPictureViewOf } from '@/library/artistPictures'
import LibraryCover from './LibraryCover.vue'

/**
 * Picture of an artist: its own picture (from its folder, uploaded or taken over from an online
 * service; a cover with data-kind="artist"), otherwise a collage of the covers of up to four of its
 * albums (artist-collage, data-count 1 to 4, each cover in an artist-collage-tile with its
 * data-index), otherwise a cover with the icon of artists. Pictures and covers load in the size
 * they are shown with, times the pixel density of the screen. The collage never moves, so a page
 * of artists stays calm.
 */
const props = defineProps<{
    id: string | null | undefined
    picture: ArtistPicture | null | undefined
}>()

const view = computed(() => artistPictureViewOf(props.picture))
</script>

<template>
    <LibraryCover
        v-if="view.kind === 'picture'"
        kind="artist"
        data-kind="artist"
        :id="id"
        :available="!!id"
        :version="view.version"
    />
    <span
        v-else-if="view.kind === 'albums'"
        data-part="artist-collage"
        :data-count="view.albumIds.length"
        aria-hidden="true"
        :style="{ '--wmp-item-count': view.albumIds.length }"
    >
        <span
            v-for="(albumId, index) in view.albumIds"
            :key="albumId"
            data-part="artist-collage-tile"
            :data-index="index"
        >
            <LibraryCover kind="album" :id="albumId" :available="true" />
        </span>
    </span>
    <BaseCover v-else data-kind="artist" :placeholder="false">
        <BaseIcon name="artist" />
    </BaseCover>
</template>
