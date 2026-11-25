// State of the transport bar. The bar is controlled through props only; the player store
// (src/stores/player.ts) provides the state and handles the events.

import type { CastAvailability, CastConnection, CastTechnology } from '@/casting/types'
import type { ConnectionStatus } from '@/devices/connection'
import type { DeviceEntry } from '@/devices/presentation'
import type { CommandStatus } from '@/devices/protocol'
import type { DeviceRole } from '@/devices/roles'
import type { PlaybackPath } from '@/player/backgroundAudio'
import type { SleepTimerMode } from '@/player/sleepTimer'
import { PlayerViewModeObject, type PlayerViewMode } from '@/api/generated/models'
import type { StageView } from '@/player/videoSync'
import type { IconSlotName } from '@/themes/iconSlots'
import type { SwipePhase } from './swipe'

export type PlaybackState = 'idle' | 'playing' | 'paused'

export type RepeatMode = 'off' | 'all' | 'one'

/**
 * Notice of the player: the track needs transcoding, which is unavailable on the server, could not
 * be loaded or failed to play. The player skips to the next track.
 */
export type PlayerMessage = 'unsupported' | 'unavailable' | 'failed'

/** The notice after the player took over the state of another device. */
export interface TransportResume {
    /** Name of the device the state comes from, e.g. "Firefox · Windows". */
    readonly device: string
}

export interface TransportState {
    playback: PlaybackState
    /** The track is being loaded or waits for data. */
    loading?: boolean
    /** Id of the current track; without one, there is nothing to play. */
    trackId?: string
    /** Whether the current track has a cover. */
    hasCover?: boolean
    title?: string
    artist?: string
    /** Favorite and rating (0 to 5) of the current track, as the API returned them. */
    isFavorite?: boolean
    rating?: number
    message?: PlayerMessage | null
    /** Position and duration in seconds. */
    position: number
    duration: number
    shuffle: boolean
    repeat: RepeatMode
    /** Volume between 0 and 1. */
    volume: number
    muted: boolean
    /** Whether the equalizer of the profile is on. */
    equalizer?: boolean
    /** Whether the crossfade of the profile is on. */
    crossfade?: boolean
    /** Whether autoplay of the profile is on. */
    autoplay?: boolean
    /** The sleep timer of this device. */
    sleepTimer?: TransportSleepTimer
    /**
     * Whether equalizer, crossfade and ReplayGain apply; not while a casting device plays. True
     * when not given.
     */
    effects?: boolean
    /**
     * The playback path of this browser: effects (through Web Audio) or plain (audio elements in
     * the background on iPhone and iPad, without equalizer, crossfade and visualization). Effects
     * when not given; only for the local playback, never published to other devices.
     */
    audioPath?: PlaybackPath
    /** Casting of this browser; hidden when not given. */
    cast?: TransportCast
}

/** Casting as the transport bar shows it: the cast button and the device that plays. */
export interface TransportCast {
    availability: CastAvailability
    connection: CastConnection
    technology: CastTechnology | null
    /** Name of the device; null while unknown (AirPlay never tells it). */
    device: string | null
    /** Whether the browser found devices. */
    devicesFound: boolean
}

/**
 * The devices of the profile as the transport bar shows them: the devices button with its picker,
 * and while this device is a remote, the device that plays.
 */
export interface TransportDevices {
    /** local: this device plays; remote: it controls the active device. */
    readonly role: DeviceRole
    /** The connection to the server. */
    readonly connection: ConnectionStatus
    /** The open devices of the profile and this device. */
    readonly entries: readonly DeviceEntry[]
    /** Name of the device that plays while this device is its remote; null otherwise. */
    readonly playingOn: string | null
    /** Whether other devices may control this one (for the running page only). */
    readonly remoteControl: boolean
    /** Whether this is an iPhone or iPad, which ends the connection in the background. */
    readonly appleMobile: boolean
    /** Outcome of the last command or hand-over of this device that was not accepted. */
    readonly status: CommandStatus | null
    /** Whether another tab of this browser took the device over. */
    readonly replaced: boolean
}

/** data-state tokens of the cast control, e.g. "chromecast available connected". */
export function castStateTokens(cast: TransportCast | undefined): string {
    if (!cast) {
        return 'unsupported'
    }

    return [
        ...(cast.technology ? [cast.technology] : []),
        cast.availability,
        cast.connection,
        ...(cast.devicesFound ? [] : ['no-devices']),
    ].join(' ')
}

export type SleepTimerChoice =
    | { readonly mode: 'off' }
    | { readonly mode: 'duration'; readonly minutes: number }
    | { readonly mode: 'endOfTrack' }
    | { readonly mode: 'endOfQueue' }

export interface TransportSleepTimer {
    mode: SleepTimerMode
    /** Seconds until a timer with a duration ends; 0 otherwise. */
    remaining: number
    /** Whether the music fades out right now. */
    fading: boolean
}

/** data-state tokens of the sleep timer, e.g. "duration fading". */
export function sleepTimerStateTokens(timer: TransportSleepTimer | undefined): string {
    const mode = timer?.mode ?? 'off'
    const token =
        mode === 'endOfTrack' ? 'end-of-track' : mode === 'endOfQueue' ? 'end-of-queue' : mode
    return [token, ...(timer?.fading ? ['fading'] : [])].join(' ')
}

/** Repeat cycles through off, all and one. */
export function nextRepeatMode(mode: RepeatMode): RepeatMode {
    switch (mode) {
        case 'off':
            return 'all'
        case 'all':
            return 'one'
        default:
            return 'off'
    }
}

export interface TransportIcons {
    readonly play: Extract<IconSlotName, 'play' | 'pause'>
    readonly repeat: Extract<IconSlotName, 'repeat' | 'repeat-one'>
    readonly volume: Extract<IconSlotName, 'volume' | 'muted'>
}

/** Icon slots that show the state of the play, repeat and mute buttons. */
export function transportIcons(
    state: Pick<TransportState, 'playback' | 'repeat' | 'muted'>,
): TransportIcons {
    return {
        play: state.playback === 'playing' ? 'pause' : 'play',
        repeat: state.repeat === 'one' ? 'repeat-one' : 'repeat',
        volume: state.muted ? 'muted' : 'volume',
    }
}

/** What the gesture and the view switch of the full-screen player add to the state of the bar. */
export interface TransportStateExtras {
    /** Phase of swiping the full-screen player down. */
    readonly swipe?: SwipePhase
    /** Whether the view switch of the full-screen player is open. */
    readonly viewSwitch?: boolean
    /** Whether this device controls another device that plays. */
    readonly remote?: boolean
}

/**
 * data-state tokens of the transport bar, e.g. "playing loading expanded"; while the full-screen
 * player is swiped, the phase ("dragging", "settling" or "dismissing"), "view-switch" while
 * its view switch is open, and "remote" while another device of the profile plays.
 */
export function transportStateTokens(
    playback: PlaybackState,
    expanded: boolean,
    loading = false,
    extras: TransportStateExtras = {},
): string {
    const swipe = expanded && extras.swipe && extras.swipe !== 'idle' ? [extras.swipe] : []
    const viewSwitch = expanded && extras.viewSwitch ? ['view-switch'] : []
    return [
        playback,
        ...(loading ? ['loading'] : []),
        ...(expanded ? ['expanded'] : []),
        ...swipe,
        ...viewSwitch,
        ...(extras.remote ? ['remote'] : []),
    ].join(' ')
}

/** What the full-screen player shows in the place of the cover. */
export type TransportView = 'cover' | 'video' | 'visualization'

/**
 * The views of the full-screen player (mobile): like the stage of Now Playing, it shows the cover,
 * the video or the visualization the profile chose, and its view switch chooses among them.
 */
export interface TransportStage {
    /** The view mode chosen by the profile. */
    readonly mode: PlayerViewMode
    /** What the stage of the player shows (StageView of the player store). */
    readonly view: StageView
    /** Whether the current song has a music video. */
    readonly hasVideo: boolean
    /** Whether a casting device plays (it shows the video; the browser does not hear the music). */
    readonly casting: boolean
    /** Whether another device of the profile plays (this one is its remote). */
    readonly remote?: boolean
}

/** Whether the music plays elsewhere: on a casting device or another device of the profile. */
function playsElsewhere(stage: TransportStage): boolean {
    return stage.casting || stage.remote === true
}

/** What the full-screen player shows: the lyrics stay on the stage of Now Playing. */
export function transportView(stage: TransportStage | null | undefined): TransportView {
    const view = stage?.view ?? 'cover'
    return view === 'lyrics' ? 'cover' : view
}

/**
 * The checked option of the view switch of the full-screen player: the chosen mode, but the cover
 * for a song without a video while the video is chosen, for the visualization while the music
 * plays elsewhere (casting, another device), and while the lyrics are chosen (the player has no
 * lyrics view).
 */
export function transportViewChoice(stage: TransportStage): PlayerViewMode {
    switch (stage.mode) {
        case PlayerViewModeObject.Video:
            return stage.hasVideo ? stage.mode : PlayerViewModeObject.Cover
        case PlayerViewModeObject.Visualization:
            return playsElsewhere(stage) ? PlayerViewModeObject.Cover : stage.mode
        case PlayerViewModeObject.Lyrics:
            return PlayerViewModeObject.Cover
        default:
            return stage.mode
    }
}

/**
 * Whether the full-screen player needs its video element: the profile chose the video, the song has
 * one and the music plays here (not on a casting device or another device). The video shows once
 * it is ready (view "video").
 */
export function transportNeedsVideo(stage: TransportStage | null | undefined): boolean {
    return (
        !!stage &&
        stage.mode === PlayerViewModeObject.Video &&
        stage.hasVideo &&
        !playsElsewhere(stage)
    )
}

/** Whether the full-screen player draws the visualization: chosen and the music plays here. */
export function transportNeedsVisualization(stage: TransportStage | null | undefined): boolean {
    return !!stage && stage.mode === PlayerViewModeObject.Visualization && !playsElsewhere(stage)
}
