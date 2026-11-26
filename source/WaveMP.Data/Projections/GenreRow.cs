namespace WaveMP.Data.Projections;

/// <summary>
/// A genre of the tracks.
/// </summary>
/// <param name="Id">Id of the genre.</param>
/// <param name="Name">Name of the genre as first found in the tracks.</param>
/// <param name="TrackCount">Number of tracks with the genre.</param>
/// <param name="AlbumCount">Number of albums with tracks of the genre.</param>
public sealed record GenreRow(Guid Id, string Name, int TrackCount, int AlbumCount);
