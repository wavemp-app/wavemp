<script setup lang="ts">
import { computed, ref, useId, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import type { MediaFile, Track } from '@/api/generated/models'
import { fetchAllPages } from '@/api/paging'
import { statusOf } from '@/api/problems'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseLink from '@/components/base/BaseLink.vue'
import FavoriteButton from '@/components/collection/FavoriteButton.vue'
import RatingStars from '@/components/collection/RatingStars.vue'
import DetailHeader from '@/components/library/DetailHeader.vue'
import LibraryCover from '@/components/library/LibraryCover.vue'
import ArtistNames from '@/components/library/ArtistNames.vue'
import MetaItem from '@/components/library/MetaItem.vue'
import OnlineMetadataDialog from '@/components/library/OnlineMetadataDialog.vue'
import TrackFileList from '@/components/library/TrackFileList.vue'
import TrackLyricsPanel from '@/components/library/TrackLyricsPanel.vue'
import TrackMetadataDialog from '@/components/library/TrackMetadataDialog.vue'
import TrackVideoOffsetForm from '@/components/library/TrackVideoOffsetForm.vue'
import { useDetailTitle } from '@/composables/useDetailTitle'
import { useFormat } from '@/composables/useFormat'
import { getTrack, listTrackFiles } from '@/library/api'
import { artistsOf, sameArtists } from '@/library/artists'
import { durationSeconds, mediaKey } from '@/library/presentation'
import { usePlayerStore } from '@/stores/player'
import { useSessionStore } from '@/stores/session'

/**
 * The details of a track: its cover, artist, album, numbers, year, genre, length and media, its
 * rating and the heart that marks it as favorite. Admins also see all its files, edit its
 * metadata and its cover rule, download its lyrics and set the offset of its video; the query
 * edit=metadata opens the editor at once (context menu of a track, library check), edit=online the
 * online search (library check).
 */
const props = defineProps<{
    id: string
}>()

const { t } = useI18n()
const route = useRoute()
const router = useRouter()
const format = useFormat()
const session = useSessionStore()
const player = usePlayerStore()
const filesTitleId = useId()
const videoSyncTitleId = useId()
const lyricsTitleId = useId()

const track = ref<Track | null>(null)
const status = ref<'loading' | 'ready' | 'notFound' | 'error'>('loading')
const files = ref<MediaFile[]>([])
const filesStatus = ref<'idle' | 'loading' | 'ready' | 'error'>('idle')
const editing = ref(false)
const searchingOnline = ref(false)
const artists = computed(() => artistsOf(track.value))
const albumArtists = computed(() => artistsOf(track.value?.album))
let generation = 0
let editRequestHandled = false

const duration = computed(() => durationSeconds(track.value?.durationMilliseconds))
/** What the address asks to open at once: the metadata editor or the online search. */
const editRequest = computed(() =>
    route.query.edit === 'metadata' || route.query.edit === 'online' ? route.query.edit : null,
)
const editRequested = computed(() => editRequest.value === 'metadata')

async function load(): Promise<void> {
    const current = ++generation
    status.value = 'loading'
    try {
        const loaded = await getTrack(props.id)
        if (current !== generation) {
            return
        }

        track.value = loaded ?? null
        status.value = loaded ? 'ready' : 'notFound'
    } catch (error) {
        if (current === generation) {
            track.value = null
            status.value = statusOf(error) === 404 ? 'notFound' : 'error'
        }
    }
}

async function reload(): Promise<void> {
    await load()
    if (status.value === 'ready' && session.isAdmin) {
        await loadFiles()
    }
}

/** New lyrics of the track: its files show the new lyrics file, the player shows the lyrics. */
function onLyricsDownloaded(): void {
    void loadFiles()
    player.refreshLyrics(track.value?.id ?? props.id)
}

async function loadFiles(): Promise<void> {
    const current = generation
    filesStatus.value = 'loading'
    try {
        const loaded = await fetchAllPages((offset, limit) =>
            listTrackFiles(props.id, { offset, limit }),
        )
        if (current === generation) {
            files.value = loaded
            filesStatus.value = 'ready'
        }
    } catch {
        if (current === generation) {
            filesStatus.value = 'error'
        }
    }
}

watch(
    () => props.id,
    () => {
        files.value = []
        filesStatus.value = 'idle'
        editRequestHandled = false
        void reload()
    },
    { immediate: true },
)

// Opens the editor or the online search once per request, as soon as the track is loaded; it
// closes again when the address no longer asks for it (back in the history).
watch(
    [editRequest, status],
    ([requested, current]) => {
        if (!requested) {
            if (editRequestHandled) {
                editing.value = false
                searchingOnline.value = false
            }
            editRequestHandled = false
        } else if (!editRequestHandled && current === 'ready' && session.isAdmin) {
            editRequestHandled = true
            if (requested === 'metadata') {
                editing.value = true
            } else {
                searchingOnline.value = true
            }
        }
    },
    { immediate: true },
)

// Closing the editor or the online search removes the request from the address, so reloading the
// page does not open it again.
watch([editing, searchingOnline], ([isEditing, isSearching]) => {
    if (!isEditing && !isSearching && editRequest.value) {
        void router.replace({ query: { ...route.query, edit: undefined } })
    }
})

useDetailTitle(() => (status.value === 'ready' ? track.value?.title : null))
</script>

<template>
    <div
        data-part="view"
        data-view="library-track"
        :aria-busy="status === 'loading' ? 'true' : undefined"
    >
        <div v-if="status === 'notFound'" data-part="empty-state">
            <p data-part="empty-state-text">{{ t('library.trackNotFound') }}</p>
            <BaseLink :to="{ name: 'library-section', params: { section: 'songs' } }">
                {{ t('library.toLibrary') }}
            </BaseLink>
        </div>
        <div v-else-if="status === 'error'" data-part="empty-state">
            <p data-part="empty-state-title">{{ t('library.loadError') }}</p>
            <BaseButton :label="t('library.retry')" @click="reload" />
        </div>
        <template v-else-if="track">
            <DetailHeader
                :title="track.title ?? ''"
                :back="{ name: 'library-section', params: { section: 'songs' } }"
            >
                <template #media>
                    <LibraryCover
                        kind="track"
                        :id="track.id"
                        :available="track.hasCover ?? false"
                    />
                </template>
                <MetaItem
                    field="artist"
                    :label="artists.length > 1 ? t('list.artists') : t('list.artist')"
                >
                    <ArtistNames :artists="artists" :fallback="t('library.unknownArtist')" />
                </MetaItem>
                <MetaItem
                    v-if="albumArtists.length > 0 && !sameArtists(albumArtists, artists)"
                    field="album-artist"
                    :label="
                        albumArtists.length > 1 ? t('list.albumArtists') : t('list.albumArtist')
                    "
                >
                    <ArtistNames :artists="albumArtists" />
                </MetaItem>
                <MetaItem field="album" :label="t('list.album')">
                    <BaseLink
                        v-if="track.album?.id"
                        :to="{ name: 'library-album', params: { id: track.album.id } }"
                    >
                        {{ track.album.title }}
                    </BaseLink>
                    <template v-else>{{ t('library.unknownAlbum') }}</template>
                </MetaItem>
                <MetaItem v-if="track.trackNumber" field="number" :label="t('list.number')">
                    {{ format.number(track.trackNumber) }}
                </MetaItem>
                <MetaItem v-if="track.discNumber" field="disc" :label="t('list.disc')">
                    {{ format.number(track.discNumber) }}
                </MetaItem>
                <MetaItem v-if="track.year" field="year" :label="t('list.year')">
                    <BaseLink :to="{ name: 'library-year', params: { year: String(track.year) } }">
                        {{ format.year(track.year) }}
                    </BaseLink>
                </MetaItem>
                <MetaItem v-if="track.genre" field="genre" :label="t('list.genre')">
                    <BaseLink :to="{ name: 'library-genre', params: { genre: track.genre } }">
                        {{ track.genre }}
                    </BaseLink>
                </MetaItem>
                <MetaItem v-if="duration !== null" field="duration" :label="t('list.duration')">
                    {{ format.duration(duration) }}
                </MetaItem>
                <MetaItem field="media" :label="t('list.media')">
                    {{ t(mediaKey({ hasAudio: track.hasAudio, hasVideo: track.hasVideo })) }}
                </MetaItem>
                <MetaItem field="rating" :label="t('collection.rating')">
                    <RatingStars :track-id="track.id ?? id" :rating="track.rating" />
                </MetaItem>
                <template #actions>
                    <BaseButton
                        v-if="track.hasAudio"
                        data-item="play"
                        variant="primary"
                        icon="play-now"
                        :label="t('library.play')"
                        @click="player.playTracks([track], 0)"
                    />
                    <FavoriteButton
                        data-item="favorite"
                        type="Track"
                        :id="track.id ?? id"
                        :favorite="track.isFavorite"
                    />
                    <BaseButton
                        v-if="session.isAdmin"
                        data-item="edit-metadata"
                        icon="edit"
                        :label="t('trackMetadata.edit')"
                        @click="editing = true"
                    />
                    <BaseButton
                        v-if="session.isAdmin"
                        data-item="search-online"
                        icon="search-online"
                        :label="t('onlineMetadata.search')"
                        @click="searchingOnline = true"
                    />
                </template>
            </DetailHeader>
            <section
                v-if="session.isAdmin"
                data-part="section"
                data-item="files"
                :aria-labelledby="filesTitleId"
            >
                <h2 :id="filesTitleId" data-part="section-title">{{ t('track.filesTitle') }}</h2>
                <div v-if="filesStatus === 'error'" data-part="empty-state">
                    <p data-part="empty-state-title">{{ t('track.filesError') }}</p>
                    <BaseButton :label="t('library.retry')" @click="loadFiles" />
                </div>
                <template v-else-if="files.length > 0">
                    <TrackFileList :files="files" :label="t('files.label')" />
                    <BaseLink
                        data-item="choose-files"
                        :to="{ name: 'admin-track-files', params: { id: track.id ?? id } }"
                    >
                        {{ t('track.openFiles') }}
                    </BaseLink>
                </template>
            </section>
            <section
                v-if="session.isAdmin"
                data-part="section"
                data-item="lyrics"
                :aria-labelledby="lyricsTitleId"
            >
                <h2 :id="lyricsTitleId" data-part="section-title">{{ t('trackLyrics.title') }}</h2>
                <TrackLyricsPanel :track-id="track.id ?? id" @downloaded="onLyricsDownloaded" />
            </section>
            <section
                v-if="session.isAdmin && track.hasVideo"
                data-part="section"
                data-item="video-sync"
                :aria-labelledby="videoSyncTitleId"
            >
                <h2 :id="videoSyncTitleId" data-part="section-title">
                    {{ t('trackVideo.title') }}
                </h2>
                <TrackVideoOffsetForm
                    :track-id="track.id ?? id"
                    @saved="(offset) => player.applyVideoOffset(track?.id ?? id, offset)"
                />
            </section>
            <TrackMetadataDialog
                v-if="session.isAdmin"
                v-model:open="editing"
                :track-id="id"
                :return-to-previous="editRequested"
                @saved="reload"
            />
            <OnlineMetadataDialog
                v-if="session.isAdmin"
                v-model:open="searchingOnline"
                owner="track"
                :id="track.id ?? id"
                :has-cover="track.hasCover ?? false"
                @applied="reload"
            />
        </template>
    </div>
</template>
