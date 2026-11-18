// What a casting device plays for a track: the video of the track while the video view is chosen and
// the track has one, otherwise its sound; with title, artist, album and cover. Pure functions, so the
// rules are tested without a device.

import {
    PlaybackMethodObject,
    PlayerViewModeObject,
    StreamProtocolObject,
    type PlayerViewMode,
    type Track,
    type TrackPlayback,
    type TrackVideoPlayback,
} from '@/api/generated/models'
import type { CastMedia } from './types'

/** MIME type of the HLS playlists of the server. */
export const hlsContentType = 'application/vnd.apple.mpegurl'

/** Whether a device plays the video or the sound of a track. */
export function castMediaKind(mode: PlayerViewMode, hasVideo: boolean): CastMedia['kind'] {
    return mode === PlayerViewModeObject.Video && hasVideo ? 'video' : 'audio'
}

/**
 * The absolute URL of a URL of the API, which is relative to the server. A device loads it itself,
 * so it needs the full address of the server as the browser reaches it.
 */
export function absoluteUrl(url: string, origin: string): string {
    return new URL(url, origin).href
}

/**
 * The media for a device from the playback the server decided for the formats of the device; null
 * if the server has no stream (transcoding needed but unavailable).
 */
export function castMediaOf(
    playback: TrackPlayback | TrackVideoPlayback,
    kind: CastMedia['kind'],
    track: Track,
    origin: string,
): CastMedia | null {
    if (!playback.streamUrl) {
        return null
    }

    const hls =
        playback.method === PlaybackMethodObject.Transcode ||
        playback.protocol === StreamProtocolObject.Hls
    const fallbackType = hls ? hlsContentType : kind === 'video' ? 'video/mp4' : 'audio/mpeg'
    const duration = playback.durationMilliseconds ?? track.durationMilliseconds ?? 0
    return {
        url: absoluteUrl(playback.streamUrl, origin),
        contentType: playback.contentType ?? fallbackType,
        kind,
        hls,
        title: track.title ?? undefined,
        artist: track.artist?.name ?? undefined,
        album: track.album?.title ?? undefined,
        artworkUrl:
            track.hasCover && playback.coverUrl
                ? absoluteUrl(playback.coverUrl, origin)
                : undefined,
        duration: duration > 0 ? duration / 1000 : 0,
    }
}
