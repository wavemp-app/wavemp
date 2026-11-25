<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import AppBottomNav from './AppBottomNav.vue'
import AppHeader from './AppHeader.vue'
import AppSidebar from './AppSidebar.vue'
import AppToolbar from './AppToolbar.vue'
import AppTransport from './AppTransport.vue'
import type { SleepTimerChoice, TransportStage } from './transport'
import { contentAreaId } from '@/router/scroll'
import BaseAlert from '@/components/base/BaseAlert.vue'
import CompactPlayer from '@/components/player/CompactPlayer.vue'
import QueuePane from '@/components/player/QueuePane.vue'
import ContextMenuHost from '@/components/menus/ContextMenuHost.vue'
import ShortcutOverview from './ShortcutOverview.vue'
import { pageTitleKey } from '@/navigation/breadcrumb'
import { useCoverColor } from '@/composables/useCoverColor'
import { useKeyboardShortcuts } from '@/composables/useKeyboardShortcuts'
import { useLayoutOptions } from '@/composables/useLayoutOptions'
import { useMediaQuery } from '@/composables/useMediaQuery'
import { miniPlayerShowsVideo } from '@/player/miniPlayer'
import { useCollectionStore } from '@/stores/collection'
import { useContextMenuStore } from '@/stores/contextMenu'
import { useDevicesStore } from '@/stores/devices'
import { useFullPlayerStore } from '@/stores/fullPlayer'
import { useHealthStore } from '@/stores/health'
import { useMiniPlayerStore } from '@/stores/miniPlayer'
import { usePageTitleStore } from '@/stores/pageTitle'
import { usePlayerStore } from '@/stores/player'
import { useResumeStore } from '@/stores/resume'
import { useSessionStore } from '@/stores/session'
import { useShortcutsStore } from '@/stores/shortcuts'
import { shortcutRegistry } from '@/shortcuts/defaults'
import { desktopMediaQuery } from '@/themes/breakpoints'
import { showsQueuePane } from '@/themes/layoutOptions'

/**
 * App shell: the fixed areas of the theme API around the content of the current page. The DOM is
 * the same on every device; the mobile base and the themes arrange it with CSS only.
 *
 * The mini player (compact mode) controls the same player: in the floating window of the browser,
 * rendered there through a teleport, or, where the browser has none, in place of the app, which
 * stays hidden but keeps its pages until the mini player closes.
 *
 * The keyboard shortcuts control the player in the app and in the mini player; their overview is a
 * dialog of the shell.
 *
 * Where the theme docks the play queue (layout option, desktop only), the queue pane follows the
 * content, except on Now Playing, which shows the queue itself.
 */
const { t } = useI18n()
const route = useRoute()
const router = useRouter()
const health = useHealthStore()
const pageTitle = usePageTitleStore()
const collection = useCollectionStore()
const contextMenu = useContextMenuStore()

const player = usePlayerStore()
const resume = useResumeStore()
// The devices of the profile: this instance registers and plays or controls the active device.
const devices = useDevicesStore()
devices.start()
const session = useSessionStore()
const miniPlayer = useMiniPlayerStore()
const isDesktop = useMediaQuery(desktopMediaQuery)
const shortcuts = useShortcutsStore()
const layout = useLayoutOptions()
const queuePaneShown = computed(() => showsQueuePane(layout.value, route.name))

useCoverColor()

const sidebarOpen = ref(false)
/** The desktop sidebar shows only its icons; kept per profile. The mobile drawer is unchanged. */
const sidebarCollapsed = computed(() => isDesktop.value && session.sidebarCollapsed)
const fullPlayer = useFullPlayerStore()
const playerExpanded = computed(() => fullPlayer.expanded)
/** The views of the full-screen player, like the stage of Now Playing. */
const transportStage = computed<TransportStage>(() => ({
    mode: player.viewMode,
    view: player.stageView,
    hasVideo: player.hasVideo,
    casting: player.casting,
}))
/** The mini player shows in the window of the app; the app is hidden meanwhile. */
const compactInline = computed(() => miniPlayer.mode === 'inline')
/** The mini player shows the video of the song, like the stage would. */
const compactVideo = computed(() =>
    miniPlayerShowsVideo({
        hasVideo: player.hasVideo,
        videoChosen: player.viewMode === 'Video',
        casting: player.casting,
    }),
)
const compactVideoReady = computed(() => player.videoState === 'ready')

// Keyboard shortcuts like a desktop player (src/shortcuts); the overview opens with ?.
useKeyboardShortcuts(compactInline)
const shortcutList = shortcutRegistry.definitions()

// Detail pages show their name (album, artist, ...), the other pages the name of the page.
const title = computed(() => pageTitle.title ?? t(pageTitleKey(route)))

/** Moves the focus to an element of the shell after the next render. */
async function focus(selector: string): Promise<void> {
    await nextTick()
    document.querySelector<HTMLElement>(`[data-part="app"] ${selector}`)?.focus()
}

function toggleSidebar(): void {
    sidebarOpen.value = !sidebarOpen.value
    if (sidebarOpen.value) {
        void focus('[data-part="sidebar"] [data-part="tree-item"][tabindex="0"]')
    }
}

function toggleSidebarCollapsed(): void {
    void session.updateSidebarCollapsed(!session.sidebarCollapsed)
}

function closeSidebar(): void {
    if (sidebarOpen.value) {
        sidebarOpen.value = false
        void focus('[data-part="btn-menu"]')
    }
}

function chooseSleepTimer(choice: SleepTimerChoice): void {
    if (choice.mode === 'duration') {
        player.startSleepTimer('duration', choice.minutes)
    } else if (choice.mode === 'off') {
        player.cancelSleepTimer()
    } else {
        player.startSleepTimer(choice.mode)
    }
}

function openEqualizer(): void {
    fullPlayer.collapse()
    void router.push({ name: 'equalizer' })
}

function expandPlayer(): void {
    fullPlayer.expand()
    void focus('[data-part="btn-collapse"]')
}

function collapsePlayer(): void {
    if (fullPlayer.expanded) {
        fullPlayer.collapse()
        void focus('[data-part="now-playing-mini"]')
    }
}

/** Opens the mini player, or closes it while it is open (the compact mode button). */
async function toggleCompact(): Promise<void> {
    if (miniPlayer.isOpen) {
        closeCompact()
        return
    }

    await miniPlayer.open()
    if (compactInline.value) {
        await nextTick()
        document
            .querySelector<HTMLElement>('[data-part="compact-player"] [data-part="btn-play"]')
            ?.focus()
    }
}

/** Closes the mini player and returns to the app. */
function closeCompact(): void {
    const floating = miniPlayer.mode === 'window'
    miniPlayer.close()
    if (floating) {
        window.focus()
    }
    void focus('[data-part="btn-compact"]')
}

/**
 * Escape closes the mini player in place of the app, the drawer and the full-screen player, unless
 * something took it before. The keyboard shortcuts listen first (useKeyboardShortcuts adds its
 * listener before this one); the shortcut of the overview of the groups of a list leaves Escape
 * alone while one of these is open (src/library/groupJump.ts), so one Escape never does both. No
 * preventDefault here: a dialog open at the same time still closes with the same Escape.
 */
function onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape' && !event.defaultPrevented) {
        if (compactInline.value) {
            closeCompact()
            return
        }
        closeSidebar()
        collapsePlayer()
    }
}

// Navigating closes the drawer and the full-screen player and dismisses the error of a favorite, a
// rating or a context menu action.
watch(
    () => route.fullPath,
    () => {
        sidebarOpen.value = false
        fullPlayer.collapse()
        collection.clearError()
        contextMenu.clearError()
    },
)

function checkHealth(): void {
    void health.check()
}

onMounted(() => {
    checkHealth()
    document.addEventListener('keydown', onKeydown)
    window.addEventListener('online', checkHealth)
    window.addEventListener('offline', checkHealth)
})

onUnmounted(() => {
    // Without the shell (e.g. on the profile selection) the mini player has nothing to show.
    miniPlayer.close()
    document.removeEventListener('keydown', onKeydown)
    window.removeEventListener('online', checkHealth)
    window.removeEventListener('offline', checkHealth)
})
</script>

<template>
    <div data-part="app" :hidden="compactInline || undefined">
        <AppHeader :sidebar-open="sidebarOpen" @toggle-sidebar="toggleSidebar" />
        <AppToolbar />
        <AppSidebar
            :open="sidebarOpen"
            :collapsed="sidebarCollapsed"
            @close="closeSidebar"
            @toggle-collapsed="toggleSidebarCollapsed"
        />
        <main :id="contentAreaId" data-part="content">
            <h1 data-part="content-title">{{ title }}</h1>
            <BaseAlert
                v-if="collection.error"
                tone="error"
                :message="
                    t(
                        collection.error === 'rating'
                            ? 'collection.errors.rating'
                            : 'collection.errors.favorite',
                    )
                "
            />
            <BaseAlert v-if="contextMenu.error" tone="error" :message="t(contextMenu.error)" />
            <slot />
        </main>
        <QueuePane
            v-if="queuePaneShown"
            :items="player.queue.items"
            :current-index="player.queue.index"
            :lyrics="player.lyrics"
            :playing="player.playback === 'playing'"
            :position="player.position"
            :duration="player.duration"
            :clock="player.livePosition"
            @play="player.playItem"
            @move="player.moveItem"
            @remove="player.removeItem"
            @clear="player.clearQueue"
            @seek="player.seek"
        />
        <AppTransport
            :state="player.state"
            :expanded="playerExpanded"
            :compact="miniPlayer.isOpen"
            :resume="resume.notice"
            :stage="transportStage"
            :audio="player.analyser"
            :visualization="session.visualization"
            @play="player.play"
            @pause="player.pause"
            @stop="player.stop"
            @previous="player.previous"
            @next="player.next"
            @seek="player.seek"
            @toggle-shuffle="player.toggleShuffle"
            @cycle-repeat="player.cycleRepeat"
            @toggle-crossfade="player.toggleCrossfade"
            @toggle-autoplay="player.toggleAutoplay"
            @toggle-mute="player.toggleMute"
            @change-volume="player.setVolume"
            @show-now-playing="router.push({ name: 'now-playing' })"
            @open-equalizer="openEqualizer"
            @sleep-timer="chooseSleepTimer"
            @cast="player.requestCast"
            @compact="toggleCompact"
            @expand="expandPlayer"
            @collapse="collapsePlayer"
            @resume="resume.resume"
            @dismiss-resume="resume.dismiss"
            @select-view="player.selectViewMode"
            @attach-video="player.attachVideo"
            @detach-video="player.detachVideo"
        />
        <AppBottomNav />
        <ContextMenuHost />
        <ShortcutOverview
            v-if="shortcuts.overviewOpen"
            v-model:open="shortcuts.overviewOpen"
            :shortcuts="shortcutList"
        />
    </div>
    <CompactPlayer
        v-if="compactInline"
        kind="inline"
        :state="player.state"
        :video="compactVideo"
        :video-ready="compactVideoReady"
        @play="player.play"
        @pause="player.pause"
        @previous="player.previous"
        @next="player.next"
        @seek="player.seek"
        @toggle-mute="player.toggleMute"
        @change-volume="player.setVolume"
        @close="closeCompact"
        @attach-video="player.attachVideo"
        @detach-video="player.detachVideo"
    />
    <Teleport v-if="miniPlayer.target" :to="miniPlayer.target">
        <CompactPlayer
            kind="window"
            :state="player.state"
            :video="compactVideo"
            :video-ready="compactVideoReady"
            @play="player.play"
            @pause="player.pause"
            @previous="player.previous"
            @next="player.next"
            @seek="player.seek"
            @toggle-mute="player.toggleMute"
            @change-volume="player.setVolume"
            @close="closeCompact"
            @attach-video="player.attachVideo"
            @detach-video="player.detachVideo"
        />
    </Teleport>
</template>
