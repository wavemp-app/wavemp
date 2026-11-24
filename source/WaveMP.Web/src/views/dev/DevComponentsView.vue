<script setup lang="ts">
// Internal component overview, only part of the development build (see devRoutes). It shows every
// part of the theme API in the current theme and both layouts of the real app side by side.
// Its own structure uses dev-* parts, which are not part of the theme API.
import '@/styles/dev.css'
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type {
    Backup,
    CacheUsage,
    LyricsLookupSummary,
    FileSelectionFilter,
    FileSelectionSort,
    LibraryIssueSort,
    Job,
    MediaSource,
    PlayerViewMode,
    RecapPeriod,
    RecapShare,
    Scan,
    ScanIssue,
    ScrobblingService,
    Theme,
    TranscodeJob,
    TranscodingStatus,
    TrackReviewSort,
} from '@/api/generated/models'
import {
    PlayerViewModeObject,
    RecapPeriodObject,
    SmartPlaylistFieldObject,
    SmartPlaylistOperatorObject,
} from '@/api/generated/models'
import { DateOnly } from '@microsoft/kiota-abstractions'
import { fileCategories, fileChoices } from '@/admin/fileSelections'
import AdminNav from '@/components/admin/AdminNav.vue'
import BackupList from '@/components/admin/BackupList.vue'
import ThemeErrorList from '@/components/admin/ThemeErrorList.vue'
import ThemeList from '@/components/admin/ThemeList.vue'
import type { ThemePackageError } from '@/admin/themeErrors'
import BackupStatusPanel from '@/components/admin/BackupStatus.vue'
import CacheUsagePanel from '@/components/admin/CacheUsage.vue'
import LyricsLookupResults from '@/components/admin/LyricsLookupResults.vue'
import FileChoiceGroup from '@/components/admin/FileChoiceGroup.vue'
import FileSelectionFilters from '@/components/admin/FileSelectionFilters.vue'
import FileSelectionList from '@/components/admin/FileSelectionList.vue'
import LibraryCheckStatus from '@/components/admin/LibraryCheckStatus.vue'
import LibraryIssueFilters from '@/components/admin/LibraryIssueFilters.vue'
import LibraryIssueList from '@/components/admin/LibraryIssueList.vue'
import JobList from '@/components/admin/JobList.vue'
import MediaSourceList from '@/components/admin/MediaSourceList.vue'
import ScanHistory from '@/components/admin/ScanHistory.vue'
import ScanIssues from '@/components/admin/ScanIssues.vue'
import ScanPanel from '@/components/admin/ScanPanel.vue'
import ScanSummary from '@/components/admin/ScanSummary.vue'
import TranscodeJobList from '@/components/admin/TranscodeJobList.vue'
import TranscodingStatusPanel from '@/components/admin/TranscodingStatus.vue'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseCheckbox from '@/components/base/BaseCheckbox.vue'
import BaseColorPicker from '@/components/base/BaseColorPicker.vue'
import BaseContextMenu, { type MenuItem } from '@/components/base/BaseContextMenu.vue'
import BaseDialog from '@/components/base/BaseDialog.vue'
import ShortcutOverview from '@/components/shell/ShortcutOverview.vue'
import { defaultShortcuts } from '@/shortcuts/defaults'
import BaseIcon from '@/components/base/BaseIcon.vue'
import BaseMeter from '@/components/base/BaseMeter.vue'
import BaseProgress from '@/components/base/BaseProgress.vue'
import BaseRoundButton from '@/components/base/BaseRoundButton.vue'
import BaseSearchField from '@/components/base/BaseSearchField.vue'
import BaseSegmentedControl from '@/components/base/BaseSegmentedControl.vue'
import BaseSegmentedField from '@/components/base/BaseSegmentedField.vue'
import BaseSelect from '@/components/base/BaseSelect.vue'
import BaseSlider from '@/components/base/BaseSlider.vue'
import BaseTabs from '@/components/base/BaseTabs.vue'
import BaseTextField from '@/components/base/BaseTextField.vue'
import BaseTree from '@/components/base/BaseTree.vue'
import type { TreeNode } from '@/components/base/BaseTreeItem.vue'
import UserList, { type UserListItem } from '@/components/admin/UserList.vue'
import BaseCover from '@/components/base/BaseCover.vue'
import BaseLink from '@/components/base/BaseLink.vue'
import HomeShelf from '@/components/collection/HomeShelf.vue'
import LibraryScopeSwitch from '@/components/collection/LibraryScopeSwitch.vue'
import DetailHeader from '@/components/library/DetailHeader.vue'
import LibraryEmpty from '@/components/library/LibraryEmpty.vue'
import LoadMore from '@/components/library/LoadMore.vue'
import MetaItem from '@/components/library/MetaItem.vue'
import OnlineCompareTable from '@/components/library/OnlineCompareTable.vue'
import OnlineMatchList from '@/components/library/OnlineMatchList.vue'
import { compareRows, defaultSelection, onlineTrackFields } from '@/library/onlineMetadata'
import TileList from '@/components/library/TileList.vue'
import TrackFileList from '@/components/library/TrackFileList.vue'
import TrackGroupList from '@/components/library/TrackGroupList.vue'
import EqualizerPanel from '@/components/player/EqualizerPanel.vue'
import NowPlayingStage from '@/components/player/NowPlayingStage.vue'
import VisualizationControls from '@/components/player/VisualizationControls.vue'
import { aforceModes } from '@/visualization/modules/aforce/modes'
import { aforceColorSchemes } from '@/visualization/modules/aforce/schemes'
import { defaultVisualizationSettings, type VisualizationSettings } from '@/visualization/settings'
import AutoplayList from '@/components/player/AutoplayList.vue'
import CompactPlayer from '@/components/player/CompactPlayer.vue'
import PlayQueue from '@/components/player/PlayQueue.vue'
import PlaylistTrackList, {
    type PlaylistTrackRow,
} from '@/components/playlists/PlaylistTrackList.vue'
import SmartPlaylistRuleRow from '@/components/playlists/SmartPlaylistRuleRow.vue'
import { newRule } from '@/playlists/smartRules'
import PinPad from '@/components/profiles/PinPad.vue'
import AvatarCropper from '@/components/profiles/AvatarCropper.vue'
import AvatarPresetPicker from '@/components/profiles/AvatarPresetPicker.vue'
import ProfileAvatar from '@/components/profiles/ProfileAvatar.vue'
import ProfileSelect from '@/components/profiles/ProfileSelect.vue'
import ProfileSummary from '@/components/profiles/ProfileSummary.vue'
import ScrobbleList from '@/components/profiles/ScrobbleList.vue'
import RecapExport from '@/components/recaps/RecapExport.vue'
import RecapNav from '@/components/recaps/RecapNav.vue'
import RecapPeriodPicker from '@/components/recaps/RecapPeriodPicker.vue'
import RecapShareList from '@/components/recaps/RecapShareList.vue'
import RecapStory from '@/components/recaps/RecapStory.vue'
import RecapStorySlide from '@/components/recaps/RecapStorySlide.vue'
import StatisticsChart from '@/components/recaps/StatisticsChart.vue'
import StatisticsTable from '@/components/recaps/StatisticsTable.vue'
import type { RecapSlide } from '@/recaps/slides'
import ReviewActions from '@/components/review/ReviewActions.vue'
import ReviewBulkBar from '@/components/review/ReviewBulkBar.vue'
import ReviewFilters from '@/components/review/ReviewFilters.vue'
import ReviewInboxButton from '@/components/review/ReviewInboxButton.vue'
import ReviewList from '@/components/review/ReviewList.vue'
import ReviewNav from '@/components/review/ReviewNav.vue'
import type { ProfileTileData } from '@/components/profiles/ProfileTile.vue'
import AppBottomNav from '@/components/shell/AppBottomNav.vue'
import AppHeader from '@/components/shell/AppHeader.vue'
import AppSidebar from '@/components/shell/AppSidebar.vue'
import AppToolbar from '@/components/shell/AppToolbar.vue'
import AppTransport from '@/components/shell/AppTransport.vue'
import type { TransportState } from '@/components/shell/transport'
import { useFormat } from '@/composables/useFormat'
import { setLocale } from '@/i18n'
import { languageName, supportedLocales } from '@/i18n/locales'
import {
    coverPriorityKey,
    coverRuleOptionKey,
    coverRuleOptions,
    coverRuleToken,
} from '@/library/coverRules'
import { groupTracksByAlbum } from '@/library/grouping'
import { sortItemId, sortLabelKey, sortOptions } from '@/library/sorting'
import { libraryTree, mainNavigation, type NavigationTreeNode } from '@/navigation/libraryTree'
import {
    fallbackEqualizerBands,
    fallbackMaxGainDb,
    fallbackMinGainDb,
    flatCurve,
} from '@/player/equalizer'
import { stageViewOf } from '@/player/videoSync'
import { initialCrop, type AvatarCrop } from '@/profiles/avatarCrop'
import { profileColorOptions } from '@/profiles/presentation'
import type { MissingTagsFilter } from '@/review/inbox'
import type { LibraryIssueFilter } from '@/admin/libraryIssues'
import { useReviewStore } from '@/stores/review'
import { iconSlotNames, iconSlots } from '@/themes/iconSlots'
import { parts } from '@/themes/parts'
import ThemePicker from '@/components/themes/ThemePicker.vue'
import { applyTheme, themeById, themes, type ThemeVariant } from '@/themes/themes'
import NotFoundView from '@/views/NotFoundView.vue'
import NowPlayingView from '@/views/NowPlayingView.vue'
import PlaylistsView from '@/views/PlaylistsView.vue'
import {
    sampleFileSelection,
    sampleFileSummaries,
    sampleLibraryIssues,
    sampleLibraryIssueSummary,
    sampleFiles,
    sampleLyrics,
    sampleLyricsPosition,
    sampleOnlineCurrent,
    sampleOnlineMatches,
    sampleTiles,
    sampleTrackReviews,
    sampleTracks,
} from './samples'

const i18n = useI18n({ useScope: 'global' })
const { t } = i18n
const format = useFormat()

function switchLanguage(locale: string): void {
    setLocale(i18n, locale)
}

// Theme switch of the overview: shows every part in another theme and variant at once, without
// saving anything for a profile.
const devTheme = ref(document.documentElement.dataset.theme ?? themes[0].id)
const devVariant = ref<string>(document.documentElement.dataset.themeVariant ?? 'dark')
const devVariantOptions = computed(() =>
    themeById(devTheme.value).variants.map((variant) => ({
        value: variant,
        label: t(`settings.themeVariants.${variant}`),
    })),
)
const devThemeOptions = themes.map((theme) => ({ value: theme.id, label: theme.name }))

function switchTheme(id: string, variant?: string): void {
    const theme = themeById(id)
    const shown = theme.variants.find((candidate) => candidate === variant) ?? theme.variants[0]
    devTheme.value = theme.id
    devVariant.value = shown
    applyTheme(theme.id, shown as ThemeVariant)
}

// Buttons, tabs and tree
const tabs = computed(() =>
    mainNavigation.map((item) => ({
        id: item.id,
        label: t(item.labelKey),
        icon: item.icon,
        to: item.to,
    })),
)

function toTreeNode(node: NavigationTreeNode): TreeNode {
    return {
        id: node.id,
        label: t(node.labelKey),
        icon: node.icon,
        to: node.to,
        disabled: node.disabled,
        children: node.children?.map(toTreeNode),
    }
}

const treeNodes = computed(() => libraryTree.map(toTreeNode))
const treeExpanded = ref<string[]>(['playlists', 'library'])
const treeCollapsed = ref<string[]>([])

// List, library and sliders and fields
const selectedTrack = ref<string | undefined>('sample-track-2')
const sampleGroups = computed(() => groupTracksByAlbum(sampleTracks(t)))
const sampleQueue = computed(() =>
    sampleTracks(t)
        .filter((track) => track.hasAudio)
        .map((track, index) => ({ key: `sample-${index}`, track })),
)
const sampleQueueIndex = ref(1)
/** Songs autoplay plays after the sample queue, among them one with a music video only. */
const sampleAutoplay = ref(
    sampleTracks(t)
        .slice()
        .reverse()
        .slice(0, 3)
        .map((track, index) => ({ key: `sample-autoplay-${index}`, track })),
)
const sampleViewMode = ref<PlayerViewMode>('Video')
const sampleVideoTrack = computed(() => sampleTracks(t).find((track) => track.hasVideo))
const sampleLyricsTrack = computed(() => sampleTracks(t).find((track) => !track.hasVideo))
const sampleLyricsMode = ref<PlayerViewMode>('Lyrics')
// The controls of the visualization, which the stage shows only while a visualization draws.
const sampleVisualization = ref<VisualizationSettings>({
    ...defaultVisualizationSettings,
    module: 'AForce',
})
const sampleVisualizationPresets = computed(() =>
    aforceModes.map((mode) => ({ id: mode.id, label: t(mode.labelKey) })),
)
const sampleVisualizationPreset = ref('rings')
const sampleVisualizationColorSchemes = computed(() =>
    aforceColorSchemes.map((scheme) => ({ id: scheme.id, label: t(scheme.labelKey) })),
)
const sampleVisualizationColorScheme = ref('aurora')
const sampleTrackLyrics = computed(() =>
    sampleLyricsTrack.value ? sampleLyrics(t, sampleLyricsTrack.value.id ?? '') : null,
)
const tiles = computed(() => sampleTiles(t))
const playlistRows = computed<PlaylistTrackRow[]>(() =>
    sampleTracks(t).map((track, index) => ({ id: `sample-entry-${index}`, track })),
)
const smartRules = [
    { ...newRule(SmartPlaylistFieldObject.Genre), text: 'Rock' },
    {
        ...newRule(SmartPlaylistFieldObject.Year),
        operator: SmartPlaylistOperatorObject.Between,
        number: '1990',
        numberTo: '1999',
    },
    newRule(SmartPlaylistFieldObject.Favorite),
]
const files = sampleFiles()
const fileSummaries = computed(() => sampleFileSummaries(t))
const libraryIssues = computed(() => sampleLibraryIssues(t))
const libraryIssueSummary = sampleLibraryIssueSummary()
const issueSearch = ref('')
const issueFilter = ref<LibraryIssueFilter>('all')
const issueSort = ref<LibraryIssueSort>('Path')
const fileSelection = sampleFileSelection()
const onlineMatches = computed(() => sampleOnlineMatches(t))
const onlineCurrent = computed(() => sampleOnlineCurrent(t))
const onlineSelected = ref<string | null>('sample-match-1')
const onlineValues = computed(
    () =>
        onlineMatches.value.find((match) => match.key === onlineSelected.value)?.values ??
        onlineCurrent.value,
)
const onlineRows = computed(() =>
    compareRows(onlineTrackFields, onlineCurrent.value, onlineValues.value),
)
const onlineChosen = computed(() =>
    defaultSelection(onlineTrackFields, onlineCurrent.value, onlineValues.value),
)
const onlineUseCover = ref(true)
const fileSearch = ref('')
const sampleCoverRule = ref('Inherit')
const sampleCoverRuleOptions = computed(() =>
    coverRuleOptions.map((option) => ({
        value: option,
        item: coverRuleToken(option),
        label: t(coverRuleOptionKey(option), { priority: t(coverPriorityKey('AlbumFirst')) }),
    })),
)
const fileFilter = ref<FileSelectionFilter>('Choices')
const fileSort = ref<FileSelectionSort>('Path')
const reviewSamples = computed(() => sampleTrackReviews(t))
const reviewFilter = ref<MissingTagsFilter>('missing')
const reviewSort = ref<TrackReviewSort>('FirstSeenAt')
const reviewSearch = ref('')
const reviewSelected = new Set(['sample-review-missing'])
const reviewDone = new Set<string>()
// The badge of the review inbox shows a sample number; the overview never calls the API.
useReviewStore().unreviewedCount = 12
const sortSampleMenu = ref<InstanceType<typeof BaseContextMenu> | null>(null)
const sortSampleItems = computed<MenuItem[]>(() => [
    ...sortOptions('tracks', true).map((key) => ({
        id: sortItemId(key),
        label: t(sortLabelKey(key)),
        checked: key === 'Title',
    })),
    { id: 'separator', separator: true },
    { id: 'direction-asc', label: t('sort.ascending'), checked: true },
    { id: 'direction-desc', label: t('sort.descending'), checked: false },
])

function openSortSample(event: MouseEvent): void {
    const rect = (event.currentTarget as HTMLElement).getBoundingClientRect()
    void sortSampleMenu.value?.openAt(rect.left, rect.bottom + 4)
}

/** A picture of the theme stands in for a cover from the server. */
const sampleCover = '/themes/midnight/images/cover-placeholder.svg'
const sliders = ref([0, 0.5, 1])

// Equalizer (fixture): on with the own curve, off without Web Audio
const equalizerGains = ref([4, 3, 2, 0.5, -1, -1.5, 0, 2, 3.5, 5])
const equalizerPreamp = ref(-2)
const equalizerOn = ref(true)
const disabledSlider = ref(0.3)
const textEmpty = ref('')
const textFilled = ref('')
const searchEmpty = ref('')
const searchFilled = ref('')
const textInvalid = ref('')
const yearSample = ref('2006')
const numberSample = ref('1')
const selected = ref('one')
const selectOptions = computed(() => [
    { value: 'one', label: t('dev.samples.optionOne') },
    { value: 'two', label: t('dev.samples.optionTwo') },
])
textFilled.value = t('dev.samples.textValue')
searchFilled.value = t('dev.samples.searchValue')

// Dialog and context menu
const dialogOpen = ref(false)
const shortcutsOpen = ref(false)
const menu = ref<InstanceType<typeof BaseContextMenu> | null>(null)
const menuItems = computed<MenuItem[]>(() => [
    { id: 'edit', label: t('actions.edit'), icon: 'edit' },
    { id: 'delete', label: t('actions.delete'), icon: 'delete' },
    { id: 'separator-1', separator: true },
    { id: 'add', label: t('actions.add'), icon: 'add' },
    { id: 'settings', label: t('actions.settings'), icon: 'settings' },
    { id: 'profile', label: t('actions.profile'), icon: 'profile', disabled: true },
    {
        id: 'rate',
        label: t('contextMenu.rate'),
        icon: 'rate',
        children: [
            { id: 'rate-0', label: t('contextMenu.rating.none'), checked: false },
            { id: 'rate-3', label: t('contextMenu.rating.three'), checked: true },
            { id: 'rate-5', label: t('contextMenu.rating.five'), checked: false },
        ],
    },
    { id: 'separator-2', separator: true },
    { id: 'view-video', label: t('actions.showVideo'), icon: 'view-video' },
    { id: 'view-visualization', label: t('actions.showVisualization'), icon: 'view-visualization' },
    { id: 'view-cover', label: t('actions.showCover'), icon: 'view-cover' },
])

function openMenuAtElement(event: MouseEvent): void {
    const rect = (event.currentTarget as HTMLElement).getBoundingClientRect()
    void menu.value?.openAt(rect.left, rect.bottom + 4)
}

function openMenuAtPointer(event: MouseEvent): void {
    void menu.value?.openAt(event.clientX, event.clientY)
}

// Transport in all states (fixtures)
const idle: TransportState = {
    playback: 'idle',
    position: 0,
    duration: 0,
    shuffle: false,
    repeat: 'off',
    volume: 0.8,
    muted: false,
}
const track = computed(() => ({
    trackId: 'dev-track',
    title: t('placeholders.track', { number: 1 }),
    artist: t('placeholders.artist', { number: 1 }),
    position: 65,
    duration: 225,
}))
const transports = computed(() => [
    { id: 'idle', label: t('dev.transportStates.idle'), state: idle, expanded: false },
    {
        id: 'playing',
        label: t('dev.transportStates.playing'),
        state: { ...idle, ...track.value, playback: 'playing' as const },
        expanded: false,
    },
    {
        id: 'paused',
        label: t('dev.transportStates.paused'),
        state: { ...idle, ...track.value, playback: 'paused' as const },
        expanded: false,
    },
    {
        id: 'shuffle',
        label: t('dev.transportStates.shuffle'),
        state: { ...idle, ...track.value, playback: 'playing' as const, shuffle: true },
        expanded: false,
    },
    {
        id: 'repeat-all',
        label: t('dev.transportStates.repeatAll'),
        state: { ...idle, ...track.value, playback: 'playing' as const, repeat: 'all' as const },
        expanded: false,
    },
    {
        id: 'repeat-one',
        label: t('dev.transportStates.repeatOne'),
        state: { ...idle, ...track.value, playback: 'playing' as const, repeat: 'one' as const },
        expanded: false,
    },
    {
        id: 'muted',
        label: t('dev.transportStates.muted'),
        state: { ...idle, ...track.value, playback: 'paused' as const, muted: true },
        expanded: false,
    },
    {
        id: 'loading',
        label: t('dev.transportStates.loading'),
        state: { ...idle, ...track.value, playback: 'playing' as const, loading: true },
        expanded: false,
    },
    {
        id: 'message',
        label: t('dev.transportStates.message'),
        state: {
            ...idle,
            ...track.value,
            playback: 'playing' as const,
            message: 'unsupported' as const,
        },
        expanded: false,
    },
    {
        id: 'resume',
        label: t('dev.transportStates.resume'),
        state: { ...idle, ...track.value, playback: 'paused' as const, position: 42 },
        expanded: false,
        resume: { device: t('dev.resumeDevice') },
    },
    {
        id: 'sleep-timer',
        label: t('dev.transportStates.sleepTimer'),
        state: {
            ...idle,
            ...track.value,
            playback: 'playing' as const,
            equalizer: true,
            sleepTimer: { mode: 'duration' as const, remaining: 754, fading: false },
        },
        expanded: false,
    },
    {
        id: 'crossfade',
        label: t('dev.transportStates.crossfade'),
        state: { ...idle, ...track.value, playback: 'playing' as const, crossfade: true },
        expanded: false,
    },
    {
        id: 'autoplay',
        label: t('dev.transportStates.autoplay'),
        state: { ...idle, ...track.value, playback: 'playing' as const, autoplay: true },
        expanded: false,
    },
    {
        id: 'casting',
        label: t('dev.transportStates.casting'),
        state: {
            ...idle,
            ...track.value,
            playback: 'playing' as const,
            effects: false,
            cast: {
                availability: 'available' as const,
                connection: 'connected' as const,
                technology: 'chromecast' as const,
                device: t('dev.castDevice'),
                devicesFound: true,
            },
        },
        expanded: false,
    },
    {
        id: 'cast-insecure',
        label: t('dev.transportStates.castInsecure'),
        state: {
            ...idle,
            ...track.value,
            playback: 'paused' as const,
            cast: {
                availability: 'insecure' as const,
                connection: 'disconnected' as const,
                technology: 'chromecast' as const,
                device: null,
                devicesFound: false,
            },
        },
        expanded: false,
    },
    {
        id: 'expanded',
        label: t('dev.transportStates.expanded'),
        state: { ...idle, ...track.value, playback: 'playing' as const },
        expanded: true,
        stage: {
            mode: PlayerViewModeObject.Cover,
            view: 'cover' as const,
            hasVideo: true,
            casting: false,
        },
    },
    {
        id: 'expanded-video',
        label: t('dev.transportStates.expandedVideo'),
        state: { ...idle, ...track.value, playback: 'playing' as const },
        expanded: true,
        stage: {
            mode: PlayerViewModeObject.Video,
            view: 'video' as const,
            hasVideo: true,
            casting: false,
        },
    },
    {
        id: 'expanded-visualization',
        label: t('dev.transportStates.expandedVisualization'),
        state: { ...idle, ...track.value, playback: 'playing' as const },
        expanded: true,
        stage: {
            mode: PlayerViewModeObject.Visualization,
            view: 'visualization' as const,
            hasVideo: false,
            casting: false,
        },
    },
])

const libraryLocation = { name: 'library-section', params: { section: 'songs' } }

// Profiles (fixtures; the colors are part of the palette of the server)
const adminProfile = computed<ProfileTileData>(() => ({
    id: 'admin',
    name: t('dev.samples.adminName'),
    initials: t('dev.samples.adminInitials'),
    color: '#1f6fd6',
    isBuiltIn: true,
}))
const sampleProfiles = computed<ProfileTileData[]>(() => [
    {
        id: 'first',
        name: t('dev.samples.profileName'),
        initials: t('dev.samples.profileInitials'),
        color: '#2e7d32',
        isBuiltIn: false,
    },
    {
        id: 'second',
        name: t('dev.samples.secondProfileName'),
        initials: t('dev.samples.secondProfileInitials'),
        color: '#c2185b',
        isBuiltIn: false,
        hasPin: true,
    },
    adminProfile.value,
])
const firstProfile = computed(() => ({ ...sampleProfiles.value[0]!, role: 'Admin' as const }))
const samplePin = ref('12')
const samplePresets = ['bubble', 'leaf', 'drop', 'sun']
const samplePresetPictures = Object.fromEntries(samplePresets.map((id) => [id, sampleCover]))
const samplePreset = ref<string | null>('leaf')
const sampleCropPicture = { width: 600, height: 400 }
const sampleCrop = ref<AvatarCrop>(initialCrop(sampleCropPicture))
const color = ref<string | null>('#2e7d32')
const colorOptions = computed(() =>
    profileColorOptions.map((option) => ({ value: option.value, label: t(option.labelKey) })),
)
const sampleUsers = computed<UserListItem[]>(() =>
    sampleProfiles.value.map((profile, index) => ({
        ...profile,
        isAdmin: profile.isBuiltIn || index === 1,
        hasPin: !!profile.hasPin,
        createdAt: new Date(Date.UTC(2026, 10, 1 + index)),
        avatarVersion: null,
        avatarPresetId: null,
    })),
)

// Administration (fixtures of the scan and the jobs)
const checkboxOn = ref(true)
const checkboxOff = ref(false)
const sampleTime = new Date(Date.UTC(2026, 10, 11, 9, 30))
const sampleIssues: ScanIssue[] = [
    { path: 'Artist/Album/03 Broken.mp3', code: 'file.probe_failed' },
    { path: 'Artist/Album/04 Locked.flac', code: 'file.tags_unreadable' },
]
// Problems in several media folders name the folder of every entry.
const sampleSourceIssues: ScanIssue[] = [
    { path: '', code: 'source.not_found', sourceId: 'videos', sourceName: 'Music videos' },
    {
        path: 'Artist/Album/03 Broken.mp3',
        code: 'file.probe_failed',
        sourceId: 'media',
        sourceName: 'Media',
    },
]
const sampleSources: MediaSource[] = [
    {
        id: 'media',
        name: 'Media',
        path: '/media',
        followsMediaPath: true,
        isActive: true,
        isAvailable: true,
        isWatched: true,
        trackCount: 1240,
        fileCount: 2610,
    },
    {
        id: 'videos',
        name: 'Music videos',
        path: '/media-videos',
        followsMediaPath: false,
        isActive: false,
        isAvailable: false,
        isWatched: false,
        trackCount: 86,
        fileCount: 172,
    },
]
const sampleSourceFilter = ref('')
const sampleSourceOptions = computed(() => [
    { value: '', label: t('library.sourceFilter.all') },
    ...sampleSources.map((source) => ({ value: source.id ?? '', label: source.name ?? '' })),
])
const sampleProcessing: Scan = {
    id: 'processing',
    status: 'Running',
    trigger: 'Manual',
    queuedAt: sampleTime,
    startedAt: sampleTime,
    progress: {
        phase: 'Processing',
        directoriesFound: 42,
        filesFound: 512,
        directoriesToProcess: 6,
        directoriesProcessed: 2,
        filesToRead: 40,
        filesRead: 17,
    },
    issues: [],
}
const sampleDiscovering: Scan = {
    ...sampleProcessing,
    id: 'discovering',
    progress: { ...sampleProcessing.progress, phase: 'Discovering' },
}
const sampleFinished: Scan = {
    id: 'finished',
    status: 'Succeeded',
    trigger: 'Schedule',
    queuedAt: sampleTime,
    startedAt: sampleTime,
    finishedAt: new Date(sampleTime.getTime() + 83_000),
    durationMilliseconds: 83_000,
    isPartial: false,
    statistics: {
        directoriesScanned: 42,
        directoriesChanged: 3,
        filesFound: 512,
        filesAdded: 12,
        filesUpdated: 3,
        filesRemoved: 1,
        filesUnchanged: 496,
        filesFailed: 2,
        tracksAdded: 6,
        tracksUpdated: 2,
        tracksRemoved: 1,
        tracksTotal: 230,
        albumsTotal: 21,
        artistsTotal: 14,
    },
    issues: sampleIssues,
}
const sampleHistory: Scan[] = [
    sampleFinished,
    {
        ...sampleFinished,
        id: 'partial',
        trigger: 'FileWatcher',
        isPartial: true,
        issues: [],
    },
    {
        id: 'failed',
        status: 'Failed',
        trigger: 'Manual',
        queuedAt: sampleTime,
        startedAt: sampleTime,
        finishedAt: sampleTime,
        durationMilliseconds: 120,
        errorCode: 'scan.media_folder_empty',
        issues: [],
    },
    { id: 'interrupted', status: 'Interrupted', trigger: 'Schedule', queuedAt: sampleTime },
]
const sampleIdleJob: Job[] = [
    {
        id: 'library-scan',
        state: 'Idle',
        intervalMinutes: 360,
        nextRunAt: new Date(sampleTime.getTime() + 6 * 3_600_000),
        lastRun: {
            id: 'last',
            jobId: 'library-scan',
            status: 'Succeeded',
            trigger: 'Schedule',
            queuedAt: sampleTime,
            startedAt: sampleTime,
            durationMilliseconds: 83_000,
        },
    },
]
const sampleRunningJob: Job[] = [
    {
        ...sampleIdleJob[0],
        state: 'Running',
        lastRun: { ...sampleIdleJob[0]!.lastRun, status: 'Running', durationMilliseconds: null },
    },
]
const sampleCoverJob: Job[] = [
    {
        id: 'cover-cache',
        state: 'Idle',
        lastRun: {
            id: 'covers',
            jobId: 'cover-cache',
            status: 'Failed',
            trigger: 'LibraryScan',
            queuedAt: sampleTime,
            startedAt: sampleTime,
            durationMilliseconds: 1_200,
            errorCode: 'cache.ffmpeg_unavailable',
        },
    },
]
const sampleBackupJob: Job = {
    id: 'database-backup',
    state: 'Idle',
    intervalMinutes: 1440,
    dailyAt: '03:00',
    timeZone: 'UTC',
    nextRunAt: new Date(sampleTime.getTime() + 12 * 3_600_000),
    lastRun: {
        id: 'backup',
        jobId: 'database-backup',
        status: 'Failed',
        trigger: 'Schedule',
        queuedAt: sampleTime,
        startedAt: sampleTime,
        durationMilliseconds: 4_000,
        errorCode: 'backup.pg_dump_unavailable',
    },
}
const sampleScrobbleServices: ScrobblingService[] = [
    {
        service: 'LastFm',
        isAvailable: true,
        status: 'Connected',
        accountName: 'anna',
        connectedAt: sampleTime,
        lastScrobbledAt: sampleTime,
        queuedScrobbles: 2,
    },
    {
        service: 'ListenBrainz',
        isAvailable: true,
        status: 'AuthorizationFailed',
        queuedScrobbles: 5,
    },
]
const sampleRecapPeriod = ref<RecapPeriod>(RecapPeriodObject.Custom)
const sampleRecapRange = ref({ from: '2026-10-01', to: '2026-09-01' })
const sampleRecapBars = Array.from({ length: 12 }, (_, index) => ({
    key: String(index),
    label: String(index + 1),
    description: String(index + 1),
    ratio: [0.2, 0.5, 0.35, 1, 0.6, 0.4, 0.1, 0, 0.3, 0.7, 0.45, 0.25][index] ?? 0,
    peak: index === 3,
    labelled: index % 3 === 0,
}))
const sampleRecapSlides: RecapSlide[] = [
    { kind: 'intro', title: 'Lars', figure: 'Aurora', figureLabel: 'Aurora', caption: '2026' },
    {
        kind: 'top-artists',
        title: 'Aurora',
        items: [1, 2, 3].map((rank) => ({
            key: String(rank),
            rank,
            title: `Artist ${rank}`,
            subtitle: 'Pop',
            value: String(10 - rank),
            coverTrackId: null,
        })),
    },
    { kind: 'timeline', title: 'Aurora', bars: sampleRecapBars, caption: 'Aurora' },
    {
        kind: 'summary',
        title: 'Aurora',
        coverTrackId: null,
        facts: [
            { key: 'minutes', label: 'Minutes', value: '1,234' },
            { key: 'tracks', label: 'Songs', value: '56' },
        ],
    },
]
const sampleRecapShares: RecapShare[] = [
    {
        id: 'share-1',
        owner: { id: 'user-1', name: 'Ada' },
        isOwn: false,
        period: RecapPeriodObject.LastMonth,
        from: new DateOnly({ year: 2026, month: 10, day: 21 }),
        to: new DateOnly({ year: 2026, month: 11, day: 20 }),
        timeZone: 'Europe/Berlin',
        design: 'aurora',
        updatedAt: new Date(Date.UTC(2026, 10, 20, 18)),
    },
]
const sampleStatisticsColumns = [
    { key: 'rank', label: '#', numeric: true },
    { key: 'title', label: 'Aurora' },
    { key: 'plays', label: '12', numeric: true },
]
const sampleStatisticsRows = [
    { key: 'a', cells: { rank: '1', title: 'First Song', plays: '12' } },
    { key: 'b', cells: { rank: '2', title: 'Second Song', plays: '7' } },
]

const sampleBackups: Backup[] = [
    {
        id: 'wavemp-20261116T030000Z-schedule',
        fileName: 'wavemp-20261116T030000Z-schedule.dump',
        createdAt: sampleTime,
        sizeBytes: 4_200_000,
        trigger: 'Schedule',
    },
    {
        id: 'wavemp-20261115T193000Z-manual',
        fileName: 'wavemp-20261115T193000Z-manual.dump',
        createdAt: sampleTime,
        sizeBytes: 4_150_000,
        trigger: 'Manual',
    },
    {
        id: 'wavemp-20261114T080000Z-migration',
        fileName: 'wavemp-20261114T080000Z-migration.dump',
        createdAt: sampleTime,
        sizeBytes: 4_100_000,
        trigger: 'Migration',
    },
]
const sampleThemeFile = (hash: string, extension: string, id = 'aurora') =>
    `/api/v1/themes/${id}/files/${hash.repeat(32)}.${extension}`
const sampleThemes: Theme[] = [
    {
        id: 'aurora',
        name: 'Aurora',
        author: 'WaveMP',
        version: '1.2.0',
        themeApi: 5,
        variants: ['Light', 'Dark'],
        stylesheetUrl: sampleThemeFile('a', 'css'),
        previews: [
            { variant: 'Light', url: sampleThemeFile('b', 'png') },
            { variant: 'Dark', url: sampleThemeFile('c', 'png') },
        ],
        sizeBytes: 182_000,
        installedAt: sampleTime,
        status: 'Current',
        problems: [],
    },
    {
        id: 'borealis',
        name: 'Borealis',
        author: 'WaveMP',
        version: '0.9.0',
        themeApi: 5,
        variants: ['Dark'],
        stylesheetUrl: sampleThemeFile('d', 'css', 'borealis'),
        previews: [{ variant: 'Dark', url: sampleThemeFile('e', 'png', 'borealis') }],
        sizeBytes: 96_000,
        installedAt: sampleTime,
        status: 'Outdated',
        problems: [
            {
                code: 'manifest.unsupported_api_version',
                path: 'manifest.json',
                field: 'themeApi',
                min: 5,
                max: 7,
            },
            { code: 'css.layer', path: 'theme.css', line: 48 },
        ],
    },
    {
        id: 'corona',
        name: 'Corona',
        author: 'WaveMP',
        version: '0.4.0',
        themeApi: 5,
        variants: ['Light'],
        stylesheetUrl: sampleThemeFile('f', 'css', 'corona'),
        previews: [{ variant: 'Light', url: sampleThemeFile('0', 'png', 'corona') }],
        sizeBytes: 64_000,
        installedAt: sampleTime,
        status: 'Blocked',
        problems: [
            { code: 'svg.not_cleaned', path: 'images/play.svg' },
            { code: 'css.at_rule_not_allowed', path: 'theme.css', line: 48 },
        ],
    },
]
const sampleThemeErrors: ThemePackageError[] = [
    { code: 'css.external_url', path: 'theme.css', line: 12 },
    { code: 'svg.script', path: 'images/play.svg' },
    {
        code: 'manifest.unsupported_api_version',
        path: 'manifest.json',
        field: 'themeApi',
        min: 5,
        max: 5,
    },
    { code: 'package.too_large', max: 33_554_432 },
]
const sampleCacheUsage: CacheUsage = {
    isAvailable: true,
    sizeBytes: 1_240_000_000,
    entryCount: 5_321,
    maxSizeBytes: 2_000_000_000,
    covers: { sizeBytes: 1_240_000_000, entryCount: 5_321 },
    transcodes: { sizeBytes: 0, entryCount: 0 },
}
const sampleLyricsLookups: LyricsLookupSummary = {
    notFoundCount: 128,
    instrumentalCount: 14,
    lookUpAgainAfterMinutes: 43_200,
}
const sampleTranscodingStatus: TranscodingStatus = {
    available: true,
    ffmpegAvailable: true,
    cacheAvailable: true,
    runningJobs: 1,
    maxParallelJobs: 2,
    hardware: {
        state: 'Unavailable',
        accelerator: 'None',
        reason: 'no_device',
        detectedAt: sampleTime,
    },
}
const sampleTranscodeJobs = computed<TranscodeJob[]>(() => {
    const track = sampleTracks(t).find((item) => item.hasVideo)
    return [
        {
            id: 'sample-transcode',
            trackId: track?.id,
            track,
            media: 'Video',
            accelerator: 'None',
            startSegment: 0,
            completedSegments: 12,
            segmentCount: 40,
            startedAt: sampleTime,
            lastAccessAt: sampleTime,
        },
    ]
})
</script>

<template>
    <div data-part="dev-components">
        <header data-part="dev-header">
            <h1 data-part="dev-title">{{ t('dev.title') }}</h1>
            <p data-part="dev-intro">{{ t('dev.intro') }}</p>
            <div data-part="dev-language" role="group" :aria-label="t('dev.language')">
                <BaseButton
                    v-for="locale in supportedLocales"
                    :key="locale"
                    :label="languageName(locale)"
                    :pressed="i18n.locale.value === locale"
                    @click="switchLanguage(locale)"
                />
            </div>
            <div data-part="dev-theme">
                <BaseSegmentedControl
                    :model-value="devTheme"
                    item="dev-theme"
                    :label="t('dev.theme')"
                    :options="devThemeOptions"
                    @update:model-value="switchTheme($event, devVariant)"
                />
                <BaseSegmentedControl
                    :model-value="devVariant"
                    item="dev-theme-variant"
                    :label="t('dev.themeVariant')"
                    :options="devVariantOptions"
                    :disabled="devVariantOptions.length < 2"
                    @update:model-value="switchTheme(devTheme, $event)"
                />
            </div>
        </header>

        <section data-part="dev-section">
            <h2 data-part="dev-section-title">{{ t('dev.sections.buttons') }}</h2>
            <div data-part="dev-samples">
                <BaseButton :label="t('dev.samples.button')" />
                <BaseButton variant="primary" icon="add" :label="t('dev.samples.primary')" />
                <BaseButton :label="t('dev.samples.disabled')" disabled />
                <BaseButton :label="t('dev.samples.pressed')" :pressed="true" />
                <BaseButton variant="icon" icon="settings" :label="t('actions.settings')" />
                <BaseButton variant="icon" icon="edit" :label="t('actions.edit')" :pressed="true" />
                <BaseButton
                    variant="icon"
                    icon="view-details"
                    dropdown
                    :label="t('toolbar.view')"
                />
            </div>
            <div data-part="dev-samples" data-tone="dark">
                <BaseRoundButton icon="back" :label="t('nav.back')" />
                <BaseRoundButton icon="forward" :label="t('nav.forward')" :pressed="true" />
                <BaseRoundButton icon="back" :label="t('dev.samples.disabled')" disabled />
                <BaseButton part="btn-play" variant="round" icon="play" :label="t('player.play')" />
                <BaseButton
                    part="btn-play"
                    variant="round"
                    icon="pause"
                    :label="t('player.pause')"
                    state="playing"
                />
                <BaseButton
                    part="btn-play"
                    variant="round"
                    icon="play"
                    :label="t('dev.samples.disabled')"
                    disabled
                />
            </div>
        </section>

        <section data-part="dev-section">
            <h2 data-part="dev-section-title">{{ t('dev.sections.tabs') }}</h2>
            <div data-part="dev-samples" data-tone="dark">
                <BaseTabs :items="tabs" :label="t('nav.main')" active="library" />
            </div>
        </section>

        <section data-part="dev-section">
            <h2 data-part="dev-section-title">{{ t('dev.sections.tree') }}</h2>
            <div data-part="dev-samples">
                <div data-part="dev-sample">
                    <BaseTree
                        v-model:expanded="treeExpanded"
                        :nodes="treeNodes"
                        :label="t('sidebar.label')"
                        active="songs"
                    />
                </div>
                <div data-part="dev-sample">
                    <BaseTree
                        v-model:expanded="treeCollapsed"
                        :nodes="treeNodes"
                        :label="t('sidebar.label')"
                    />
                </div>
            </div>
        </section>

        <section data-part="dev-section">
            <h2 data-part="dev-section-title">{{ t('dev.sections.list') }}</h2>
            <p data-part="placeholder-notice" data-state="placeholder" role="note">
                <BaseIcon name="info" />
                <span data-part="label">{{ t('library.placeholderNotice') }}</span>
            </p>
            <TrackGroupList
                v-model:selected="selectedTrack"
                :groups="sampleGroups"
                :label="t('library.songs')"
                state="placeholder"
            />
        </section>

        <section data-part="dev-section">
            <h2 data-part="dev-section-title">{{ t('dev.sections.library') }}</h2>
            <div data-part="view" data-view="library" data-section="albums">
                <TileList :tiles="tiles" :label="t('library.albums')" />
                <LoadMore />
                <LoadMore loading />
            </div>
            <div data-part="view" data-view="library-album">
                <DetailHeader
                    :title="t('placeholders.album', { number: 1 })"
                    :back="{ name: 'library-section', params: { section: 'albums' } }"
                >
                    <template #media>
                        <BaseCover :src="sampleCover" />
                    </template>
                    <MetaItem field="artist" :label="t('list.artist')">
                        <BaseLink
                            :to="{ name: 'library-artist', params: { id: 'sample-artist-1' } }"
                        >
                            {{ t('placeholders.artist', { number: 1 }) }}
                        </BaseLink>
                    </MetaItem>
                    <MetaItem field="year" :label="t('list.year')">{{
                        format.year(2006)
                    }}</MetaItem>
                    <MetaItem field="tracks" :label="t('list.songs')">{{
                        format.number(4)
                    }}</MetaItem>
                    <template #actions>
                        <BaseButton
                            data-item="edit-metadata"
                            icon="edit"
                            :label="t('trackMetadata.edit')"
                        />
                    </template>
                </DetailHeader>
                <section data-part="section" data-item="files">
                    <h2 data-part="section-title">{{ t('track.filesTitle') }}</h2>
                    <TrackFileList :files="files" :label="t('files.label')" />
                </section>
                <section data-part="section" data-item="cover-rule">
                    <h2 data-part="section-title">{{ t('coverRule.title') }}</h2>
                    <form data-part="form" @submit.prevent>
                        <figure data-part="cover-preview" data-state="ready">
                            <BaseCover :src="sampleCover" />
                            <figcaption data-part="label">{{ t('coverRule.preview') }}</figcaption>
                        </figure>
                        <BaseSegmentedField
                            v-model="sampleCoverRule"
                            data-field="cover-rule"
                            item="cover-rule"
                            :label="t('coverRule.label')"
                            :options="sampleCoverRuleOptions"
                            :hint="t('coverRule.trackHint')"
                        />
                        <div data-part="view-actions">
                            <BaseButton
                                type="submit"
                                variant="primary"
                                data-item="save-cover-rule"
                                :label="t('coverRule.save')"
                            />
                        </div>
                    </form>
                </section>
            </div>
            <div data-part="view" data-view="playlist">
                <DetailHeader :title="t('placeholders.playlist', { number: 1 })">
                    <template #media>
                        <span
                            data-part="playlist-cover"
                            data-count="5"
                            data-state="rotating"
                            aria-hidden="true"
                        >
                            <span
                                v-for="tile in 4"
                                :key="tile"
                                data-part="playlist-cover-tile"
                                :data-index="tile - 1"
                                :data-state="tile === 2 ? 'changing' : undefined"
                            >
                                <span data-part="playlist-cover-slide" data-state="active">
                                    <BaseCover :src="sampleCover" />
                                </span>
                                <span
                                    v-if="tile === 2"
                                    data-part="playlist-cover-slide"
                                    data-state="entering"
                                    data-from="end"
                                >
                                    <BaseCover />
                                </span>
                            </span>
                        </span>
                    </template>
                    <MetaItem field="tracks" :label="t('list.songs')">{{
                        format.number(playlistRows.length)
                    }}</MetaItem>
                    <template #actions>
                        <BaseButton data-item="rename" icon="edit" :label="t('playlists.rename')" />
                        <BaseButton
                            data-item="cover"
                            icon="change-cover"
                            :label="t('playlists.changeCover')"
                        />
                    </template>
                </DetailHeader>
                <PlaylistTrackList
                    :rows="playlistRows"
                    :label="t('placeholders.playlist', { number: 1 })"
                    sortable
                />
            </div>
            <div data-part="view" data-view="smart-playlist-edit" data-state="create">
                <form data-part="form" data-kind="smart-playlist" @submit.prevent>
                    <section data-part="section" data-item="templates">
                        <h3 data-part="section-title">{{ t('smartPlaylists.templatesTitle') }}</h3>
                        <div data-part="smart-templates">
                            <BaseButton
                                data-item="four-stars-and-more"
                                data-state="applied"
                                icon="create-smart-playlist"
                                :label="t('smartPlaylists.templates.fourStarsAndMore')"
                            />
                            <BaseButton
                                data-item="never-played"
                                icon="create-smart-playlist"
                                :label="t('smartPlaylists.templates.neverPlayed')"
                            />
                        </div>
                    </section>
                    <section data-part="section" data-item="rules">
                        <h3 data-part="section-title">{{ t('smartPlaylists.rulesTitle') }}</h3>
                        <ol data-part="smart-rules">
                            <SmartPlaylistRuleRow
                                v-for="(rule, index) in smartRules"
                                :key="rule.key"
                                :rule="rule"
                                :index="index"
                                :errors="index === 1 ? { numberTo: t('validation.required') } : {}"
                            />
                        </ol>
                    </section>
                </form>
            </div>
            <div data-part="view" data-view="smart-playlist">
                <section data-part="smart-summary" :aria-label="t('smartPlaylists.rulesTitle')">
                    <p data-part="smart-summary-intro">
                        {{ t('smartPlaylists.summary.match.all') }}
                    </p>
                    <ul data-part="smart-summary-rules">
                        <li data-part="smart-summary-rule">
                            {{
                                t('smartPlaylists.summary.is', {
                                    field: t('smartPlaylists.fields.genre'),
                                    value: 'Rock',
                                })
                            }}
                        </li>
                    </ul>
                    <p data-part="smart-summary-order">
                        {{
                            t('smartPlaylists.summary.sorted', {
                                sort: t('smartPlaylists.sorts.artist'),
                                direction: t('smartPlaylists.direction.asc'),
                            })
                        }}
                    </p>
                </section>
            </div>
            <div data-part="dev-samples">
                <ul data-part="playlist-choices" :aria-label="t('playlists.addDialog.listLabel')">
                    <li>
                        <button type="button" data-part="playlist-choice">
                            <BaseCover :placeholder="false">
                                <BaseIcon name="playlist" />
                            </BaseCover>
                            <span data-part="label">{{
                                t('placeholders.playlist', { number: 1 })
                            }}</span>
                            <span data-part="playlist-choice-count">{{
                                t('library.songCount', { count: 3 })
                            }}</span>
                        </button>
                    </li>
                    <li>
                        <button type="button" data-part="playlist-choice" data-state="busy">
                            <BaseCover :src="sampleCover" />
                            <span data-part="label">{{
                                t('placeholders.playlist', { number: 2 })
                            }}</span>
                            <span data-part="playlist-choice-count">{{
                                t('library.songCount', { count: 12 })
                            }}</span>
                        </button>
                    </li>
                </ul>
            </div>
            <div data-part="dev-samples">
                <BaseButton
                    part="sort"
                    variant="icon"
                    icon="sort"
                    dropdown
                    :label="t('toolbar.sort')"
                    aria-haspopup="menu"
                    @click="openSortSample"
                />
            </div>
            <BaseContextMenu
                ref="sortSampleMenu"
                :items="sortSampleItems"
                :label="t('sort.menu')"
            />
        </section>

        <section data-part="dev-section">
            <h2 data-part="dev-section-title">{{ t('dev.sections.home') }}</h2>
            <div data-part="view" data-view="library" data-section="songs">
                <LibraryScopeSwitch />
                <!-- The filter by media folder shows only with several folders in the store; its markup. -->
                <div data-part="source-filter">
                    <BaseSelect
                        v-model="sampleSourceFilter"
                        :label="t('library.sourceFilter.label')"
                        :options="sampleSourceOptions"
                    />
                </div>
            </div>
            <div data-part="view" data-view="home">
                <HomeShelf
                    item="recently-added"
                    :title="t('library.recentlyAdded')"
                    :tiles="tiles"
                    :to="{ name: 'library' }"
                >
                    <template #actions>
                        <BaseButton data-item="play" icon="play-now" :label="t('library.play')" />
                    </template>
                </HomeShelf>
            </div>
        </section>

        <section data-part="dev-section">
            <h2 data-part="dev-section-title">{{ t('dev.sections.sliders') }}</h2>
            <div data-part="dev-samples">
                <div v-for="(_, index) in sliders" :key="index" data-part="dev-sample">
                    <BaseSlider
                        :model-value="sliders[index] ?? 0"
                        :label="t('dev.samples.slider', { value: index + 1 })"
                        :value-text="format.percent(sliders[index] ?? 0)"
                        @update:model-value="sliders[index] = $event"
                    />
                </div>
                <div data-part="dev-sample">
                    <BaseSlider
                        v-model="disabledSlider"
                        :label="t('dev.samples.sliderDisabled')"
                        disabled
                    />
                </div>
            </div>
        </section>

        <section data-part="dev-section">
            <h2 data-part="dev-section-title">{{ t('dev.sections.equalizer') }}</h2>
            <div data-part="dev-sample">
                <EqualizerPanel
                    :enabled="equalizerOn"
                    preset="Custom"
                    :preamp-db="equalizerPreamp"
                    :gains-db="equalizerGains"
                    :bands="fallbackEqualizerBands"
                    :min-gain-db="fallbackMinGainDb"
                    :max-gain-db="fallbackMaxGainDb"
                    @enable="equalizerOn = $event"
                    @band="(index, db) => (equalizerGains[index] = db)"
                    @preamp="equalizerPreamp = $event"
                />
            </div>
            <div data-part="dev-sample">
                <EqualizerPanel
                    :enabled="false"
                    preset="Flat"
                    :preamp-db="0"
                    :gains-db="flatCurve()"
                    :bands="fallbackEqualizerBands"
                    :min-gain-db="fallbackMinGainDb"
                    :max-gain-db="fallbackMaxGainDb"
                    :available="false"
                />
            </div>
        </section>

        <section data-part="dev-section">
            <h2 data-part="dev-section-title">{{ t('dev.sections.fields') }}</h2>
            <div data-part="dev-samples">
                <BaseTextField v-model="textEmpty" :label="t('dev.samples.textField')" />
                <BaseTextField v-model="textFilled" :label="t('dev.samples.textField')" />
                <BaseTextField v-model="textFilled" :label="t('dev.samples.disabled')" disabled />
                <BaseSearchField
                    v-model="searchEmpty"
                    :label="t('toolbar.search')"
                    :placeholder="t('toolbar.searchPlaceholder')"
                    :clear-label="t('toolbar.clearSearch')"
                />
                <BaseSearchField
                    v-model="searchFilled"
                    :label="t('toolbar.search')"
                    :placeholder="t('toolbar.searchPlaceholder')"
                    :clear-label="t('toolbar.clearSearch')"
                />
            </div>
        </section>

        <section data-part="dev-section">
            <h2 data-part="dev-section-title">{{ t('dev.sections.forms') }}</h2>
            <div data-part="dev-samples">
                <ProfileSummary
                    :name="firstProfile.name"
                    :initials="firstProfile.initials"
                    :color="firstProfile.color"
                />
            </div>
            <ThemePicker
                :model-value="devTheme"
                :themes="themes"
                :label="t('settings.theme')"
                appearance="system"
                :system-dark="devVariant === 'dark'"
                @update:model-value="switchTheme($event, devVariant)"
            />
            <form data-part="form" @submit.prevent>
                <BaseTextField
                    v-model="textFilled"
                    :label="t('dev.samples.textField')"
                    :hint="t('dev.samples.hint')"
                    required
                    :maxlength="50"
                />
                <BaseTextField
                    v-model="textInvalid"
                    :label="t('dev.samples.textField')"
                    :error="t('dev.samples.error')"
                />
                <BaseSelect
                    v-model="selected"
                    :label="t('dev.samples.selectLabel')"
                    :options="selectOptions"
                    :hint="t('dev.samples.hint')"
                />
                <BaseSelect
                    v-model="selected"
                    :label="t('dev.samples.selectLabel')"
                    :options="selectOptions"
                    :error="t('dev.samples.error')"
                />
                <BaseSegmentedField
                    v-model="selected"
                    item="sample"
                    :label="t('dev.samples.selectLabel')"
                    :options="selectOptions"
                    :hint="t('dev.samples.hint')"
                />
                <BaseSegmentedField
                    v-model="selected"
                    item="sample"
                    :label="t('dev.samples.selectLabel')"
                    :options="selectOptions"
                    :error="t('dev.samples.error')"
                />
                <BaseSegmentedField
                    v-model="selected"
                    item="sample"
                    :label="t('dev.samples.selectLabel')"
                    :options="selectOptions"
                    disabled
                />
                <BaseColorPicker
                    v-model="color"
                    :label="t('dev.samples.colorLabel')"
                    :options="colorOptions"
                />
                <div data-part="field-row">
                    <BaseTextField
                        v-model="yearSample"
                        :label="t('trackMetadata.fields.year')"
                        inputmode="numeric"
                        :maxlength="4"
                    />
                    <BaseTextField
                        v-model="numberSample"
                        :label="t('trackMetadata.fields.trackNumber')"
                        inputmode="numeric"
                        :maxlength="4"
                    />
                    <BaseTextField
                        v-model="numberSample"
                        :label="t('trackMetadata.fields.discNumber')"
                        inputmode="numeric"
                        :maxlength="4"
                        disabled
                    />
                </div>
                <p data-part="form-note">{{ t('trackMetadata.emptyRemoves') }}</p>
            </form>
        </section>

        <section data-part="dev-section">
            <h2 data-part="dev-section-title">{{ t('dev.sections.admin') }}</h2>
            <div data-part="view" data-view="admin-users">
                <div data-part="view-actions">
                    <BaseButton variant="primary" icon="add" :label="t('adminUsers.add')" />
                </div>
                <UserList :users="sampleUsers" />
            </div>
        </section>

        <section data-part="dev-section">
            <h2 data-part="dev-section-title">{{ t('dev.sections.administration') }}</h2>
            <div data-part="view" data-view="admin-library">
                <AdminNav />
                <section data-part="section">
                    <h2 data-part="section-title">{{ t('adminSources.title') }}</h2>
                    <div data-part="view-actions">
                        <BaseButton
                            variant="primary"
                            icon="add"
                            data-item="add-source"
                            :label="t('adminSources.add')"
                        />
                    </div>
                    <MediaSourceList :sources="sampleSources" />
                </section>
                <section data-part="section">
                    <h2 data-part="section-title">{{ t('adminLibrary.scanTitle') }}</h2>
                    <ScanPanel :scan="null" activity="idle" />
                    <ScanPanel :scan="sampleDiscovering" activity="running" />
                    <ScanPanel :scan="sampleProcessing" activity="running" />
                    <ScanSummary :scan="sampleFinished" />
                    <ScanIssues :issues="sampleIssues" :failed="3" open />
                    <ScanIssues :issues="sampleSourceIssues" :failed="2" open />
                </section>
                <section data-part="section">
                    <h2 data-part="section-title">{{ t('adminLibrary.historyTitle') }}</h2>
                    <ScanHistory :scans="sampleHistory" has-more />
                </section>
                <section data-part="section">
                    <h2 data-part="section-title">{{ t('adminLibrary.settingsTitle') }}</h2>
                    <form data-part="form" @submit.prevent>
                        <BaseCheckbox
                            v-model="checkboxOn"
                            :label="t('adminLibrary.fileWatcher')"
                            :hint="t('adminLibrary.fileWatcherHint')"
                        />
                        <BaseCheckbox
                            v-model="checkboxOff"
                            :label="t('dev.samples.textField')"
                            :error="t('dev.samples.error')"
                        />
                        <BaseProgress :label="t('adminLibrary.progressLabel')" :value="0.4" />
                        <BaseProgress :label="t('adminLibrary.progressLabel')" />
                    </form>
                </section>
                <section data-part="section" data-item="lyrics-lookups">
                    <h2 data-part="section-title">{{ t('lyricsLookups.title') }}</h2>
                    <LyricsLookupResults :summary="sampleLyricsLookups" result="forgotten" />
                    <LyricsLookupResults :summary="null" />
                </section>
            </div>
            <div data-part="view" data-view="admin-jobs">
                <JobList :jobs="sampleIdleJob" />
                <JobList :jobs="sampleRunningJob" />
                <JobList :jobs="sampleCoverJob" />
            </div>
            <div data-part="view" data-view="admin-cache">
                <section data-part="section" data-item="usage">
                    <h2 data-part="section-title">{{ t('adminCache.usageTitle') }}</h2>
                    <CacheUsagePanel :usage="sampleCacheUsage" />
                    <BaseMeter :label="t('adminCache.usageLabel')" :value="0.95" />
                </section>
            </div>
            <div data-part="view" data-view="admin-backups">
                <section data-part="section" data-item="status">
                    <h2 data-part="section-title">{{ t('adminBackups.statusTitle') }}</h2>
                    <BackupStatusPanel :job="sampleBackupJob" :backups="sampleBackups" />
                </section>
                <section data-part="section" data-item="backups">
                    <h2 data-part="section-title">{{ t('adminBackups.backupsTitle') }}</h2>
                    <BackupList
                        :backups="sampleBackups"
                        deleting="wavemp-20261115T193000Z-manual"
                    />
                    <BackupList :backups="[]" />
                </section>
            </div>
            <div data-part="view" data-view="admin-themes">
                <section data-part="section" data-item="theme-upload">
                    <h2 data-part="section-title">{{ t('adminThemes.uploadTitle') }}</h2>
                    <ThemeErrorList :errors="sampleThemeErrors" />
                </section>
                <section data-part="section" data-item="themes">
                    <h2 data-part="section-title">{{ t('adminThemes.themesTitle') }}</h2>
                    <ThemeList :themes="sampleThemes" busy="aurora" />
                    <ThemeList :themes="[]" />
                </section>
            </div>
            <div data-part="view" data-view="settings">
                <section data-part="section" data-item="scrobbling">
                    <h2 data-part="section-title">{{ t('scrobbling.title') }}</h2>
                    <ScrobbleList :services="sampleScrobbleServices" busy="LastFm" />
                    <ScrobbleList :services="[]" />
                </section>
            </div>
            <div data-part="view" data-view="admin-files">
                <section data-part="section" data-item="songs">
                    <h2 data-part="section-title">{{ t('adminFiles.songsTitle') }}</h2>
                    <FileSelectionFilters
                        v-model:search="fileSearch"
                        v-model:filter="fileFilter"
                        v-model:sort="fileSort"
                    />
                    <FileSelectionList :items="fileSummaries" :label="t('adminFiles.listLabel')" />
                </section>
            </div>
            <div data-part="view" data-view="admin-checks">
                <section data-part="section" data-item="check-status">
                    <h2 data-part="section-title">{{ t('adminChecks.title') }}</h2>
                    <LibraryCheckStatus :summary="libraryIssueSummary" :job="null" />
                </section>
                <section data-part="section" data-item="issues">
                    <h2 data-part="section-title">{{ t('adminChecks.issuesTitle') }}</h2>
                    <LibraryIssueFilters
                        v-model:search="issueSearch"
                        v-model:filter="issueFilter"
                        v-model:sort="issueSort"
                        :summary="libraryIssueSummary"
                    />
                    <LibraryIssueList :items="libraryIssues" :label="t('adminChecks.listLabel')" />
                </section>
            </div>
            <div data-part="view" data-view="admin-review">
                <section data-part="section" data-item="review">
                    <h2 data-part="section-title">{{ t('reviewInbox.listTitle') }}</h2>
                    <ReviewFilters
                        v-model:search="reviewSearch"
                        v-model:filter="reviewFilter"
                        v-model:sort="reviewSort"
                    />
                    <ReviewBulkBar :selected-count="reviewSelected.size" :selectable-count="2" />
                    <ReviewList
                        :items="reviewSamples"
                        :reviewed="reviewDone"
                        :selected="reviewSelected"
                        :label="t('reviewInbox.listLabel')"
                    />
                </section>
            </div>
            <div data-part="view" data-view="admin-review-track">
                <ReviewActions form-id="dev-review-form" :can-save="true" />
                <ReviewNav :index="1" :count="3" :has-previous="true" :has-next="true" />
                <div data-part="empty-state" data-state="finished">
                    <p data-part="empty-state-title">{{ t('reviewInbox.finished') }}</p>
                    <p data-part="empty-state-text">{{ t('reviewInbox.finishedText') }}</p>
                </div>
            </div>
            <div data-part="view" data-view="admin-track-files">
                <section
                    v-for="category in fileCategories"
                    :key="category"
                    data-part="section"
                    :data-item="category"
                >
                    <h2 :id="`dev-file-choices-${category}`" data-part="section-title">
                        {{ t(`adminFiles.categories.${category}`) }}
                    </h2>
                    <FileChoiceGroup
                        :category="category"
                        :choices="fileChoices(fileSelection[category], files)"
                        :labelledby="`dev-file-choices-${category}`"
                    />
                </section>
            </div>
            <div data-part="online-search" data-owner="track" data-state="ready">
                <section data-part="online-section" data-item="matches">
                    <h3 id="dev-online-matches" data-part="online-section-title">
                        {{ t('onlineMetadata.matches') }}
                    </h3>
                    <OnlineMatchList
                        v-model="onlineSelected"
                        owner="track"
                        :matches="onlineMatches"
                        labelledby="dev-online-matches"
                    />
                </section>
                <section data-part="online-section" data-item="compare">
                    <h3 data-part="online-section-title">{{ t('onlineMetadata.compare') }}</h3>
                    <OnlineCompareTable
                        v-model:use-cover="onlineUseCover"
                        owner="track"
                        :id="null"
                        :has-cover="false"
                        :rows="onlineRows"
                        :chosen="onlineChosen"
                        :tags-writable="true"
                        :show-cover="true"
                        cover-state="missing"
                        :cover-url="null"
                    />
                </section>
            </div>
            <div data-part="view" data-view="admin-transcoding">
                <section data-part="section" data-item="status">
                    <h2 data-part="section-title">{{ t('adminTranscoding.statusTitle') }}</h2>
                    <TranscodingStatusPanel :status="sampleTranscodingStatus" />
                </section>
                <section data-part="section" data-item="jobs">
                    <h2 data-part="section-title">{{ t('adminTranscoding.jobs') }}</h2>
                    <TranscodeJobList :jobs="sampleTranscodeJobs" />
                    <TranscodeJobList :jobs="[]" />
                </section>
            </div>
        </section>

        <section data-part="dev-section">
            <h2 data-part="dev-section-title">{{ t('dev.sections.dialog') }}</h2>
            <div data-part="dev-samples">
                <BaseButton :label="t('dev.samples.openDialog')" @click="dialogOpen = true" />
                <BaseButton :label="t('shortcuts.title')" @click="shortcutsOpen = true" />
            </div>
            <ShortcutOverview v-model:open="shortcutsOpen" :shortcuts="defaultShortcuts" />
            <BaseDialog v-model:open="dialogOpen" :title="t('dev.samples.dialogTitle')">
                <p>{{ t('dev.samples.dialogText') }}</p>
                <template #footer>
                    <BaseButton :label="t('actions.cancel')" @click="dialogOpen = false" />
                    <BaseButton
                        variant="primary"
                        :label="t('actions.confirm')"
                        @click="dialogOpen = false"
                    />
                </template>
            </BaseDialog>
        </section>

        <section data-part="dev-section">
            <h2 data-part="dev-section-title">{{ t('dev.sections.contextMenu') }}</h2>
            <div data-part="dev-samples">
                <BaseButton
                    :label="t('dev.samples.openContextMenu')"
                    aria-haspopup="menu"
                    @click="openMenuAtElement"
                />
                <div data-part="dev-context-area" @contextmenu.prevent="openMenuAtPointer">
                    {{ t('dev.samples.contextMenuArea') }}
                </div>
            </div>
            <BaseContextMenu
                ref="menu"
                :items="menuItems"
                :label="t('dev.samples.openContextMenu')"
            />
        </section>

        <section data-part="dev-section">
            <h2 data-part="dev-section-title">{{ t('dev.sections.alerts') }}</h2>
            <div data-part="dev-samples">
                <BaseAlert tone="error" :message="t('dev.samples.alertError')" />
                <BaseAlert tone="success" :message="t('dev.samples.alertSuccess')" />
                <BaseAlert tone="info" :message="t('dev.samples.alertInfo')" />
            </div>
        </section>

        <section data-part="dev-section">
            <h2 data-part="dev-section-title">{{ t('dev.sections.profiles') }}</h2>
            <div data-part="dev-sample">
                <ProfileSelect
                    :profiles="sampleProfiles"
                    state="ready"
                    current-id="first"
                    :error="t('profiles.selectError')"
                />
            </div>
            <div data-part="dev-samples">
                <div data-part="dev-sample">
                    <p data-part="dev-sample-title">{{ t('dev.samples.firstStart') }}</p>
                    <ProfileSelect :profiles="[adminProfile]" state="ready" />
                </div>
                <div data-part="dev-sample">
                    <p data-part="dev-sample-title">{{ t('dev.samples.loadError') }}</p>
                    <ProfileSelect :profiles="[]" state="error" />
                </div>
                <div data-part="dev-sample">
                    <p data-part="dev-sample-title">{{ t('dev.samples.pinEntry') }}</p>
                    <ProfileSelect
                        :profiles="sampleProfiles"
                        state="ready"
                        :pin-profile="sampleProfiles[1]"
                        :pin-error="t('pin.wrong')"
                    />
                </div>
                <div data-part="dev-sample">
                    <p data-part="dev-sample-title">{{ t('dev.samples.pinPad') }}</p>
                    <PinPad v-model="samplePin" :label="t('pin.field')" />
                </div>
                <div data-part="dev-sample">
                    <p data-part="dev-sample-title">{{ t('dev.samples.avatarPresets') }}</p>
                    <div data-part="avatar-editor" data-state="preset">
                        <ProfileAvatar
                            :initials="firstProfile.initials"
                            :color="firstProfile.color"
                            :image="sampleCover"
                        />
                        <AvatarPresetPicker
                            v-model="samplePreset"
                            :label="t('avatars.presetList')"
                            :presets="samplePresets"
                            :pictures="samplePresetPictures"
                        />
                    </div>
                </div>
                <div data-part="dev-sample">
                    <p data-part="dev-sample-title">{{ t('dev.samples.avatarCrop') }}</p>
                    <div data-part="avatar-editor" data-state="upload">
                        <AvatarCropper
                            v-model="sampleCrop"
                            :src="sampleCover"
                            :picture="sampleCropPicture"
                        />
                    </div>
                </div>
            </div>
        </section>

        <section data-part="dev-section">
            <h2 data-part="dev-section-title">{{ t('dev.sections.header') }}</h2>
            <AppHeader active-tab="library" :profile="firstProfile" :location="libraryLocation" />
            <ReviewInboxButton :poll="false" />
        </section>

        <section data-part="dev-section">
            <h2 data-part="dev-section-title">{{ t('dev.sections.toolbar') }}</h2>
            <AppToolbar :location="libraryLocation" />
        </section>

        <section data-part="dev-section">
            <h2 data-part="dev-section-title">{{ t('dev.sections.sidebar') }}</h2>
            <div data-part="dev-sample">
                <AppSidebar :location="libraryLocation" />
            </div>
            <div data-part="dev-sample">
                <p data-part="dev-sample-title">{{ t('dev.sidebarCollapsed') }}</p>
                <AppSidebar :location="libraryLocation" collapsed />
            </div>
        </section>

        <section data-part="dev-section">
            <h2 data-part="dev-section-title">{{ t('dev.sections.transport') }}</h2>
            <div v-for="transport in transports" :key="transport.id" data-part="dev-sample">
                <p data-part="dev-sample-title">{{ transport.label }}</p>
                <AppTransport
                    :state="transport.state"
                    :expanded="transport.expanded"
                    :resume="transport.resume"
                    :stage="transport.stage"
                />
            </div>
        </section>

        <section data-part="dev-section">
            <h2 data-part="dev-section-title">{{ t('dev.sections.compactPlayer') }}</h2>
            <div data-part="dev-samples">
                <div data-part="dev-sample">
                    <p data-part="dev-sample-title">{{ t('dev.compactPlayerSamples.inline') }}</p>
                    <CompactPlayer kind="inline" :state="idle" />
                </div>
                <div data-part="dev-sample">
                    <p data-part="dev-sample-title">{{ t('dev.compactPlayerSamples.window') }}</p>
                    <CompactPlayer
                        kind="window"
                        :state="{ ...idle, ...track, playback: 'playing' }"
                    />
                </div>
                <div data-part="dev-sample" data-kind="wide">
                    <p data-part="dev-sample-title">{{ t('dev.compactPlayerSamples.wide') }}</p>
                    <CompactPlayer
                        kind="window"
                        :state="{ ...idle, ...track, playback: 'paused' }"
                        video
                    />
                </div>
            </div>
        </section>

        <section data-part="dev-section">
            <h2 data-part="dev-section-title">{{ t('dev.sections.bottomNav') }}</h2>
            <div data-part="dev-sample">
                <AppBottomNav active-tab="library" />
            </div>
        </section>

        <section data-part="dev-section">
            <h2 data-part="dev-section-title">{{ t('dev.sections.recaps') }}</h2>
            <div data-part="view" data-view="recap">
                <RecapNav />
                <RecapPeriodPicker
                    v-model="sampleRecapPeriod"
                    v-model:custom="sampleRecapRange"
                    invalid
                />
                <div data-part="recap-layout">
                    <div data-part="recap-stage">
                        <RecapStory
                            :slides="sampleRecapSlides"
                            design="aurora"
                            :label="t('recap.title')"
                        />
                    </div>
                    <div data-part="recap-panel">
                        <RecapExport
                            :slides="sampleRecapSlides"
                            :index="0"
                            design="aurora"
                            :story="null"
                            file-stem="dev"
                        />
                    </div>
                </div>
                <div data-part="dev-samples">
                    <div
                        v-for="design in ['aurora', 'frost', 'prism']"
                        :key="design"
                        data-part="recap-story"
                        :data-recap-design="design"
                        data-decoration="glow"
                    >
                        <span data-part="recap-backdrop"></span>
                        <div data-part="recap-slides">
                            <RecapStorySlide
                                v-for="slide in sampleRecapSlides"
                                :key="slide.kind"
                                :slide="slide"
                            />
                        </div>
                    </div>
                </div>
            </div>
            <div data-part="view" data-view="statistics">
                <StatisticsChart
                    :bars="sampleRecapBars"
                    :label="t('statistics.timeline')"
                    interactive
                />
                <StatisticsTable
                    labelledby="dev-statistics"
                    :columns="sampleStatisticsColumns"
                    :rows="sampleStatisticsRows"
                />
                <RecapShareList :shares="sampleRecapShares" />
                <RecapShareList :shares="sampleRecapShares" own />
            </div>
        </section>

        <section data-part="dev-section">
            <h2 data-part="dev-section-title">{{ t('dev.sections.states') }}</h2>
            <div data-part="dev-samples">
                <div data-part="dev-sample">
                    <div data-part="view" data-view="library" data-section="albums">
                        <LibraryEmpty />
                    </div>
                </div>
                <div data-part="dev-sample">
                    <NowPlayingView />
                </div>
                <div data-part="dev-sample">
                    <div data-part="view" data-view="now-playing">
                        <NowPlayingStage
                            :track="sampleQueue[sampleQueueIndex]?.track"
                            :mode="sampleViewMode"
                            :view="
                                stageViewOf(
                                    sampleViewMode,
                                    sampleQueue[sampleQueueIndex]?.track.hasVideo === true,
                                    'ready',
                                )
                            "
                            video-state="ready"
                            @select-mode="sampleViewMode = $event"
                        />
                        <PlayQueue
                            :items="sampleQueue"
                            :current-index="sampleQueueIndex"
                            @play="
                                (key) =>
                                    (sampleQueueIndex = sampleQueue.findIndex(
                                        (item) => item.key === key,
                                    ))
                            "
                        >
                            <AutoplayList
                                :entries="sampleAutoplay"
                                :loading="false"
                                @remove="
                                    (key) =>
                                        (sampleAutoplay = sampleAutoplay.filter(
                                            (entry) => entry.key !== key,
                                        ))
                                "
                            />
                        </PlayQueue>
                    </div>
                </div>
                <div data-part="dev-sample">
                    <div data-part="view" data-view="now-playing">
                        <NowPlayingStage
                            :track="sampleVideoTrack"
                            :mode="sampleViewMode"
                            :view="stageViewOf(sampleViewMode, true, 'failed')"
                            video-state="failed"
                            @select-mode="sampleViewMode = $event"
                        />
                    </div>
                </div>
                <div data-part="dev-sample">
                    <div data-part="view" data-view="now-playing">
                        <div data-part="stage">
                            <VisualizationControls
                                :presets="sampleVisualizationPresets"
                                :preset="sampleVisualizationPreset"
                                :color-schemes="sampleVisualizationColorSchemes"
                                :color-scheme="sampleVisualizationColorScheme"
                                :settings="sampleVisualization"
                                fullscreen-available
                                @select-preset="sampleVisualizationPreset = $event"
                                @select-color-scheme="sampleVisualizationColorScheme = $event"
                                @select-module="
                                    sampleVisualization = { ...sampleVisualization, module: $event }
                                "
                                @toggle-auto-change="
                                    sampleVisualization = {
                                        ...sampleVisualization,
                                        autoChange: !sampleVisualization.autoChange,
                                    }
                                "
                            />
                        </div>
                    </div>
                </div>
                <div data-part="dev-sample">
                    <div data-part="view" data-view="now-playing">
                        <NowPlayingStage
                            :track="sampleLyricsTrack"
                            :mode="sampleLyricsMode"
                            :view="stageViewOf(sampleLyricsMode, false, 'ready', true)"
                            :lyrics="sampleTrackLyrics"
                            :position="sampleLyricsPosition"
                            :duration="24"
                            @select-mode="sampleLyricsMode = $event"
                        />
                    </div>
                </div>
                <div data-part="dev-sample">
                    <PlayQueue :items="sampleQueue.slice(0, 2)" :current-index="1">
                        <AutoplayList :entries="[]" :loading="true" />
                    </PlayQueue>
                </div>
                <div data-part="dev-sample">
                    <PlaylistsView />
                </div>
                <div data-part="dev-sample">
                    <NotFoundView />
                </div>
            </div>
        </section>

        <section data-part="dev-section">
            <h2 data-part="dev-section-title">{{ t('dev.sections.previews') }}</h2>
            <div data-part="dev-previews">
                <figure data-part="dev-preview" data-device="mobile">
                    <figcaption data-part="dev-sample-title">
                        {{ t('dev.previews.mobile') }}
                    </figcaption>
                    <iframe
                        data-part="dev-frame"
                        data-device="mobile"
                        src="/library/songs"
                        :title="t('dev.previews.mobile')"
                    ></iframe>
                </figure>
                <figure data-part="dev-preview" data-device="desktop">
                    <figcaption data-part="dev-sample-title">
                        {{ t('dev.previews.desktop') }}
                    </figcaption>
                    <iframe
                        data-part="dev-frame"
                        data-device="desktop"
                        src="/library/songs"
                        :title="t('dev.previews.desktop')"
                    ></iframe>
                </figure>
            </div>
        </section>

        <section data-part="dev-section">
            <h2 data-part="dev-section-title">{{ t('dev.sections.parts') }}</h2>
            <ul data-part="dev-part-list" :aria-label="t('dev.table.part')">
                <li v-for="part in parts" :key="part" data-part="dev-part">
                    <code>{{ part }}</code>
                </li>
            </ul>
        </section>

        <section data-part="dev-section">
            <h2 data-part="dev-section-title">{{ t('dev.sections.icons') }}</h2>
            <table data-part="dev-table">
                <thead>
                    <tr>
                        <th scope="col">{{ t('dev.table.preview') }}</th>
                        <th scope="col">{{ t('dev.table.slot') }}</th>
                        <th scope="col">{{ t('dev.table.file') }}</th>
                    </tr>
                </thead>
                <tbody>
                    <tr v-for="slot in iconSlotNames" :key="slot">
                        <td><BaseIcon :name="slot" /></td>
                        <td>
                            <code>{{ slot }}</code>
                        </td>
                        <td>
                            <code>{{ iconSlots[slot].file }}</code>
                        </td>
                    </tr>
                </tbody>
            </table>
        </section>
    </div>
</template>
