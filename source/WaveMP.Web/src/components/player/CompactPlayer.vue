<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseCover from '@/components/base/BaseCover.vue'
import BaseSlider from '@/components/base/BaseSlider.vue'
import LibraryCover from '@/components/library/LibraryCover.vue'
import { transportIcons, type TransportState } from '@/components/shell/transport'
import { useFormat } from '@/composables/useFormat'
import { miniPlayerStateTokens } from '@/player/miniPlayer'

/**
 * The mini player (compact mode): cover or video, title, artist, progress, previous, play and
 * pause, next and the volume of the running playback. It is controlled through props and events
 * only, like the transport bar. It is shown in the floating window of the browser (kind "window")
 * or, where the browser has none, as compact view in the window of the app (kind "inline"); the
 * DOM is the same, the theme can tell both apart by data-kind. While it shows a video, its video
 * element is handed to the player (attach) and taken back before it goes (detach).
 */
const props = withDefaults(
    defineProps<{
        state: TransportState
        kind: 'window' | 'inline'
        /** Whether the mini player shows the video of the current song instead of its cover. */
        video?: boolean
        /** Whether that video is ready at the position of the music. */
        videoReady?: boolean
    }>(),
    { video: false, videoReady: false },
)

const emit = defineEmits<{
    play: []
    pause: []
    previous: []
    next: []
    seek: [position: number]
    toggleMute: []
    changeVolume: [volume: number]
    close: []
    attachVideo: [element: HTMLVideoElement]
    detachVideo: [element: HTMLVideoElement]
}>()

const { t } = useI18n()
const format = useFormat()

const videoElement = ref<HTMLVideoElement | null>(null)

const idle = computed(() => props.state.playback === 'idle')
const empty = computed(() => !props.state.trackId)
const playing = computed(() => props.state.playback === 'playing')
const icons = computed(() => transportIcons(props.state))
const videoShown = computed(() => props.video && props.videoReady)
const tokens = computed(() =>
    miniPlayerStateTokens(props.state.playback, props.state.loading ?? false, videoShown.value),
)

const positionText = computed(() =>
    t('player.position', {
        position: format.duration(props.state.position),
        duration: format.duration(props.state.duration),
    }),
)

// The video element comes and goes with the video of the song; the player follows it.
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
</script>

<template>
    <section
        data-part="compact-player"
        :data-kind="kind"
        :data-state="tokens"
        :aria-label="t('compactPlayer.label')"
    >
        <div data-part="compact-player-panel">
            <div data-part="compact-player-media" :data-state="videoShown ? 'video' : 'cover'">
                <LibraryCover
                    v-if="state.trackId"
                    kind="track"
                    :id="state.trackId"
                    :available="state.hasCover ?? false"
                />
                <BaseCover v-else />
                <video
                    v-if="video"
                    ref="videoElement"
                    data-part="compact-player-video"
                    muted
                    playsinline
                    disablepictureinpicture
                    :aria-label="t('nowPlaying.video')"
                ></video>
            </div>
            <div data-part="compact-player-info" :data-state="state.title ? undefined : 'empty'">
                <span data-part="compact-player-title">{{
                    state.title ?? t('player.nothingPlaying')
                }}</span>
                <span data-part="compact-player-artist">{{ state.artist }}</span>
            </div>
            <div data-part="compact-player-seek" :data-state="idle ? 'disabled' : undefined">
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
            <span
                data-part="compact-player-time"
                aria-hidden="true"
                :data-state="idle ? 'empty' : undefined"
            >
                <span data-part="time-elapsed">{{ format.duration(state.position) }}</span>
                <span data-part="time-duration">{{ format.duration(state.duration) }}</span>
            </span>
            <div
                data-part="compact-player-controls"
                role="group"
                :aria-label="t('player.controls')"
            >
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
            </div>
            <div data-part="compact-player-volume" role="group" :aria-label="t('player.volume')">
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
            <BaseButton
                part="btn-compact-close"
                variant="icon"
                icon="expand-player"
                :label="t('compactPlayer.close')"
                @click="emit('close')"
            />
        </div>
    </section>
</template>
