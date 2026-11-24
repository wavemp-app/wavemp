<script setup lang="ts">
import { computed, ref, useId, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { ArtistPictureKindObject, type Album, type Artist } from '@/api/generated/models'
import { statusOf } from '@/api/problems'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseLink from '@/components/base/BaseLink.vue'
import FavoriteButton from '@/components/collection/FavoriteButton.vue'
import ArtistCredit from '@/components/library/ArtistCredit.vue'
import ArtistPicture from '@/components/library/ArtistPicture.vue'
import ArtistPictureDialog from '@/components/library/ArtistPictureDialog.vue'
import DetailHeader from '@/components/library/DetailHeader.vue'
import MetaItem from '@/components/library/MetaItem.vue'
import TileCollection from '@/components/library/TileCollection.vue'
import TrackCollection from '@/components/library/TrackCollection.vue'
import { useDetailTitle } from '@/composables/useDetailTitle'
import { useFormat } from '@/composables/useFormat'
import { useLibraryTiles } from '@/composables/useLibraryTiles'
import type { PagedListStatus } from '@/composables/usePagedList'
import { getArtist, listArtistAlbums } from '@/library/api'
import { useSessionStore } from '@/stores/session'

/**
 * An artist with its picture (with the credit of a picture from an online service), its albums
 * (also those it only contributes to), its songs and the heart that marks it as favorite. Admins
 * change the picture in a dialog.
 */
const props = defineProps<{
    id: string
}>()

const { t } = useI18n()
const format = useFormat()
const tiles = useLibraryTiles()
const albumsTitleId = useId()
const songsTitleId = useId()
const session = useSessionStore()
const editingPicture = ref(false)

const artist = ref<Artist | null>(null)
const status = ref<'loading' | 'ready' | 'notFound' | 'error'>('loading')
const albumsStatus = ref<PagedListStatus>('idle')
const songsStatus = ref<PagedListStatus>('idle')
let generation = 0

const busy = computed(
    () =>
        status.value === 'loading' ||
        albumsStatus.value === 'loading' ||
        songsStatus.value === 'loading',
)
const filter = computed(() => ({ artistId: props.id }))

async function load(): Promise<void> {
    const current = ++generation
    status.value = 'loading'
    try {
        const loaded = await getArtist(props.id)
        if (current !== generation) {
            return
        }

        artist.value = loaded ?? null
        status.value = loaded ? 'ready' : 'notFound'
    } catch (error) {
        if (current === generation) {
            artist.value = null
            status.value = statusOf(error) === 404 ? 'notFound' : 'error'
        }
    }
}

/** The picture changed: the answer of the API holds the artist, otherwise it is loaded again. */
async function onPictureChanged(changed?: Artist): Promise<void> {
    if (changed?.id === props.id) {
        artist.value = changed
        return
    }

    try {
        const loaded = await getArtist(props.id)
        if (loaded) {
            artist.value = loaded
        }
    } catch {
        // The page keeps what it shows; the next visit loads the artist again.
    }
}

const showsOnlinePicture = computed(
    () => artist.value?.picture?.kind === ArtistPictureKindObject.Online,
)

function fetchAlbums(offset: number, limit: number) {
    return listArtistAlbums(props.id, { offset, limit, sort: 'Year' })
}

watch(() => props.id, load, { immediate: true })
useDetailTitle(() => (status.value === 'ready' ? artist.value?.name : null))
</script>

<template>
    <div data-part="view" data-view="library-artist" :aria-busy="busy ? 'true' : undefined">
        <div v-if="status === 'notFound'" data-part="empty-state">
            <p data-part="empty-state-text">{{ t('library.artistNotFound') }}</p>
            <BaseLink :to="{ name: 'library-section', params: { section: 'artists' } }">
                {{ t('library.toLibrary') }}
            </BaseLink>
        </div>
        <div v-else-if="status === 'error'" data-part="empty-state">
            <p data-part="empty-state-title">{{ t('library.loadError') }}</p>
            <BaseButton :label="t('library.retry')" @click="load" />
        </div>
        <template v-else-if="artist">
            <DetailHeader
                :title="artist.name ?? ''"
                :back="{ name: 'library-section', params: { section: 'artists' } }"
            >
                <template #media>
                    <ArtistPicture :id="artist.id" :picture="artist.picture" />
                </template>
                <MetaItem field="albums" :label="t('list.albums')">
                    {{ format.number(artist.albumCount ?? 0) }}
                </MetaItem>
                <MetaItem field="tracks" :label="t('list.songs')">
                    {{ format.number(artist.trackCount ?? 0) }}
                </MetaItem>
                <ArtistCredit v-if="showsOnlinePicture" :credit="artist.picture?.onlineCredit" />
                <template #actions>
                    <FavoriteButton
                        data-item="favorite"
                        type="Artist"
                        :id="artist.id ?? id"
                        :favorite="artist.isFavorite"
                    />
                    <BaseButton
                        v-if="session.isAdmin"
                        data-item="artist-picture"
                        icon="change-cover"
                        :label="t('artistPictures.change')"
                        @click="editingPicture = true"
                    />
                </template>
            </DetailHeader>
            <section
                v-if="(artist.albumCount ?? 0) > 0"
                data-part="section"
                data-item="albums"
                :aria-labelledby="albumsTitleId"
            >
                <h2 :id="albumsTitleId" data-part="section-title">
                    {{ t('library.albumsTitle') }}
                </h2>
                <TileCollection
                    :label="t('library.albumsTitle')"
                    :fetch-page="fetchAlbums"
                    :to-tile="(item: Album) => tiles.album(item)"
                    :reload-key="id"
                    filtered
                    @status="albumsStatus = $event"
                />
            </section>
            <section data-part="section" data-item="tracks" :aria-labelledby="songsTitleId">
                <h2 :id="songsTitleId" data-part="section-title">{{ t('library.songsTitle') }}</h2>
                <TrackCollection
                    :label="t('library.songsTitle')"
                    :filter="filter"
                    @status="songsStatus = $event"
                />
            </section>
            <ArtistPictureDialog
                v-if="session.isAdmin"
                v-model:open="editingPicture"
                :artist="artist"
                @changed="onPictureChanged"
            />
        </template>
    </div>
</template>
