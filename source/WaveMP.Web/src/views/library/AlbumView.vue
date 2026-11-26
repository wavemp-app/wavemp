<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import type { Album, Track } from '@/api/generated/models'
import { fetchAllPages } from '@/api/paging'
import { statusOf } from '@/api/problems'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseLink from '@/components/base/BaseLink.vue'
import FavoriteButton from '@/components/collection/FavoriteButton.vue'
import AlbumMetadataDialog from '@/components/library/AlbumMetadataDialog.vue'
import DetailHeader from '@/components/library/DetailHeader.vue'
import GroupOverview from '@/components/library/GroupOverview.vue'
import LibraryCover from '@/components/library/LibraryCover.vue'
import MetaItem from '@/components/library/MetaItem.vue'
import OnlineMetadataDialog from '@/components/library/OnlineMetadataDialog.vue'
import TrackGroupList from '@/components/library/TrackGroupList.vue'
import { useDetailTitle } from '@/composables/useDetailTitle'
import { useFormat } from '@/composables/useFormat'
import { useGroupJump } from '@/composables/useGroupJump'
import { getAlbum, listAlbumTracks } from '@/library/api'
import { groupTracksByDisc } from '@/library/grouping'
import { discOverviewGroups } from '@/library/groupOverview'
import { artistLabelKey, durationSeconds } from '@/library/presentation'
import { usePlayerStore } from '@/stores/player'
import { useSessionStore } from '@/stores/session'

/**
 * An album with its cover, details and all its tracks in the order of disc and track number, and the
 * heart that marks it as favorite. Admins also edit the album (its exceptions from the cover rules)
 * and search its info online. An album with
 * several discs offers the quick jump between them (a click on the heading of a disc, Ctrl+Minus,
 * Escape or pinching the list).
 */
const props = defineProps<{
    id: string
}>()

const { t } = useI18n()
const format = useFormat()
const player = usePlayerStore()
const session = useSessionStore()
const router = useRouter()
const searchingOnline = ref(false)
const editing = ref(false)

const album = ref<Album | null>(null)
const tracks = ref<Track[]>([])
const status = ref<'loading' | 'ready' | 'notFound' | 'error'>('loading')
let generation = 0

const groups = computed(() =>
    album.value ? groupTracksByDisc(tracks.value, album.value.title ?? '', album.value) : [],
)
const artistKey = computed(() =>
    artistLabelKey(album.value?.artist, album.value?.isCompilation ?? false),
)
const duration = computed(() => durationSeconds(album.value?.durationMilliseconds))
/** Tracks with audio in the order of the page (discs, then track numbers). */
const playable = computed(() =>
    groups.value.flatMap((group) => group.tracks).filter((track) => track.hasAudio),
)
const hasAudio = computed(() => playable.value.length > 0)

const trackList = ref<InstanceType<typeof TrackGroupList> | null>(null)
const listElement = computed(() => {
    const element: unknown = trackList.value?.$el
    return element instanceof HTMLElement ? element : null
})
const discs = computed(() => discOverviewGroups(groups.value))
const jump = useGroupJump({ list: listElement, available: () => discs.value.length >= 2 })

function play(shuffle: boolean): void {
    player.playTracks(playable.value, shuffle ? -1 : 0, {
        shuffle,
        source: { kind: 'album', id: props.id },
    })
}

async function load(): Promise<void> {
    const current = ++generation
    status.value = 'loading'
    try {
        const [loaded, loadedTracks] = await Promise.all([
            getAlbum(props.id),
            fetchAllPages((offset, limit) => listAlbumTracks(props.id, { offset, limit })),
        ])
        if (current !== generation) {
            return
        }

        album.value = loaded ?? null
        tracks.value = loadedTracks
        status.value = loaded ? 'ready' : 'notFound'
    } catch (error) {
        if (current === generation) {
            album.value = null
            status.value = statusOf(error) === 404 ? 'notFound' : 'error'
        }
    }
}

/**
 * After the online search the album may have another id (new album name or album artist): the
 * page moves to it, otherwise it shows the new values.
 */
function onApplied(result: { albumId: string | null }): void {
    if (result.albumId && result.albumId !== props.id) {
        void router.replace({ name: 'library-album', params: { id: result.albumId } })
    } else {
        void load()
    }
}

watch(() => props.id, load, { immediate: true })
useDetailTitle(() => (status.value === 'ready' ? album.value?.title : null))
</script>

<template>
    <div
        data-part="view"
        data-view="library-album"
        :aria-busy="status === 'loading' ? 'true' : undefined"
    >
        <div v-if="status === 'notFound'" data-part="empty-state">
            <p data-part="empty-state-text">{{ t('library.albumNotFound') }}</p>
            <BaseLink :to="{ name: 'library-section', params: { section: 'albums' } }">
                {{ t('library.toLibrary') }}
            </BaseLink>
        </div>
        <div v-else-if="status === 'error'" data-part="empty-state">
            <p data-part="empty-state-title">{{ t('library.loadError') }}</p>
            <BaseButton :label="t('library.retry')" @click="load" />
        </div>
        <template v-else-if="album">
            <DetailHeader
                :title="album.title ?? ''"
                :back="{ name: 'library-section', params: { section: 'albums' } }"
            >
                <template #media>
                    <LibraryCover
                        kind="album"
                        :id="album.id"
                        :available="album.hasCover ?? false"
                    />
                </template>
                <MetaItem field="artist" :label="t('list.artist')">
                    <BaseLink
                        v-if="album.artist?.id"
                        :to="{ name: 'library-artist', params: { id: album.artist.id } }"
                    >
                        {{ album.artist.name }}
                    </BaseLink>
                    <template v-else>{{ t(artistKey ?? 'library.unknownArtist') }}</template>
                </MetaItem>
                <MetaItem v-if="album.year" field="year" :label="t('list.year')">
                    <BaseLink :to="{ name: 'library-year', params: { year: String(album.year) } }">
                        {{ format.year(album.year) }}
                    </BaseLink>
                </MetaItem>
                <MetaItem v-if="album.genre" field="genre" :label="t('list.genre')">
                    <BaseLink :to="{ name: 'library-genre', params: { genre: album.genre } }">
                        {{ album.genre }}
                    </BaseLink>
                </MetaItem>
                <MetaItem field="tracks" :label="t('list.songs')">
                    {{ format.number(album.trackCount ?? 0) }}
                </MetaItem>
                <MetaItem v-if="duration !== null" field="duration" :label="t('list.duration')">
                    {{ format.duration(duration) }}
                </MetaItem>
                <template #actions>
                    <BaseButton
                        v-if="hasAudio"
                        data-item="play"
                        variant="primary"
                        icon="play-now"
                        :label="t('library.play')"
                        @click="play(false)"
                    />
                    <BaseButton
                        v-if="hasAudio"
                        data-item="shuffle"
                        icon="shuffle-play"
                        :label="t('library.shufflePlay')"
                        @click="play(true)"
                    />
                    <FavoriteButton
                        data-item="favorite"
                        type="Album"
                        :id="album.id ?? id"
                        :favorite="album.isFavorite"
                    />
                    <BaseButton
                        v-if="session.isAdmin"
                        data-item="edit-metadata"
                        icon="edit"
                        :label="t('albumMetadata.edit')"
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
            <TrackGroupList
                ref="trackList"
                :groups="groups"
                :label="album.title ?? ''"
                :show-group-info="false"
                :source="{ kind: 'album', id: props.id }"
                :state="jump.open.value ? 'zoomed-out' : undefined"
                :group-jump="discs.length >= 2"
                :group-jump-open="jump.open.value"
                @group-jump="(position, opener) => jump.openOverview({ position, opener })"
            />
            <GroupOverview
                v-if="discs.length >= 2"
                v-model:open="jump.open.value"
                :groups="discs"
                :current="jump.current.value"
                unit="songs"
                @choose="(group) => jump.choose(group.position)"
            />
            <AlbumMetadataDialog
                v-if="session.isAdmin"
                v-model:open="editing"
                :album-id="album.id ?? id"
                @saved="load"
            />
            <OnlineMetadataDialog
                v-if="session.isAdmin"
                v-model:open="searchingOnline"
                owner="album"
                :id="album.id ?? id"
                :has-cover="album.hasCover ?? false"
                @applied="onApplied"
            />
        </template>
    </div>
</template>
