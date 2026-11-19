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
import { getPlayback, getVideoPlayback, recordPlay, reportNowPlaying } from '@/player/api'
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
    'load' | 'unload' | 'setActive' | 'setOffset' | 'sync' | 'dispose'
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
    /** Whether the video of the current track is to be shown. */
    const videoWanted = computed(
        () =>
            videoElement.value !== null &&
            hasVideo.value &&
            viewMode.value === PlayerViewModeObject.Video &&
            pageVisible.value &&
            !casting.value,
    )
    /** What a casting device plays of the current track: its video or its sound. */
    const castKind = computed(() => castMediaKind(viewMode.value, hasVideo.value))

    let engine: PlayerEngine | null = null
    /** The engine of the casting device, created with the first connection. */
    let castEngine: PlayerEngine | null = null
    /** Where the music plays; events of the other engine are ignored. */
    let output: Output = 'local'
    let follower: PlayerVideoFollower | null = null
    /** The queue entry whose video is loaded or requested. */
    let videoKey: string | null = null
    /** Increases with every request of a video, so late answers are ignored. */
    let videoGeneration = 0
    /** The entry whose failed video was already requested once more. */
    let videoRetried: string | null = null
    /** The loaded track and its playback; null while loading or idle. */
    let loaded: Loaded | null = null
    /** The track prepared on the other deck. */
    let prepared: Loaded | null = null
    let preparing: string | null = null
    /** Increases with every new track, so late answers of older requests are ignored. */
    let generation = 0
    /** Tracks skipped in a row; stops when the whole queue failed. */
    let skipped = 0
    let retried: string | null = null
    let artworkHeld: { id: string; version: string | undefined } | null = null

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

    /** Whether the current entry is the last one in the order of the queue, without repeat. */
    function isLastEntry(): boolean {
        return nextIndex(queue.value, 'off', false) === null
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
        const result = await getPlayback(item.track.id ?? '', playerDependencies.audioFormats())
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
        forgetPrepared()
        loaded = null
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
        const player = activeEngine()
        player.load(result.source, startPosition)
        history.start(item.track.id ?? '', startPosition)
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
        message.value = notice
        loading.value = false
        skipped++
        const next = nextIndex(queue.value, repeat.value, false)
        if (next === null || skipped >= queue.value.items.length) {
            skipped = 0
            stopPlayback()
            return
        }

        queue.value = withIndex(queue.value, next)
        void startCurrent(autoplay)
    }

    function onTimeUpdate(time: number, length: number): void {
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
     * prepared when the sleep timer stops after the current track.
     */
    async function prepareNext(): Promise<void> {
        const index = nextIndex(queue.value, repeat.value, true)
        const item = index === null ? undefined : queue.value.items[index]
        if (
            output === 'cast' ||
            !item ||
            !loaded ||
            prepared?.key === item.key ||
            preparing === item.key ||
            sleep.stopsAfterCurrent(isLastEntry())
        ) {
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

        const index = nextIndex(queue.value, repeat.value, true)
        if (index === null) {
            // The end of the queue: the last track stays, play starts it again.
            history.stop()
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

    /** Replaces the queue with the tracks and plays the one at start. */
    function playTrackList(
        tracks: readonly Track[],
        start = 0,
        options: { shuffle?: boolean } = {},
    ): void {
        const shuffled = options.shuffle ?? queue.value.shuffle
        queue.value = playTracks(tracks, start, shuffled, playerDependencies.random)
        message.value = null
        skipped = 0
        retried = null
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

    function playNext(tracks: readonly Track[]): void {
        queue.value = insertNext(queue.value, tracks)
        afterAdding()
    }

    function addToQueue(tracks: readonly Track[]): void {
        queue.value = append(queue.value, tracks)
        afterAdding()
    }

    function playItem(key: string): void {
        queue.value = jumpTo(queue.value, key)
        retried = null
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
        message.value = null
        void startCurrent(autoplay)
    }

    function next(): void {
        if (current.value) {
            goTo(nextIndex(queue.value, repeat.value, false))
        }
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

    /** Chooses what the stage shows; the audio is never touched. */
    function selectViewMode(mode: PlayerViewMode): void {
        void session.updatePlayerViewMode(mode)
    }

    /** The stage shows its video element; it shows the video of tracks with one from now on. */
    function attachVideo(element: HTMLVideoElement): void {
        if (videoElement.value === element) {
            return
        }

        detachVideo()
        follower = playerDependencies.createVideoFollower(element, clock, {
            stateChange: onVideoState,
            // The own controls of the video (e.g. full screen on the iPhone) control the audio.
            userPlay: play,
            userPause: pause,
        })
        videoElement.value = element
    }

    /** The stage closes; the audio simply goes on. */
    function detachVideo(element?: HTMLVideoElement): void {
        if (!videoElement.value || (element && element !== videoElement.value)) {
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
        sleep.cancel()
        stopPlayback()
        queue.value = emptyQueue
        message.value = null
        reset()
    }

    function dismissMessage(): void {
        message.value = null
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
