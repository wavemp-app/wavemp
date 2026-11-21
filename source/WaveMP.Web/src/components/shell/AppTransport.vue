<script setup lang="ts">
import { computed, onBeforeUnmount, ref, useId, watch } from 'vue'
import { I18nT, useI18n } from 'vue-i18n'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseIcon from '@/components/base/BaseIcon.vue'
import BaseCover from '@/components/base/BaseCover.vue'
import BaseMarquee from '@/components/base/BaseMarquee.vue'
import BaseSegmentedControl, {
    type SegmentOption,
} from '@/components/base/BaseSegmentedControl.vue'
import BaseSlider from '@/components/base/BaseSlider.vue'
import FavoriteButton from '@/components/collection/FavoriteButton.vue'
import CastButton from '@/components/player/CastButton.vue'
import PlayerVisualization from '@/components/player/PlayerVisualization.vue'
import RatingStars from '@/components/collection/RatingStars.vue'
import SleepTimerControl from '@/components/player/SleepTimerControl.vue'
import LibraryCover from '@/components/library/LibraryCover.vue'
import { useFormat } from '@/composables/useFormat'
import { useMediaQuery } from '@/composables/useMediaQuery'
import { useSwipeDismiss } from '@/composables/useSwipeDismiss'
import { PlayerViewModeObject, type PlayerViewMode } from '@/api/generated/models'
import type { VisualizationSettings } from '@/visualization/settings'
import { castStatusText } from '@/casting/presentation'
import { desktopMediaQuery } from '@/themes/breakpoints'
import {
    transportIcons,
    transportNeedsVideo,
    transportNeedsVisualization,
    transportStateTokens,
    transportView,
    transportViewChoice,
    type TransportStage,
    type PlayerMessage,
    type SleepTimerChoice,
    type TransportResume,
    type TransportState,
} from './transport'

/**
 * Transport bar: seek bar, mini player and playback controls. It is controlled through props and
 * events only; on mobile, the same DOM becomes the mini player and the full-screen player. The heart
 * and the stars of the current track save the favorite and the rating of the profile themselves.
 * While a casting device plays, the bar names it and greys out the crossfade and the equalizer.
 * The compact mode button opens and closes the mini player (desktop only).
 *
 * The full-screen player (mobile) closes with its button or by swiping it down (useSwipeDismiss).
 * Like the stage of Now Playing, it shows the cover, the video or the visualization the profile
 * chose; tapping the cover and title opens its view switch. Its video element is handed to the
 * player (attachVideo) and taken back before it goes (detachVideo). Title and artist scroll when
 * they are too long for their place (BaseMarquee).
 */
const props = withDefaults(
    defineProps<{
        state: TransportState
        /** Full-screen player on mobile. */
        expanded?: boolean
        /** Whether the mini player (compact mode) is open. */
        compact?: boolean
        /** The state of another device was taken over: the notice to continue from there. */
        resume?: TransportResume | null
        /** The views of the full-screen player; without it, it shows the cover only. */
        stage?: TransportStage | null
        /** The output of the player for the visualization of the full-screen player. */
        audio?: AnalyserNode | null
        /** The visualization settings of the profile. */
        visualization?: VisualizationSettings
    }>(),
    {
        expanded: false,
        compact: false,
        resume: null,
        stage: null,
        audio: null,
        visualization: undefined,
    },
)

const emit = defineEmits<{
    play: []
    pause: []
    stop: []
    previous: []
    next: []
    seek: [position: number]
    toggleShuffle: []
    cycleRepeat: []
    toggleCrossfade: []
    toggleAutoplay: []
    toggleMute: []
    changeVolume: [volume: number]
    compact: []
    openEqualizer: []
    sleepTimer: [choice: SleepTimerChoice]
    /** Opens the device selection for casting. */
    cast: []
    showNowPlaying: []
    expand: []
    collapse: []
    /** Plays the state taken over from another device. */
    resume: []
    dismissResume: []
    /** The view switch of the full-screen player chose a view (the same choice as on Now Playing). */
    selectView: [mode: PlayerViewMode]
    /** The video element of the full-screen player is shown; the player shows videos there. */
    attachVideo: [element: HTMLVideoElement]
    /** The video element of the full-screen player closes. */
    detachVideo: [element: HTMLVideoElement]
}>()

const { t } = useI18n()
const format = useFormat()
const isDesktop = useMediaQuery(desktopMediaQuery)

const idle = computed(() => props.state.playback === 'idle')
/** Without a current track there is nothing to play, skip or seek. */
const empty = computed(() => !props.state.trackId)
const playing = computed(() => props.state.playback === 'playing')
const icons = computed(() => transportIcons(props.state))
const root = ref<HTMLElement | null>(null)
const videoElement = ref<HTMLVideoElement | null>(null)
const viewSwitchOpen = ref(false)
const viewSwitchId = useId()

const swipe = useSwipeDismiss(root, {
    enabled: () => props.expanded && !isDesktop.value,
    onDismiss: () => emit('collapse'),
})
const swipePhase = swipe.phase
const swipeOffset = swipe.offset
const swipeProgress = swipe.progress
const swipeDuration = swipe.duration

const tokens = computed(() =>
    transportStateTokens(props.state.playback, props.expanded, props.state.loading ?? false, {
        swipe: swipePhase.value,
        viewSwitch: viewSwitchOpen.value,
    }),
)

/** The views of the full-screen player; on the bar it shows the cover only. */
const fullScreen = computed(() => props.expanded)
const needsVideo = computed(() => fullScreen.value && transportNeedsVideo(props.stage))
const needsVisualization = computed(
    () => fullScreen.value && transportNeedsVisualization(props.stage),
)
const shownView = computed(() => (fullScreen.value ? transportView(props.stage) : 'cover'))
/** data-state of player-media: the shown view, and "loading" while the chosen video loads. */
const mediaTokens = computed(() =>
    needsVideo.value && shownView.value !== 'video'
        ? `${shownView.value} loading`
        : shownView.value,
)

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
        disabled: !props.stage?.hasVideo,
    },
    {
        value: PlayerViewModeObject.Visualization,
        item: 'visualization',
        icon: 'view-visualization',
        label: props.stage?.casting
            ? t('cast.unavailableWhileCasting', { name: t('nowPlaying.viewVisualization') })
            : t('nowPlaying.viewVisualization'),
        disabled: !!props.stage?.casting,
    },
])

const viewChoice = computed(() =>
    props.stage ? transportViewChoice(props.stage) : PlayerViewModeObject.Cover,
)

function selectView(value: string): void {
    const option = viewOptions.value.find((item) => item.value === value)
    if (option && !option.disabled) {
        emit('selectView', option.value as PlayerViewMode)
    }
}

/** What the button with cover and title does, for assistive technology. */
const miniPlayerLabel = computed(() => {
    if (isDesktop.value) {
        return t('player.showNowPlaying')
    }

    return props.expanded ? t('player.chooseView') : t('player.open')
})

/** aria-expanded of the button: the full-screen player on the bar, the view switch in it. */
const miniPlayerExpanded = computed(() => {
    if (isDesktop.value) {
        return undefined
    }

    return (props.expanded ? viewSwitchOpen.value : false) ? 'true' : 'false'
})

// The view switch starts closed every time the full-screen player opens.
watch(
    () => props.expanded,
    (expanded) => {
        if (!expanded) {
            viewSwitchOpen.value = false
        }
    },
)

// The video element comes and goes with the chosen video; the player follows it.
watch(
    videoElement,
    (element, previous) => {
        if (previous) {
            emit('detachVideo', previous)
        }
        if (element) {
            emit('attachVideo', element)
        }
    },
    { flush: 'post' },
)

onBeforeUnmount(() => {
    if (videoElement.value) {
        emit('detachVideo', videoElement.value)
    }
})

const messageText = computed(() => {
    const messages: Record<PlayerMessage, string> = {
        unsupported: t('player.messages.unsupported'),
        unavailable: t('player.messages.unavailable'),
        failed: t('player.messages.failed'),
    }
    return props.state.message ? messages[props.state.message] : ''
})

/** Whether equalizer and crossfade apply; not while a casting device plays. */
const effects = computed(() => props.state.effects !== false)

/** The label of a button that rests while casting. */
function effectLabel(name: string): string {
    return effects.value ? name : t('cast.unavailableWhileCasting', { name })
}

/** "Playing on ..." while a casting device connects or plays. */
const castStatus = computed(() => {
    const text = castStatusText(props.state.cast)
    return text ? t(text.key, { ...text.params }) : ''
})

const repeatLabel = computed(() => {
    switch (props.state.repeat) {
        case 'all':
            return t('player.repeatAll')
        case 'one':
            return t('player.repeatOne')
        default:
            return t('player.repeatOff')
    }
})

const positionText = computed(() =>
    t('player.position', {
        position: format.duration(props.state.position),
        duration: format.duration(props.state.duration),
    }),
)

function onMiniPlayer(): void {
    if (isDesktop.value) {
        emit('showNowPlaying')
    } else if (!props.expanded) {
        emit('expand')
    } else {
        viewSwitchOpen.value = !viewSwitchOpen.value
    }
}
</script>

<template>
    <section
        ref="root"
        data-part="transport"
        :aria-label="t('player.label')"
        :data-state="tokens"
        :style="{
            '--wmp-swipe-offset': `${swipeOffset}px`,
            '--wmp-swipe-progress': swipeProgress,
            '--wmp-swipe-duration': `${swipeDuration}ms`,
        }"
    >
        <!-- The grip of the full-screen player that shows it can be swiped down (mobile). -->
        <span data-part="swipe-grip" aria-hidden="true"></span>
        <div data-part="seek" :data-state="idle ? 'disabled' : undefined">
            <BaseSlider
                :model-value="state.position"
                :min="0"
                :max="Math.max(state.duration, 1)"
                :step="1"
                :large-step="10"
                :label="t('player.seek')"
                :value-text="positionText"
                :disabled="idle"
                @update:model-value="emit('seek', $event)"
            />
        </div>
        <!-- Elapsed time and length for themes with a time display; the slider tells both. -->
        <span
            data-part="transport-time"
            aria-hidden="true"
            :data-state="idle ? 'empty' : undefined"
        >
            <span data-part="time-elapsed">{{ format.duration(state.position) }}</span>
            <span data-part="time-duration">{{ format.duration(state.duration) }}</span>
        </span>
        <button
            type="button"
            data-part="now-playing-mini"
            :data-state="state.title ? undefined : 'empty'"
            :aria-expanded="miniPlayerExpanded"
            :aria-controls="expanded && stage ? viewSwitchId : undefined"
            @click="onMiniPlayer"
        >
            <span data-part="player-media" :data-state="mediaTokens">
                <LibraryCover
                    v-if="state.trackId"
                    kind="track"
                    :id="state.trackId"
                    :available="state.hasCover ?? false"
                />
                <BaseCover v-else />
                <video
                    v-if="needsVideo"
                    ref="videoElement"
                    data-part="player-video"
                    aria-hidden="true"
                    muted
                    playsinline
                    disablepictureinpicture
                ></video>
                <PlayerVisualization
                    v-if="needsVisualization"
                    :active="shownView === 'visualization'"
                    :audio="audio"
                    :settings="visualization"
                />
            </span>
            <span data-part="now-playing-meta">
                <BaseMarquee
                    part="now-playing-title"
                    :text="state.title ?? t('player.nothingPlaying')"
                />
                <BaseMarquee part="now-playing-artist" :text="state.artist ?? ''" />
                <span v-if="castStatus" data-part="cast-status">{{ castStatus }}</span>
            </span>
            <span data-part="label">{{ miniPlayerLabel }}</span>
        </button>
        <div
            v-if="expanded && stage"
            :id="viewSwitchId"
            data-part="player-view-switch"
            :data-state="viewSwitchOpen ? 'open' : 'closed'"
            :hidden="!viewSwitchOpen || undefined"
        >
            <BaseSegmentedControl
                item="player-view"
                :label="t('player.views')"
                :options="viewOptions"
                :model-value="viewChoice"
                @update:model-value="selectView"
            />
        </div>
        <div
            data-part="now-playing-actions"
            role="group"
            :aria-label="t('player.trackActions')"
            :data-state="state.trackId ? undefined : 'empty'"
        >
            <template v-if="state.trackId">
                <FavoriteButton type="Track" :id="state.trackId" :favorite="state.isFavorite" />
                <RatingStars :track-id="state.trackId" :rating="state.rating" />
            </template>
        </div>
        <p
            data-part="transport-message"
            role="status"
            :data-state="state.message ? state.message : 'empty'"
        >
            {{ messageText }}
        </p>
        <div v-if="resume" data-part="resume-notice" role="status">
            <BaseIcon name="resume-device" />
            <I18nT keypath="player.resume.from" tag="span" data-part="label" scope="global">
                <template #device>
                    <span data-part="resume-device">{{ resume.device }}</span>
                </template>
            </I18nT>
            <BaseButton
                part="btn-resume"
                variant="primary"
                icon="play"
                :label="t('player.resume.continue')"
                @click="emit('resume')"
            />
            <BaseButton
                part="btn-dismiss-resume"
                variant="icon"
                icon="close"
                :label="t('player.resume.dismiss')"
                @click="emit('dismissResume')"
            />
        </div>
        <div data-part="transport-controls" role="group" :aria-label="t('player.controls')">
            <BaseButton
                part="btn-shuffle"
                variant="icon"
                icon="shuffle"
                :label="t('player.shuffle')"
                :pressed="state.shuffle"
                :state="state.shuffle ? 'on' : 'off'"
                @click="emit('toggleShuffle')"
            />
            <BaseButton
                part="btn-repeat"
                variant="icon"
                :icon="icons.repeat"
                :label="repeatLabel"
                :pressed="state.repeat !== 'off'"
                :state="state.repeat"
                @click="emit('cycleRepeat')"
            />
            <BaseButton
                part="btn-crossfade"
                variant="icon"
                icon="crossfade"
                :label="effectLabel(t('player.crossfade'))"
                :pressed="state.crossfade ?? false"
                :state="state.crossfade ? 'on' : 'off'"
                :disabled="!effects"
                @click="emit('toggleCrossfade')"
            />
            <BaseButton
                part="btn-autoplay"
                variant="icon"
                icon="autoplay"
                :label="t('player.autoplay')"
                :pressed="state.autoplay ?? false"
                :state="state.autoplay ? 'on' : 'off'"
                @click="emit('toggleAutoplay')"
            />
            <BaseButton
                part="btn-stop"
                variant="icon"
                icon="stop"
                :label="t('player.stop')"
                :disabled="idle"
                @click="emit('stop')"
            />
            <BaseButton
                part="btn-prev"
                variant="icon"
                icon="previous"
                :label="t('player.previous')"
                :disabled="empty"
                @click="emit('previous')"
            />
            <BaseButton
                part="btn-play"
                variant="round"
                :icon="icons.play"
                :label="playing ? t('player.pause') : t('player.play')"
                :state="state.playback"
                :disabled="empty"
                @click="playing ? emit('pause') : emit('play')"
            />
            <BaseButton
                part="btn-next"
                variant="icon"
                icon="next"
                :label="t('player.next')"
                :disabled="empty"
                @click="emit('next')"
            />
            <BaseButton
                part="btn-mute"
                variant="icon"
                :icon="icons.volume"
                :label="t('player.mute')"
                :pressed="state.muted"
                :state="state.muted ? 'muted' : 'unmuted'"
                @click="emit('toggleMute')"
            />
            <div data-part="volume" :data-state="state.muted ? 'muted' : undefined">
                <BaseSlider
                    :model-value="state.volume"
                    :step="0.05"
                    :large-step="0.1"
                    :label="t('player.volume')"
                    :value-text="format.percent(state.volume)"
                    @update:model-value="emit('changeVolume', $event)"
                />
            </div>
        </div>
        <div data-part="mode-switch" role="group" :aria-label="t('player.modes')">
            <BaseButton
                part="btn-compact"
                variant="icon"
                icon="compact"
                :label="t('player.compactMode')"
                :pressed="compact"
                :state="compact ? 'on' : 'off'"
                @click="emit('compact')"
            />
            <BaseButton
                part="btn-equalizer"
                variant="icon"
                icon="equalizer"
                :label="effectLabel(t('player.equalizer'))"
                :state="state.equalizer ? 'on' : 'off'"
                :disabled="!effects"
                @click="emit('openEqualizer')"
            />
            <SleepTimerControl :timer="state.sleepTimer" @choose="emit('sleepTimer', $event)" />
            <CastButton :cast="state.cast" @cast="emit('cast')" />
        </div>
        <BaseButton
            part="btn-collapse"
            variant="icon"
            icon="collapse-player"
            :label="t('player.close')"
            @click="emit('collapse')"
        />
    </section>
</template>
