<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { Track } from '@/api/generated/models'

/**
 * Songs to pick for an own album, each a track-picker-item with a checkbox, its title and artist
 * and album; state selected while checked, disabled with the reason for songs without an audio
 * file and songs already in the album.
 */
const props = withDefaults(
    defineProps<{
        tracks: readonly Track[]
        /** Ids of the checked songs. */
        chosenIds: readonly string[]
        /** Ids of the songs already in the album. */
        excludeIds?: readonly string[]
        busy?: boolean
    }>(),
    { excludeIds: () => [], busy: false },
)

const emit = defineEmits<{
    toggle: [track: Track]
}>()

const { t } = useI18n()

const chosen = computed(() => new Set(props.chosenIds))
const excluded = computed(() => new Set(props.excludeIds))

const rows = computed(() =>
    props.tracks.map((track) => {
        const id = track.id ?? ''
        const reason = excluded.value.has(id)
            ? t('ownAlbums.picker.inAlbum')
            : track.hasAudio
              ? null
              : t('ownAlbums.picker.noAudio')
        const artist = track.artist?.name ?? ''
        const album = track.album?.title ?? ''
        return {
            track,
            id,
            reason,
            checked: chosen.value.has(id),
            context:
                artist && album
                    ? t('ownAlbums.tracks.context', { artist, album })
                    : artist || album,
        }
    }),
)
</script>

<template>
    <ul
        data-part="track-picker-list"
        :aria-label="t('ownAlbums.picker.results')"
        :aria-busy="busy ? 'true' : undefined"
    >
        <li v-for="row in rows" :key="row.id">
            <label
                data-part="track-picker-item"
                :data-state="row.reason ? 'disabled' : row.checked ? 'selected' : undefined"
            >
                <input
                    type="checkbox"
                    data-part="checkbox"
                    :checked="row.checked"
                    :disabled="!!row.reason"
                    @change="emit('toggle', row.track)"
                />
                <span data-part="label" data-field="title">{{ row.track.title }}</span>
                <span data-part="label" data-field="context">{{ row.context }}</span>
                <span v-if="row.reason" data-part="label" data-field="reason">
                    {{ row.reason }}
                </span>
            </label>
        </li>
    </ul>
</template>
