<script setup lang="ts">
import NowPlayingStage from '@/components/player/NowPlayingStage.vue'
import PlayQueue from '@/components/player/PlayQueue.vue'
import { usePlayerStore } from '@/stores/player'
import { useSessionStore } from '@/stores/session'
import type { VisualizationSettingsChange } from '@/visualization/settings'

/**
 * Now Playing: the stage with the current track (cover, video, visualization or lyrics) and the queue of
 * the player.
 */
const player = usePlayerStore()
const session = useSessionStore()

/**
 * Applies a change of the visualization at once and saves it for the profile. If saving fails
 * (e.g. offline), the previous settings come back; the visualization goes on.
 */
function updateVisualization(change: VisualizationSettingsChange): void {
    session.updateVisualization(change).catch(() => undefined)
}
</script>

<template>
    <div data-part="view" data-view="now-playing">
        <NowPlayingStage
            :track="player.current?.track"
            :mode="player.viewMode"
            :view="player.stageView"
            :video-state="player.videoState"
            :audio="player.analyser"
            :visualization="session.visualization"
            :lyrics="player.lyrics"
            :playing="player.playback === 'playing'"
            :position="player.position"
            :duration="player.duration"
            :clock="player.livePosition"
            @select-mode="player.selectViewMode"
            @attach-video="player.attachVideo"
            @detach-video="player.detachVideo"
            @update-visualization="updateVisualization"
            @seek="player.seek"
        />
        <PlayQueue
            v-if="player.queue.items.length > 0"
            :items="player.queue.items"
            :current-index="player.queue.index"
            @play="player.playItem"
            @move="player.moveItem"
            @remove="player.removeItem"
            @clear="player.clearQueue"
        />
    </div>
</template>
