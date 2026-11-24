<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { OwnAlbum } from '@/api/generated/models'
import BaseLink from '@/components/base/BaseLink.vue'
import { useFormat } from '@/composables/useFormat'
import OwnAlbumCover from './OwnAlbumCover.vue'

/**
 * The own albums of the admin: per album its cover, title (a link to its editor), album artist, the
 * number of songs and whether it was applied to the files (badge and data-state draft, applied or
 * changed).
 */
defineProps<{
    albums: readonly OwnAlbum[]
}>()

const { t } = useI18n()
const format = useFormat()

function statusKey(status: string | null | undefined): string {
    return `ownAlbums.statuses.${(status ?? 'Draft').toLowerCase()}`
}
</script>

<template>
    <ul data-part="own-album-list" :aria-label="t('ownAlbums.listLabel')">
        <li
            v-for="album in albums"
            :key="album.id ?? ''"
            data-part="own-album-item"
            :data-state="(album.status ?? 'Draft').toLowerCase()"
        >
            <OwnAlbumCover
                :id="album.id"
                :has-cover="!!album.hasCover"
                :version="album.coverUpdatedAt?.toISOString() ?? null"
            />
            <span data-part="own-album-info">
                <BaseLink
                    part="own-album-title"
                    :to="{ name: 'admin-own-album', params: { id: album.id ?? '' } }"
                >
                    {{ album.title }}
                </BaseLink>
                <span data-part="label" data-field="album-artist">{{ album.albumArtist }}</span>
                <span data-part="own-album-meta">
                    <span data-part="badge" :data-state="(album.status ?? 'Draft').toLowerCase()">
                        <span data-part="label">{{ t(statusKey(album.status)) }}</span>
                    </span>
                    <span data-part="label" data-field="songs">
                        {{
                            t('library.songCount', { count: format.number(album.trackCount ?? 0) })
                        }}
                    </span>
                    <span v-if="album.year" data-part="label" data-field="year">
                        {{ format.year(album.year) }}
                    </span>
                </span>
            </span>
        </li>
    </ul>
</template>
