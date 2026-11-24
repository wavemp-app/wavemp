namespace WaveMP.Data.Repositories;

/// <summary>
/// A folder of a source with tracks of an artist (as track artist or as album artist of their album), with the name of
/// the artist.
/// </summary>
public sealed record ArtistFolder(Guid ArtistId, string ArtistName, Guid SourceId, string DirectoryPath);
