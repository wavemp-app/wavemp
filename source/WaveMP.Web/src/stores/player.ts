import { defineStore } from 'pinia'
import { computed, ref, shallowRef, watch } from 'vue'
import {
    PlaybackMethodObject,
    PlaybackTargetObject,
    PlayerViewModeObject,
    StreamProtocolObject,
    type AudioFormat,
    type PlayerViewMode,
    type Track,
    type TrackPlayback,
    type TrackVideoPlayback,
} from '@/api/generated/models'
import { statusOf } from '@/api/problems'
import { CastEngine } from '@/casting/engine'
import type { CastLayer } from '@/casting/layer'
import { castMediaKind, castMediaOf } from '@/casting/media'
import {
    nextRepeatMode,
    type PlaybackState,
    type PlayerMessage,
    type RepeatMode,
    type TransportState,
} from '@/components/shell/transport'
import { getLyrics } from '@/lyrics/api'
import type { TrackLyrics } from '@/lyrics/timing'
import {
    getAutoplayTracks,
    getPlayback,
    getVideoPlayback,
    recordPlay,
    reportNowPlaying,
} from '@/player/api'
import {
    autoplayBatchSize,
    autoplayContext,
    autoplayExcluded,
    autoplayMaxSkips,
    autoplayRefillBelow,
    autoplaySignature,
    insertAutoplay,
    isEndOfList,
    isPlaybackFresh,
    toAutoplayEntries,
    withoutUpcomingAutoplay,
    type AutoplayEntry,
} from '@/player/autoplay'
import { crossfadeDuration } from '@/player/crossfade'
import {
    AudioEngine,
    browserAudioEnvironment,
    type DeckSource,
    type EngineEvents,
} from '@/player/engine'
import { browserAudioFormats } from '@/player/formats'
import { PlayHistoryTracker } from '@/player/history'
import { connectMediaSession, updateMediaSession } from '@/player/mediaSession'
import {
    append,
    clearUpcoming,
    currentItem,
    emptyQueue,
    insertNext,
    jumpTo,
    move,
    moveNext,
    nextIndex,
    playTracks,
    previousIndex,
    remove,
    setShuffle,
    withIndex,
    type Queue,
    type QueueItem,
} from '@/player/queue'
import { replayGainFactor } from '@/player/replayGain'
import type { PlayerSource, RestoredState } from '@/player/resume'
import { shouldPreload, shouldRestart } from '@/player/scheduling'
import {
    browserSleepTimerClock,
    SleepTimer,
    sleepTimerOff,
    type SleepTimerClock,
    type SleepTimerMode,
    type SleepTimerState,
} from '@/player/sleepTimer'
import {
    VideoFollower,
    type FollowerClock,
    type VideoFollowerEvents,
    type VideoFollowerSource,
} from '@/player/videoFollower'
import { browserVideoFormats, type VideoFormats } from '@/player/videoFormats'
import { stageViewOf, type StageView, type VideoState } from '@/player/videoSync'
import { clampVolume, loadVolume, saveVolume, volumeToGain } from '@/player/volume'
import { useCastStore } from './cast'
import { useCoversStore } from './covers'
import { useEqualizerStore } from './equalizer'
import { useSessionStore } from './session'

/** What the store needs from the engine; tests replace the engine. */
export type PlayerEngine = Pick<
    AudioEngine,
    | 'load'
    | 'prepare'
    | 'play'
    | 'pause'
    | 'stop'
    | 'seek'
    | 'setVolume'
    | 'setTrackGain'
    | 'setEqualizer'
    | 'fadeOut'
    | 'restoreOutput'
    | 'equalizerAvailable'
    | 'hasPrepared'
    | 'position'
    | 'analyser'
>

/** What the store needs from the video follower; tests replace it. */
export type PlayerVideoFollower = Pick<
    VideoFollower,
    'load' | 'unload' | 'setActive' | 'setOffset' | 'sync' | 'dispose' | 'allowPlayback'
>

/** Parts of the browser the player uses; tests replace them. */
export const playerDependencies = {
    createEngine: (events: EngineEvents): PlayerEngine => new AudioEngine(events),
    /** The engine while a casting device plays. */
    createCastEngine: (layer: CastLayer, events: EngineEvents): PlayerEngine =>
        new CastEngine(layer, events),
    /** Origin of the server as the browser reaches it; casting devices load absolute URLs. */
    origin: (): string => (typeof window === 'undefined' ? '' : window.location.origin),
    createVideoFollower: (
        element: HTMLVideoElement,
        clock: () => FollowerClock,
        events: VideoFollowerEvents,
    ): PlayerVideoFollower => new VideoFollower(element, clock, events),
    audioFormats: (): readonly AudioFormat[] => browserAudioFormats(),
    videoFormats: (): VideoFormats => browserVideoFormats(),
    random: (): number => Math.random(),
    now: (): number => Date.now(),
    mediaSession: true,
    /** Whether the browser has Web Audio, which the equalizer and the crossfade need. */
    webAudio: (): boolean => browserAudioEnvironment.webAudio !== false,
    sleepClock: browserSleepTimerClock as SleepTimerClock,
}

/** Size of the cover shown by the media session (lock screen, notifications). */
const artworkSize = 300

interface Loaded {
    readonly key: string
    /** The audio playback; for a casting device also the video playback. */
    readonly playback: TrackPlayback | TrackVideoPlayback
    readonly source: DeckSource
}

/** Where the music plays: in the browser or on a casting device. */
type Output = 'local' | 'cast'

function isHls(value: TrackPlayback | TrackVideoPlayback): boolean {
    return (
        value.method === PlaybackMethodObject.Transcode ||
        value.protocol === StreamProtocolObject.Hls
    )
}

function seconds(milliseconds: number | null | undefined): number {
    return typeof milliseconds === 'number' && milliseconds > 0 ? milliseconds / 1000 : 0
}

function artistOf(track: Track | undefined): string | undefined {
    return track?.artist?.name ?? undefined
}

/**
 * The player: the queue, the playback through the audio engine and the state the transport bar
 * shows. The server decides per track whether the browser plays the file directly or a transcoded
 * HLS stream; tracks that cannot be played or fail are skipped with a notice. Plays count for the
 * history once they were listened to long enough.
 *
 * The audio always leads, also for tracks with a video: the Now Playing stage shows the cover, the
 * video or the visualization of the current track, and switching between them never touches the
 * audio. The video plays muted and follows the position of the audio plus the offset of the track;
 * it is only loaded while the stage shows it.
 *
 * The equalizer and the crossfade of the profile apply through the engine; tracks only fade into
 * each other when one ends on its own. The sleep timer belongs to this device: it fades out and
 * pauses after a time, at the end of the track or at the end of the queue.
 *
 * Autoplay (see src/player/autoplay.ts): while it is on for the profile and repeat is off, the
 * server chooses songs that fit the list; they wait below the queue. When the list has ended, the
 * first one moves into the queue (marked as autoplay) and plays, early enough to fade into it or
 * play it without a gap, and more songs load before the waiting ones run out. Adding songs to the
 * queue, repeat or turning autoplay off takes back the songs autoplay put after the current one.
 *
 * While a casting device is connected (see src/casting), the device plays instead of the browser:
 * the queue and the controls stay here, the device gets one track after the other (its video while
 * the video view is chosen), and equalizer, crossfade, ReplayGain and the visualization rest.
 */
export const usePlayerStore = defineStore('player', () => {
    const session = useSessionStore()
    const covers = useCoversStore()
    const equalizer = useEqualizerStore()
    const cast = useCastStore()

    const queue = shallowRef<Queue>(emptyQueue)
    const playback = ref<PlaybackState>('idle')
    const loading = ref(false)
    const position = ref(0)
    const duration = ref(0)
    const repeat = ref<RepeatMode>('off')
    const message = ref<PlayerMessage | null>(null)
    const storedVolume = loadVolume()
    const volume = ref(storedVolume.volume)
    const muted = ref(storedVolume.muted)
    const artwork = ref<string | null>(null)
    /** The video element of the Now Playing stage, while the stage is open. */
    const videoElement = shallowRef<HTMLVideoElement | null>(null)
    /** State of the video of the current track. */
    const videoState = ref<VideoState>('idle')
    /** Whether the page is visible; a hidden page shows no video. */
    const pageVisible = ref(
        typeof document === 'undefined' || document.visibilityState !== 'hidden',
    )
    /** Whether the equalizer can shape the sound in this browser. */
    const localEqualizer = ref(playerDependencies.webAudio())
    /** Whether a casting device plays the music. */
    const casting = computed(() => cast.connected)
    /** Whether the equalizer, crossfade and ReplayGain apply: not while casting. */
    const equalizerAvailable = computed(() => localEqualizer.value && !casting.value)
    /**
     * The output of the player (after volume and equalizer) for the visualizations; null without Web
     * Audio and before the first playback, which builds the audio graph.
     */
    const analyser = shallowRef<AnalyserNode | null>(null)
    /** The sleep timer and the seconds until a timer with a duration ends. */
    const sleepTimer = ref<SleepTimerState>(sleepTimerOff)
    const sleepRemaining = ref(0)
    /** The lyrics of the current track; null while they load and for tracks without lyrics. */
    const lyrics = shallowRef<TrackLyrics | null>(null)
    /** The songs autoplay plays after the list, in their order. */
    const autoplay = shallowRef<readonly AutoplayEntry[]>([])
    /** Whether songs of autoplay are being requested. */
    const autoplayLoading = ref(false)
    /** Where the list in the queue came from; null for single songs. */
    const listSource = shallowRef<PlayerSource | null>(null)

    const current = computed<QueueItem | undefined>(() => currentItem(queue.value))
    const shuffle = computed(() => queue.value.shuffle)
    const hasVideo = computed(() => current.value?.track.hasVideo === true)
    /** The view mode chosen by the profile. */
    const viewMode = computed<PlayerViewMode>(() => session.playerViewMode)
    /** What the stage shows: the video only once it is ready at the right position. */
    /** Whether the current track has lyrics (loaded). */
    const hasLyrics = computed(
        () =>
            lyrics.value !== null &&
            current.value !== undefined &&
            lyrics.value.trackId === current.value.track.id,
    )
    /**
     * What the stage shows. While casting it shows the cover instead of the video (the device shows
     * it) and of the visualization (the browser does not hear the music).
     */
    const stageView = computed<StageView>(() => {
        const view = stageViewOf(viewMode.value, hasVideo.value, videoState.value, hasLyrics.value)
        return casting.value && (view === 'visualization' || view === 'video') ? 'cover' : view
    })
    /**
     * Whether the video element lies in another window than the app, the floating mini player:
     * that window stays above the others, so its video keeps playing while the app is hidden.
     */
    const videoInOtherWindow = computed(() => {
        const owner = videoElement.value?.ownerDocument
        return !!owner && typeof document !== 'undefined' && owner !== document
    })
    /** Whether the video of the current track is to be shown. */
    const videoWanted = computed(
        () =>
            videoElement.value !== null &&
            hasVideo.value &&
            viewMode.value === PlayerViewModeObject.Video &&
            (pageVisible.value || videoInOtherWindow.value) &&
            !casting.value,
    )
    /** What a casting device plays of the current track: its video or its sound. */
    const castKind = computed(() => castMediaKind(viewMode.value, hasVideo.value))
    /** The songs of the list autoplay goes on from. */
    const autoplayList = computed(() => autoplayContext(queue.value))
    /** Whether autoplay goes on after the list: on for the profile, repeat off and a list to follow. */
    const autoplayActive = computed(
        () => session.autoplay.enabled && repeat.value === 'off' && autoplayList.value.length > 0,
    )

    let engine: PlayerEngine | null = null
    /** The engine of the casting device, created with the first connection. */
    let castEngine: PlayerEngine | null = null
    /** Where the music plays; events of the other engine are ignored. */
    let output: Output = 'local'
    let follower: PlayerVideoFollower | null = null
    /**
     * The video elements that want to show the video, the newest last: the stage of Now Playing
     * and the mini player. Only the newest one shows it; when it goes, the one before takes over.
     */
    const videoElements: HTMLVideoElement[] = []
    /** The queue entry whose video is loaded or requested. */
    let videoKey: string | null = null
    /** Increases with every request of a video, so late answers are ignored. */
    let videoGeneration = 0
    /** The entry whose failed video was already requested once more. */
    let videoRetried: string | null = null
    /** The loaded track and its playback; null while loading or idle. */
    let loaded: Loaded | null = null
    /** Where the user moved the track while it was still loading; it starts there once loaded. */
    let seekWhileLoading: number | null = null
    /** The track prepared on the other deck. */
    let prepared: Loaded | null = null
    let preparing: string | null = null
    /** Increases with every new track, so late answers of older requests are ignored. */
    let generation = 0
    /** Tracks skipped in a row; stops when the whole queue failed. */
    let skipped = 0
    let retried: string | null = null
    /**
     * The entry of a restored state that waits paused and was not played yet. When it cannot be
     * loaded (a request canceled by a reload, a server error), the player waits at it instead of
     * moving on: nothing played, so the saved state must not change. Play requests it again.
     */
    let restoredKey: string | null = null
    let artworkHeld: { id: string; version: string | undefined } | null = null
    /** Increases with every new list of autoplay, so late answers for an older list are ignored. */
    let autoplayGeneration = 0
    /** The running request of autoplay songs. */
    let autoplayRequest: Promise<void> | null = null
    /** Whether the server had no more songs for the list; more are requested for the next list. */
    let autoplayExhausted = false
    /** Songs the user removed from autoplay or skipped there; they do not come back for this list. */
    const autoplayDismissed = new Set<string>()
    /** Playbacks autoplay already brought for its songs in the queue, by key; each is used once. */
    const autoplayPlaybacks = new Map<string, TrackPlayback>()

    const history = new PlayHistoryTracker(
        (trackId, playedAt) => recordPlay(trackId, playedAt),
        undefined,
        (trackId) => reportNowPlaying(trackId),
    )

    const engineEvents: EngineEvents = {
        timeupdate: onTimeUpdate,
        playing: () => {
            analyser.value = output === 'local' ? (engine?.analyser ?? null) : null
            playback.value = 'playing'
            loading.value = false
            skipped = 0
        },
        paused: () => {
            // Loading a new source pauses the element as well; that is no pause of the user.
            if (playback.value === 'playing' && !loading.value) {
                playback.value = 'paused'
            }
        },
        waiting: () => {
            // Only a playing track waits for data; a paused one does not load.
            if (playback.value === 'playing') {
                loading.value = true
            }
        },
        ended: onEnded,
        advanced: onAdvanced,
        error: onError,
    }

    /** The events of the engine of an output; only those of the current output count. */
    function eventsOf(source: Output): EngineEvents {
        const when =
            <T extends unknown[]>(handler: ((...args: T) => void) | undefined) =>
            (...args: T): void => {
                if (output === source) {
                    handler?.(...args)
                }
            }
        return {
            timeupdate: when(engineEvents.timeupdate),
            playing: when(engineEvents.playing),
            paused: when(engineEvents.paused),
            waiting: when(engineEvents.waiting),
            ended: when(engineEvents.ended),
            advanced: when(engineEvents.advanced),
            error: when(engineEvents.error),
        }
    }

    function outputGain(): number {
        return volumeToGain({ volume: volume.value, muted: muted.value })
    }

    const sleepClock = playerDependencies.sleepClock
    let sleepTicker: number | null = null
    const sleep = new SleepTimer(
        {
            fadeOut: (seconds) => runningEngine()?.fadeOut(seconds),
            restore: () => runningEngine()?.restoreOutput(),
            pause: () => pause(),
            changed: (value) => {
                sleepTimer.value = value
                updateSleepTicker()
            },
        },
        () => session.sleepTimer.fadeSeconds,
        sleepClock,
    )

    function getEngine(): PlayerEngine {
        if (!engine) {
            engine = playerDependencies.createEngine(eventsOf('local'))
            engine.setEqualizer(equalizer.output, equalizer.layout)
        }

        engine.setVolume(outputGain())
        return engine
    }

    function getCastEngine(): PlayerEngine {
        castEngine ??= playerDependencies.createCastEngine(cast.layer, eventsOf('cast'))
        return castEngine
    }

    /** The engine of the current output. */
    function activeEngine(): PlayerEngine {
        return output === 'cast' ? getCastEngine() : getEngine()
    }

    /** The engine of the current output, if it was created. */
    function runningEngine(): PlayerEngine | null {
        return output === 'cast' ? castEngine : engine
    }

    /** Starts the engine and notes whether its equalizer works (only the browser has one). */
    async function startEngine(player: PlayerEngine): Promise<void> {
        await player.play()
        if (player === engine) {
            localEqualizer.value = player.equalizerAvailable
        }
    }

    /**
     * Whether the current entry is the last one of the list in the order of the queue, without
     * repeat; songs of autoplay do not count, so the sleep timer stops at the end of the list.
     */
    function isLastEntry(): boolean {
        return isEndOfList(queue.value)
    }

    /** Counts down the seconds of a timer with a duration; also checks it while nothing plays. */
    function updateSleepTicker(): void {
        const { mode, endsAt } = sleepTimer.value
        if (mode !== 'duration' || endsAt === null) {
            if (sleepTicker !== null) {
                sleepClock.clearTimeout(sleepTicker)
                sleepTicker = null
            }

            sleepRemaining.value = 0
            return
        }

        sleepRemaining.value = Math.max(0, Math.ceil((endsAt - sleepClock.now()) / 1000))
        if (sleepTicker === null) {
            const tick = (): void => {
                sleepTicker = null
                sleep.check()
                updateSleepTicker()
            }
            sleepTicker = sleepClock.setTimeout(tick, 1000)
        }
    }

    function gainOf(value: TrackPlayback | TrackVideoPlayback | null | undefined): number {
        return replayGainFactor(
            session.replayGain,
            value && 'replayGain' in value ? value.replayGain : undefined,
        )
    }

    function sourceOf(value: TrackPlayback): DeckSource | null {
        if (!value.streamUrl) {
            return null
        }

        const source = { url: value.streamUrl, gain: gainOf(value) }
        return isHls(value) ? { ...source, hls: true } : source
    }

    function videoSourceOf(value: TrackVideoPlayback | undefined): VideoFollowerSource | null {
        if (!value?.streamUrl) {
            return null
        }

        return isHls(value) ? { url: value.streamUrl, hls: true } : { url: value.streamUrl }
    }

    /** The playback of an entry for the current output, or the notice why it cannot play. */
    async function fetchPlayback(item: QueueItem): Promise<Loaded | PlayerMessage> {
        try {
            return output === 'cast'
                ? await fetchCastPlayback(item)
                : await fetchLocalPlayback(item)
        } catch (error) {
            const status = statusOf(error)
            return status === undefined || status >= 500 ? 'failed' : 'unavailable'
        }
    }

    async function fetchLocalPlayback(item: QueueItem): Promise<Loaded | PlayerMessage> {
        const known = autoplayPlaybacks.get(item.key)
        autoplayPlaybacks.delete(item.key)
        const result = isPlaybackFresh(known, playerDependencies.now())
            ? known
            : await getPlayback(item.track.id ?? '', playerDependencies.audioFormats())
        const source = result ? sourceOf(result) : null
        return result && source ? { key: item.key, playback: result, source } : 'unsupported'
    }

    /**
     * The playback for the casting device, decided by its formats: the video of the track while the
     * video view is chosen and the track has one, otherwise its sound.
     */
    async function fetchCastPlayback(item: QueueItem): Promise<Loaded | PlayerMessage> {
        const track = item.track
        const formats = cast.layer.formats()
        const kind = castMediaKind(viewMode.value, track.hasVideo === true)
        const result =
            kind === 'video'
                ? await getVideoPlayback(track.id ?? '', formats.video, PlaybackTargetObject.Cast)
                : await getPlayback(track.id ?? '', formats.audio, PlaybackTargetObject.Cast)
        const media = result ? castMediaOf(result, kind, track, playerDependencies.origin()) : null
        if (!result || !media) {
            return 'unsupported'
        }

        return { key: item.key, playback: result, source: { url: media.url, gain: 1, cast: media } }
    }

    function forgetPrepared(): void {
        prepared = null
        preparing = null
        runningEngine()?.prepare(null)
    }

    /** Loads the current entry and plays it (autoplay) or waits paused at the position. */
    async function startCurrent(autoplay = true, startPosition = 0): Promise<void> {
        const item = current.value
        const token = ++generation
        // Any other load ends the wait of the restored entry.
        if (autoplay || item?.key !== restoredKey) {
            restoredKey = null
        }

        forgetPrepared()
        loaded = null
        seekWhileLoading = null
        history.stop()
        if (!item) {
            reset()
            return
        }

        loading.value = true
        position.value = startPosition
        duration.value = seconds(item.track.durationMilliseconds)
        playback.value = autoplay ? 'playing' : 'paused'

        const result = await fetchPlayback(item)
        if (token !== generation) {
            return
        }

        // Paused while loading: the track waits.
        const shouldPlay = autoplay && playback.value === 'playing'
        if (typeof result === 'string') {
            skip(result, shouldPlay)
            return
        }

        loaded = result
        duration.value = seconds(result.playback.durationMilliseconds) || duration.value
        // A seek of the user while the track loaded wins over the position it was started at.
        const at = seekWhileLoading ?? startPosition
        seekWhileLoading = null
        position.value = at
        const player = activeEngine()
        player.load(result.source, at)
        history.start(item.track.id ?? '', at)
        if (shouldPlay) {
            playback.value = 'playing'
            await startEngine(player)
        } else {
            playback.value = 'paused'
            loading.value = false
        }
    }

    /** Shows the notice and moves on to the next entry, unless every entry failed. */
    function skip(notice: PlayerMessage, autoplay: boolean): void {
        if (!autoplay && restoredKey !== null && current.value?.key === restoredKey) {
            waitAtRestored()
            return
        }

        message.value = notice
        loading.value = false
        skipped++
        let next = nextIndex(queue.value, repeat.value, false)
        if (next === null && skipped <= autoplayMaxSkips) {
            next = promoteAutoplay()
        }

        if (next === null || skipped >= queue.value.items.length) {
            skipped = 0
            stopPlayback()
            return
        }

        queue.value = withIndex(queue.value, next)
        void startCurrent(autoplay)
    }

    /**
     * The restored entry could not be loaded: it waits paused at its position without a notice
     * (nothing was skipped, and a canceled request needs none); play requests it again.
     */
    function waitAtRestored(): void {
        forgetPrepared()
        loaded = null
        history.stop()
        runningEngine()?.stop()
        loading.value = false
        playback.value = 'paused'
    }

    function onTimeUpdate(time: number, length: number): void {
        // While the next track loads, the time of the previous one is not its position.
        if (!loaded) {
            return
        }

        position.value = time
        if (length > 0) {
            duration.value = length
        }

        history.progress(time, duration.value)
        sleep.check()
        if (duration.value > 0) {
            sleep.progress(duration.value - time, isLastEntry())
        }

        if (shouldPreload(time, duration.value)) {
            void prepareNext()
        }
    }

    /** Seconds the current track fades into the entry; 0 plays it gapless. */
    function crossfadeInto(item: QueueItem): number {
        const track = current.value?.track
        return track ? crossfadeDuration(session.crossfade, track, item.track, duration.value) : 0
    }

    /**
     * Loads the next entry on the other deck, so it starts without a gap or fades in. Nothing is
     * prepared when the sleep timer stops after the current track. At the end of the list, the next
     * song of autoplay moves into the queue now, so it can follow the same way.
     */
    async function prepareNext(): Promise<void> {
        if (output === 'cast' || !loaded || sleep.stopsAfterCurrent(isLastEntry())) {
            return
        }

        let index = nextIndex(queue.value, repeat.value, true)
        if (index === null && playback.value === 'playing') {
            index = promoteAutoplay()
        }

        const item = index === null ? undefined : queue.value.items[index]
        if (!item || prepared?.key === item.key || preparing === item.key) {
            return
        }

        preparing = item.key
        const token = generation
        const result = await fetchPlayback(item)
        if (token !== generation || preparing !== item.key) {
            return
        }

        preparing = null
        if (typeof result === 'string') {
            // It fails again when it is due and is skipped then.
            return
        }

        prepared = result
        const crossfade = crossfadeInto(item)
        getEngine().prepare(crossfade > 0 ? { ...result.source, crossfade } : result.source)
    }

    function onAdvanced(): void {
        const next = prepared
        prepared = null
        const index = next ? queue.value.items.findIndex((item) => item.key === next.key) : -1
        if (!next || index < 0) {
            return
        }

        generation++
        queue.value = withIndex(queue.value, index)
        loaded = next
        retried = null
        message.value = null
        position.value = 0
        duration.value = seconds(next.playback.durationMilliseconds)
        history.start(queue.value.items[index]?.track.id ?? '')
    }

    /**
     * The sleep timer stops after the track that ended: at the end of the queue the last track stays,
     * otherwise the next entry waits paused at its start, so play goes on with it.
     */
    function stopForSleep(): void {
        const index =
            sleep.state.mode === 'endOfTrack' ? nextIndex(queue.value, repeat.value, true) : null
        history.stop()
        if (index === null) {
            playback.value = 'paused'
            loading.value = false
        } else {
            queue.value = withIndex(queue.value, index)
            void startCurrent(false)
        }

        sleep.trackEnded()
    }

    function onEnded(): void {
        if (sleep.stopsAfterCurrent(isLastEntry())) {
            stopForSleep()
            return
        }

        const index = nextIndex(queue.value, repeat.value, true) ?? promoteAutoplay()
        if (index !== null) {
            queue.value = withIndex(queue.value, index)
            void startCurrent(true)
            return
        }

        history.stop()
        const request = autoplayActive.value ? autoplayRequest : null
        if (request) {
            // Autoplay is still choosing its songs: the first one plays once it has them.
            const token = generation
            playback.value = 'playing'
            loading.value = true
            void request.then(() => {
                if (token === generation && playback.value === 'playing') {
                    continueWithAutoplay()
                }
            })
            return
        }

        // The end of the queue: the last track stays, play starts it again.
        playback.value = 'paused'
        loading.value = false
    }

    /** Plays the next song of autoplay after the list has ended, or stops at the end. */
    function continueWithAutoplay(): void {
        const index = promoteAutoplay()
        if (index === null) {
            playback.value = 'paused'
            loading.value = false
            return
        }

        queue.value = withIndex(queue.value, index)
        void startCurrent(true)
    }

    function onError(error: unknown): void {
        const item = current.value
        const wasPlaying = playback.value === 'playing'
        if (error instanceof DOMException && error.name === 'NotAllowedError') {
            // The browser blocked the start without a user gesture; play starts it.
            playback.value = 'paused'
            loading.value = false
            return
        }

        // A stream URL may have expired while paused: load the track once more at its position.
        if (item && retried !== item.key) {
            retried = item.key
            void startCurrent(wasPlaying, position.value)
            return
        }

        retried = null
        skip('failed', wasPlaying)
    }

    function reset(): void {
        loading.value = false
        playback.value = 'idle'
        position.value = 0
        duration.value = current.value ? seconds(current.value.track.durationMilliseconds) : 0
    }

    function stopPlayback(): void {
        generation++
        forgetPrepared()
        loaded = null
        history.stop()
        runningEngine()?.stop()
        reset()
    }

    /**
     * Replaces the queue with the tracks and plays the one at start. The source names the album,
     * artist or playlist the tracks come from.
     */
    function playTrackList(
        tracks: readonly Track[],
        start = 0,
        options: { shuffle?: boolean; source?: PlayerSource } = {},
    ): void {
        const shuffled = options.shuffle ?? queue.value.shuffle
        queue.value = playTracks(tracks, start, shuffled, playerDependencies.random)
        listSource.value = options.source ?? null
        message.value = null
        skipped = 0
        retried = null
        restoredKey = null
        void startCurrent(true)
    }

    /** Without a current track, the first new entry becomes current, ready to play. */
    function afterAdding(): void {
        if (!current.value && queue.value.items.length > 0) {
            queue.value = withIndex(queue.value, 0)
            void startCurrent(false)
        } else {
            forgetPrepared()
        }
    }

    /** Songs of the user come before those of autoplay, which follow the new list. */
    function playNext(tracks: readonly Track[]): void {
        retractAutoplay()
        queue.value = insertNext(queue.value, tracks)
        afterAdding()
    }

    function addToQueue(tracks: readonly Track[]): void {
        retractAutoplay()
        queue.value = append(queue.value, tracks)
        afterAdding()
    }

    function playItem(key: string): void {
        queue.value = jumpTo(queue.value, key)
        retried = null
        restoredKey = null
        void startCurrent(true)
    }

    function removeItem(key: string): void {
        queue.value = remove(queue.value, key)
        forgetPrepared()
    }

    function moveItem(key: string, offset: -1 | 1): void {
        queue.value = move(queue.value, key, offset)
        forgetPrepared()
    }

    /** Moves an entry of the queue right after the current one. */
    function moveItemNext(key: string): void {
        queue.value = moveNext(queue.value, key)
        forgetPrepared()
    }

    function clearQueue(): void {
        queue.value = clearUpcoming(queue.value)
        forgetPrepared()
    }

    function play(): void {
        restoredKey = null
        if (!loaded) {
            void startCurrent(true, position.value)
            return
        }

        playback.value = 'playing'
        const player = activeEngine()
        // After a stop of the sleep timer the output is back already; this only covers a fade it left.
        if (!sleep.state.fading) {
            player.restoreOutput()
        }

        void startEngine(player)
    }

    function pause(): void {
        runningEngine()?.pause()
        if (playback.value === 'playing') {
            playback.value = 'paused'
        }
        // A loaded track that waited for data stops waiting; one that is still being requested
        // shows loading until startCurrent has it.
        if (loaded) {
            loading.value = false
        }
    }

    function togglePlay(): void {
        if (playback.value === 'playing') {
            pause()
        } else {
            play()
        }
    }

    function stop(): void {
        stopPlayback()
    }

    function goTo(index: number | null): void {
        if (index === null) {
            stopPlayback()
            return
        }

        const autoplay = playback.value !== 'paused'
        queue.value = withIndex(queue.value, index)
        retried = null
        restoredKey = null
        message.value = null
        void startCurrent(autoplay)
    }

    /** Skips to the next entry; at the end of a playing list to the next song of autoplay. */
    function next(): void {
        if (!current.value) {
            return
        }

        let index = nextIndex(queue.value, repeat.value, false)
        if (index === null && playback.value === 'playing') {
            index = promoteAutoplay()
        }

        goTo(index)
    }

    function previous(): void {
        if (!current.value) {
            return
        }

        const index = previousIndex(queue.value, repeat.value)
        if (shouldRestart(position.value) || index === null) {
            seek(0)
        } else {
            goTo(index)
        }
    }

    function seek(to: number): void {
        const target = Math.max(0, duration.value > 0 ? Math.min(to, duration.value) : to)
        position.value = target
        history.seek(target)
        if (loaded) {
            activeEngine().seek(target)
        } else if (loading.value) {
            // The track is still being requested: it starts at this position once it is loaded.
            seekWhileLoading = target
        }

        follower?.sync()
    }

    function toggleShuffle(): void {
        queue.value = setShuffle(queue.value, !queue.value.shuffle, playerDependencies.random)
        forgetPrepared()
    }

    function cycleRepeat(): void {
        repeat.value = nextRepeatMode(repeat.value)
        forgetPrepared()
    }

    /**
     * Turns the crossfade of the profile on or off; it applies from the next track on. If saving
     * fails, the switch goes back (the settings page shows the same state).
     */
    function toggleCrossfade(): void {
        session.setCrossfadeEnabled(!session.crossfade.enabled).catch(() => undefined)
    }

    /**
     * Turns autoplay of the profile on or off. Off takes back the songs it put after the current
     * one; the current song plays on. If saving fails, the switch goes back.
     */
    function toggleAutoplay(): void {
        session.setAutoplayEnabled(!session.autoplay.enabled).catch(() => undefined)
    }

    /**
     * Moves the first waiting song of autoplay into the queue, at its end (or right after the
     * current entry), and loads more songs when few are left. Returns its index, or null if autoplay
     * is off or no song waits.
     */
    function promoteAutoplay(afterCurrent = false): number | null {
        const entry = autoplay.value[0]
        if (!autoplayActive.value || !entry) {
            return null
        }

        autoplay.value = autoplay.value.slice(1)
        if (entry.playback) {
            autoplayPlaybacks.set(entry.key, entry.playback)
        }

        queue.value = insertAutoplay(queue.value, entry, afterCurrent)
        refillAutoplay()
        return queue.value.items.findIndex((item) => item.key === entry.key)
    }

    /**
     * Requests songs of autoplay for the current list: a new list replaces the waiting songs, a
     * refill adds to them. Answers for an older list are ignored; a failed request leaves the songs
     * as they are.
     */
    async function fetchAutoplay(replace: boolean): Promise<void> {
        if (!autoplayActive.value) {
            return
        }

        const token = replace ? ++autoplayGeneration : autoplayGeneration
        const waiting = replace ? [] : autoplay.value
        const list = autoplayList.value
        const excluded = autoplayExcluded(queue.value, waiting, autoplayDismissed)
        autoplayLoading.value = true
        const request = (async () => {
            let items
            try {
                items = (
                    await getAutoplayTracks(
                        list,
                        excluded,
                        playerDependencies.audioFormats(),
                        autoplayBatchSize,
                    )
                )?.items
            } catch {
                items = null
            }

            if (token !== autoplayGeneration) {
                return
            }

            autoplayLoading.value = false
            if (!items) {
                return
            }

            const base = replace ? [] : autoplay.value
            const added = toAutoplayEntries(items, [
                ...autoplayDismissed,
                ...base.map((entry) => entry.track.id),
                ...queue.value.items.filter((item) => item.autoplay).map((item) => item.track.id),
            ])
            autoplayExhausted = added.length === 0
            autoplay.value = [...base, ...added]
        })()
        autoplayRequest = request
        await request
        if (autoplayRequest === request) {
            autoplayRequest = null
        }
    }

    /** Loads more songs of autoplay when few are left, unless the list has no more. */
    function refillAutoplay(): void {
        if (
            autoplayActive.value &&
            !autoplayRequest &&
            !autoplayExhausted &&
            autoplay.value.length < autoplayRefillBelow
        ) {
            void fetchAutoplay(false)
        }
    }

    /** Forgets the waiting songs and ignores answers still on their way. */
    function resetAutoplay(): void {
        autoplayGeneration++
        autoplayRequest = null
        autoplayExhausted = false
        autoplayDismissed.clear()
        autoplay.value = []
        autoplayLoading.value = false
    }

    /** Takes back the songs autoplay put after the current entry. */
    function retractAutoplay(): void {
        const retracted = withoutUpcomingAutoplay(queue.value)
        if (retracted !== queue.value) {
            const kept = new Set(retracted.items.map((item) => item.key))
            for (const item of queue.value.items) {
                if (!kept.has(item.key)) {
                    autoplayPlaybacks.delete(item.key)
                }
            }

            queue.value = retracted
            forgetPrepared()
        }
    }

    function dismissAutoplay(entries: readonly AutoplayEntry[]): void {
        for (const entry of entries) {
            if (entry.track.id) {
                autoplayDismissed.add(entry.track.id)
            }
        }
    }

    /** Removes a waiting song of autoplay; more load when few are left. */
    function removeAutoplayEntry(key: string): void {
        dismissAutoplay(autoplay.value.filter((entry) => entry.key === key))
        autoplay.value = autoplay.value.filter((entry) => entry.key !== key)
        refillAutoplay()
    }

    /**
     * Plays a waiting song of autoplay now: the songs before it are skipped, the song follows the
     * current entry in the queue and plays.
     */
    function playAutoplayEntry(key: string): void {
        const at = autoplay.value.findIndex((entry) => entry.key === key)
        if (at < 0) {
            return
        }

        retractAutoplay()
        dismissAutoplay(autoplay.value.slice(0, at))
        autoplay.value = autoplay.value.slice(at)
        const index = promoteAutoplay(true)
        if (index !== null) {
            queue.value = withIndex(queue.value, index)
            retried = null
            message.value = null
            void startCurrent(true)
        }
    }

    function applyVolume(): void {
        saveVolume({ volume: volume.value, muted: muted.value })
        engine?.setVolume(outputGain())
    }

    /** The audio the video follows. */
    function clock(): FollowerClock {
        const player = runningEngine()
        return {
            position: loaded && player ? player.position : position.value,
            playing: playback.value === 'playing' && !loading.value && loaded !== null,
        }
    }

    /**
     * The position of the music in seconds as the audio plays it right now, between the time
     * updates of the engine; for views that follow the music closely, such as the lyrics.
     */
    function livePosition(): number {
        return clock().position
    }

    /** Increases with every request of lyrics, so late answers are ignored. */
    let lyricsGeneration = 0

    /**
     * Loads the lyrics of a track if it is the current one; tracks without lyrics or with failed
     * requests have none. Also called after the lyrics of a track were downloaded.
     */
    async function loadLyrics(trackId: string | null): Promise<void> {
        const request = ++lyricsGeneration
        if (lyrics.value?.trackId !== trackId) {
            lyrics.value = null
        }

        if (!trackId) {
            return
        }

        let loadedLyrics: TrackLyrics | null = null
        try {
            loadedLyrics = await getLyrics(trackId)
        } catch {
            loadedLyrics = null
        }

        if (request === lyricsGeneration && current.value?.track.id === trackId) {
            lyrics.value = loadedLyrics ?? null
        }
    }

    /** Loads the lyrics of a track again if it is the current one, e.g. after a download. */
    function refreshLyrics(trackId: string): void {
        if (current.value?.track.id === trackId) {
            void loadLyrics(trackId)
        }
    }

    /**
     * Chooses what the stage shows; the audio is never touched. Called within the tap or click on
     * the view, so a browser that lets a video play only after a gesture of the user (Safari on the
     * iPhone and iPad) allows the video of the stage to play from now on.
     */
    function selectViewMode(mode: PlayerViewMode): void {
        if (mode === PlayerViewModeObject.Video) {
            follower?.allowPlayback()
        }

        void session.updatePlayerViewMode(mode)
    }

    /**
     * A view shows its video element (the stage of Now Playing or the mini player); it shows the
     * video of tracks with one from now on. The newest element shows the video, the ones before
     * rest until it goes.
     */
    function attachVideo(element: HTMLVideoElement): void {
        if (videoElements.includes(element)) {
            return
        }

        videoElements.push(element)
        showVideoIn(element)
    }

    /**
     * A view closes its video element (without one: the element that shows the video); the audio
     * simply goes on, and the element before takes over the video.
     */
    function detachVideo(element?: HTMLVideoElement): void {
        const target = element ?? videoElement.value
        if (!target) {
            return
        }

        const index = videoElements.indexOf(target)
        if (index >= 0) {
            videoElements.splice(index, 1)
        }

        if (target === videoElement.value) {
            releaseVideo()
            const previous = videoElements.at(-1)
            if (previous) {
                showVideoIn(previous)
            }
        }
    }

    /** Lets the video follow the audio in the element; the element before stops showing it. */
    function showVideoIn(element: HTMLVideoElement): void {
        releaseVideo()
        follower = playerDependencies.createVideoFollower(element, clock, {
            stateChange: onVideoState,
            // The own controls of the video (e.g. full screen on the iPhone) control the audio.
            userPlay: play,
            userPause: pause,
        })
        videoElement.value = element
        // Not left to the watcher: when another element showed the video before, whether it is
        // wanted does not change, but the new element has to load it.
        updateVideo(false)
    }

    /** The element that showed the video stops showing it. */
    function releaseVideo(): void {
        if (!videoElement.value) {
            return
        }

        videoGeneration++
        videoKey = null
        videoElement.value = null
        follower?.dispose()
        follower = null
        videoState.value = 'idle'
    }

    /** Applies a new offset to the video of a track at once, if it is the current one. */
    function applyVideoOffset(trackId: string, offsetMilliseconds: number): void {
        if (current.value?.track.id === trackId) {
            follower?.setOffset(offsetMilliseconds)
            follower?.sync()
        }
    }

    function onVideoState(state: VideoState): void {
        videoState.value = state
        const item = current.value
        // A signed URL may have expired while the video waited: request it once more.
        if (state === 'failed' && item && videoKey === item.key && videoRetried !== item.key) {
            videoRetried = item.key
            void loadVideo(item)
        }
    }

    /** Requests the video of the entry and loads it into the follower. */
    async function loadVideo(item: QueueItem): Promise<void> {
        const token = ++videoGeneration
        videoKey = item.key
        videoState.value = 'loading'
        let result: TrackVideoPlayback | undefined
        try {
            result = await getVideoPlayback(item.track.id ?? '', playerDependencies.videoFormats())
        } catch {
            result = undefined
        }

        if (token !== videoGeneration || !follower) {
            return
        }

        const source = videoSourceOf(result)
        if (!source) {
            onVideoState('failed')
            return
        }

        follower.load(source, result?.offsetMilliseconds ?? 0)
        follower.setActive(videoWanted.value)
    }

    /** Loads, shows or hides the video of the current entry; a new entry unloads the previous one. */
    function updateVideo(entryChanged: boolean): void {
        const item = current.value
        if (entryChanged) {
            videoGeneration++
            videoKey = null
            videoRetried = null
            follower?.unload()
            videoState.value = 'idle'
        }

        if (!follower) {
            return
        }

        if (!videoWanted.value || !item) {
            follower.setActive(false)
        } else if (videoKey === item.key) {
            follower.setActive(true)
        } else {
            void loadVideo(item)
        }
    }

    /** Mutes the browser, or the device while casting. */
    function toggleMute(): void {
        if (casting.value) {
            cast.setMuted(!cast.muted)
            return
        }

        muted.value = !muted.value
        applyVolume()
    }

    /** Sets the volume of the browser, or of the device while casting. */
    function setVolume(value: number): void {
        if (casting.value) {
            const level = clampVolume(value)
            cast.setVolume(level)
            if (level > 0 && cast.muted) {
                cast.setMuted(false)
            }

            return
        }

        volume.value = clampVolume(value)
        if (volume.value > 0) {
            muted.value = false
        }

        applyVolume()
    }

    /**
     * Moves the playback to the casting device or back to the browser: the engine of the previous
     * output stops, and the current track loads on the new one at its position. On the device it
     * goes on playing if it played; back in the browser it waits paused.
     */
    function switchOutput(next: Output): void {
        if (next === output) {
            return
        }

        const wasPlaying = playback.value === 'playing'
        const at = position.value
        generation++
        forgetPrepared()
        runningEngine()?.stop()
        output = next
        loaded = null
        analyser.value = null
        history.stop()
        if (current.value && playback.value !== 'idle') {
            void startCurrent(next === 'cast' && wasPlaying, at)
        }
    }

    /** Opens the device selection; the preview helps AirPlay to offer its devices. */
    function requestCast(): void {
        cast.requestSession(output === 'local' ? (loaded?.source.url ?? null) : null)
    }

    /**
     * Starts the sleep timer, or stops it with off. A timer that stops after the current track
     * forgets the prepared one, so nothing fades into it.
     */
    function startSleepTimer(mode: SleepTimerMode, minutes?: number): void {
        if (mode === 'off') {
            cancelSleepTimer()
            return
        }

        sleep.start(mode, minutes)
        forgetPrepared()
        if (duration.value > 0) {
            sleep.progress(duration.value - position.value, isLastEntry())
        }
    }

    /** Stops the sleep timer; a running fade-out returns smoothly. */
    function cancelSleepTimer(): void {
        sleep.cancel()
        forgetPrepared()
    }

    /** Stops and empties the queue, e.g. when the profile changes. */
    function clear(): void {
        restoredKey = null
        sleep.cancel()
        stopPlayback()
        queue.value = emptyQueue
        listSource.value = null
        autoplayPlaybacks.clear()
        message.value = null
        reset()
    }

    function dismissMessage(): void {
        message.value = null
    }

    /**
     * Takes over a saved state (see src/player/resume.ts) in place of the queue: the current entry
     * waits paused at the saved position, so play goes on there. Browsers do not start playback
     * without an action of the user, so nothing plays by itself.
     */
    function restore(state: RestoredState): void {
        stopPlayback()
        queue.value = state.queue
        repeat.value = state.repeat
        listSource.value = state.source
        message.value = null
        skipped = 0
        retried = null
        autoplayPlaybacks.clear()
        restoredKey = current.value?.key ?? null
        if (current.value) {
            void startCurrent(false, state.position)
        }
    }

    // A new ReplayGain mode applies to the current track at once and to the next when it loads.
    watch(
        () => session.replayGain,
        () => {
            if (loaded && output === 'local') {
                engine?.setTrackGain(gainOf(loaded.playback))
            }

            forgetPrepared()
        },
    )

    // A new list, autoplay turned on or off, repeat or the choice of songs with a video only: the
    // waiting songs of autoplay follow. Off (or repeat) also takes back those it put into the queue.
    watch(
        [
            autoplayActive,
            () => autoplaySignature(autoplayList.value),
            () => session.autoplay.includeVideoOnly,
        ],
        ([active]) => {
            resetAutoplay()
            if (active) {
                void fetchAutoplay(true)
            } else {
                retractAutoplay()
            }
        },
    )

    // A new crossfade applies from the next track on.
    watch(
        () => session.crossfade,
        () => forgetPrepared(),
        { deep: true },
    )

    // The equalizer follows every change at once, also during playback.
    watch(
        () => [equalizer.output, equalizer.layout] as const,
        ([curve, layout]) => engine?.setEqualizer(curve, layout),
    )

    // A new entry, view mode, stage or visibility: the video follows; the audio is not touched.
    watch([() => current.value?.key ?? null, videoWanted], ([key], [previousKey]) =>
        updateVideo(key !== previousKey),
    )

    // The audio starts, pauses or waits for data: the video does the same at once.
    watch([playback, loading], () => follower?.sync())

    // A new current track: its lyrics load in the background, for the lyrics view of the stage.
    watch(
        () => current.value?.track.id ?? null,
        (trackId) => void loadLyrics(trackId),
    )

    if (typeof document !== 'undefined') {
        document.addEventListener('visibilitychange', () => {
            pageVisible.value = document.visibilityState !== 'hidden'
        })
    }

    // A casting device connects or goes: the playback moves to it or back to the browser.
    watch(casting, (now) => switchOutput(now ? 'cast' : 'local'), { immediate: true })

    // The video view is chosen or left while casting: the device plays the video or the sound.
    watch(castKind, () => {
        if (output === 'cast' && loaded) {
            void startCurrent(playback.value === 'playing', position.value)
        }
    })

    // Another profile (or none) gets an empty player: stream URLs and history belong to the session.
    watch(
        () => session.token,
        () => clear(),
    )

    // The cover of the current track for the media session, loaded again after a cover rule changed.
    watch(
        () =>
            [
                current.value?.track.hasCover ? (current.value.track.id ?? null) : null,
                covers.versionOf('track'),
            ] as const,
        async ([id, version]) => {
            if (artworkHeld) {
                covers.release('track', artworkHeld.id, artworkSize, artworkHeld.version)
                artworkHeld = null
            }

            artwork.value = null
            if (id && playerDependencies.mediaSession) {
                const held = { id, version }
                artworkHeld = held
                const url = await covers.acquire('track', id, artworkSize, version)
                if (artworkHeld === held) {
                    artwork.value = url
                }
            }
        },
    )

    const state = computed<TransportState>(() => {
        const track = current.value?.track
        return {
            playback: playback.value,
            loading: loading.value,
            trackId: track?.id ?? undefined,
            hasCover: track?.hasCover ?? false,
            title: track?.title ?? undefined,
            artist: artistOf(track),
            isFavorite: track?.isFavorite ?? false,
            rating: track?.rating ?? 0,
            position: position.value,
            duration: duration.value,
            shuffle: shuffle.value,
            repeat: repeat.value,
            volume: casting.value ? cast.volume : volume.value,
            muted: casting.value ? cast.muted : muted.value,
            message: message.value,
            equalizer: equalizer.settings.enabled,
            crossfade: session.crossfade.enabled,
            autoplay: session.autoplay.enabled,
            sleepTimer: {
                mode: sleepTimer.value.mode,
                remaining: sleepRemaining.value,
                fading: sleepTimer.value.fading,
            },
            effects: !casting.value,
            cast: {
                availability: cast.availability,
                connection: cast.connection,
                technology: cast.technology,
                device: cast.deviceName,
                devicesFound: cast.devicesFound,
            },
        }
    })

    if (playerDependencies.mediaSession) {
        connectMediaSession({
            play,
            pause,
            stop,
            previous,
            next,
            seekTo: seek,
        })
        watch(
            () => ({ ...state.value, artwork: artwork.value }),
            (value) =>
                updateMediaSession({
                    playback: value.playback,
                    title: value.title,
                    artist: value.artist,
                    album: current.value?.track.album?.title ?? undefined,
                    artwork: value.artwork,
                    position: value.position,
                    duration: value.duration,
                }),
        )
    }

    if (typeof window !== 'undefined') {
        window.addEventListener('online', () => void history.flush())
    }

    return {
        queue,
        current,
        state,
        playback,
        loading,
        position,
        duration,
        repeat,
        shuffle,
        volume,
        muted,
        message,
        viewMode,
        stageView,
        videoState,
        hasVideo,
        lyrics,
        hasLyrics,
        livePosition,
        refreshLyrics,
        equalizerAvailable,
        casting,
        analyser,
        sleepTimer,
        sleepRemaining,
        autoplay,
        autoplayLoading,
        autoplayActive,
        source: listSource,
        restore,
        playTracks: playTrackList,
        playNext,
        addToQueue,
        playItem,
        removeItem,
        moveItem,
        moveItemNext,
        clearQueue,
        play,
        pause,
        togglePlay,
        stop,
        next,
        previous,
        seek,
        toggleShuffle,
        cycleRepeat,
        toggleCrossfade,
        toggleAutoplay,
        removeAutoplayEntry,
        playAutoplayEntry,
        toggleMute,
        setVolume,
        clear,
        dismissMessage,
        selectViewMode,
        attachVideo,
        detachVideo,
        applyVideoOffset,
        startSleepTimer,
        cancelSleepTimer,
        requestCast,
    }
})
