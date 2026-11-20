import type { RouteRecordRaw } from 'vue-router'
import EqualizerView from '@/views/EqualizerView.vue'
import HomeView from '@/views/HomeView.vue'
import LastFmCallbackView from '@/views/LastFmCallbackView.vue'
import NotFoundView from '@/views/NotFoundView.vue'
import NowPlayingView from '@/views/NowPlayingView.vue'
import PinSettingsView from '@/views/PinSettingsView.vue'
import PlaylistsView from '@/views/PlaylistsView.vue'
import ProfileSelectView from '@/views/ProfileSelectView.vue'
import SettingsView from '@/views/SettingsView.vue'
import AdminBackupsView from '@/views/admin/AdminBackupsView.vue'
import AdminThemesView from '@/views/admin/AdminThemesView.vue'
import AdminCacheView from '@/views/admin/AdminCacheView.vue'
import AdminChecksView from '@/views/admin/AdminChecksView.vue'
import AdminJobsView from '@/views/admin/AdminJobsView.vue'
import AdminFilesView from '@/views/admin/AdminFilesView.vue'
import AdminLibraryView from '@/views/admin/AdminLibraryView.vue'
import AdminReviewTrackView from '@/views/admin/AdminReviewTrackView.vue'
import AdminReviewView from '@/views/admin/AdminReviewView.vue'
import AdminTrackFilesView from '@/views/admin/AdminTrackFilesView.vue'
import AdminTranscodingView from '@/views/admin/AdminTranscodingView.vue'
import AdminUsersView from '@/views/admin/AdminUsersView.vue'
import AlbumView from '@/views/library/AlbumView.vue'
import ArtistView from '@/views/library/ArtistView.vue'
import GenreView from '@/views/library/GenreView.vue'
import LibraryView from '@/views/library/LibraryView.vue'
import SearchView from '@/views/library/SearchView.vue'
import TrackView from '@/views/library/TrackView.vue'
import YearView from '@/views/library/YearView.vue'
import PlaylistView from '@/views/playlists/PlaylistView.vue'
import RecentlyPlayedView from '@/views/playlists/RecentlyPlayedView.vue'
import SmartPlaylistEditView from '@/views/playlists/SmartPlaylistEditView.vue'
import SmartPlaylistView from '@/views/playlists/SmartPlaylistView.vue'
import RecapView from '@/views/recaps/RecapView.vue'
import SharedRecapView from '@/views/recaps/SharedRecapView.vue'
import SharedRecapsView from '@/views/recaps/SharedRecapsView.vue'
import StatisticsView from '@/views/recaps/StatisticsView.vue'
import {
    defaultLibrarySection,
    librarySectionPattern,
    type MainTab,
} from '@/navigation/libraryTree'

declare module 'vue-router' {
    interface RouteMeta {
        /** Main tab (and bottom navigation item) of the page. */
        tab?: MainTab
        /** Rendered without the app shell (profile selection, internal pages). */
        bare?: boolean
        /** Reachable without a selected profile. */
        public?: boolean
        /** Only for profiles with the role admin. */
        admin?: boolean
        /**
         * Kept alive in the background per history entry: back and forward return to the page with
         * its loaded data, its filters and its scroll position (pages with lists).
         */
        keepAlive?: boolean
    }
}

/**
 * Internal pages that exist only in development mode. The check of import.meta.env.DEV stays
 * literally at the import, so the production build leaves out the page and its chunk.
 */
export const devRoutes: RouteRecordRaw[] = import.meta.env.DEV
    ? [
          {
              path: '/dev/components',
              name: 'dev-components',
              component: () => import('@/views/dev/DevComponentsView.vue'),
              meta: { bare: true, public: true },
          },
      ]
    : []

const librarySongs = { name: 'library-section', params: { section: defaultLibrarySection } }

/**
 * Routes of the web app. The views are imported eagerly: there are only a few small ones, and no
 * chunk of an outdated version has to be loaded after an update of the service worker.
 */
export function createRoutes(extra: readonly RouteRecordRaw[] = []): RouteRecordRaw[] {
    return [
        { path: '/', name: 'home', component: HomeView, meta: { tab: 'home', keepAlive: true } },
        {
            path: '/profiles',
            name: 'profiles',
            component: ProfileSelectView,
            meta: { bare: true, public: true },
        },
        {
            path: '/now-playing',
            name: 'now-playing',
            component: NowPlayingView,
            meta: { tab: 'now-playing' },
        },
        {
            path: '/now-playing/equalizer',
            name: 'equalizer',
            component: EqualizerView,
            meta: { tab: 'now-playing' },
        },
        { path: '/library', name: 'library', redirect: librarySongs },
        {
            path: `/library/:section(${librarySectionPattern})`,
            name: 'library-section',
            component: LibraryView,
            props: true,
            meta: { tab: 'library', keepAlive: true },
        },
        {
            path: '/library/search',
            name: 'library-search',
            component: SearchView,
            meta: { tab: 'library', keepAlive: true },
        },
        {
            path: '/library/albums/:id',
            name: 'library-album',
            component: AlbumView,
            props: true,
            meta: { tab: 'library', keepAlive: true },
        },
        {
            path: '/library/artists/:id',
            name: 'library-artist',
            component: ArtistView,
            props: true,
            meta: { tab: 'library', keepAlive: true },
        },
        {
            path: '/library/genres/:genre',
            name: 'library-genre',
            component: GenreView,
            props: true,
            meta: { tab: 'library', keepAlive: true },
        },
        {
            path: '/library/years/:year(\\d{1,4})',
            name: 'library-year',
            component: YearView,
            props: true,
            meta: { tab: 'library', keepAlive: true },
        },
        {
            path: '/library/tracks/:id',
            name: 'library-track',
            component: TrackView,
            props: true,
            meta: { tab: 'library' },
        },
        {
            path: '/playlists',
            name: 'playlists',
            component: PlaylistsView,
            meta: { tab: 'playlists', keepAlive: true },
        },
        {
            path: '/playlists/new',
            name: 'playlists-new',
            component: PlaylistsView,
            props: { create: true },
            meta: { tab: 'playlists' },
        },
        {
            path: '/playlists/recently-played',
            name: 'playlists-recently-played',
            component: RecentlyPlayedView,
            meta: { tab: 'playlists', keepAlive: true },
        },
        {
            path: '/playlists/smart/new',
            name: 'smart-playlists-new',
            component: SmartPlaylistEditView,
            meta: { tab: 'playlists' },
        },
        {
            path: '/playlists/smart/:id',
            name: 'smart-playlist',
            component: SmartPlaylistView,
            props: true,
            meta: { tab: 'playlists', keepAlive: true },
        },
        {
            path: '/playlists/smart/:id/edit',
            name: 'smart-playlist-edit',
            component: SmartPlaylistEditView,
            props: true,
            meta: { tab: 'playlists' },
        },
        {
            path: '/playlists/:id',
            name: 'playlist',
            component: PlaylistView,
            props: true,
            meta: { tab: 'playlists', keepAlive: true },
        },
        { path: '/recap', name: 'recap', component: RecapView, meta: { tab: 'home' } },
        {
            path: '/recap/statistics',
            name: 'statistics',
            component: StatisticsView,
            meta: { tab: 'home', keepAlive: true },
        },
        {
            path: '/recap/shared',
            name: 'shared-recaps',
            component: SharedRecapsView,
            meta: { tab: 'home', keepAlive: true },
        },
        {
            path: '/recap/shared/:id',
            name: 'shared-recap',
            component: SharedRecapView,
            props: true,
            meta: { tab: 'home' },
        },
        { path: '/settings', name: 'settings', component: SettingsView },
        { path: '/settings/pin', name: 'settings-pin', component: PinSettingsView },
        {
            path: '/settings/scrobbling/last-fm',
            name: 'settings-scrobbling-lastfm',
            component: LastFmCallbackView,
        },
        { path: '/admin', name: 'admin', redirect: { name: 'admin-library' } },
        {
            path: '/admin/library',
            name: 'admin-library',
            component: AdminLibraryView,
            meta: { admin: true },
        },
        {
            path: '/admin/files',
            name: 'admin-files',
            component: AdminFilesView,
            meta: { admin: true, keepAlive: true },
        },
        {
            path: '/admin/files/:id',
            name: 'admin-track-files',
            component: AdminTrackFilesView,
            props: true,
            meta: { admin: true },
        },
        {
            path: '/admin/review',
            name: 'admin-review',
            component: AdminReviewView,
            meta: { admin: true, keepAlive: true },
        },
        {
            path: '/admin/review/:id',
            name: 'admin-review-track',
            component: AdminReviewTrackView,
            props: true,
            meta: { admin: true },
        },
        {
            path: '/admin/checks',
            name: 'admin-checks',
            component: AdminChecksView,
            meta: { admin: true, keepAlive: true },
        },
        {
            path: '/admin/jobs',
            name: 'admin-jobs',
            component: AdminJobsView,
            meta: { admin: true },
        },
        {
            path: '/admin/cache',
            name: 'admin-cache',
            component: AdminCacheView,
            meta: { admin: true },
        },
        {
            path: '/admin/transcoding',
            name: 'admin-transcoding',
            component: AdminTranscodingView,
            meta: { admin: true },
        },
        {
            path: '/admin/backups',
            name: 'admin-backups',
            component: AdminBackupsView,
            meta: { admin: true },
        },
        {
            path: '/admin/themes',
            name: 'admin-themes',
            component: AdminThemesView,
            meta: { admin: true },
        },
        {
            path: '/admin/users',
            name: 'admin-users',
            component: AdminUsersView,
            meta: { admin: true },
        },
        ...extra,
        { path: '/:pathMatch(.*)*', name: 'not-found', component: NotFoundView },
    ]
}
