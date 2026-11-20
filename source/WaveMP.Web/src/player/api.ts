// Requests of the player. The only place of the web app that calls the playback and history
// endpoints, so the player store and its tests can replace it as a whole (vi.mock('@/player/api')).
// The settings of the profile (ReplayGain, equalizer, crossfade) belong to the session store.

import { apiClient } from '@/api/client'
import type {
    AudioFormat,
    AutoplayResult,
    Equalizer,
    PlaybackState,
    PlaybackTarget,
    PlayHistoryEntry,
    TrackPlayback,
    TrackVideoPlayback,
    UpdatePlaybackState,
} from '@/api/generated/models'
import type { VideoFormats } from './videoFormats'

/**
 * How the client plays a track, with the signed stream URL for direct play. With the target Cast,
 * the formats are those of a casting device; the URLs then stay valid longer and the cover is
 * signed as well.
 */
export function getPlayback(
    trackId: string,
    audioFormats: readonly AudioFormat[],
    target?: PlaybackTarget,
): Promise<TrackPlayback | undefined> {
    return apiClient.api.v1.tracks.byId(trackId).playback.get({
        queryParameters: { audioFormats: [...audioFormats], ...(target ? { target } : {}) },
    })
}

/** How the client (or a casting device) plays the video of a track: the file or an HLS stream. */
export function getVideoPlayback(
    trackId: string,
    formats: VideoFormats,
    target?: PlaybackTarget,
): Promise<TrackVideoPlayback | undefined> {
    return apiClient.api.v1.tracks.byId(trackId).video.playback.get({
        queryParameters: {
            videoContainers: [...formats.containers],
            videoCodecs: [...formats.videoCodecs],
            audioCodecs: [...formats.audioCodecs],
            ...(target ? { target } : {}),
        },
    })
}

/**
 * The songs autoplay plays after a list: chosen by the server from the songs of the list, without
 * the excluded ones, each with its playback for the formats of the browser.
 */
export function getAutoplayTracks(
    trackIds: readonly string[],
    excludedTrackIds: readonly string[],
    audioFormats: readonly AudioFormat[],
    count: number,
): Promise<AutoplayResult | undefined> {
    return apiClient.api.v1.me.autoplay.next.post({
        trackIds: [...trackIds],
        excludedTrackIds: [...excludedTrackIds],
        audioFormats: [...audioFormats],
        count,
    })
}

/**
 * Tells the scrobbling services of the current profile which track is playing now. The server
 * answers 204 even if no service is connected.
 */
export function reportNowPlaying(trackId: string): Promise<void> {
    return apiClient.api.v1.me.nowPlaying.put({ trackId })
}

/** Records a play in the history of the current profile. */
export function recordPlay(trackId: string, playedAt: Date): Promise<PlayHistoryEntry | undefined> {
    return apiClient.api.v1.me.history.post({ trackId, playedAt })
}

/** The equalizer of the server: bands, limits and the curves of the presets. */
export function getEqualizer(): Promise<Equalizer | undefined> {
    return apiClient.api.v1.equalizer.get()
}

/** The saved playback state of the current profile; the request fails with 404 before the first save. */
export function getPlaybackState(): Promise<PlaybackState | undefined> {
    return apiClient.api.v1.me.playbackState.get()
}

/**
 * Saves the playback state of this device for the current profile. A state of another device that
 * changed later stays; the request then fails with 409 (playback.state_outdated).
 */
export function savePlaybackState(state: UpdatePlaybackState): Promise<void> {
    return apiClient.api.v1.me.playbackState.put(state)
}

/** Clears the saved playback state of the current profile; succeeds also without one. */
export function clearPlaybackState(): Promise<void> {
    return apiClient.api.v1.me.playbackState.delete()
}
