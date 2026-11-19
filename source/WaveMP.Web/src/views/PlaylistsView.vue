<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import type { Playlist } from '@/api/generated/models'
import BaseButton from '@/components/base/BaseButton.vue'
import TileList, { type Tile } from '@/components/library/TileList.vue'
import PlaylistEditDialog from '@/components/playlists/PlaylistEditDialog.vue'
import { useLibraryTiles } from '@/composables/useLibraryTiles'
import { usePlaylistsStore } from '@/stores/playlists'
import { useSmartPlaylistsStore } from '@/stores/smartPlaylists'

/**
 * The playlists and smart playlists of the current profile as tiles, together sorted by name and
 * led by the automatic list "Recently played". Creates new playlists (the route /playlists/new
 * opens the dialog at once, sidebar) and leads to the editor of a new smart playlist.
 */
const props = withDefaults(
    defineProps<{
        create?: boolean
    }>(),
    { create: false },
)

const { t } = useI18n()
const libraryTiles = useLibraryTiles()
const router = useRouter()
const playlists = usePlaylistsStore()
const smartPlaylists = useSmartPlaylistsStore()

const creating = ref(false)
/** Set once a new playlist opens, so closing the dialog does not lead back to the overview. */
let opening = false

const tiles = computed<Tile[]>(() => [
    {
        id: 'recently-played',
        kind: 'recently-played',
        title: t('playlists.recentlyPlayed'),
        subtitle: t('playlists.automatic'),
        to: { name: 'playlists-recently-played' },
    },
    ...[
        ...playlists.playlists.map((playlist) => libraryTiles.playlist(playlist)),
        ...smartPlaylists.smartPlaylists.map((item) => libraryTiles.smartPlaylist(item)),
    ].sort((a, b) => a.title.localeCompare(b.title, undefined, { sensitivity: 'base' })),
])

const empty = computed(
    () =>
        playlists.status === 'ready' &&
        playlists.playlists.length === 0 &&
        smartPlaylists.smartPlaylists.length === 0,
)
const failed = computed(() => playlists.status === 'error' || smartPlaylists.status === 'error')

function load(): void {
    void playlists.load()
    void smartPlaylists.load()
}

function created(playlist: Playlist): void {
    if (playlist.id) {
        opening = true
        const target = { name: 'playlist', params: { id: playlist.id } }
        void (props.create ? router.replace(target) : router.push(target))
    }
}

watch(
    () => props.create,
    (value) => {
        if (value) {
            creating.value = true
        }
    },
    { immediate: true },
)

// /playlists/new stands for the open dialog; closing it leads back to the overview.
watch(creating, (value) => {
    if (!value && props.create && !opening) {
        void router.replace({ name: 'playlists' })
    }
})

onMounted(load)
</script>

<template>
    <div
        data-part="view"
        data-view="playlists"
        :aria-busy="
            playlists.status === 'loading' || smartPlaylists.status === 'loading'
                ? 'true'
                : undefined
        "
    >
        <div data-part="view-actions">
            <BaseButton
                data-item="create-playlist"
                variant="primary"
                icon="add"
                :label="t('playlists.create')"
                @click="creating = true"
            />
            <BaseButton
                data-item="create-smart-playlist"
                icon="create-smart-playlist"
                :label="t('smartPlaylists.new')"
                @click="router.push({ name: 'smart-playlists-new' })"
            />
        </div>
        <div v-if="failed" data-part="empty-state">
            <p data-part="empty-state-title">{{ t('playlists.errors.loadFailed') }}</p>
            <BaseButton :label="t('library.retry')" @click="load" />
        </div>
        <TileList :tiles="tiles" :label="t('nav.playlists')" />
        <div v-if="empty" data-part="empty-state">
            <p data-part="empty-state-title">{{ t('playlists.emptyTitle') }}</p>
            <p data-part="empty-state-text">{{ t('playlists.emptyText') }}</p>
        </div>
        <PlaylistEditDialog v-model:open="creating" @saved="created" />
    </div>
</template>
