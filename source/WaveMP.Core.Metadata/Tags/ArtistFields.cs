using WaveMP.Core.Metadata.Texts;

namespace WaveMP.Core.Metadata.Tags;

/// <summary>
/// Reads and writes the artist and album artist fields of a file with their multi-values, per kind of tag, since the
/// common tag of TagLib# hides how the values are stored:
/// <list type="bullet">
/// <item>ID3v2 (MP3, WAV, AIFF): the frames TPE1 and TPE2. ID3v2.4 separates several values properly; ID3v2.3 cannot,
/// and TagLib# splits its text at every <c>/</c>, which would break names like "AC/DC", so the text is joined back.
/// The frames TXXX ARTISTS and ALBUMARTISTS hold the values in both versions.</item>
/// <item>Vorbis comments (FLAC, Ogg): several fields ARTIST and ALBUMARTIST, and the fields ARTISTS and
/// ALBUMARTISTS.</item>
/// <item>MP4: several values of the atoms of artist and album artist, and the freeform atoms ARTISTS and
/// ALBUMARTISTS.</item>
/// </list>
/// Other kinds of tags only hold the text of the main field. The fields ARTISTS and ALBUMARTISTS are the names
/// MusicBrainz Picard and other taggers use; WaveMP writes them next to a display text in the main field.
/// </summary>
internal static class ArtistFields
{
    private const string Mean = "com.apple.iTunes";

    /// <summary>
    /// Most values of an MP4 field that are removed one by one.
    /// </summary>
    private const int MaxValues = 64;

    public enum Kind
    {
        Artist,
        AlbumArtist,
    }

    /// <summary>
    /// The artist fields of a file.
    /// </summary>
    /// <param name="Text">The text of the main field, several values joined by <c>"; "</c>; <c>null</c> if the
    /// kind of tag is not handled here.</param>
    /// <param name="Values">The separate values (see <see cref="MediaTags.ArtistValues"/>).</param>
    /// <param name="MusicBrainzIds">All MusicBrainz artists of the field.</param>
    public sealed record FieldValues(string? Text, IReadOnlyList<string> Values, IReadOnlyList<string> MusicBrainzIds);

    public static FieldValues Read(TagLib.File file, Kind kind)
    {
        if (file.GetTag(TagLib.TagTypes.Xiph, false) is TagLib.Ogg.XiphComment { IsEmpty: false } xiph)
        {
            string[] main = kind == Kind.Artist ? xiph.Performers : xiph.AlbumArtists;
            return Combine(
                main,
                xiph.GetField(MultiValueName(kind)),
                xiph.GetField(kind == Kind.Artist ? "MUSICBRAINZ_ARTISTID" : "MUSICBRAINZ_ALBUMARTISTID"));
        }

        if (file.GetTag(TagLib.TagTypes.Apple, false) is TagLib.Mpeg4.AppleTag { IsEmpty: false } apple)
        {
            string[] main = kind == Kind.Artist ? apple.Performers : apple.AlbumArtists;
            return Combine(
                main,
                apple.GetDashBoxes(Mean, MultiValueName(kind)) ?? [],
                apple.GetDashBoxes(Mean, MusicBrainzName(kind)) ?? []);
        }

        if (file.GetTag(TagLib.TagTypes.Id3v2, false) is TagLib.Id3v2.Tag { IsEmpty: false } id3)
        {
            string[] main = TagLib.Id3v2.TextInformationFrame
                .Get(id3, kind == Kind.Artist ? "TPE1" : "TPE2", false)?.Text ?? [];
            if (id3.Version < 4 && main.Length > 1)
            {
                // ID3v2.3 has no separator of its own; TagLib# splits the text at "/", which is never split here.
                main = [string.Join('/', main)];
            }

            return Combine(
                main,
                UserText(id3, MultiValueName(kind)),
                UserText(id3, MusicBrainzName(kind)));
        }

        return new FieldValues(null, [], []);
    }

    /// <summary>
    /// Writes the separate values into the field ARTISTS or ALBUMARTISTS, for the kinds of tags of <see cref="Read"/>;
    /// the caller writes the display text into the main field through the common tag. With <paramref name="values"/>
    /// empty, that field is removed, so a text written alone is not overruled by old values.
    /// </summary>
    public static void Write(TagLib.File file, Kind kind, IReadOnlyList<string> values)
    {
        string[] multi = [.. values];
        if (file.GetTag(TagLib.TagTypes.Xiph, false) is TagLib.Ogg.XiphComment xiph)
        {
            xiph.SetField(MultiValueName(kind), multi);
        }

        if (file.GetTag(TagLib.TagTypes.Apple, false) is TagLib.Mpeg4.AppleTag apple)
        {
            // One call removes one value of the box only; a bound guards against a box that does not go.
            for (int attempt = 0;
                attempt < MaxValues && apple.GetDashBoxes(Mean, MultiValueName(kind)) is { Length: > 0 };
                attempt++)
            {
                apple.SetDashBox(Mean, MultiValueName(kind), string.Empty);
            }

            if (multi.Length > 0)
            {
                apple.SetDashBoxes(Mean, MultiValueName(kind), multi);
            }
        }

        if (file.GetTag(TagLib.TagTypes.Id3v2, false) is TagLib.Id3v2.Tag id3)
        {
            var frame = TagLib.Id3v2.UserTextInformationFrame.Get(
                id3,
                MultiValueName(kind),
                multi.Length > 0);
            if (multi.Length > 0)
            {
                frame!.Text = multi;
            }
            else if (frame is not null)
            {
                id3.RemoveFrame(frame);
            }
        }
    }

    private static FieldValues Combine(string[] main, string[] multi, string[] musicBrainzIds)
    {
        string[] mainValues = [.. main.Select(MetadataText.Normalize).OfType<string>()];
        string[] multiValues = [.. multi.Select(MetadataText.Normalize).OfType<string>()];
        string? text = mainValues.Length == 0 ? null : string.Join("; ", mainValues);
        IReadOnlyList<string> values = multiValues.Length > 0
            ? multiValues
            : mainValues.Length > 1 ? mainValues : [];
        return new FieldValues(text, values, MusicBrainzIds.AllTexts(musicBrainzIds));
    }

    private static string[] UserText(TagLib.Id3v2.Tag tag, string description)
    {
        return TagLib.Id3v2.UserTextInformationFrame.Get(tag, description, false)?.Text ?? [];
    }

    private static string MultiValueName(Kind kind)
    {
        return kind == Kind.Artist ? "ARTISTS" : "ALBUMARTISTS";
    }

    private static string MusicBrainzName(Kind kind)
    {
        return kind == Kind.Artist ? "MusicBrainz Artist Id" : "MusicBrainz Album Artist Id";
    }
}
