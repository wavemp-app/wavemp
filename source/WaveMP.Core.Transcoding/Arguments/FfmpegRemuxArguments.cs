using WaveMP.Core.Streaming.Decisions;
using WaveMP.Core.Streaming.Formats;
using WaveMP.Core.Transcoding.Profiles;

namespace WaveMP.Core.Transcoding.Arguments;

/// <summary>
/// The ffmpeg arguments of a remux: the picture is copied as it is into an MP4 file with its index at the start
/// (faststart), so a video element can play and seek it with range requests. The sound is copied or converted to AAC,
/// subtitles and data streams are left out. Pure functions.
/// </summary>
public static class FfmpegRemuxArguments
{
    public static IReadOnlyList<string> Build(
        string inputPath,
        VideoCodec? videoCodec,
        RemuxAudio audio,
        string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        List<string> arguments = ["-hide_banner", "-nostdin", "-loglevel", "error", "-y", "-i", inputPath];
        arguments.AddRange(["-map", "0:v:0", "-c:v", "copy"]);

        // Safari plays HEVC in MP4 only with the tag hvc1 (ffmpeg writes hev1 by default).
        if (videoCodec == VideoCodec.Hevc)
        {
            arguments.AddRange(["-tag:v", "hvc1"]);
        }

        switch (audio)
        {
            case RemuxAudio.None:
                arguments.Add("-an");
                break;
            case RemuxAudio.Copy:
                arguments.AddRange(["-map", "0:a:0", "-c:a", "copy"]);
                break;
            case RemuxAudio.Aac:
                arguments.AddRange(
                [
                    "-map", "0:a:0",
                    "-c:a", "aac",
                    "-b:a", $"{TranscodeProfiles.VideoAudioBitrateKbps}k",
                    "-ac", "2",
                ]);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(audio), audio, "Unknown sound of a remux.");
        }

        arguments.AddRange(
        [
            "-sn", "-dn",
            "-map_metadata", "-1",
            "-movflags", "+faststart",
            "-f", "mp4",
            outputPath,
        ]);
        return arguments;
    }
}
