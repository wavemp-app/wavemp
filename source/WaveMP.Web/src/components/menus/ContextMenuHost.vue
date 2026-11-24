<script setup lang="ts">
import { computed, nextTick, ref, shallowRef, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import {
    PlayerViewModeObject,
    type Playlist,
    type SmartPlaylist,
    type Track,
} from '@/api/generated/models'
import { fetchAllPages } from '@/api/paging'
import { problemCodeOf, statusOf } from '@/api/problems'
import BaseContextMenu, { type MenuItem } from '@/components/base/BaseContextMenu.vue'
import AddToOwnAlbumDialog from '@/components/organize/AddToOwnAlbumDialog.vue'
import AddToPlaylistDialog from '@/components/playlists/AddToPlaylistDialog.vue'
import PlaylistDeleteDialog from '@/components/playlists/PlaylistDeleteDialog.vue'
import PlaylistEditDialog from '@/components/playlists/PlaylistEditDialog.vue'
import SmartPlaylistConvertDialog from '@/components/playlists/SmartPlaylistConvertDialog.vue'
import { listAlbumTracks, listTracks } from '@/library/api'
import { groupTracksByAlbum, groupTracksByDisc } from '@/library/grouping'
import { registerDefaultMenuActions } from '@/menus/actions'
import { resolveMenu, runMenuAction, type ResolvedMenuEntry } from '@/menus/registry'
import type { MenuServices, MenuTarget, TrackSource } from '@/menus/targets'
import { listPlaylistEntries, uploadPlaylistCover } from '@/playlists/api'
import {
    convertFailureKey,
    coverFailureKey,
    coverProblemKey,
    coverUploadProblem,
    coverUploadTypes,
} from '@/playlists/presentation'
import { listSmartPlaylistTracks } from '@/playlists/smartApi'
import { useCollectionStore } from '@/stores/collection'
import { useContextMenuStore } from '@/stores/contextMenu'
import { usePlayerStore } from '@/stores/player'
import { usePlaylistsStore } from '@/stores/playlists'
import { useSmartPlaylistsStore } from '@/stores/smartPlaylists'
import { useSessionStore } from '@/stores/session'

/**
 * The one context menu of the app (see src/menus): it opens for the target an element hands to
 * the store and shows the entries registered for its type, and it holds the dialogs its entries
 * open (add to a playlist, rename or delete a playlist, convert or delete a smart playlist) and
 * the file choice of a playlist cover.
 */
registerDefaultMenuActions()

const { t } = useI18n()
const router = useRouter()
const route = useRoute()
const store = useContextMenuStore()
const player = usePlayerStore()
const collection = useCollectionStore()
const session = useSessionStore()
const playlists = usePlaylistsStore()
const smartPlaylists = useSmartPlaylistsStore()

const menu = ref<InstanceType<typeof BaseContextMenu> | null>(null)
const target = shallowRef<MenuTarget | null>(null)
const addingTrackIds = ref<readonly string[]>([])
const adding = ref(false)
const addingToOwnAlbum = ref(false)
const playlist = ref<Playlist | null>(null)
const renaming = ref(false)
const deleting = ref(false)
const deleteBusy = ref(false)
const deleteError = ref<string | null>(null)
const coverInput = ref<HTMLInputElement | null>(null)
const smartPlaylist = ref<SmartPlaylist | null>(null)
const smartAction = ref<'convert' | 'delete' | null>(null)
const smartBusy = ref(false)
const smartError = ref<string | null>(null)
const converting = computed({
    get: () => smartAction.value === 'convert',
    set: (value) => {
        smartAction.value = value ? 'convert' : null
    },
})
const deletingSmart = computed({
    get: () => smartAction.value === 'delete',
    set: (value) => {
        smartAction.value = value ? 'delete' : null
    },
})
const acceptedTypes = coverUploadTypes.join(',')

function openSmartAction(value: SmartPlaylist, action: 'convert' | 'delete'): void {
    smartPlaylist.value = value
    smartError.value = null
    smartAction.value = action
}

async function tracksOf(source: TrackSource): Promise<Track[]> {
    if (source.kind === 'album') {
        const tracks = await fetchAllPages((offset, limit) =>
            listAlbumTracks(source.id, { offset, limit }),
        )
        return groupTracksByDisc(tracks, '').flatMap((group) => group.tracks)
    }

    if (source.kind === 'artist') {
        const tracks = await fetchAllPages((offset, limit) =>
            listTracks({ offset, limit, artistId: source.id }),
        )
        return groupTracksByAlbum(tracks).flatMap((group) => group.tracks)
    }

    if (source.kind === 'smart-playlist') {
        return fetchAllPages((offset, limit) => listSmartPlaylistTracks(source.id, offset, limit))
    }

    const entries = await fetchAllPages((offset, limit) =>
        listPlaylistEntries(source.id, offset, limit),
    )
    return entries.flatMap((entry) => (entry.track ? [entry.track] : []))
}

const services: MenuServices = {
    get isAdmin() {
        return session.isAdmin
    },
    playTracks: (tracks, start, options) => player.playTracks(tracks, start, options),
    playNext: (tracks) => player.playNext(tracks),
    addToQueue: (tracks) => player.addToQueue(tracks),
    isFavorite: (type, id, fallback) => collection.isFavorite(type, id, fallback),
    setFavorite: (type, id, value) => void collection.setFavorite(type, id, value),
    ratingOf: (id, fallback) => collection.ratingOf(id, fallback),
    rate: (id, rating) => void collection.rate(id, rating),
    navigate: (to) => void router.push(to),
    addToPlaylist(trackIds) {
        addingTrackIds.value = trackIds
        adding.value = true
    },
    addToOwnAlbum(trackIds) {
        addingTrackIds.value = trackIds
        addingToOwnAlbum.value = true
    },
    watchVideo(track) {
        player.playTracks([track], 0)
        player.selectViewMode(PlayerViewModeObject.Video)
        void router.push({ name: 'now-playing' })
    },
    tracksOf,
    renamePlaylist(value) {
        playlist.value = value
        renaming.value = true
    },
    changePlaylistCover(value) {
        playlist.value = value
        coverInput.value?.click()
    },
    deletePlaylist(value) {
        playlist.value = value
        deleteError.value = null
        deleting.value = true
    },
    convertSmartPlaylist(value) {
        openSmartAction(value, 'convert')
    },
    deleteSmartPlaylist(value) {
        openSmartAction(value, 'delete')
    },
    queue: {
        playNext: (key) => player.moveItemNext(key),
        move: (key, offset) => player.moveItem(key, offset),
        remove: (key) => player.removeItem(key),
    },
}

function toItem(entry: ResolvedMenuEntry, count: number): MenuItem {
    return {
        id: entry.id,
        separator: entry.separator,
        label: entry.labelKey ? t(entry.labelKey, { count }) : undefined,
        icon: entry.icon,
        disabled: entry.disabled,
        checked: entry.checked,
        children: entry.children?.map((child) => toItem(child, count)),
    }
}

const songCount = computed(() => (target.value?.type === 'song' ? target.value.tracks.length : 1))

const items = computed<MenuItem[]>(() =>
    target.value
        ? resolveMenu(target.value, services).map((entry) => toItem(entry, songCount.value))
        : [],
)

const label = computed(() => {
    switch (target.value?.type) {
        case 'song':
            return songCount.value > 1
                ? t('contextMenu.labels.songs', { count: songCount.value })
                : t('contextMenu.labels.song')
        case 'album':
            return t('contextMenu.labels.album')
        case 'artist':
            return t('contextMenu.labels.artist')
        case 'playlist':
            return t('contextMenu.labels.playlist')
        case 'smart-playlist':
            return t('contextMenu.labels.smartPlaylist')
        case 'queue-entry':
            return t('contextMenu.labels.queueEntry')
        case 'now-playing':
            return t('contextMenu.labels.nowPlaying')
        default:
            return t('contextMenu.labels.song')
    }
})

watch(
    () => store.request,
    async (request) => {
        if (!request) {
            return
        }

        target.value = request.target
        await nextTick()
        await menu.value?.openAt(request.x, request.y)
    },
)

async function choose(id: string): Promise<void> {
    const current = target.value
    if (!current) {
        return
    }

    store.clearError()
    try {
        await runMenuAction(current, id, services)
    } catch {
        store.fail('contextMenu.errors.loadFailed')
    }
}

async function uploadCover(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement
    const file = input.files?.[0]
    input.value = ''
    const id = playlist.value?.id
    if (!file || !id) {
        return
    }

    store.clearError()
    const problem = coverUploadProblem(file)
    if (problem) {
        store.fail(coverProblemKey(problem))
        return
    }

    try {
        playlists.apply(await uploadPlaylistCover(id, await file.arrayBuffer(), file.type))
    } catch (failure) {
        store.fail(coverFailureKey(problemCodeOf(failure)))
    }
}

/** After deleting, the page of the playlist (if shown) leads back to the overview. */
function leaveDeleted(id: string): void {
    deleting.value = false
    if (route.name === 'playlist' && route.params.id === id) {
        void router.push({ name: 'playlists' })
    }
}

async function confirmDelete(): Promise<void> {
    const id = playlist.value?.id
    if (!id) {
        return
    }

    deleteBusy.value = true
    deleteError.value = null
    try {
        await playlists.remove(id)
        leaveDeleted(id)
    } catch (failure) {
        if (statusOf(failure) === 404) {
            playlists.forget(id)
            leaveDeleted(id)
        } else {
            deleteError.value = t('playlists.errors.deleteFailed')
        }
    } finally {
        deleteBusy.value = false
    }
}
/** The page of the smart playlist (if shown) is gone after a conversion or deletion. */
function leaveSmartPlaylist(id: string, playlist?: Playlist): void {
    smartAction.value = null
    if (route.params.id !== id || !String(route.name ?? '').startsWith('smart-playlist')) {
        return
    }

    void router.push(
        playlist?.id ? { name: 'playlist', params: { id: playlist.id } } : { name: 'playlists' },
    )
}

async function confirmSmartAction(): Promise<void> {
    const id = smartPlaylist.value?.id
    const action = smartAction.value
    if (!id || !action) {
        return
    }

    smartBusy.value = true
    smartError.value = null
    try {
        if (action === 'convert') {
            leaveSmartPlaylist(id, await smartPlaylists.convert(id))
        } else {
            await smartPlaylists.remove(id)
            leaveSmartPlaylist(id)
        }
    } catch (failure) {
        if (statusOf(failure) === 404) {
            smartPlaylists.forget(id)
            leaveSmartPlaylist(id)
        } else {
            smartError.value = t(
                action === 'convert'
                    ? convertFailureKey(problemCodeOf(failure))
                    : 'smartPlaylists.errors.deleteFailed',
            )
        }
    } finally {
        smartBusy.value = false
    }
}
</script>

<template>
    <BaseContextMenu ref="menu" :items="items" :label="label" @select="choose" />
    <!-- The dialogs exist only while they are open, so the pages do not hold them all the time. -->
    <AddToPlaylistDialog v-if="adding" v-model:open="adding" :track-ids="addingTrackIds" />
    <AddToOwnAlbumDialog
        v-if="addingToOwnAlbum"
        v-model:open="addingToOwnAlbum"
        :track-ids="addingTrackIds"
    />
    <PlaylistEditDialog v-if="renaming" v-model:open="renaming" :playlist="playlist" />
    <PlaylistDeleteDialog
        v-if="deleting"
        v-model:open="deleting"
        :playlist="playlist"
        :busy="deleteBusy"
        :error="deleteError"
        @confirm="confirmDelete"
    />
    <SmartPlaylistConvertDialog
        v-if="converting"
        v-model:open="converting"
        :smart-playlist="smartPlaylist"
        :busy="smartBusy"
        :error="smartError"
        @confirm="confirmSmartAction"
    />
    <PlaylistDeleteDialog
        v-if="deletingSmart"
        v-model:open="deletingSmart"
        kind="smart-playlist"
        :playlist="smartPlaylist"
        :busy="smartBusy"
        :error="smartError"
        @confirm="confirmSmartAction"
    />
    <input ref="coverInput" type="file" :accept="acceptedTypes" hidden @change="uploadCover" />
</template>
