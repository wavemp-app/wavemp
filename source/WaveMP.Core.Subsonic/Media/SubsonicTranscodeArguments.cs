using System.Globalization;

namespace WaveMP.Core.Subsonic.Media;

/// <summary>
/// The arguments of ffmpeg for a transcode of <c>stream</c>: the first audio stream of the file, from the offset on,
/// in stereo, without tags and pictures, written as one continuous stream to the standard output.
/// </summary>
public static class SubsonicTranscodeArguments
{
    public static IReadOnlyList<string> Build(string inputPath, SubsonicStreamPlan plan)
    {
        List<string> arguments = ["-nostdin", "-hide_banner", "-loglevel", "error"];
        if (plan.OffsetSeconds > 0)
        {
            arguments.AddRange(["-ss", plan.OffsetSeconds.ToString(CultureInfo.InvariantCulture)]);
        }

        arguments.AddRange(["-i", inputPath, "-map", "0:a:0", "-vn", "-sn", "-dn", "-map_metadata", "-1"]);
        arguments.AddRange(["-ac", "2"]);
        string bitrate = string.Create(CultureInfo.InvariantCulture, $"{plan.BitrateKbps}k");
        arguments.AddRange(plan.Format switch
        {
            SubsonicTranscodeFormat.Opus => ["-c:a", "libopus", "-b:a", bitrate, "-f", "ogg"],
            SubsonicTranscodeFormat.Aac => ["-c:a", "aac", "-b:a", bitrate, "-f", "adts"],
            _ => ["-c:a", "libmp3lame", "-b:a", bitrate, "-f", "mp3"],
        });
        arguments.Add("pipe:1");
        return arguments;
    }
}
