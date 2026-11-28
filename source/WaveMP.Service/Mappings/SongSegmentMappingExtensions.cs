using WaveMP.Core.Analysis.Services;
using WaveMP.Core.Streaming.Sources;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Segments;
using WaveMP.Service.Dtos;

namespace WaveMP.Service.Mappings;

public static class SongSegmentMappingExtensions
{
    public static SongSegmentDto ToDto(this SongSegment segment)
    {
        return new SongSegmentDto
        {
            StartMilliseconds = segment.StartMilliseconds,
            EndMilliseconds = segment.EndMilliseconds,
            Pieces = [.. segment.Pieces.Select(piece => piece.ToDto())],
            Method = segment.Method.ToDto(),
            Confidence = segment.Confidence,
            Confident = segment.IsConfident,
            Manual = segment.Manual,
            Mismatch = segment.Mismatch,
        };
    }

    public static SongPieceDto ToDto(this SongPiece piece)
    {
        return new SongPieceDto
        {
            FileStartMilliseconds = piece.FileStartMilliseconds,
            SongStartMilliseconds = piece.SongStartMilliseconds,
            DurationMilliseconds = piece.DurationMilliseconds,
        };
    }

    public static SongSegmentMethodDto ToDto(this SongSegmentMethod method)
    {
        return method switch
        {
            SongSegmentMethod.WholeFile => SongSegmentMethodDto.WholeFile,
            SongSegmentMethod.Alignment => SongSegmentMethodDto.Alignment,
            SongSegmentMethod.Lyrics => SongSegmentMethodDto.Lyrics,
            SongSegmentMethod.Structure => SongSegmentMethodDto.Structure,
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Unknown song segment method."),
        };
    }

    public static SongAnalysisStatusDto ToDto(this SongAnalysisStatus status)
    {
        return status switch
        {
            SongAnalysisStatus.Analyzed => SongAnalysisStatusDto.Analyzed,
            SongAnalysisStatus.NoSound => SongAnalysisStatusDto.NoSound,
            SongAnalysisStatus.Failed => SongAnalysisStatusDto.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown song analysis status."),
        };
    }

    public static SongSegmentNoticeDto ToDto(this SongSegmentNotice notice)
    {
        return notice switch
        {
            SongSegmentNotice.LowConfidence => SongSegmentNoticeDto.LowConfidence,
            SongSegmentNotice.Mismatch => SongSegmentNoticeDto.Mismatch,
            _ => throw new ArgumentOutOfRangeException(nameof(notice), notice, "Unknown song segment notice."),
        };
    }

    public static MediaFileKind ToEntity(this SongSegmentKindDto kind)
    {
        return kind switch
        {
            SongSegmentKindDto.Audio => MediaFileKind.Audio,
            SongSegmentKindDto.Video => MediaFileKind.Video,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown song segment kind."),
        };
    }

    public static SoundSource ToSoundSource(this SoundSourceDto sound)
    {
        return sound switch
        {
            SoundSourceDto.Audio => SoundSource.Audio,
            SoundSourceDto.Video => SoundSource.Video,
            _ => throw new ArgumentOutOfRangeException(nameof(sound), sound, "Unknown sound source."),
        };
    }

    public static TrackSongSegmentsDto ToDto(this TrackSongSegments segments)
    {
        return new TrackSongSegmentsDto
        {
            TrackId = segments.TrackId,
            Audio = segments.Audio?.ToDto(),
            Video = segments.Video?.ToDto(),
        };
    }

    public static FileSongSegmentDto ToDto(this FileSongSegment segment)
    {
        return new FileSongSegmentDto
        {
            FileId = segment.FileId,
            DurationMilliseconds = segment.Duration is { } duration
                ? (long)Math.Round(duration.TotalMilliseconds)
                : null,
            Status = segment.Analysis?.Status.ToDto(),
            Pending = segment.Pending,
            AnalyzedAt = segment.Analysis?.AnalyzedAt,
            LoudnessLufs = segment.Analysis?.LoudnessLufs,
            Detected = segment.Detected.ToDto(),
            ManualStartMilliseconds = segment.ManualStartMilliseconds,
            ManualEndMilliseconds = segment.ManualEndMilliseconds,
            Effective = segment.Effective.ToDto(),
        };
    }
}
