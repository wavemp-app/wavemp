namespace WaveMP.Data.Repositories;

/// <summary>
/// Outcome of removing a picture of an existing artist.
/// </summary>
/// <param name="RemovedPictureId">The removed picture; <c>null</c> if the artist had none of the kind.</param>
public sealed record ArtistPictureRemoval(Guid? RemovedPictureId);
