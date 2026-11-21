using WaveMP.Core.Metadata.Texts;

namespace WaveMP.Core.Metadata.Tags;

/// <summary>
/// Reads tags with TagLib#. Media properties are not read (ffprobe provides them) and pictures are read lazily, so
/// only the tags are loaded. The file is closed before the method returns.
/// </summary>
internal sealed class TagLibTagReader : ITagReader
{
    public TagReadResult Read(string path)
    {
        try
        {
            using var file = TagLib.File.Create(path, TagLib.ReadStyle.None | TagLib.ReadStyle.PictureLazy);
            TagLib.Tag tag = file.Tag;
            return new TagReadResult(
                TagReadStatus.Read,
                ToTags(tag) with { IsCompilation = IsCompilation(file) },
                ToPicture(tag.Pictures));
        }
        catch (TagLib.UnsupportedFormatException)
        {
            return TagReadResult.Failed(TagReadStatus.Unsupported);
        }
        catch (Exception exception) when (exception is TagLib.CorruptFileException
            or IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotImplementedException
            or IndexOutOfRangeException
            or OverflowException)
        {
            // TagLib# reports some damaged files with general exceptions.
            return TagReadResult.Failed(TagReadStatus.Unreadable);
        }
    }

    private static MediaTags ToTags(TagLib.Tag tag)
    {
        return new MediaTags
        {
            Title = MetadataText.Normalize(tag.Title),
            TitleSort = MetadataText.Normalize(tag.TitleSort),
            Artist = MetadataText.Normalize(tag.FirstPerformer),
            Artists = MetadataText.Normalize(tag.JoinedPerformers),
            ArtistSort = MetadataText.Normalize(tag.FirstPerformerSort),
            AlbumArtist = MetadataText.Normalize(tag.FirstAlbumArtist),
            AlbumArtistSort = MetadataText.Normalize(tag.FirstAlbumArtistSort),
            Album = MetadataText.Normalize(tag.Album),
            AlbumSort = MetadataText.Normalize(tag.AlbumSort),
            TrackNumber = Positive(tag.Track),
            TrackCount = Positive(tag.TrackCount),
            DiscNumber = Positive(tag.Disc),
            DiscCount = Positive(tag.DiscCount),
            Year = Positive(tag.Year),
            Genre = MetadataText.Normalize(tag.FirstGenre),
            Genres = MetadataText.Normalize(tag.JoinedGenres),
            Composer = MetadataText.Normalize(tag.FirstComposer),
            ReplayGainTrackGain = Finite(tag.ReplayGainTrackGain),
            ReplayGainTrackPeak = Finite(tag.ReplayGainTrackPeak),
            ReplayGainAlbumGain = Finite(tag.ReplayGainAlbumGain),
            ReplayGainAlbumPeak = Finite(tag.ReplayGainAlbumPeak),
            MusicBrainzTrackId = MetadataText.Normalize(tag.MusicBrainzTrackId),
            MusicBrainzReleaseId = MetadataText.Normalize(tag.MusicBrainzReleaseId),
        };
    }

    /// <summary>
    /// Whether one of the tags of the file marks it as part of a compilation. The common tag of TagLib# does not know
    /// the flag, so the tag types that do are asked one by one: ID3v2 (<c>TCMP</c>), MP4 (<c>cpil</c>) and the
    /// Vorbis comment of FLAC and Ogg (<c>COMPILATION</c>).
    /// </summary>
    private static bool IsCompilation(TagLib.File file)
    {
        return (file.GetTag(TagLib.TagTypes.Id3v2, false) as TagLib.Id3v2.Tag)?.IsCompilation == true
            || (file.GetTag(TagLib.TagTypes.Apple, false) as TagLib.Mpeg4.AppleTag)?.IsCompilation == true
            || (file.GetTag(TagLib.TagTypes.Xiph, false) as TagLib.Ogg.XiphComment)?.IsCompilation == true;
    }

    private static EmbeddedPicture? ToPicture(TagLib.IPicture[]? pictures)
    {
        if (pictures is not { Length: > 0 })
        {
            return null;
        }

        TagLib.IPicture picture = pictures.FirstOrDefault(item => item.Type == TagLib.PictureType.FrontCover)
            ?? pictures[0];
        return new EmbeddedPicture(
            MetadataText.Normalize(picture.MimeType),
            picture.Type == TagLib.PictureType.FrontCover);
    }

    private static int? Positive(uint value)
    {
        return value is > 0 and <= int.MaxValue ? (int)value : null;
    }

    private static double? Finite(double value)
    {
        return double.IsFinite(value) ? value : null;
    }
}
