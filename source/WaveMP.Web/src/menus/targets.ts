// The elements that have a context menu (targets) and the services their actions use. A target
// carries the element (one or more songs, an album, ...) and, where only the page knows how, what
// an action does there (e.g. play a list from a song on, move an entry of a playlist). Everything
// else the actions take from the services, so the actions stay plain functions that tests can run
// with fakes.

import type {
    Album,
    Artist,
    FavoriteType,
    PlayerViewMode,
    Playlist,
    SmartPlaylist,
    Track,
} from '@/api/generated/models'
import type { RouteLocationRaw } from 'vue-router'

/**
 * An entry of a playlist on the page of the playlist: it can be moved and removed there. The values
 * follow the page while the menu is open, so they must never be copied (e.g. with a spread).
 */
export interface PlaylistEntryContext {
    readonly canMoveUp: boolean
    readonly canMoveDown: boolean
    /** At least one of the chosen entries is still in the list. */
    readonly canRemove: boolean
    /** A change of the playlist is running; the entries wait for it. */
    readonly busy: boolean
    moveUp(): void
    moveDown(): void
    /** Removes all chosen entries from the playlist. */
    remove(): void
}

/** One or more songs, e.g. the selected rows of a list or a tile of the home page. */
export interface SongTarget {
    readonly type: 'song'
    readonly tracks: readonly Track[]
    /** Plays the list the song belongs to from this song on (one song only). */
    readonly playFrom?: () => void
    /** The songs are entries of a playlist shown on its page. */
    readonly playlistEntry?: PlaylistEntryContext
}

export interface AlbumTarget {
    readonly type: 'album'
    readonly album: Album
}

export interface ArtistTarget {
    readonly type: 'artist'
    readonly artist: Artist
}

export interface PlaylistTarget {
    readonly type: 'playlist'
    readonly playlist: Playlist
}

export interface SmartPlaylistTarget {
    readonly type: 'smart-playlist'
    readonly smartPlaylist: SmartPlaylist
}

/** An entry of the play queue. */
export interface QueueEntryTarget {
    readonly type: 'queue-entry'
    readonly key: string
    readonly track: Track
    readonly index: number
    readonly count: number
    /** Index of the current entry of the queue. */
    readonly currentIndex: number
}

/** A view of the stage of Now Playing and whether the current song offers it. */
export interface StageViewChoice {
    readonly mode: PlayerViewMode
    readonly available: boolean
}

/** The stage of Now Playing: its views and the full screen of the view shown. */
export interface NowPlayingTarget {
    readonly type: 'now-playing'
    readonly views: readonly StageViewChoice[]
    readonly selected: PlayerViewMode
    selectView(mode: PlayerViewMode): void
    readonly fullscreen: {
        readonly available: boolean
        readonly active: boolean
        toggle(): void
    }
}

export type MenuTarget =
    | SongTarget
    | AlbumTarget
    | ArtistTarget
    | PlaylistTarget
    | SmartPlaylistTarget
    | QueueEntryTarget
    | NowPlayingTarget

export type MenuTargetType = MenuTarget['type']

export type TargetOf<Type extends MenuTargetType> = Extract<MenuTarget, { type: Type }>

/** Where the songs of an album, an artist, a playlist or a smart playlist come from. */
export interface TrackSource {
    readonly kind: 'album' | 'artist' | 'playlist' | 'smart-playlist'
    readonly id: string
}

/** What the actions of the menus can do; the menu host provides it with the stores and the router. */
export interface MenuServices {
    readonly isAdmin: boolean
    playTracks(
        tracks: readonly Track[],
        start: number,
        options?: { shuffle?: boolean; source?: TrackSource },
    ): void
    playNext(tracks: readonly Track[]): void
    addToQueue(tracks: readonly Track[]): void
    isFavorite(type: FavoriteType, id: string, fallback?: boolean | null): boolean
    setFavorite(type: FavoriteType, id: string, value: boolean): void
    ratingOf(id: string, fallback?: number | null): number
    rate(id: string, rating: number): void
    navigate(to: RouteLocationRaw): void
    /** Opens the dialog that adds the songs to a playlist. */
    addToPlaylist(trackIds: readonly string[]): void
    /** Plays the song and shows its video on Now Playing. */
    watchVideo(track: Track): void
    /** All songs of an album, an artist, a playlist or a smart playlist, in the order they play. */
    tracksOf(source: TrackSource): Promise<Track[]>
    renamePlaylist(playlist: Playlist): void
    changePlaylistCover(playlist: Playlist): void
    /** Asks before the playlist is deleted. */
    deletePlaylist(playlist: Playlist): void
    /** Asks before the smart playlist becomes a normal playlist. */
    convertSmartPlaylist(smartPlaylist: SmartPlaylist): void
    /** Asks before the smart playlist is deleted. */
    deleteSmartPlaylist(smartPlaylist: SmartPlaylist): void
    readonly queue: {
        playNext(key: string): void
        move(key: string, offset: -1 | 1): void
        remove(key: string): void
    }
}
