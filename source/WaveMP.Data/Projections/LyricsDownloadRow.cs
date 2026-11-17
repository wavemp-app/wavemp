using WaveMP.Data.Entities;

namespace WaveMP.Data.Projections;

/// <summary>
/// The result of the online lookup of the lyrics of a track.
/// </summary>
/// <param name="Status">What the lookup found.</param>
/// <param name="Content">The lyrics stored in the database (LRC format), if any.</param>
/// <param name="CheckedAt">When the track was looked up.</param>
public sealed record LyricsDownloadRow(LyricsDownloadStatus Status, string? Content, DateTimeOffset CheckedAt);
