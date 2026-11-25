// Messages of the real-time connection of the devices (ASP.NET Core SignalR, JSON hub protocol).
// The contract is asyncapi.yaml in the root of the repository; Kiota does not cover real-time
// connections, so the types are written by hand with the names of its schemas and enums. Schemas
// it takes from the REST API (Device, Track, PlaybackSource, ValidationError, ...) are the models of
// the generated client: the connection decodes them with its deserializer (see wire.ts), so dates
// are Date objects as in every REST answer. src/devices/__tests__/protocol.spec.ts compares the
// enums and the methods with asyncapi.yaml.

import type {
    Device,
    DeviceType,
    PlaybackSource,
    RepeatMode,
    Track,
    ValidationError,
} from '@/api/generated/models'

/** Path of the hub, below the API of the same origin. */
export const deviceHubPath = '/api/v1/hubs/devices'

/** Methods of the hub a client calls (operations with the action `receive`). */
export const hubMethods = {
    register: 'Register',
    setRemoteControl: 'SetRemoteControl',
    publishPlayback: 'PublishPlayback',
    sendCommand: 'SendCommand',
    transferPlayback: 'TransferPlayback',
    getQueue: 'GetQueue',
} as const

/** Messages the server sends (operations with the action `send`). */
export const hubEvents = {
    devicesChanged: 'DevicesChanged',
    playbackChanged: 'PlaybackChanged',
    commandReceived: 'CommandReceived',
    deviceReplaced: 'DeviceReplaced',
} as const

export const RegisterStatusObject = {
    Registered: 'Registered',
    Invalid: 'Invalid',
} as const
export type RegisterStatus = (typeof RegisterStatusObject)[keyof typeof RegisterStatusObject]

export const LivePlaybackStatusObject = {
    Idle: 'Idle',
    Loading: 'Loading',
    Playing: 'Playing',
    Paused: 'Paused',
} as const
export type LivePlaybackStatus =
    (typeof LivePlaybackStatusObject)[keyof typeof LivePlaybackStatusObject]

export const PublishStatusObject = {
    Accepted: 'Accepted',
    NotRegistered: 'NotRegistered',
    NotActive: 'NotActive',
    Invalid: 'Invalid',
} as const
export type PublishStatus = (typeof PublishStatusObject)[keyof typeof PublishStatusObject]

export const DeviceCommandTypeObject = {
    Play: 'Play',
    Pause: 'Pause',
    Stop: 'Stop',
    Next: 'Next',
    Previous: 'Previous',
    Seek: 'Seek',
    SetVolume: 'SetVolume',
    SetShuffle: 'SetShuffle',
    SetRepeat: 'SetRepeat',
    SetCrossfade: 'SetCrossfade',
    SetAutoplay: 'SetAutoplay',
    PlayTracks: 'PlayTracks',
    AddToQueue: 'AddToQueue',
    PlayNext: 'PlayNext',
    PlayQueueEntry: 'PlayQueueEntry',
    RemoveQueueEntry: 'RemoveQueueEntry',
    MoveQueueEntry: 'MoveQueueEntry',
    ClearQueue: 'ClearQueue',
} as const
export type DeviceCommandType =
    (typeof DeviceCommandTypeObject)[keyof typeof DeviceCommandTypeObject]

export const CommandStatusObject = {
    Accepted: 'Accepted',
    NotRegistered: 'NotRegistered',
    DeviceNotFound: 'DeviceNotFound',
    RemoteControlNotAllowed: 'RemoteControlNotAllowed',
    NotActive: 'NotActive',
    Conflict: 'Conflict',
    Invalid: 'Invalid',
} as const
export type CommandStatus = (typeof CommandStatusObject)[keyof typeof CommandStatusObject]

/** Registers the device the connection runs on; the first call of every connection. */
export interface RegisterDevice {
    deviceId: string
    name: string
    type: DeviceType
    /** Whether the user allowed remote control on the running page; never stored. */
    remoteControl: boolean
}

export interface RegisterResult {
    status: RegisterStatus
    snapshot: DeviceSessionSnapshot | null
    errors: ValidationError[]
}

/** All devices of the profile and the live playback, for a device that registered. */
export interface DeviceSessionSnapshot {
    deviceId: string
    devices: Device[]
    playback: LivePlayback
}

export interface SetRemoteControl {
    allowed: boolean
}

/** The state of the player of this device; with claim, the device takes playback over. */
export interface PublishPlayback {
    claim: boolean
    playback: LivePlaybackInput
}

export interface LivePlaybackInput {
    status: LivePlaybackStatus
    /** The queue; null or missing if it did not change since the last publication. */
    queue?: LiveQueueInput | null
    currentIndex: number
    positionMilliseconds: number
    durationMilliseconds: number
    repeat: RepeatMode
    volume: number
    muted: boolean
    crossfade: boolean
    autoplay: boolean
}

export interface LiveQueueInput {
    items: LiveQueueItem[]
    shuffle: boolean
    originalOrder?: number[] | null
    source?: PlaybackSource | null
}

export interface LiveQueueItem {
    trackId: string
    autoplay: boolean
}

export interface PublishResult {
    status: PublishStatus
    version: number
    queueVersion: number
    errors: ValidationError[]
}

/** A command for the active device of the profile; only the members its type uses are read. */
export interface DeviceCommand {
    targetDeviceId: string
    type: DeviceCommandType
    expectedQueueVersion?: number | null
    positionMilliseconds?: number | null
    volume?: number | null
    muted?: boolean | null
    enabled?: boolean | null
    repeat?: RepeatMode | null
    trackIds?: string[] | null
    startIndex?: number | null
    source?: PlaybackSource | null
    shuffle?: boolean | null
    index?: number | null
    toIndex?: number | null
}

/** Result of a command, a hand-over or a change of the permission. */
export interface CommandResult {
    status: CommandStatus
    version: number
    queueVersion: number
    errors: ValidationError[]
}

export interface TransferPlayback {
    targetDeviceId: string
    play: boolean
    expectedVersion?: number | null
}

export interface DeviceQueueResult {
    status: CommandStatus
    queue: DeviceQueue | null
}

/** The live queue of the profile with its songs. */
export interface DeviceQueue {
    version: number
    queueVersion: number
    currentIndex: number
    shuffle: boolean
    source: PlaybackSource | null
    items: DeviceQueueEntry[]
}

export interface DeviceQueueEntry {
    /** Its position in the live queue; commands name entries by it. */
    position: number
    trackId: string
    autoplay: boolean
    /** The song; null if it left the library. */
    track: Track | null
}

export interface DeviceList {
    devices: Device[]
}

/** The live playback of the profile as the remotes show it. */
export interface LivePlayback {
    version: number
    queueVersion: number
    activeDeviceId: string | null
    status: LivePlaybackStatus
    currentIndex: number
    currentTrackId: string | null
    queueLength: number
    /** Position when the server sent the state (while playing, the time since the publication is added). */
    positionMilliseconds: number
    durationMilliseconds: number
    shuffle: boolean
    repeat: RepeatMode
    source: PlaybackSource | null
    volume: number
    muted: boolean
    crossfade: boolean
    autoplay: boolean
    handover: PlaybackHandover | null
}

export interface PlaybackHandover {
    fromDeviceId: string | null
    play: boolean
}

/** A command for this device, checked by the server; members its type does not use are null. */
export interface ReceivedCommand {
    fromDeviceId: string
    type: DeviceCommandType
    version: number
    expectedQueueVersion: number | null
    positionMilliseconds: number | null
    volume: number | null
    muted: boolean | null
    enabled: boolean | null
    repeat: RepeatMode | null
    tracks: Track[] | null
    startIndex: number | null
    source: PlaybackSource | null
    shuffle: boolean | null
    index: number | null
    toIndex: number | null
}

/** No members. */
export type DeviceReplaced = Record<string, never>
