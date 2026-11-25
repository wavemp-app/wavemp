import { readFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import { commandFor, type RemoteAction } from '../commands'
import {
    CommandStatusObject,
    DeviceCommandTypeObject,
    hubEvents,
    hubMethods,
    LivePlaybackStatusObject,
    PublishStatusObject,
    RegisterStatusObject,
} from '../protocol'
import { toPublication } from '../publication'
import { mirrorQueue } from '../remote'
import { deviceQueue, live, track } from './fakes'

// The contract of the real-time connection is asyncapi.yaml in the root of the repository; the
// types of protocol.ts are written by hand. This test reads the file as text (the web app has no
// YAML parser) and compares the methods, the enums and the members of the messages this client
// builds. Not new URL(..., import.meta.url): Vite rewrites that pattern into an asset URL.
const contractFile = path.resolve(
    path.dirname(fileURLToPath(import.meta.url)),
    '../../../../../asyncapi.yaml',
)
const contract = readFileSync(contractFile, 'utf8').replace(/\r\n/g, '\n')

/** The lines of a top-level section, e.g. operations. */
function section(name: string): string {
    const start = contract.indexOf(`\n${name}:\n`)
    expect(start, `section ${name}`).toBeGreaterThanOrEqual(0)
    const rest = contract.slice(start + name.length + 2)
    const end = rest.search(/\n\S/)
    return end < 0 ? rest : rest.slice(0, end)
}

/** The block of a schema of components.schemas. */
function schema(name: string): string {
    const schemas = contract.slice(contract.indexOf('\n  schemas:\n'))
    const start = schemas.indexOf(`\n    ${name}:\n`)
    expect(start, `schema ${name}`).toBeGreaterThanOrEqual(0)
    const rest = schemas.slice(start + name.length + 6)
    const end = rest.search(/\n {0,4}\S/)
    return end < 0 ? rest : rest.slice(0, end)
}

function list(value: string | undefined): string[] {
    return (value ?? '')
        .split(',')
        .map((entry) => entry.trim())
        .filter((entry) => entry.length > 0)
}

function enumOf(name: string): string[] {
    return list(/\n {6}enum: \[(.*)\]/.exec(schema(name))?.[1])
}

function requiredOf(name: string): string[] {
    return list(/\n {6}required: \[(.*)\]/.exec(schema(name))?.[1])
}

function propertiesOf(name: string): string[] {
    return [...schema(name).matchAll(/\n {8}(\w+):\n/g)].map((match) => match[1]!)
}

/** Checks that an object of this client fits a schema: only its members, all required ones. */
function expectFits(value: object, name: string): void {
    const keys = Object.keys(value)
    expect(propertiesOf(name)).toEqual(expect.arrayContaining(keys))
    expect(keys).toEqual(expect.arrayContaining(requiredOf(name)))
}

function operations(action: 'receive' | 'send'): string[] {
    return [...section('operations').matchAll(/\n {2}(\w+):\n {4}action: (\w+)/g)]
        .filter((match) => match[2] === action)
        .map((match) => match[1]!)
}

describe('device hub protocol', () => {
    it('calls every method of the hub that asyncapi.yaml documents, and no other', () => {
        expect(Object.values(hubMethods).sort()).toEqual(operations('receive').sort())
    })

    it('listens to every message of the server that asyncapi.yaml documents', () => {
        expect(Object.values(hubEvents).sort()).toEqual(operations('send').sort())
    })

    it.each([
        ['RegisterStatus', RegisterStatusObject],
        ['LivePlaybackStatus', LivePlaybackStatusObject],
        ['PublishStatus', PublishStatusObject],
        ['DeviceCommandType', DeviceCommandTypeObject],
        ['CommandStatus', CommandStatusObject],
    ])('names the values of %s like asyncapi.yaml', (name, values) => {
        expect(Object.values(values)).toEqual(enumOf(name))
    })

    it('publishes the state of the player with the members of LivePlaybackInput', () => {
        const queue = mirrorQueue(deviceQueue([track('a'), track('b')]))
        const publication = toPublication(
            {
                queue: { ...queue, shuffle: true, original: queue.items.map((item) => item.key) },
                repeat: 'all',
                position: 12,
                duration: 200,
                playback: 'playing',
                loading: false,
                volume: 0.5,
                muted: false,
                crossfade: true,
                autoplay: false,
                source: { kind: 'album', id: 'al' },
            },
            null,
        )

        expectFits(publication.input, 'LivePlaybackInput')
        expectFits(publication.input.queue!, 'LiveQueueInput')
        expectFits(publication.input.queue!.items[0]!, 'LiveQueueItem')
        expectFits({ claim: false, playback: publication.input }, 'PublishPlayback')
    })

    it('sends every command with the members of DeviceCommand', () => {
        const mirror = mirrorQueue(deviceQueue([track('a'), track('b'), track('c')]))
        const context = {
            target: 'living-room',
            live: live({ activeDeviceId: 'living-room', status: 'Playing', currentIndex: 0 }),
            mirror,
            queueVersion: 90,
        }
        const tracks = [track('x')]
        const actions: RemoteAction[] = [
            { kind: 'play' },
            { kind: 'pause' },
            { kind: 'togglePlay' },
            { kind: 'stop' },
            { kind: 'next' },
            { kind: 'previous' },
            { kind: 'seek', seconds: 3 },
            { kind: 'setVolume', volume: 0.3 },
            { kind: 'toggleMute' },
            { kind: 'toggleShuffle' },
            { kind: 'cycleRepeat' },
            { kind: 'toggleCrossfade' },
            { kind: 'toggleAutoplay' },
            {
                kind: 'playTracks',
                tracks,
                start: 0,
                shuffle: true,
                source: { kind: 'album', id: 'al' },
            },
            { kind: 'playNext', tracks },
            { kind: 'addToQueue', tracks },
            { kind: 'playItem', key: mirror.items[1]!.key },
            { kind: 'removeItem', key: mirror.items[1]!.key },
            { kind: 'moveItem', key: mirror.items[1]!.key, offset: 1 },
            { kind: 'moveItemNext', key: mirror.items[2]!.key },
            { kind: 'clearQueue' },
        ]

        const types = actions.map((action) => {
            const command = commandFor(action, context)
            expect(command, action.kind).not.toBeNull()
            expectFits(command!, 'DeviceCommand')
            return command!.type
        })
        // Every type of command is sent by some action.
        expect(new Set(types)).toEqual(new Set(Object.values(DeviceCommandTypeObject)))
    })

    it('hands playback over and allows remote control with the documented members', () => {
        expectFits({ targetDeviceId: 'tv', play: true, expectedVersion: 3 }, 'TransferPlayback')
        expectFits({ allowed: true }, 'SetRemoteControl')
        expectFits(
            { deviceId: 'a', name: 'Firefox · Windows', type: 'Desktop', remoteControl: false },
            'RegisterDevice',
        )
    })
})
