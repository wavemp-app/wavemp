namespace WaveMP.Data.Repositories;

/// <summary>
/// Random tracks for the Subsonic API.
/// </summary>
/// <param name="Size">Maximum number of tracks.</param>
/// <param name="Genre">Only tracks of this genre, without regard to case.</param>
/// <param name="FromYear">Only tracks of this year or later.</param>
/// <param name="ToYear">Only tracks of this year or earlier.</param>
public sealed record SubsonicRandomTrackQuery(int Size, string? Genre = null, int? FromYear = null, int? ToYear = null);
