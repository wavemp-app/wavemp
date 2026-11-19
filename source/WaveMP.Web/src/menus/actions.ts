// The entries of the context menus of songs, albums, artists, playlists, queue entries and Now
// Playing. registerDefaultMenuActions adds them to the registry once; further entries can be
// registered after them (registerMenuActions).

import {
    FavoriteTypeObject,
    PlayerViewModeObject,
    type FavoriteType,
    type PlayerViewMode,
    type Track,
} from '@/api/generated/models'
import type { IconSlotName } from '@/themes/iconSlots'
import { registerMenuActions, type MenuAction } from './registry'
import type {
    AlbumTarget,
    ArtistTarget,
    MenuServices,
    NowPlayingTarget,
    PlaylistTarget,
    QueueEntryTarget,
    SongTarget,
    TrackSource,
} from './targets'

/** i18n keys of the ratings, from no rating (0) to five stars. */
const ratingLabels = ['none', 'one', 'two', 'three', 'four', 'five'] as const

function withAudio(tracks: readonly Track[]): Track[] {
    return tracks.filter((track) => track.hasAudio)
}

function idsOf(tracks: readonly Track[]): string[] {
    return tracks.flatMap((track) => (track.id ? [track.id] : []))
}

/** The song, if the menu is that of exactly one song. */
function single(target: SongTarget): Track | undefined {
    return target.tracks.length === 1 ? target.tracks[0] : undefined
}

function allFavorite(
    type: FavoriteType,
    items: readonly { id?: string | null; isFavorite?: boolean | null }[],
    services: MenuServices,
): boolean {
    const withId = items.filter((item) => item.id)
    return (
        withId.length > 0 &&
        withId.every((item) => services.isFavorite(type, item.id!, item.isFavorite))
    )
}

/** The favorite entry: adds all items, or removes them when all of them are favorites already. */
function favoriteAction<Target extends SongTarget | AlbumTarget | ArtistTarget>(
    type: FavoriteType,
    group: string,
    itemsOf: (target: Target) => readonly { id?: string | null; isFavorite?: boolean | null }[],
): MenuAction<Target> {
    return {
        id: 'favorite',
        group,
        label: (target, services) =>
            allFavorite(type, itemsOf(target), services)
                ? 'contextMenu.removeFavorite'
                : 'contextMenu.addFavorite',
        icon: (target, services) =>
            allFavorite(type, itemsOf(target), services) ? 'remove-favorite' : 'add-favorite',
        enabled: (target) => itemsOf(target).some((item) => item.id),
        run: (target, services) => {
            const value = !allFavorite(type, itemsOf(target), services)
            for (const item of itemsOf(target)) {
                if (item.id) {
                    services.setFavorite(type, item.id, value)
                }
            }
        },
    }
}

/** Entries of the submenu "Rate": no rating and one to five stars, for all songs at once. */
function ratingActions(): MenuAction<SongTarget>[] {
    return ratingLabels.map((name, rating) => ({
        id: `rate-${rating}`,
        group: 'rate',
        label: `contextMenu.rating.${name}`,
        checked: (target: SongTarget, services: MenuServices) =>
            target.tracks.length > 0 &&
            target.tracks.every(
                (track) => !!track.id && services.ratingOf(track.id, track.rating) === rating,
            ),
        run: (target: SongTarget, services: MenuServices) => {
            for (const id of idsOf(target.tracks)) {
                services.rate(id, rating)
            }
        },
    }))
}

export const songActions: readonly MenuAction<SongTarget>[] = [
    {
        id: 'play',
        group: 'play',
        label: 'contextMenu.play',
        icon: 'play-now',
        enabled: (target) => withAudio(target.tracks).length > 0,
        run: (target, services) => {
            if (target.playFrom && single(target)) {
                target.playFrom()
            } else {
                services.playTracks(withAudio(target.tracks), 0)
            }
        },
    },
    {
        id: 'play-next',
        group: 'play',
        label: 'contextMenu.playNext',
        icon: 'play-next',
        enabled: (target) => withAudio(target.tracks).length > 0,
        run: (target, services) => services.playNext(withAudio(target.tracks)),
    },
    {
        id: 'add-to-queue',
        group: 'play',
        label: 'contextMenu.addToQueue',
        icon: 'add-to-queue',
        enabled: (target) => withAudio(target.tracks).length > 0,
        run: (target, services) => services.addToQueue(withAudio(target.tracks)),
    },
    {
        id: 'add-to-playlist',
        group: 'collection',
        label: 'contextMenu.addToPlaylist',
        icon: 'add-to-playlist',
        enabled: (target) => idsOf(target.tracks).length > 0,
        run: (target, services) => services.addToPlaylist(idsOf(target.tracks)),
    },
    favoriteAction<SongTarget>(FavoriteTypeObject.Track, 'collection', (target) => target.tracks),
    {
        id: 'rate',
        group: 'collection',
        label: 'contextMenu.rate',
        icon: 'rate',
        enabled: (target) => idsOf(target.tracks).length > 0,
        children: ratingActions(),
    },
    {
        id: 'details',
        group: 'navigation',
        label: 'contextMenu.details',
        icon: 'info',
        enabled: (target) => !!single(target)?.id,
        run: (target, services) =>
            services.navigate({ name: 'library-track', params: { id: single(target)!.id! } }),
    },
    {
        id: 'album',
        group: 'navigation',
        label: 'contextMenu.album',
        icon: 'go-to-album',
        enabled: (target) => !!single(target)?.album?.id,
        run: (target, services) =>
            services.navigate({
                name: 'library-album',
                params: { id: single(target)!.album!.id! },
            }),
    },
    {
        id: 'artist',
        group: 'navigation',
        label: 'contextMenu.artist',
        icon: 'go-to-artist',
        enabled: (target) => !!single(target)?.artist?.id,
        run: (target, services) =>
            services.navigate({
                name: 'library-artist',
                params: { id: single(target)!.artist!.id! },
            }),
    },
    {
        id: 'watch-video',
        group: 'navigation',
        label: 'contextMenu.watchVideo',
        icon: 'watch-video',
        // The player plays the music and shows the video with it; songs without audio cannot play.
        enabled: (target) => {
            const track = single(target)
            return !!track?.id && !!track.hasVideo && !!track.hasAudio
        },
        run: (target, services) => services.watchVideo(single(target)!),
    },
    {
        id: 'edit-metadata',
        group: 'admin',
        label: 'contextMenu.editMetadata',
        icon: 'edit',
        visible: (_, services) => services.isAdmin,
        enabled: (target) => !!single(target)?.id,
        run: (target, services) =>
            services.navigate({
                name: 'library-track',
                params: { id: single(target)!.id! },
                query: { edit: 'metadata' },
            }),
    },
    {
        id: 'show-files',
        group: 'admin',
        label: 'contextMenu.showFiles',
        icon: 'show-files',
        visible: (_, services) => services.isAdmin,
        enabled: (target) => !!single(target)?.id,
        run: (target, services) =>
            services.navigate({
                name: 'admin-track-files',
                params: { id: single(target)!.id! },
            }),
    },
    {
        id: 'move-up',
        group: 'playlist',
        label: 'contextMenu.moveUp',
        icon: 'move-up',
        visible: (target) => !!target.playlistEntry,
        enabled: (target) =>
            !!target.playlistEntry?.canMoveUp &&
            !target.playlistEntry.busy &&
            target.tracks.length === 1,
        run: (target) => target.playlistEntry?.moveUp(),
    },
    {
        id: 'move-down',
        group: 'playlist',
        label: 'contextMenu.moveDown',
        icon: 'move-down',
        visible: (target) => !!target.playlistEntry,
        enabled: (target) =>
            !!target.playlistEntry?.canMoveDown &&
            !target.playlistEntry.busy &&
            target.tracks.length === 1,
        run: (target) => target.playlistEntry?.moveDown(),
    },
    {
        id: 'remove-from-playlist',
        group: 'playlist',
        label: 'contextMenu.removeFromPlaylist',
        icon: 'remove-from-playlist',
        visible: (target) => !!target.playlistEntry,
        enabled: (target) => !target.playlistEntry?.busy,
        run: (target) => target.playlistEntry?.remove(),
    },
]

/** Plays all songs with audio of an album, an artist or a playlist, optionally shuffled. */
async function playSource(source: TrackSource, services: MenuServices, shuffle: boolean) {
    const tracks = withAudio(await services.tracksOf(source))
    if (tracks.length > 0) {
        services.playTracks(tracks, shuffle ? -1 : 0, { shuffle })
    }
}

function playActions<Target extends AlbumTarget | ArtistTarget | PlaylistTarget>(
    sourceOf: (target: Target) => TrackSource | undefined,
    hasTracks: (target: Target) => boolean,
): MenuAction<Target>[] {
    return [false, true].map((shuffle) => ({
        id: shuffle ? 'shuffle' : 'play',
        group: 'play',
        label: shuffle ? 'contextMenu.shuffle' : 'contextMenu.play',
        icon: shuffle ? 'shuffle-play' : 'play-now',
        enabled: (target: Target) => !!sourceOf(target) && hasTracks(target),
        run: (target: Target, services: MenuServices) =>
            playSource(sourceOf(target)!, services, shuffle),
    }))
}

function albumSource(target: AlbumTarget): TrackSource | undefined {
    return target.album.id ? { kind: 'album', id: target.album.id } : undefined
}

export const albumActions: readonly MenuAction<AlbumTarget>[] = [
    ...playActions<AlbumTarget>(albumSource, (target) => (target.album.trackCount ?? 0) > 0),
    {
        id: 'add-to-queue',
        group: 'play',
        label: 'contextMenu.addToQueue',
        icon: 'add-to-queue',
        enabled: (target) => !!albumSource(target) && (target.album.trackCount ?? 0) > 0,
        run: async (target, services) =>
            services.addToQueue(withAudio(await services.tracksOf(albumSource(target)!))),
    },
    {
        id: 'add-to-playlist',
        group: 'collection',
        label: 'contextMenu.addToPlaylist',
        icon: 'add-to-playlist',
        enabled: (target) => !!albumSource(target) && (target.album.trackCount ?? 0) > 0,
        run: async (target, services) =>
            services.addToPlaylist(idsOf(await services.tracksOf(albumSource(target)!))),
    },
    favoriteAction<AlbumTarget>(FavoriteTypeObject.Album, 'collection', (target) => [target.album]),
    {
        id: 'artist',
        group: 'navigation',
        label: 'contextMenu.artist',
        icon: 'go-to-artist',
        enabled: (target) => !!target.album.artist?.id,
        run: (target, services) =>
            services.navigate({
                name: 'library-artist',
                params: { id: target.album.artist!.id! },
            }),
    },
]

export const artistActions: readonly MenuAction<ArtistTarget>[] = [
    ...playActions<ArtistTarget>(
        (target) => (target.artist.id ? { kind: 'artist', id: target.artist.id } : undefined),
        (target) => (target.artist.trackCount ?? 0) > 0,
    ),
    favoriteAction<ArtistTarget>(FavoriteTypeObject.Artist, 'collection', (target) => [
        target.artist,
    ]),
]

export const playlistActions: readonly MenuAction<PlaylistTarget>[] = [
    ...playActions<PlaylistTarget>(
        (target) => (target.playlist.id ? { kind: 'playlist', id: target.playlist.id } : undefined),
        (target) => (target.playlist.trackCount ?? 0) > 0,
    ),
    {
        id: 'rename',
        group: 'edit',
        label: 'contextMenu.rename',
        icon: 'edit',
        enabled: (target) => !!target.playlist.id,
        run: (target, services) => services.renamePlaylist(target.playlist),
    },
    {
        id: 'change-cover',
        group: 'edit',
        label: 'contextMenu.changeCover',
        icon: 'change-cover',
        enabled: (target) => !!target.playlist.id,
        run: (target, services) => services.changePlaylistCover(target.playlist),
    },
    {
        id: 'delete',
        group: 'delete',
        label: 'contextMenu.deletePlaylist',
        icon: 'delete',
        enabled: (target) => !!target.playlist.id,
        run: (target, services) => services.deletePlaylist(target.playlist),
    },
]

export const queueEntryActions: readonly MenuAction<QueueEntryTarget>[] = [
    {
        id: 'play-next',
        group: 'order',
        label: 'contextMenu.playNext',
        icon: 'play-next',
        enabled: (target) =>
            target.index !== target.currentIndex && target.index !== target.currentIndex + 1,
        run: (target, services) => services.queue.playNext(target.key),
    },
    {
        id: 'move-up',
        group: 'order',
        label: 'contextMenu.moveUp',
        icon: 'move-up',
        enabled: (target) => target.index > 0,
        run: (target, services) => services.queue.move(target.key, -1),
    },
    {
        id: 'move-down',
        group: 'order',
        label: 'contextMenu.moveDown',
        icon: 'move-down',
        enabled: (target) => target.index < target.count - 1,
        run: (target, services) => services.queue.move(target.key, 1),
    },
    {
        id: 'remove',
        group: 'remove',
        label: 'contextMenu.removeFromQueue',
        icon: 'remove-from-queue',
        // The current entry stays; stopping ends it.
        enabled: (target) => target.index !== target.currentIndex,
        run: (target, services) => services.queue.remove(target.key),
    },
]

const stageViews: readonly {
    mode: PlayerViewMode
    id: string
    label: string
    icon: IconSlotName
}[] = [
    {
        mode: PlayerViewModeObject.Cover,
        id: 'view-cover',
        label: 'contextMenu.viewCover',
        icon: 'view-cover',
    },
    {
        mode: PlayerViewModeObject.Video,
        id: 'view-video',
        label: 'contextMenu.viewVideo',
        icon: 'view-video',
    },
    {
        mode: PlayerViewModeObject.Visualization,
        id: 'view-visualization',
        label: 'contextMenu.viewVisualization',
        icon: 'view-visualization',
    },
    {
        mode: PlayerViewModeObject.Lyrics,
        id: 'view-lyrics',
        label: 'contextMenu.viewLyrics',
        icon: 'view-lyrics',
    },
]

export const nowPlayingActions: readonly MenuAction<NowPlayingTarget>[] = [
    ...stageViews.map(({ mode, id, label, icon }): MenuAction<NowPlayingTarget> => ({
        id,
        group: 'view',
        label,
        icon,
        visible: (target) => target.views.some((view) => view.mode === mode),
        enabled: (target) => target.views.some((view) => view.mode === mode && view.available),
        checked: (target) => target.selected === mode,
        run: (target) => target.selectView(mode),
    })),
    {
        id: 'fullscreen',
        group: 'screen',
        label: (target) =>
            target.fullscreen.active ? 'contextMenu.exitFullscreen' : 'contextMenu.fullscreen',
        icon: (target) => (target.fullscreen.active ? 'exit-fullscreen' : 'fullscreen'),
        enabled: (target) => target.fullscreen.available,
        run: (target) => target.fullscreen.toggle(),
    },
]

let registered = false

/** Registers the entries above once. */
export function registerDefaultMenuActions(): void {
    if (registered) {
        return
    }

    registered = true
    registerMenuActions('song', songActions)
    registerMenuActions('album', albumActions)
    registerMenuActions('artist', artistActions)
    registerMenuActions('playlist', playlistActions)
    registerMenuActions('queue-entry', queueEntryActions)
    registerMenuActions('now-playing', nowPlayingActions)
}

/** Allows registerDefaultMenuActions to register again, after clearMenuActions (tests). */
export function resetDefaultMenuActions(): void {
    registered = false
}
