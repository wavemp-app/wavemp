namespace WaveMP.Core.Subsonic.Elements;

/// <summary>
/// The MIME types the Subsonic API reports for the files of the library, by extension.
/// </summary>
public static class SubsonicContentTypes
{
    public const string Fallback = "application/octet-stream";

    private static readonly Dictionary<string, string> _types = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mp3"] = "audio/mpeg",
        ["flac"] = "audio/flac",
        ["m4a"] = "audio/mp4",
        ["aac"] = "audio/aac",
        ["ogg"] = "audio/ogg",
        ["oga"] = "audio/ogg",
        ["opus"] = "audio/ogg",
        ["wav"] = "audio/wav",
        ["wma"] = "audio/x-ms-wma",
        ["aif"] = "audio/aiff",
        ["aiff"] = "audio/aiff",
        ["ape"] = "audio/x-ape",
        ["wv"] = "audio/x-wavpack",
        ["mka"] = "audio/x-matroska",
        ["mpc"] = "audio/x-musepack",
        ["dsf"] = "audio/x-dsf",
        ["mp4"] = "video/mp4",
        ["m4v"] = "video/x-m4v",
        ["mkv"] = "video/x-matroska",
        ["webm"] = "video/webm",
        ["mov"] = "video/quicktime",
        ["avi"] = "video/x-msvideo",
        ["wmv"] = "video/x-ms-wmv",
        ["mpg"] = "video/mpeg",
        ["mpeg"] = "video/mpeg",
        ["ts"] = "video/mp2t",
    };

    public static string Of(string extension)
    {
        return _types.TryGetValue(extension, out string? type) ? type : Fallback;
    }
}
