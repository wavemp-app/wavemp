using WaveMP.Data.Entities;

namespace WaveMP.Data.Projections;

/// <summary>
/// The result of the online lookup of the lyrics of a track.
/// </summary>
/// <param name="Status">What the lookup found.</param>
/// <param name="Content">The lyrics stored in the database (LRC format), if any.</param>
/// <param name="CheckedAt">When the track was looked up.</param>
/// <param name="OtherVersion">Whether the saved lyrics belong to another version of the song, so only their plain
/// lines were kept.</param>
/// <param name="MatcherVersion">The version of the rules that chose the lyrics.</param>
public sealed record LyricsDownloadRow(
    LyricsDownloadStatus Status,
    string? Content,
    DateTimeOffset CheckedAt,
    bool OtherVersion = false,
    int MatcherVersion = 0);
