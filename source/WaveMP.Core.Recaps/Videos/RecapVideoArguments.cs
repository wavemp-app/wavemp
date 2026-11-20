namespace WaveMP.Core.Recaps.Videos;

/// <summary>
/// The arguments of ffmpeg for the conversion of a recorded video into MP4.
/// </summary>
public static class RecapVideoArguments
{
    /// <summary>
    /// Frame rate of the converted video; recordings of browsers often have a variable one.
    /// </summary>
    public const int FrameRate = 30;

    /// <summary>
    /// H.264 (yuv420p, even width and height, constant frame rate) and AAC, if the recording has sound, with the index
    /// at the start of the file, so players start at once.
    /// </summary>
    public static IReadOnlyList<string> Build(string input, string output, string x264Preset)
    {
        return
        [
            "-hide_banner",
            "-nostdin",
            "-y",
            "-i", input,
            "-map", "0:v:0",
            "-map", "0:a:0?",
            "-vf", "scale=trunc(iw/2)*2:trunc(ih/2)*2",
            "-r", FrameRate.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "-c:v", "libx264",
            "-preset", x264Preset,
            "-crf", "20",
            "-pix_fmt", "yuv420p",
            "-c:a", "aac",
            "-b:a", "160k",
            "-ar", "48000",
            "-movflags", "+faststart",
            "-f", "mp4",
            output,
        ];
    }
}
