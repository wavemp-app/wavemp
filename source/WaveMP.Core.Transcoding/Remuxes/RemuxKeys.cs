using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using WaveMP.Core.Streaming.Decisions;
using WaveMP.Core.Streaming.Sources;

namespace WaveMP.Core.Transcoding.Remuxes;

/// <summary>
/// The key of a remux: a hash of the file version and the sound. Pure functions.
/// </summary>
public static class RemuxKeys
{
    private const string Version = "remux-v1";

    /// <summary>
    /// 32 lowercase hexadecimal characters.
    /// </summary>
    public static string Of(VideoSource source, RemuxAudio audio)
    {
        ArgumentNullException.ThrowIfNull(source);
        string text = string.Join(
            "|",
            Version,
            source.FileId.ToString("N"),
            source.Length.ToString(CultureInfo.InvariantCulture),
            source.LastModified.UtcTicks.ToString(CultureInfo.InvariantCulture),
            audio.ToString());
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexStringLower(hash, 0, 16);
    }

    /// <summary>
    /// Name of the remuxed file in the cache area <c>Transcodes</c>.
    /// </summary>
    public static string NameOf(string key)
    {
        return $"remux/{key}.mp4";
    }
}
