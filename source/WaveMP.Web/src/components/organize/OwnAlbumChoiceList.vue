<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { OwnAlbum } from '@/api/generated/models'
import { useFormat } from '@/composables/useFormat'
import OwnAlbumCover from './OwnAlbumCover.vue'

/**
 * The own albums to choose from in the dialog "Add to own album": one own-album-choice button per
 * album with its cover, title and number of songs; state busy while the songs are added to it.
 */
defineProps<{
    albums: readonly OwnAlbum[]
    /** Id of the album the songs are being added to. */
    busy?: string | null
}>()

const emit = defineEmits<{
    choose: [album: OwnAlbum]
}>()

const { t } = useI18n()
const format = useFormat()
</script>

<template>
    <ul data-part="own-album-choices" :aria-label="t('ownAlbums.listLabel')">
        <li v-for="album in albums" :key="album.id ?? ''">
            <button
                type="button"
                data-part="own-album-choice"
                :data-state="busy && busy === album.id ? 'busy' : undefined"
                :disabled="!!busy"
                @click="emit('choose', album)"
            >
                <OwnAlbumCover
                    :id="album.id"
                    :has-cover="!!album.hasCover"
                    :version="album.coverUpdatedAt?.toISOString() ?? null"
                />
                <span data-part="label" data-field="title">{{ album.title }}</span>
                <span data-part="label" data-field="songs">
                    {{ t('library.songCount', { count: format.number(album.trackCount ?? 0) }) }}
                </span>
            </button>
        </li>
    </ul>
</template>
