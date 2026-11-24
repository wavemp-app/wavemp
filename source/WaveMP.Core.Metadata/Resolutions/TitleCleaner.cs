using WaveMP.Core.Metadata.Texts;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Metadata.Resolutions;

/// <summary>
/// Removes the name of the artist from the start of a title tag, e.g. <c>"Alan Walker - Faded"</c> becomes
/// <c>"Faded"</c>, as the layout template asks for (see <see cref="TitleCleaningMode"/>). Only the shown title
/// changes; the tags of the file stay as they are.
/// </summary>
public static class TitleCleaner
{
    /// <summary>
    /// What stands between the artist and the title.
    /// </summary>
    public const string Separator = " - ";

    /// <summary>
    /// The title without the leading <c>"&lt;artist&gt; - "</c>, or the title unchanged if the mode does not apply,
    /// the prefix is not the artist or nothing would be left of the title.
    /// </summary>
    /// <param name="title">The title from the tags.</param>
    /// <param name="artistTag">The artist from the tags, or <c>null</c>.</param>
    /// <param name="pathArtist">The artist from the folder and file name, or <c>null</c>; only compared with
    /// <see cref="TitleCleaningMode.LooseArtist"/> and only without an artist tag.</param>
    /// <param name="mode">How the title is cleaned.</param>
    public static string Clean(string title, string? artistTag, string? pathArtist, TitleCleaningMode mode)
    {
        int separator = title.IndexOf(Separator, StringComparison.Ordinal);
        if (mode == TitleCleaningMode.None || separator <= 0)
        {
            return title;
        }

        // The prefix may itself contain the separator ("A - B - Title" with the artist "A - B").
        while (separator > 0)
        {
            string prefix = title[..separator];
            string rest = title[(separator + Separator.Length)..].Trim();
            if (rest.Length > 0 && Matches(prefix, artistTag, pathArtist, mode))
            {
                return rest;
            }

            separator = title.IndexOf(Separator, separator + 1, StringComparison.Ordinal);
        }

        return title;
    }

    private static bool Matches(string prefix, string? artistTag, string? pathArtist, TitleCleaningMode mode)
    {
        if (mode == TitleCleaningMode.ExactArtistTag)
        {
            return artistTag is not null && string.Equals(prefix, artistTag, StringComparison.Ordinal);
        }

        string? artist = artistTag ?? pathArtist;
        return artist is not null
            && MetadataText.ToKey(prefix) is { } key
            && string.Equals(key, MetadataText.ToKey(artist), StringComparison.Ordinal);
    }
}
