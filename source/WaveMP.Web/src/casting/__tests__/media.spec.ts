import { describe, expect, it } from 'vitest'
import type { Track, TrackPlayback, TrackVideoPlayback } from '@/api/generated/models'
import { absoluteUrl, castMediaKind, castMediaOf, hlsContentType } from '../media'

const origin = 'https://music.example'

const track: Track = {
    id: 't1',
    title: 'Song',
    hasAudio: true,
    hasCover: true,
    hasVideo: true,
    durationMilliseconds: 180_000,
    artist: { id: 'a', name: 'Band' },
    album: { id: 'b', title: 'Album' },
}

function direct(extra: Partial<TrackPlayback> = {}): TrackPlayback {
    return {
        trackId: 't1',
        method: 'DirectPlay',
        format: 'Flac',
        contentType: 'audio/flac',
        streamUrl: '/api/v1/tracks/t1/stream?uid=u&exp=1&sig=x',
        coverUrl: '/api/v1/tracks/t1/cover?size=1000&uid=u&exp=1&sig=y',
        durationMilliseconds: 200_000,
        ...extra,
    }
}

describe('media for a casting device', () => {
    it('casts the video only while the video view is chosen and the track has one', () => {
        expect(castMediaKind('Video', true)).toBe('video')
        expect(castMediaKind('Video', false)).toBe('audio')
        expect(castMediaKind('Cover', true)).toBe('audio')
        expect(castMediaKind('Visualization', true)).toBe('audio')
    })

    it('makes the URLs of the server absolute, as the device loads them itself', () => {
        expect(absoluteUrl('/api/v1/tracks/t1/stream?sig=x', origin)).toBe(
            'https://music.example/api/v1/tracks/t1/stream?sig=x',
        )
        expect(absoluteUrl('https://other.example/a', origin)).toBe('https://other.example/a')
    })

    it('sends the stream with title, artist, album, cover and duration', () => {
        expect(castMediaOf(direct(), 'audio', track, origin)).toEqual({
            url: 'https://music.example/api/v1/tracks/t1/stream?uid=u&exp=1&sig=x',
            contentType: 'audio/flac',
            kind: 'audio',
            hls: false,
            title: 'Song',
            artist: 'Band',
            album: 'Album',
            artworkUrl: 'https://music.example/api/v1/tracks/t1/cover?size=1000&uid=u&exp=1&sig=y',
            duration: 200,
        })
    })

    it('marks transcoded streams as HLS', () => {
        const media = castMediaOf(
            direct({ method: 'Transcode', contentType: null, format: null }),
            'audio',
            track,
            origin,
        )

        expect(media).toMatchObject({ hls: true, contentType: hlsContentType })
    })

    it('casts a video with its HLS protocol', () => {
        const video: TrackVideoPlayback = {
            trackId: 't1',
            method: 'DirectPlay',
            protocol: 'Hls',
            streamUrl: '/api/v1/tracks/t1/video/hls/master.m3u8?sig=z',
            durationMilliseconds: 200_000,
        }

        expect(castMediaOf(video, 'video', track, origin)).toMatchObject({
            kind: 'video',
            hls: true,
            contentType: hlsContentType,
        })
    })

    it('sends no cover for a track without one and takes the duration of the track', () => {
        const media = castMediaOf(
            direct({ durationMilliseconds: null }),
            'audio',
            { ...track, hasCover: false },
            origin,
        )

        expect(media?.artworkUrl).toBeUndefined()
        expect(media?.duration).toBe(180)
    })

    it('has nothing to cast without a stream', () => {
        expect(castMediaOf(direct({ streamUrl: null }), 'audio', track, origin)).toBeNull()
    })
})
