import { describe, expect, it } from 'vitest'
import {
    decodeDevice,
    decodeDeviceList,
    decodeQueueResult,
    decodeReceivedCommand,
    decodeRegisterResult,
    decodeTrack,
} from '../wire'
import { received } from './fakes'

const rawTrack = {
    id: '6f9c4d1e-0000-4000-8000-000000000001',
    title: 'Song',
    hasAudio: true,
    durationMilliseconds: 1000,
    firstSeenAt: '2026-11-20T08:30:00+00:00',
    artist: { id: '6f9c4d1e-0000-4000-8000-000000000002', name: 'Band' },
}

const rawDevice = {
    id: 'living-room',
    name: 'Living room',
    reportedName: 'Firefox · Windows',
    customName: 'Living room',
    type: 'Desktop',
    online: true,
    remoteControl: true,
    active: false,
    lastActiveAt: '2026-11-25T10:00:00+00:00',
}

describe('decoding of hub messages', () => {
    it('decodes songs like the REST answers, with dates', () => {
        const track = decodeTrack(rawTrack)

        expect(track?.title).toBe('Song')
        expect(track?.artist?.name).toBe('Band')
        expect(track?.firstSeenAt).toBeInstanceOf(Date)
        expect(track?.firstSeenAt?.toISOString()).toBe('2026-11-20T08:30:00.000Z')
        expect(decodeTrack(null)).toBeNull()
        expect(decodeTrack('x')).toBeNull()
    })

    it('decodes devices with their last activity as a date', () => {
        const device = decodeDevice(rawDevice)

        expect(device).toMatchObject({ id: 'living-room', type: 'Desktop', remoteControl: true })
        expect(device?.lastActiveAt).toBeInstanceOf(Date)
        expect(
            decodeDeviceList({ devices: [rawDevice as never, null as never] }).devices,
        ).toHaveLength(1)
    })

    it('decodes the registration, the queue and received commands', () => {
        const registered = decodeRegisterResult({
            status: 'Registered',
            errors: [],
            snapshot: { deviceId: 'me', devices: [rawDevice as never], playback: {} as never },
        })
        expect(registered.snapshot?.devices[0]?.lastActiveAt).toBeInstanceOf(Date)
        expect(
            decodeRegisterResult({ status: 'Invalid', errors: [], snapshot: null }).snapshot,
        ).toBeNull()

        const queue = decodeQueueResult({
            status: 'Accepted',
            queue: {
                version: 1,
                queueVersion: 1,
                currentIndex: 0,
                shuffle: false,
                source: null,
                items: [
                    {
                        position: 0,
                        trackId: rawTrack.id,
                        autoplay: false,
                        track: rawTrack as never,
                    },
                    { position: 1, trackId: 'gone', autoplay: false, track: null },
                ],
            },
        })
        expect(queue.queue?.items[0]?.track?.firstSeenAt).toBeInstanceOf(Date)
        expect(queue.queue?.items[1]?.track).toBeNull()
        expect(decodeQueueResult({ status: 'NotRegistered', queue: null }).queue).toBeNull()

        const command = decodeReceivedCommand(
            received({ type: 'AddToQueue', tracks: [rawTrack as never] }),
        )
        expect(command.tracks?.[0]?.firstSeenAt).toBeInstanceOf(Date)
        expect(decodeReceivedCommand(received({ type: 'Play' })).tracks).toBeNull()
    })
})
