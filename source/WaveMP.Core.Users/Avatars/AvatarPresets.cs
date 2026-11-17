using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using WaveMP.Core.Caching.Covers;

namespace WaveMP.Core.Users.Avatars;

/// <summary>
/// The built-in avatars that profiles can choose instead of uploading a picture. They are drawn for WaveMP (sources in
/// <c>assets/avatars</c>) and embedded as square WebP pictures in the sizes of <see cref="CoverSizes"/>.
/// </summary>
public static class AvatarPresets
{
    private const string ResourcePrefix = "avatars/";
    private const string FileExtension = ".webp";
    private const int HashLength = 12;

    private static readonly Assembly _assembly = typeof(AvatarPresets).Assembly;
    private static readonly ConcurrentDictionary<string, string> _hashes = new(StringComparer.Ordinal);

    /// <summary>
    /// The ids of the built-in avatars in the order clients offer them.
    /// </summary>
    public static IReadOnlyList<string> Ids { get; } =
        ["bubble", "leaf", "drop", "sun", "wave", "cloud", "star", "fish"];

    /// <summary>
    /// Whether a built-in avatar with the id exists; ids are compared exactly.
    /// </summary>
    public static bool Contains(string? id)
    {
        return id is not null && Ids.Contains(id, StringComparer.Ordinal);
    }

    /// <summary>
    /// Opens the picture of a built-in avatar in the size (see <see cref="CoverSizes.Snap"/>), or returns <c>null</c>
    /// for an unknown id. The caller disposes it.
    /// </summary>
    public static Stream? Open(string id, int size)
    {
        return Contains(id) ? _assembly.GetManifestResourceStream(ResourceName(id, CoverSizes.Snap(size))) : null;
    }

    /// <summary>
    /// A short hash of the picture of a built-in avatar in the size, part of its entity tag, so a picture changed in a
    /// new version of WaveMP gets a new tag. <c>null</c> for an unknown id.
    /// </summary>
    public static string? HashOf(string id, int size)
    {
        if (!Contains(id))
        {
            return null;
        }

        string name = ResourceName(id, CoverSizes.Snap(size));
        return _hashes.GetOrAdd(name, static resource =>
        {
            using Stream? content = _assembly.GetManifestResourceStream(resource);
            if (content is null)
            {
                return string.Empty;
            }

            byte[] hash = SHA256.HashData(content);
            return Convert.ToHexStringLower(hash)[..HashLength];
        });
    }

    private static string ResourceName(string id, int size)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{ResourcePrefix}{id}-{size}{FileExtension}");
    }
}
