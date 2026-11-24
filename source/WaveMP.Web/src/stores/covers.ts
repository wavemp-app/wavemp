import { defineStore } from 'pinia'
import { reactive } from 'vue'
import { statusOf } from '@/api/problems'
import { fetchAlbumCover, fetchTrackCover } from '@/library/api'
import {
    fetchArtistPicture,
    fetchCandidatePreview,
    fetchOnlineArtistPicture,
    fetchUploadedArtistPicture,
} from '@/library/artistPictureApi'
import { defaultCoverSize, type CoverSize } from '@/library/covers'
import { fetchPlaylistCover } from '@/playlists/api'
import { fetchAvatar, fetchAvatarPreset } from '@/profiles/avatarApi'

/**
 * What a picture belongs to: covers of albums, tracks and playlists, avatars of profiles (by the id
 * of the profile), built-in avatars (by their id), the picture an artist shows, its uploaded and
 * its online picture (for the admin, by the id of the artist) and the previews of pictures found
 * online (by artistCandidateId()).
 */
export type CoverKind =
    | 'album'
    | 'track'
    | 'playlist'
    | 'avatar'
    | 'avatar-preset'
    | 'artist'
    | 'artist-uploaded'
    | 'artist-online'
    | 'artist-candidate'

/** Kinds of the pictures an admin changes in the dialog of an artist picture. */
export const artistPictureSlotKinds: readonly CoverKind[] = ['artist-uploaded', 'artist-online']

/** The id of the preview of a picture found online for an artist, for the kind artist-candidate. */
export function artistCandidateId(artistId: string, candidateId: string): string {
    return `${artistId}/${candidateId}`
}

/** Covers kept loaded without a component that shows them, the least recently used go first. */
export const maxCachedCovers = 100

/** Covers whose picture follows the cover rules (cover of the album or of the track first). */
export const ruleCoverKinds: readonly CoverKind[] = ['album', 'track']

interface Entry {
    kind: CoverKind
    url: string | null
    loaded: boolean
    pending: Promise<string | null> | null
    refs: number
    used: number
    /** Replaced by a newer picture; freed as soon as no component shows it. */
    stale: boolean
}

/**
 * Covers as object URLs. Pictures of the API need the session token, which an <img> element cannot
 * send, so they are loaded through the API client and shown from a blob. Every size of a cover is
 * an entry of its own. Components acquire a cover while they show it and release it afterwards;
 * requests for the same cover and size are shared. The browser keeps the pictures as well and asks
 * the API with their ETag whether they changed. A missing cover (404) is remembered as null, other
 * errors are not, so the next attempt loads again. Own covers of playlists, avatars and the
 * pictures of artists carry the version the API names, so a new picture gets a new entry instead
 * of the one already loaded. Covers of albums and tracks have no such version; after a change of a
 * cover rule, invalidate() raises the revision of their kind, and components that show them load
 * them again with versionOf(). The uploaded and the online picture of an artist have no version
 * in the API either; the dialog of the artist picture invalidates them after every change.
 */
export const useCoversStore = defineStore('covers', () => {
    const entries = new Map<string, Entry>()
    const revisions = reactive<Partial<Record<CoverKind, number>>>({})
    let clock = 0

    function keyOf(kind: CoverKind, id: string, size: CoverSize, version?: string): string {
        return version ? `${kind}:${id}:${version}:${size}` : `${kind}:${id}:${size}`
    }

    function fetchCover(kind: CoverKind, id: string, size: CoverSize) {
        switch (kind) {
            case 'album':
                return fetchAlbumCover(id, size)
            case 'playlist':
                return fetchPlaylistCover(id, size)
            case 'avatar':
                return fetchAvatar(id, size)
            case 'avatar-preset':
                return fetchAvatarPreset(id, size)
            case 'artist':
                return fetchArtistPicture(id, size)
            case 'artist-uploaded':
                return fetchUploadedArtistPicture(id, size)
            case 'artist-online':
                return fetchOnlineArtistPicture(id, size)
            case 'artist-candidate': {
                const [artistId = '', candidateId = ''] = id.split('/')
                return fetchCandidatePreview(artistId, candidateId, size)
            }
            default:
                return fetchTrackCover(id, size)
        }
    }

    async function load(kind: CoverKind, id: string, size: CoverSize): Promise<string | null> {
        try {
            const buffer = await fetchCover(kind, id, size)
            return buffer && buffer.byteLength > 0 ? URL.createObjectURL(new Blob([buffer])) : null
        } catch (error) {
            if (statusOf(error) === 404) {
                return null
            }

            throw error
        }
    }

    function free(key: string, entry: Entry): void {
        if (entry.url) {
            URL.revokeObjectURL(entry.url)
        }

        if (entries.get(key) === entry) {
            entries.delete(key)
        }
    }

    /** Forgets an entry whose cover failed to load once no component shows it any more. */
    function forgetFailed(key: string, entry: Entry): void {
        if (entry.refs === 0 && !entry.loaded && !entry.pending && entries.get(key) === entry) {
            entries.delete(key)
        }
    }

    /** Frees a replaced entry once no component shows it and it is not loading any more. */
    function freeStale(key: string, entry: Entry): void {
        if (entry.stale && entry.refs === 0 && !entry.pending) {
            free(key, entry)
        }
    }

    function evict(): void {
        const idle = [...entries].filter(([, entry]) => entry.refs === 0 && entry.loaded)
        if (idle.length <= maxCachedCovers) {
            return
        }

        idle.sort(([, a], [, b]) => a.used - b.used)
        for (const [key, entry] of idle.slice(0, idle.length - maxCachedCovers)) {
            free(key, entry)
        }
    }

    /**
     * The object URL of a cover in the size, or null if there is none; release it with the same
     * size (and version) when it is not shown.
     */
    function acquire(
        kind: CoverKind,
        id: string,
        size: CoverSize = defaultCoverSize,
        version?: string,
    ): Promise<string | null> {
        const key = keyOf(kind, id, size, version)
        let entry = entries.get(key)
        if (!entry) {
            entry = {
                kind,
                url: null,
                loaded: false,
                pending: null,
                refs: 0,
                used: 0,
                stale: false,
            }
            entries.set(key, entry)
        }

        entry.refs++
        entry.used = ++clock
        if (entry.loaded) {
            return Promise.resolve(entry.url)
        }

        let pending = entry.pending
        if (!pending) {
            const current = entry
            pending = load(kind, id, size).then(
                (url) => {
                    current.url = url
                    current.loaded = true
                    current.pending = null
                    freeStale(key, current)
                    evict()
                    return url
                },
                () => {
                    current.pending = null
                    forgetFailed(key, current)
                    freeStale(key, current)
                    return null
                },
            )
            current.pending = pending
        }

        return pending
    }

    function release(
        kind: CoverKind,
        id: string,
        size: CoverSize = defaultCoverSize,
        version?: string,
    ): void {
        const key = keyOf(kind, id, size, version)
        const entry = entries.get(key)
        if (!entry) {
            return
        }

        entry.refs = Math.max(0, entry.refs - 1)
        entry.used = ++clock
        forgetFailed(key, entry)
        freeStale(key, entry)
        evict()
    }

    /** Revision of the pictures of a kind; it rises with every invalidate() of the kind. */
    function revisionOf(kind: CoverKind): number {
        return revisions[kind] ?? 0
    }

    /**
     * The version to acquire and release a cover with: the version of its own picture and the
     * revision of its kind. Reading it in a watcher or computed property loads the cover again
     * after invalidate().
     */
    function versionOf(kind: CoverKind, version?: string): string | undefined {
        const revision = revisionOf(kind)
        if (revision === 0) {
            return version
        }

        return version ? `${version}.r${revision}` : `r${revision}`
    }

    /**
     * Drops the loaded pictures of the kinds, e.g. after a cover rule changed: the same album or
     * track may show another picture now. Pictures no component shows are freed at once, the
     * others as soon as their components release them; the next acquire loads them again.
     */
    function invalidate(kinds: readonly CoverKind[] = ruleCoverKinds): void {
        for (const [key, entry] of entries) {
            if (!kinds.includes(entry.kind)) {
                continue
            }

            // Entries still shown or loading are freed by release() or when their load ends.
            entry.stale = true
            freeStale(key, entry)
        }

        for (const kind of kinds) {
            revisions[kind] = revisionOf(kind) + 1
        }
    }

    /** Number of covers kept, for tests and diagnostics. */
    function size(): number {
        return entries.size
    }

    return { acquire, release, revisionOf, versionOf, invalidate, size }
})
