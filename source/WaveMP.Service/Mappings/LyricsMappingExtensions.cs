using WaveMP.Core.Lyrics.Documents;
using WaveMP.Service.Dtos;

namespace WaveMP.Service.Mappings;

public static class LyricsMappingExtensions
{
    public static LyricsDto ToDto(this TrackLyrics lyrics)
    {
        return new LyricsDto
        {
            Type = lyrics.Document.Type.ToDto(),
            Source = lyrics.Source.ToDto(),
            Lines = [.. lyrics.Document.Lines.Select(line => line.ToDto())],
        };
    }

    public static LyricsLineDto ToDto(this LyricsLine line)
    {
        return new LyricsLineDto
        {
            StartMilliseconds = Milliseconds(line.Start),
            EndMilliseconds = Milliseconds(line.End),
            Text = line.Text,
            Words = [.. line.Words.Select(word => new LyricsWordDto
            {
                StartMilliseconds = (long)word.Start.TotalMilliseconds,
                EndMilliseconds = Milliseconds(word.End),
                Text = word.Text,
            })],
        };
    }

    public static LyricsTypeDto ToDto(this LyricsType type)
    {
        return type switch
        {
            LyricsType.LineSynchronized => LyricsTypeDto.LineSynchronized,
            LyricsType.WordSynchronized => LyricsTypeDto.WordSynchronized,
            LyricsType.Static => LyricsTypeDto.Static,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown lyrics type."),
        };
    }

    public static LyricsSourceDto ToDto(this LyricsSource source)
    {
        return source switch
        {
            LyricsSource.File => LyricsSourceDto.File,
            LyricsSource.Download => LyricsSourceDto.Download,
            LyricsSource.Tags => LyricsSourceDto.Tags,
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown lyrics source."),
        };
    }

    private static long? Milliseconds(TimeSpan? time)
    {
        return time is { } value ? (long)value.TotalMilliseconds : null;
    }
}
