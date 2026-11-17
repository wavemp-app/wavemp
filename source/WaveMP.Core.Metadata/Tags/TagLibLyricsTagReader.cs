using TagLib.Id3v2;

namespace WaveMP.Core.Metadata.Tags;

/// <summary>
/// Reads lyrics with TagLib#: synchronized lyrics from the ID3v2 frame SYLT (with timestamps in milliseconds; frames
/// with MPEG frame numbers are skipped, because their times depend on the stream), unsynchronized lyrics from the
/// lyrics of all tag formats TagLib# knows (ID3v2 USLT, Vorbis and FLAC comment LYRICS, MP4 ©lyr, APE and others) and
/// from the Vorbis comment UNSYNCEDLYRICS that some taggers write. The file is closed before the method returns.
/// </summary>
internal sealed class TagLibLyricsTagReader : ILyricsTagReader
{
    private static readonly SynchedTextType[] _lyricsTypes =
        [SynchedTextType.Lyrics, SynchedTextType.TextTranscription, SynchedTextType.Other];

    public TagLyrics? Read(string path)
    {
        try
        {
            using var file = TagLib.File.Create(path, TagLib.ReadStyle.None | TagLib.ReadStyle.PictureLazy);
            IReadOnlyList<TimedLyricsText> synchronized = ReadSynchronized(file);
            string? unsynchronized = Text(file.Tag.Lyrics) ?? ReadUnsyncedLyricsComment(file);
            return synchronized.Count == 0 && unsynchronized is null
                ? null
                : new TagLyrics(synchronized, unsynchronized);
        }
        catch (Exception exception) when (exception is TagLib.UnsupportedFormatException
            or TagLib.CorruptFileException
            or IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotImplementedException
            or IndexOutOfRangeException
            or OverflowException)
        {
            // TagLib# reports some damaged files with general exceptions.
            return null;
        }
    }

    private static TimedLyricsText[] ReadSynchronized(TagLib.File file)
    {
        if (file.GetTag(TagLib.TagTypes.Id3v2, false) is not TagLib.Id3v2.Tag tag)
        {
            return [];
        }

        SynchronisedLyricsFrame? frame = tag.GetFrames<SynchronisedLyricsFrame>()
            .Where(item => item.Format == TimestampFormat.AbsoluteMilliseconds
                && Array.IndexOf(_lyricsTypes, item.Type) >= 0
                && item.Text is { Length: > 0 })
            .OrderBy(item => Array.IndexOf(_lyricsTypes, item.Type))
            .FirstOrDefault();
        if (frame is null)
        {
            return [];
        }

        return [.. frame.Text
            .Where(entry => entry.Text is not null && entry.Time >= 0)
            .Select(entry => new TimedLyricsText(TimeSpan.FromMilliseconds(entry.Time), entry.Text))];
    }

    private static string? ReadUnsyncedLyricsComment(TagLib.File file)
    {
        return file.GetTag(TagLib.TagTypes.Xiph, false) is TagLib.Ogg.XiphComment comment
            ? Text(comment.GetFirstField("UNSYNCEDLYRICS"))
            : null;
    }

    private static string? Text(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
