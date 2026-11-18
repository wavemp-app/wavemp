// The formats of the devices. The server plays these directly and transcodes everything else to an
// HLS stream (AAC, H.264), which every device plays.

import {
    AudioCodecObject,
    AudioFormatObject,
    VideoCodecObject,
    VideoContainerObject,
} from '@/api/generated/models'
import type { CastFormats } from './types'

/**
 * What the Default Media Receiver of a Chromecast plays (the supported media of Google Cast): MP3,
 * AAC, FLAC, Opus, Vorbis and WAV for the sound; MP4 and WebM with H.264, VP8 or VP9 for videos.
 */
export const chromecastFormats: CastFormats = {
    audio: [
        AudioFormatObject.Mp3,
        AudioFormatObject.Flac,
        AudioFormatObject.AdtsAac,
        AudioFormatObject.Mp4Aac,
        AudioFormatObject.OggOpus,
        AudioFormatObject.OggVorbis,
        AudioFormatObject.WebmOpus,
        AudioFormatObject.WebmVorbis,
        AudioFormatObject.WavPcm,
    ],
    video: {
        containers: [VideoContainerObject.Mp4, VideoContainerObject.Webm],
        videoCodecs: [VideoCodecObject.H264, VideoCodecObject.Vp8, VideoCodecObject.Vp9],
        audioCodecs: [
            AudioCodecObject.Aac,
            AudioCodecObject.Mp3,
            AudioCodecObject.Opus,
            AudioCodecObject.Vorbis,
        ],
    },
}
