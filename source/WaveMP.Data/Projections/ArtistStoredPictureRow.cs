namespace WaveMP.Data.Projections;

/// <summary>
/// A picture of an artist stored in the data folder.
/// </summary>
/// <param name="Id">Id of the picture, also the name of the folder of its files.</param>
/// <param name="CreatedAt">When the picture was stored.</param>
public sealed record ArtistStoredPictureRow(Guid Id, DateTimeOffset CreatedAt);
