<script setup lang="ts">
import { computed } from 'vue'
import { I18nT, useI18n } from 'vue-i18n'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseIcon from '@/components/base/BaseIcon.vue'
import BaseCover from '@/components/base/BaseCover.vue'
import BaseSlider from '@/components/base/BaseSlider.vue'
import FavoriteButton from '@/components/collection/FavoriteButton.vue'
import CastButton from '@/components/player/CastButton.vue'
import RatingStars from '@/components/collection/RatingStars.vue'
import SleepTimerControl from '@/components/player/SleepTimerControl.vue'
import LibraryCover from '@/components/library/LibraryCover.vue'
import { useFormat } from '@/composables/useFormat'
import { useMediaQuery } from '@/composables/useMediaQuery'
import { castStatusText } from '@/casting/presentation'
import { desktopMediaQuery } from '@/themes/breakpoints'
import {
    transportIcons,
    transportStateTokens,
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
    }>(),
    { expanded: false, compact: false, resume: null },
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
}>()

const { t } = useI18n()
const format = useFormat()
const isDesktop = useMediaQuery(desktopMediaQuery)

const idle = computed(() => props.state.playback === 'idle')
/** Without a current track there is nothing to play, skip or seek. */
const empty = computed(() => !props.state.trackId)
const playing = computed(() => props.state.playback === 'playing')
const icons = computed(() => transportIcons(props.state))
const tokens = computed(() =>
    transportStateTokens(props.state.playback, props.expanded, props.state.loading ?? false),
)

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
    }
}
</script>

<template>
    <section data-part="transport" :aria-label="t('player.label')" :data-state="tokens">
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
            :aria-expanded="isDesktop ? undefined : expanded ? 'true' : 'false'"
            @click="onMiniPlayer"
        >
            <LibraryCover
                v-if="state.trackId"
                kind="track"
                :id="state.trackId"
                :available="state.hasCover ?? false"
            />
            <BaseCover v-else />
            <span data-part="now-playing-meta">
                <span data-part="now-playing-title">{{
                    state.title ?? t('player.nothingPlaying')
                }}</span>
                <span data-part="now-playing-artist">{{ state.artist }}</span>
                <span v-if="castStatus" data-part="cast-status">{{ castStatus }}</span>
            </span>
            <span data-part="label">{{
                isDesktop ? t('player.showNowPlaying') : t('player.open')
            }}</span>
        </button>
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
