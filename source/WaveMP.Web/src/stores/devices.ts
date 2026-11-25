import { defineStore } from 'pinia'
import { computed, markRaw, ref, shallowRef, watch } from 'vue'
import type { Device } from '@/api/generated/models'
import { createHubTicket } from '@/devices/api'
import { commandFor, type RemoteAction } from '@/devices/commands'
import {
    createBrowserConnection,
    DeviceConnection,
    listenToBrowser,
    type ConnectionStatus,
    type HubConnectionLike,
} from '@/devices/connection'
import { browserHints, deviceType, isAppleMobile, type DeviceHints } from '@/devices/device'
import {
    CommandStatusObject,
    hubMethods,
    PublishStatusObject,
    RegisterStatusObject,
    type CommandResult,
    type CommandStatus,
    type DeviceCommand,
    type DeviceQueue,
    type DeviceQueueResult,
    type LivePlayback,
    type LiveQueueInput,
    type PublishResult,
    type ReceivedCommand,
    type RegisterResult,
} from '@/devices/protocol'
import { liveAfterPublication, toPublication, type PlayerLiveSnapshot } from '@/devices/publication'
import {
    mirrorQueue,
    optimisticLive,
    remoteTransportState,
    restoredFromLive,
    transportPlayback,
    type RemoteTransportState,
} from '@/devices/remote'
import { activeDeviceOf, roleOf, type DeviceRole } from '@/devices/roles'
import { decodeQueueResult, decodeRegisterResult } from '@/devices/wire'
import { deviceId, deviceName } from '@/player/device'
import type { Queue } from '@/player/queue'
import type { RestoredState } from '@/player/resume'
import { useSessionStore } from './session'

/**
 * What the devices store needs of the player of this device; the player store attaches it, so the
 * two stores do not import each other.
 */
export interface DevicePlayer {
    /** The state of the player as it is published. */
    snapshot(): PlayerLiveSnapshot
    /** Whether the player plays right now (reactive). */
    playing(): boolean
    /** Runs a command of a remote; positions of the queue start at windowStart. */
    execute(command: ReceivedCommand, windowStart: number): void
    /** Playback was handed over to this device: load the saved state and play if asked. */
    handover(play: boolean): Promise<void>
    /** Another device took playback over: this one pauses. */
    yieldPlayback(): void
    /** Unlocks audio playback within a click, so remote commands may start it later. */
    unlockAudio(): void
    /** Publishes the state at once (after registering as the active device). */
    publishNow(): void
}

/** Takes over the state of the device that played, when this device stops being its remote. */
export type ResumeFromRemote = (state: RestoredState, device: string | null) => boolean

/** Parts of the browser the store uses; tests replace them. */
export const devicesDependencies = {
    deviceId: (): string => deviceId(),
    deviceName: (): string =>
        deviceName(typeof navigator === 'undefined' ? '' : navigator.userAgent),
    hints: (): DeviceHints => browserHints(),
    createConnection: (accessToken: () => Promise<string>): HubConnectionLike =>
        createBrowserConnection(accessToken),
    ticket: (): Promise<string> => createHubTicket(),
    /** Calls wake on `online`, `pageshow` and when the page becomes visible. */
    listen: (wake: () => void): (() => void) => listenToBrowser(wake),
    now: (): number => Date.now(),
    setTimeout: (handler: () => void, milliseconds: number): number =>
        window.setTimeout(handler, milliseconds),
    clearTimeout: (id: number): void => window.clearTimeout(id),
    setInterval: (handler: () => void, milliseconds: number): number =>
        window.setInterval(handler, milliseconds),
    clearInterval: (id: number): void => window.clearInterval(id),
}

/** Milliseconds between two updates of the position a remote shows while the device plays. */
export const remoteClockMilliseconds = 500

/**
 * The devices of the profile and the live playback (asyncapi.yaml). Every open instance of the web
 * app registers as a device; one device of the profile plays (the active device), the others are
 * remotes that show its state live and send commands to it.
 *
 * The store connects after start (AppShell) while a profile is selected, and registers after every
 * connect with the permission of the running page: "Allow remote control" is kept in memory only
 * and is off after a reload. As the active device, the player publishes its state through
 * publish; commands of remotes reach the player. As a remote, the player shows the mirror of the
 * live queue and the remote transport state, and sends its actions with send. Newer states win:
 * a PlaybackChanged with a lower version than the known one is ignored.
 */
export const useDevicesStore = defineStore('devices', () => {
    const session = useSessionStore()

    const hints = devicesDependencies.hints()
    /** This device: its id, the name it reports and its kind. */
    const self = {
        id: devicesDependencies.deviceId(),
        name: devicesDependencies.deviceName(),
        type: deviceType(hints),
    }
    /** Whether this is an iPhone or iPad, which closes connections of pages in the background. */
    const appleMobile = isAppleMobile(hints)

    const status = ref<ConnectionStatus>('offline')
    /** Whether this device is registered with the hub. */
    const registered = ref(false)
    /** Whether another tab of this browser took the device over; it syncs again when shown. */
    const replaced = ref(false)
    const devices = shallowRef<Device[]>([])
    /** The live playback of the profile and when it arrived. */
    const live = shallowRef<LivePlayback | null>(null)
    const receivedAt = ref(0)
    /** Now, updated while a remote shows a playing device, so the position moves. */
    const clock = ref(devicesDependencies.now())
    /** Whether other devices may control this one: for the running page only, never stored. */
    const remoteControl = ref(false)
    /** The live queue as read with GetQueue. */
    const queue = shallowRef<DeviceQueue | null>(null)
    /** Outcome of the last command or hand-over of this device that was not accepted. */
    const lastStatus = ref<CommandStatus | null>(null)
    /** Whether playback is being handed over to this device right now. */
    const handingOver = ref(false)
    const player = shallowRef<DevicePlayer | null>(null)

    let started = false
    let registration = 0
    let queueRequest = 0
    let ticker: number | null = null
    /** The queue published last, its signature and where its window starts. */
    let publishedSignature: string | null = null
    let publishedQueue: LiveQueueInput | null = null
    let windowStart = 0
    const resumeHandlers = new Set<ResumeFromRemote>()

    const role = computed<DeviceRole>(() =>
        roleOf({
            deviceId: self.id,
            registered: registered.value,
            playback: live.value,
            devices: devices.value,
            playing: player.value?.playing() ?? false,
        }),
    )
    const remote = computed(() => role.value === 'remote')
    const activeDevice = computed(() => activeDeviceOf(live.value, devices.value))
    /** Whether this device is the active device of the profile. */
    const isActive = computed(
        () => registered.value && !!live.value && live.value.activeDeviceId === self.id,
    )

    const mirrorBase = computed<Queue>(() => mirrorQueue(queue.value))
    let mirrorCache: Queue | null = null
    /** The live queue as the player of a remote shows it, with the live current entry. */
    const mirror = computed<Queue>(() => {
        const base = mirrorBase.value
        const current = live.value
        const index =
            current && queue.value && queue.value.queueVersion === current.queueVersion
                ? current.currentIndex
                : base.index
        const value =
            index === base.index || index < -1 || index >= base.items.length
                ? base
                : { ...base, index }
        if (mirrorCache && mirrorCache.items === value.items && mirrorCache.index === value.index) {
            return mirrorCache
        }

        mirrorCache = value
        return value
    })

    /** What the transport bar of a remote shows; null unless this device is a remote. */
    const transport = computed<RemoteTransportState | null>(() =>
        remote.value && live.value
            ? remoteTransportState(live.value, mirror.value, receivedAt.value, clock.value)
            : null,
    )

    const connection = markRaw(
        new DeviceConnection(
            {
                createConnection: (accessToken) =>
                    devicesDependencies.createConnection(accessToken),
                ticket: () => devicesDependencies.ticket(),
                timers: {
                    setTimeout: (handler, milliseconds) =>
                        devicesDependencies.setTimeout(handler, milliseconds),
                    clearTimeout: (id) => devicesDependencies.clearTimeout(id),
                },
                listen: (wake) => devicesDependencies.listen(wake),
            },
            {
                status: (value) => {
                    status.value = value
                    // A lost connection (not one that reconnects) ends the part of this device.
                    if (value === 'offline') {
                        registered.value = false
                    }
                },
                connected: () => void register(),
                resumed: () => void register(),
                devicesChanged: (list) => {
                    devices.value = list.devices
                },
                playbackChanged: (playback) => applyPlayback(playback, false),
                commandReceived: (command) => {
                    if (isActive.value) {
                        player.value?.execute(command, windowStart)
                    }
                },
                deviceReplaced: () => {
                    registered.value = false
                    replaced.value = true
                },
            },
        ),
    )

    function now(): number {
        return devicesDependencies.now()
    }

    function setLive(playback: LivePlayback): void {
        live.value = playback
        receivedAt.value = now()
        clock.value = receivedAt.value
    }

    /**
     * Applies a live playback of the hub. Older versions are ignored. A hand-over to this device
     * loads the saved state and plays; the device it came from pauses. A device that played and
     * allows remote control pauses when another one took playback over.
     */
    function applyPlayback(playback: LivePlayback, snapshot: boolean): void {
        const previous = live.value
        if (!snapshot && previous && playback.version < previous.version) {
            return
        }

        setLive(playback)
        const handover = playback.handover
        if (handover) {
            if (playback.activeDeviceId === self.id) {
                void takeOver(handover.play)
            } else if (handover.fromDeviceId === self.id) {
                player.value?.yieldPlayback()
            }

            return
        }

        if (
            previous?.activeDeviceId === self.id &&
            playback.activeDeviceId !== null &&
            playback.activeDeviceId !== self.id &&
            remoteControl.value
        ) {
            player.value?.yieldPlayback()
        }
    }

    async function takeOver(play: boolean): Promise<void> {
        const target = player.value
        if (!target) {
            return
        }

        handingOver.value = true
        publishedSignature = null
        try {
            await target.handover(play)
        } finally {
            handingOver.value = false
            target.publishNow()
        }
    }

    function registerMessage() {
        return {
            deviceId: self.id,
            name: self.name,
            type: self.type,
            remoteControl: remoteControl.value,
        }
    }

    /** Registers this device (again) and takes over the devices and the live playback. */
    async function register(): Promise<void> {
        const own = ++registration
        let result: RegisterResult
        try {
            result = decodeRegisterResult(
                await connection.invoke<RegisterResult>(hubMethods.register, registerMessage()),
            )
        } catch {
            return
        }

        if (own !== registration) {
            return
        }

        const snapshot = result.snapshot
        if (result.status !== RegisterStatusObject.Registered || !snapshot) {
            registered.value = false
            return
        }

        devices.value = snapshot.devices
        publishedSignature = null
        publishedQueue = null
        applyPlayback(snapshot.playback, true)
        registered.value = true
        replaced.value = false
        const active = snapshot.playback.activeDeviceId
        if (active === self.id) {
            // Back after a reload or a lost connection: the state of this device is the live one.
            player.value?.publishNow()
        } else if (active === null && player.value?.playing()) {
            void publish(true)
        }
    }

    /**
     * Publishes the state of the player: as the active device, or with claim to take playback
     * over (the user started it here). Nothing happens without registration.
     */
    async function publish(claim = false): Promise<void> {
        const target = player.value
        if (!registered.value || !target || handingOver.value || (!claim && !isActive.value)) {
            return
        }

        const publication = toPublication(target.snapshot(), publishedSignature, claim)
        const input = publication.input
        windowStart = publication.windowStart
        if (input.queue) {
            publishedSignature = publication.queueSignature
            publishedQueue = input.queue
        }

        const queueInput = publishedQueue
        let result: PublishResult
        try {
            result = await connection.invoke<PublishResult>(hubMethods.publishPlayback, {
                claim,
                playback: input,
            })
        } catch {
            publishedSignature = null
            return
        }

        if (result.status !== PublishStatusObject.Accepted || !queueInput) {
            // The next publication names the queue again.
            publishedSignature = null
            return
        }

        if (!live.value || result.version >= live.value.version) {
            setLive(
                liveAfterPublication(
                    input,
                    queueInput,
                    self.id,
                    result.version,
                    result.queueVersion,
                ),
            )
        }
    }

    /** Sends a command to the active device; a conflict reads the queue again. */
    async function send(command: DeviceCommand): Promise<CommandStatus | null> {
        const current = live.value
        const optimistic = current
            ? optimisticLive(current, command, receivedAt.value, now())
            : null
        let result: CommandResult
        try {
            result = await connection.invoke<CommandResult>(hubMethods.sendCommand, command)
        } catch {
            return null
        }

        lastStatus.value = result.status === CommandStatusObject.Accepted ? null : result.status
        if (result.status === CommandStatusObject.Accepted) {
            // Shown at once; the publication of the active device follows with a higher version.
            if (optimistic && live.value === current) {
                setLive(optimistic)
            }
        } else if (result.status === CommandStatusObject.Conflict) {
            void loadQueue(true)
        }

        return result.status
    }

    /** Sends an action of the user of this remote to the active device. */
    async function sendAction(action: RemoteAction): Promise<CommandStatus | null> {
        const current = live.value
        const target = current?.activeDeviceId
        if (!current || !target || !remote.value) {
            return null
        }

        const command = commandFor(action, {
            target,
            live: current,
            mirror: mirror.value,
            queueVersion: queue.value?.queueVersion ?? current.queueVersion,
        })
        return command ? send(command) : null
    }

    /**
     * Hands playback over to a device (this one for "play here") at the same position. Without
     * play given, it plays there if it plays now. As the active device, the latest position is
     * published first.
     */
    async function transfer(targetDeviceId: string, play?: boolean): Promise<CommandStatus | null> {
        if (!registered.value) {
            return null
        }

        if (isActive.value) {
            await publish()
        }

        const current = live.value
        const playing =
            play ??
            (isActive.value
                ? (player.value?.playing() ?? false)
                : current
                  ? transportPlayback(current.status) === 'playing'
                  : false)
        let result: CommandResult
        try {
            result = await connection.invoke<CommandResult>(hubMethods.transferPlayback, {
                targetDeviceId,
                play: playing,
                expectedVersion: current?.version ?? null,
            })
        } catch {
            return null
        }

        lastStatus.value = result.status === CommandStatusObject.Accepted ? null : result.status
        return result.status
    }

    /**
     * Allows or forbids remote control of this device for the running page. Must run within the
     * click: allowing it also unlocks audio, so a remote can start playback here later.
     */
    function allowRemoteControl(allowed: boolean): void {
        if (allowed) {
            player.value?.unlockAudio()
        }

        remoteControl.value = allowed
        if (registered.value) {
            connection
                .invoke<CommandResult>(hubMethods.setRemoteControl, { allowed })
                .catch(() => undefined)
        }
    }

    /** Reads the live queue if its version changed (or always with force). */
    async function loadQueue(force = false): Promise<void> {
        const current = live.value
        if (!registered.value || !current) {
            return
        }

        if (!force && queue.value?.queueVersion === current.queueVersion) {
            return
        }

        const own = ++queueRequest
        let result: DeviceQueueResult
        try {
            result = decodeQueueResult(
                await connection.invoke<DeviceQueueResult>(hubMethods.getQueue),
            )
        } catch {
            return
        }

        const loaded = result.queue
        if (own !== queueRequest || result.status !== CommandStatusObject.Accepted || !loaded) {
            return
        }

        if (!queue.value || loaded.version >= queue.value.version) {
            queue.value = loaded
        }
    }

    /** The live playback as this device takes it over when it stops being a remote. */
    function mirrorState(): RestoredState | null {
        const current = live.value
        return current ? restoredFromLive(current, queue.value, receivedAt.value, now()) : null
    }

    /** Registers who takes over the state of the device that played; returns the unregister. */
    function onResumeFromRemote(handler: ResumeFromRemote): () => void {
        resumeHandlers.add(handler)
        return () => resumeHandlers.delete(handler)
    }

    /** Hands the state over to the registered handler; false if none took it. */
    function resumeFromRemote(state: RestoredState, device: string | null): boolean {
        for (const handler of resumeHandlers) {
            if (handler(state, device)) {
                return true
            }
        }

        return false
    }

    function attachPlayer(value: DevicePlayer): void {
        player.value = markRaw(value)
    }

    function reset(): void {
        registration++
        queueRequest++
        registered.value = false
        replaced.value = false
        devices.value = []
        live.value = null
        queue.value = null
        remoteControl.value = false
        lastStatus.value = null
        publishedSignature = null
        publishedQueue = null
        windowStart = 0
    }

    /** Connects while a profile is selected; another profile connects anew without permission. */
    function start(): void {
        if (started) {
            return
        }

        started = true
        watch(
            () => session.token,
            (token) => {
                reset()
                void connection.stop()
                if (token && started) {
                    connection.start()
                }
            },
            { immediate: true },
        )
    }

    function dismissStatus(): void {
        lastStatus.value = null
    }

    // A remote reads the queue when its version changes.
    watch([remote, () => live.value?.queueVersion], ([isRemote]) => {
        if (isRemote) {
            void loadQueue()
        }
    })

    // The position of a playing device moves on a remote.
    watch(
        () => remote.value && live.value?.status === 'Playing',
        (moving) => {
            if (moving && ticker === null) {
                ticker = devicesDependencies.setInterval(() => {
                    clock.value = now()
                }, remoteClockMilliseconds)
            } else if (!moving && ticker !== null) {
                devicesDependencies.clearInterval(ticker)
                ticker = null
            }
        },
    )

    return {
        self,
        appleMobile,
        status,
        registered,
        replaced,
        devices,
        live,
        receivedAt,
        clock,
        remoteControl,
        queue,
        lastStatus,
        handingOver,
        role,
        remote,
        activeDevice,
        isActive,
        mirror,
        transport,
        start,
        attachPlayer,
        publish,
        send,
        sendAction,
        transfer,
        allowRemoteControl,
        loadQueue,
        mirrorState,
        onResumeFromRemote,
        resumeFromRemote,
        dismissStatus,
    }
})
