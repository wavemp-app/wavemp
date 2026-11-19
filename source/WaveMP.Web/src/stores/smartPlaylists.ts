import { defineStore } from 'pinia'
import { computed, ref, watch } from 'vue'
import {
    PlaylistSortObject,
    type Playlist,
    type SaveSmartPlaylist,
    type SmartPlaylist,
    type SmartPlaylistTemplate,
} from '@/api/generated/models'
import { fetchAllPages } from '@/api/paging'
import {
    convertSmartPlaylist,
    createSmartPlaylist,
    deleteSmartPlaylist,
    listSmartPlaylistTemplates,
    listSmartPlaylists,
    updateSmartPlaylist,
} from '@/playlists/smartApi'
import { usePlaylistsStore } from '@/stores/playlists'
import { useSessionStore } from '@/stores/session'

export type SmartPlaylistsStatus = 'idle' | 'loading' | 'ready' | 'error'

/**
 * The smart playlists of the current profile, sorted by name, for the sidebar and the overview of
 * the playlists, and the built-in templates. Changes go through the store, so every place shows
 * the new state; errors are passed on to the caller, which shows them.
 */
export const useSmartPlaylistsStore = defineStore('smartPlaylists', () => {
    const session = useSessionStore()
    const playlists = usePlaylistsStore()
    const smartPlaylists = ref<SmartPlaylist[]>([])
    const status = ref<SmartPlaylistsStatus>('idle')
    const templates = ref<SmartPlaylistTemplate[]>([])
    let templatesLoading: Promise<SmartPlaylistTemplate[]> | null = null
    let generation = 0

    const sorted = computed(() =>
        [...smartPlaylists.value].sort(
            (a, b) =>
                (a.name ?? '').localeCompare(b.name ?? '', undefined, { sensitivity: 'base' }) ||
                (a.id ?? '').localeCompare(b.id ?? ''),
        ),
    )

    async function load(): Promise<void> {
        const current = ++generation
        status.value = 'loading'
        try {
            const loaded = await fetchAllPages((offset, limit) =>
                listSmartPlaylists({ offset, limit, sort: PlaylistSortObject.Name }),
            )
            if (current === generation) {
                smartPlaylists.value = loaded
                status.value = 'ready'
            }
        } catch {
            if (current === generation) {
                status.value = 'error'
            }
        }
    }

    /** Loads the smart playlists unless they are loaded or loading already. */
    async function ensureLoaded(): Promise<void> {
        if (status.value === 'idle' || status.value === 'error') {
            await load()
        }
    }

    /** The templates, loaded once; they are the same for every profile. */
    async function loadTemplates(): Promise<SmartPlaylistTemplate[]> {
        if (templates.value.length > 0) {
            return templates.value
        }

        templatesLoading ??= fetchAllPages((offset, limit) =>
            listSmartPlaylistTemplates(offset, limit),
        )
        try {
            templates.value = await templatesLoading
            return templates.value
        } finally {
            templatesLoading = null
        }
    }

    /** Takes over the state of a smart playlist the API returned. */
    function apply(smartPlaylist: SmartPlaylist | undefined): void {
        if (!smartPlaylist?.id) {
            return
        }

        const index = smartPlaylists.value.findIndex((item) => item.id === smartPlaylist.id)
        smartPlaylists.value =
            index < 0
                ? [...smartPlaylists.value, smartPlaylist]
                : smartPlaylists.value.map((item, position) =>
                      position === index ? smartPlaylist : item,
                  )
    }

    function forget(id: string): void {
        smartPlaylists.value = smartPlaylists.value.filter((item) => item.id !== id)
    }

    function find(id: string): SmartPlaylist | undefined {
        return smartPlaylists.value.find((item) => item.id === id)
    }

    async function create(body: SaveSmartPlaylist): Promise<SmartPlaylist> {
        const created = await createSmartPlaylist(body)
        if (!created) {
            throw new Error('The API returned no smart playlist.')
        }

        apply(created)
        return created
    }

    async function update(id: string, body: SaveSmartPlaylist): Promise<SmartPlaylist> {
        const updated = await updateSmartPlaylist(id, body)
        if (!updated) {
            throw new Error('The API returned no smart playlist.')
        }

        apply(updated)
        return updated
    }

    async function remove(id: string): Promise<void> {
        await deleteSmartPlaylist(id)
        forget(id)
    }

    /** Turns the smart playlist into a normal playlist; returns the new playlist. */
    async function convert(id: string): Promise<Playlist> {
        const playlist = await convertSmartPlaylist(id)
        if (!playlist) {
            throw new Error('The API returned no playlist.')
        }

        forget(id)
        playlists.apply(playlist)
        return playlist
    }

    /** Forgets everything of the profile, e.g. when the profile changes. */
    function clear(): void {
        generation++
        smartPlaylists.value = []
        status.value = 'idle'
    }

    // The smart playlists belong to the profile of the session.
    watch(
        () => session.token,
        () => clear(),
    )

    return {
        smartPlaylists: sorted,
        status,
        templates,
        load,
        ensureLoaded,
        loadTemplates,
        apply,
        forget,
        find,
        create,
        update,
        remove,
        convert,
        clear,
    }
})
