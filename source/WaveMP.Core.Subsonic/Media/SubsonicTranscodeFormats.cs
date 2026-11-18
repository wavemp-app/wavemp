namespace WaveMP.Core.Subsonic.Media;

/// <summary>
/// Names, file suffixes, MIME types and bitrate limits of the formats <c>stream</c> transcodes to.
/// </summary>
public static class SubsonicTranscodeFormats
{
    /// <summary>
    /// The format of transcodes without a known format in the request.
    /// </summary>
    public const SubsonicTranscodeFormat Default = SubsonicTranscodeFormat.Mp3;

    /// <summary>
    /// The value of <c>format</c> that asks for the file as it is.
    /// </summary>
    public const string Raw = "raw";

    public const int MinBitrateKbps = 32;

    /// <summary>
    /// The highest bitrate of MP3 and the most WaveMP uses for the other formats.
    /// </summary>
    public const int MaxBitrateKbps = 320;

    /// <summary>
    /// The format named by <c>format</c> (e.g. <c>mp3</c>, <c>opus</c>, <c>ogg</c>, <c>aac</c>, <c>m4a</c>);
    /// <c>null</c> for any other name.
    /// </summary>
    public static SubsonicTranscodeFormat? Parse(string? name)
    {
        return name?.Trim().ToLowerInvariant() switch
        {
            "mp3" => SubsonicTranscodeFormat.Mp3,
            "opus" or "ogg" or "oga" => SubsonicTranscodeFormat.Opus,
            "aac" or "m4a" or "adts" => SubsonicTranscodeFormat.Aac,
            _ => null,
        };
    }

    public static string SuffixOf(SubsonicTranscodeFormat format)
    {
        return format switch
        {
            SubsonicTranscodeFormat.Opus => "opus",
            SubsonicTranscodeFormat.Aac => "aac",
            _ => "mp3",
        };
    }

    public static string ContentTypeOf(SubsonicTranscodeFormat format)
    {
        return format switch
        {
            SubsonicTranscodeFormat.Opus => "audio/ogg",
            SubsonicTranscodeFormat.Aac => "audio/aac",
            _ => "audio/mpeg",
        };
    }

    /// <summary>
    /// Whether a file with the extension already is in the format, so it needs no transcoding for it.
    /// </summary>
    public static bool Matches(SubsonicTranscodeFormat format, string extension)
    {
        return string.Equals(SuffixOf(format), extension, StringComparison.OrdinalIgnoreCase);
    }
}
