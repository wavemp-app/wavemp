<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import BaseCover from '@/components/base/BaseCover.vue'
import BaseSegmentedControl, {
    type SegmentOption,
} from '@/components/base/BaseSegmentedControl.vue'
import LibraryCover from '@/components/library/LibraryCover.vue'
import ItemMenuButton from '@/components/menus/ItemMenuButton.vue'
import { useContextMenuTrigger } from '@/composables/useContextMenuTrigger'
import { useShortcut } from '@/composables/useShortcut'
import { useContextMenuStore } from '@/stores/contextMenu'
import LyricsStage from './LyricsStage.vue'
import StageVisualization from './StageVisualization.vue'
import VideoStage from './VideoStage.vue'
import { artistLabelKey } from '@/library/presentation'
import type { TrackLyrics } from '@/lyrics/timing'
import { PlayerViewModeObject, type PlayerViewMode, type Track } from '@/api/generated/models'
import type { StageView, VideoState } from '@/player/videoSync'
import {
    defaultVisualizationSettings,
    type VisualizationSettings,
    type VisualizationSettingsChange,
} from '@/visualization/settings'

/**
 * The stage of Now Playing: the cover, the video, the visualization or the lyrics of the current
 * track, with title, artist and album, or a hint while nothing plays. The views lie on top of each
 * other and fade into each other; the buttons choose the view, the music goes on. The video view
 * stays in the page as long as the track has a video, the lyrics view as long as it has lyrics, so
 * switching back to them is quick. While a casting device plays, the stage names it, shows the cover
 * instead of the video (the device shows it) and offers no visualization. The context menu of the
 * stage (right click, long press, the button "More options" or the keyboard) chooses the view and
 * shows the video or the visualization in full screen, like the key F.
 */
const props = withDefaults(
    defineProps<{
        track?: Track
        /** The view mode chosen by the profile. */
        mode?: PlayerViewMode
        /** The view the stage shows. */
        view?: StageView
        /** State of the video of the track. */
        videoState?: VideoState
        /** The output of the player for the visualization. */
        audio?: AnalyserNode | null
        /** The visualization settings of the profile. */
        visualization?: VisualizationSettings
        /** Whether videos show the ambient light around their picture. */
        ambient?: boolean
        /** The lyrics of the track; null for tracks without lyrics. */
        lyrics?: TrackLyrics | null
        /** Whether the music plays. */
        playing?: boolean
        /** The position of the music in seconds. */
        position?: number
        /** Duration of the track in seconds. */
        duration?: number
        /** The exact position of the music right now, in seconds. */
        clock?: () => number
        /** Whether a casting device plays the music. */
        casting?: boolean
        /** The notice while casting, e.g. "Playing on Living Room". */
        castNotice?: string
        /** Whether the full-screen player covers the stage; its visualization rests meanwhile. */
        covered?: boolean
    }>(),
    {
        track: undefined,
        mode: PlayerViewModeObject.Video,
        view: 'cover',
        videoState: 'idle',
        audio: null,
        visualization: () => defaultVisualizationSettings,
        ambient: true,
        lyrics: null,
        playing: false,
        position: 0,
        duration: 0,
        clock: undefined,
        casting: false,
        castNotice: '',
        covered: false,
    },
)

const emit = defineEmits<{
    selectMode: [mode: PlayerViewMode]
    /** The video element of the stage is shown; the player shows videos there. */
    attachVideo: [element: HTMLVideoElement]
    /** The video element closes. */
    detachVideo: [element: HTMLVideoElement]
    /** A change of the visualization settings (preset, automatic change). */
    updateVisualization: [change: VisualizationSettingsChange]
    /** A line of the lyrics was chosen: the music goes to its start, in seconds. */
    seek: [seconds: number]
}>()

const { t } = useI18n()

const hasVideo = computed(() => props.track?.hasVideo === true)
const hasLyrics = computed(
    () =>
        props.lyrics !== null &&
        props.track?.id !== undefined &&
        props.lyrics.trackId === props.track.id,
)

/** data-state of the video view: shown, still loading or positioning, or failed. */
const videoViewState = computed(() => {
    if (props.view === 'video') {
        return 'active'
    }

    return props.videoState === 'loading' || props.videoState === 'failed'
        ? props.videoState
        : undefined
})

const videoFailed = computed(
    () =>
        hasVideo.value &&
        props.mode === PlayerViewModeObject.Video &&
        props.videoState === 'failed',
)

/**
 * The pressed button: the chosen mode, but the cover for a track without a video or with a failed
 * one and for a track without lyrics. While the video loads, its button is pressed and the cover
 * stays until the video is ready.
 */
const selected = computed<PlayerViewMode>(() => {
    if (props.mode === PlayerViewModeObject.Video && (!hasVideo.value || videoFailed.value)) {
        return PlayerViewModeObject.Cover
    }

    if (props.mode === PlayerViewModeObject.Visualization && props.casting) {
        return PlayerViewModeObject.Cover
    }

    return props.mode === PlayerViewModeObject.Lyrics && !hasLyrics.value
        ? PlayerViewModeObject.Cover
        : props.mode
})

const viewOptions = computed<SegmentOption[]>(() => [
    {
        value: PlayerViewModeObject.Cover,
        item: 'cover',
        icon: 'view-cover',
        label: t('nowPlaying.viewCover'),
    },
    {
        value: PlayerViewModeObject.Video,
        item: 'video',
        icon: 'view-video',
        label: t('nowPlaying.viewVideo'),
        disabled: !hasVideo.value,
    },
    {
        value: PlayerViewModeObject.Visualization,
        item: 'visualization',
        icon: 'view-visualization',
        label: props.casting
            ? t('cast.unavailableWhileCasting', { name: t('nowPlaying.viewVisualization') })
            : t('nowPlaying.viewVisualization'),
        disabled: props.casting,
    },
    {
        value: PlayerViewModeObject.Lyrics,
        item: 'lyrics',
        icon: 'view-lyrics',
        label: t('nowPlaying.viewLyrics'),
        disabled: !hasLyrics.value,
    },
])

function selectMode(value: string): void {
    const mode = viewOptions.value.find((option) => option.value === value)?.value
    if (mode) {
        emit('selectMode', mode as PlayerViewMode)
    }
}

const contextMenu = useContextMenuStore()
const videoStage = ref<InstanceType<typeof VideoStage> | null>(null)
const stageVisualization = ref<InstanceType<typeof StageVisualization> | null>(null)

/** The shown view that has a full screen: the video or the visualization. */
function fullscreenView() {
    return props.view === 'video'
        ? videoStage.value
        : props.view === 'visualization'
          ? stageVisualization.value
          : null
}

function openMenu(_: unknown, x: number, y: number): void {
    if (!props.track) {
        return
    }

    const shown = fullscreenView()
    contextMenu.open(
        {
            type: 'now-playing',
            views: viewOptions.value.map((option) => ({
                mode: option.value as PlayerViewMode,
                available: !option.disabled,
            })),
            selected: selected.value,
            selectView: (mode) => emit('selectMode', mode),
            fullscreen: {
                available: !!shown?.fullscreenAvailable,
                active: !!shown?.fullscreen,
                toggle: () => void shown?.toggleFullscreen(),
            },
        },
        x,
        y,
    )
}

const trigger = useContextMenuTrigger(openMenu)

// The key F shows the video or the visualization in full screen and leaves it again; with the
// cover or the lyrics the shell shows the whole app in full screen instead.
useShortcut('fullscreen', () => {
    const shown = fullscreenView()
    if (!props.track || !shown?.fullscreenAvailable) {
        return false
    }

    void shown.toggleFullscreen()
})

const subtitle = computed(() => {
    const key = artistLabelKey(props.track?.artist)
    const artist = key ? t(key) : (props.track?.artist?.name ?? '')
    return [artist, props.track?.album?.title ?? ''].filter((part) => part !== '').join(' – ')
})
</script>

<template>
    <div data-part="stage" :data-state="view" @keydown="trigger.onKeydown($event, null)">
        <div
            data-part="stage-views"
            @contextmenu="trigger.onContextmenu($event, null)"
            @pointerdown="trigger.onPointerdown($event, null)"
            @pointermove="trigger.onPointermove"
            @pointerup="trigger.cancel"
            @pointercancel="trigger.cancel"
            @pointerleave="trigger.cancel"
            @click.capture="trigger.onClickCapture"
        >
            <div
                data-part="stage-view"
                data-kind="cover"
                :data-state="view === 'cover' ? 'active' : undefined"
                :aria-hidden="view === 'cover' ? undefined : 'true'"
            >
                <LibraryCover
                    v-if="track?.id"
                    kind="track"
                    :id="track.id"
                    :available="track.hasCover ?? false"
                />
                <BaseCover v-else />
            </div>
            <div
                v-if="hasVideo"
                data-part="stage-view"
                data-kind="video"
                :data-state="videoViewState"
                :aria-hidden="view === 'video' ? undefined : 'true'"
            >
                <VideoStage
                    ref="videoStage"
                    :active="view === 'video'"
                    :ambient="ambient"
                    @attach="emit('attachVideo', $event)"
                    @detach="emit('detachVideo', $event)"
                />
            </div>
            <div
                data-part="stage-view"
                data-kind="visualization"
                :data-state="view === 'visualization' ? 'active' : undefined"
                :aria-hidden="view === 'visualization' ? undefined : 'true'"
            >
                <StageVisualization
                    ref="stageVisualization"
                    :active="view === 'visualization' && !covered"
                    :audio="audio"
                    :settings="visualization"
                    @update="emit('updateVisualization', $event)"
                />
            </div>
            <div
                v-if="hasLyrics"
                data-part="stage-view"
                data-kind="lyrics"
                :data-state="view === 'lyrics' ? 'active' : undefined"
                :aria-hidden="view === 'lyrics' ? undefined : 'true'"
            >
                <LyricsStage
                    :lyrics="lyrics"
                    :active="view === 'lyrics'"
                    :playing="playing"
                    :position="position"
                    :duration="duration"
                    :clock="clock"
                    @seek="emit('seek', $event)"
                />
            </div>
        </div>
        <p v-if="videoFailed && !casting" data-part="stage-notice" role="status">
            {{ t('nowPlaying.videoFailed') }}
        </p>
        <p v-if="casting && castNotice" data-part="stage-notice" data-kind="cast" role="status">
            {{ castNotice }}
        </p>
        <div v-if="track" data-part="stage-info">
            <p data-part="stage-title">{{ track.title }}</p>
            <p data-part="stage-subtitle">{{ subtitle }}</p>
        </div>
        <div v-else data-part="empty-state">
            <p data-part="empty-state-title">{{ t('nowPlaying.emptyTitle') }}</p>
            <p data-part="empty-state-text">{{ t('nowPlaying.emptyText') }}</p>
        </div>
        <div v-if="track" data-part="stage-actions">
            <BaseSegmentedControl
                item="stage-view"
                :label="t('nowPlaying.stageActions')"
                :options="viewOptions"
                :model-value="selected"
                @update:model-value="selectMode"
            />
            <ItemMenuButton @open="(x: number, y: number) => openMenu(null, x, y)" />
        </div>
    </div>
</template>
