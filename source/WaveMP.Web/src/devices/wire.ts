// Decoding of the members of hub messages that use schemas of the REST API (Device, Track). The
// SignalR client hands over plain JSON; the deserializer of the generated client turns them into
// the same models a REST answer has (e.g. dates as Date objects), so the player and the views do
// not tell where a song came from.

import {
    AnonymousAuthenticationProvider,
    type ParseNodeFactory,
} from '@microsoft/kiota-abstractions'
import { DefaultRequestAdapter } from '@microsoft/kiota-bundle'
import {
    createDeviceFromDiscriminatorValue,
    createTrackFromDiscriminatorValue,
    type Device,
    type Track,
} from '@/api/generated/models'
import type {
    DeviceList,
    DeviceQueueResult,
    DeviceSessionSnapshot,
    ReceivedCommand,
    RegisterResult,
} from './protocol'

const jsonContentType = 'application/json'
let parsers: ParseNodeFactory | null = null

function parseNodeFactory(): ParseNodeFactory {
    // The adapter registers the JSON deserializer of Kiota; it never sends a request here.
    parsers ??= new DefaultRequestAdapter(
        new AnonymousAuthenticationProvider(),
    ).getParseNodeFactory()
    return parsers
}

function rootNode(value: unknown) {
    const content = new TextEncoder().encode(JSON.stringify(value ?? null))
    return parseNodeFactory().getRootParseNode(jsonContentType, content.buffer as ArrayBuffer)
}

function decodeList<T>(value: unknown, decode: (item: unknown) => T | null): T[] {
    return Array.isArray(value)
        ? value.flatMap((item) => {
              const decoded = decode(item)
              return decoded === null ? [] : [decoded]
          })
        : []
}

/** A song of a hub message as the generated client models it; null for anything else. */
export function decodeTrack(value: unknown): Track | null {
    if (value === null || typeof value !== 'object') {
        return null
    }

    return rootNode(value).getObjectValue<Track>(createTrackFromDiscriminatorValue) ?? null
}

/** A device of a hub message as the generated client models it; null for anything else. */
export function decodeDevice(value: unknown): Device | null {
    if (value === null || typeof value !== 'object') {
        return null
    }

    return rootNode(value).getObjectValue<Device>(createDeviceFromDiscriminatorValue) ?? null
}

export function decodeDeviceList(value: DeviceList): DeviceList {
    return { devices: decodeList(value?.devices, decodeDevice) }
}

export function decodeRegisterResult(value: RegisterResult): RegisterResult {
    const snapshot: DeviceSessionSnapshot | null = value?.snapshot ?? null
    return {
        status: value.status,
        errors: value.errors ?? [],
        snapshot: snapshot
            ? { ...snapshot, devices: decodeList(snapshot.devices, decodeDevice) }
            : null,
    }
}

export function decodeQueueResult(value: DeviceQueueResult): DeviceQueueResult {
    const queue = value?.queue ?? null
    return {
        status: value.status,
        queue: queue
            ? {
                  ...queue,
                  items: (queue.items ?? []).map((entry) => ({
                      ...entry,
                      track: decodeTrack(entry.track),
                  })),
              }
            : null,
    }
}

export function decodeReceivedCommand(value: ReceivedCommand): ReceivedCommand {
    return {
        ...value,
        tracks:
            value.tracks === null || value.tracks === undefined
                ? null
                : decodeList(value.tracks, decodeTrack),
    }
}
